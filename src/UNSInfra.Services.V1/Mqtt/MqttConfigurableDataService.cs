using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Core.Repositories;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.V1.SparkplugB;
using UNSInfra.Services.TopicDiscovery;

namespace UNSInfra.Services.V1.Mqtt;

/// <summary>
/// Enhanced MQTT data service that uses configurable input configurations
/// </summary>
public class MqttConfigurableDataService : IDataIngestionService
{
    private readonly ILogger<MqttConfigurableDataService> _logger;
    private readonly IInputOutputConfigurationRepository _configRepository;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly MqttConnectionManager _connectionManager;
    private readonly object? _sparkplugBDecoder;
    private readonly Dictionary<string, MqttInputConfiguration> _activeConfigurations = new();
    private readonly Dictionary<string, IMqttDataIngestionService> _activeConnections = new();
    private readonly Dictionary<string, string> _configConnectionMap = new();
    private bool _isRunning;
    private bool _disposed;

    public event EventHandler<DataPoint>? DataReceived;

    public MqttConfigurableDataService(
        ILogger<MqttConfigurableDataService> logger,
        IInputOutputConfigurationRepository configRepository,
        IServiceScopeFactory serviceScopeFactory,
        MqttConnectionManager connectionManager,
        object? sparkplugBDecoder = null)
    {
        _logger = logger;
        _configRepository = configRepository;
        _serviceScopeFactory = serviceScopeFactory;
        _connectionManager = connectionManager;
        _sparkplugBDecoder = sparkplugBDecoder;
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("MQTT configurable data service is already running");
            return;
        }

        try
        {
            _logger.LogInformation("Starting MQTT configurable data service");

            // Load all enabled MQTT input configurations
            var configurations = await _configRepository.GetMqttInputConfigurationsAsync(enabledOnly: true);
            var configList = configurations.ToList();

            if (!configList.Any())
            {
                _logger.LogWarning("No enabled MQTT input configurations found");
                return;
            }

            _logger.LogInformation("Found {Count} enabled MQTT input configurations", configList.Count);

            // Group configurations by connection ID
            var configsByConnection = configList.GroupBy(c => c.ConnectionId).ToList();
            
            foreach (var connectionGroup in configsByConnection)
            {
                var connectionId = connectionGroup.Key;
                if (string.IsNullOrEmpty(connectionId))
                {
                    _logger.LogError("MQTT input configuration does not have a ConnectionId specified");
                    continue;
                }

                // Get or create shared MQTT connection
                var mqttService = await _connectionManager.GetOrCreateConnectionAsync(connectionId, $"MqttInput_{Guid.NewGuid():N}");
                if (mqttService == null)
                {
                    _logger.LogError("Could not create MQTT connection for configuration ID: {ConnectionId}", connectionId);
                    continue;
                }

                _activeConnections[connectionId] = mqttService;

                // Subscribe to data received events from the connection
                mqttService.DataReceived += OnDataReceivedFromConnection;
                
                // Subscribe to topics for each input configuration using this connection
                foreach (var config in connectionGroup)
                {
                    _configConnectionMap[config.Id] = connectionId;
                    _activeConfigurations[config.Id] = config;
                    
                    // Subscribe to the topic pattern on the shared connection
                    await mqttService.SubscribeToTopicAsync(config.TopicFilter, new HierarchicalPath());
                    
                    _logger.LogInformation("Subscribed to topic filter '{TopicFilter}' for configuration '{ConfigName}' using connection '{ConnectionId}'", 
                        config.TopicFilter, config.Name, connectionId);
                }
            }

            _isRunning = true;
            _logger.LogInformation("MQTT configurable data service started successfully with {Count} configurations", 
                _activeConfigurations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT configurable data service");
        }
    }

    private async void OnDataReceivedFromConnection(object? sender, DataPoint dataPoint)
    {
        try
        {
            // Find matching configuration for this topic
            var matchingConfig = FindMatchingConfiguration(dataPoint.Topic);
            if (matchingConfig == null)
            {
                _logger.LogDebug("No matching input configuration found for topic '{Topic}'", dataPoint.Topic);
                return;
            }

            // Process the message with the matching configuration
            await ProcessMqttMessage(matchingConfig, dataPoint.Topic, dataPoint.Value?.ToString() ?? "", dataPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data received from connection for topic '{Topic}'", dataPoint.Topic);
        }
    }


    private MqttInputConfiguration? FindMatchingConfiguration(string topic)
    {
        // Find the first configuration whose topic filter matches the received topic
        foreach (var config in _activeConfigurations.Values)
        {
            if (TopicMatches(topic, config.TopicFilter))
            {
                return config;
            }
        }

        return null;
    }

    private bool TopicMatches(string topic, string topicFilter)
    {
        // Convert MQTT wildcards to regex
        var pattern = topicFilter
            .Replace("+", "[^/]+")  // Single level wildcard
            .Replace("#", ".*");    // Multi level wildcard

        return Regex.IsMatch(topic, $"^{pattern}$", RegexOptions.IgnoreCase);
    }

    private async Task ProcessMqttMessage(MqttInputConfiguration config, string topic, string payload, DataPoint originalDataPoint)
    {
        try
        {
            object? dataValue = payload;
            var hierarchicalPath = ExtractHierarchicalPath(config, topic, payload);

            // Handle Sparkplug B decoding if enabled
            if (config.UseSparkplugB && _sparkplugBDecoder != null)
            {
                try
                {
                    // SparkplugB decoding is not fully implemented yet
                    _logger.LogInformation("SparkplugB decoding requested but not fully implemented, treating as regular payload");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode Sparkplug B payload for topic '{Topic}', treating as regular payload", topic);
                }
            }

            // Try to parse JSON payload
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(payload);
                dataValue = ExtractValueFromJson(jsonElement);
            }
            catch
            {
                // Not JSON, use raw payload
                dataValue = payload;
            }

            // Remove topic prefix if configured
            var cleanTopic = topic;
            if (!string.IsNullOrEmpty(config.TopicPrefix))
            {
                cleanTopic = cleanTopic.StartsWith(config.TopicPrefix) 
                    ? cleanTopic[config.TopicPrefix.Length..].TrimStart('/')
                    : cleanTopic;
            }

            var dataPoint = CreateDataPoint(config, cleanTopic, dataValue, hierarchicalPath);
            DataReceived?.Invoke(this, dataPoint);

            // Use topic discovery service if configured for auto-mapping
            if (config.AutoMapTopicToUNS)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var topicDiscoveryService = scope.ServiceProvider.GetService<ITopicDiscoveryService>();
                if (topicDiscoveryService != null)
                {
                    await topicDiscoveryService.CreateUnverifiedTopicAsync(cleanTopic, "MQTT", hierarchicalPath);
                }
            }

            _logger.LogDebug("Successfully processed MQTT message for topic '{Topic}' using configuration '{ConfigName}'", 
                topic, config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message for topic '{Topic}' using configuration '{ConfigName}'", 
                topic, config.Name);
        }
    }

    private HierarchicalPath? ExtractHierarchicalPath(MqttInputConfiguration config, string topic, string payload)
    {
        if (string.IsNullOrEmpty(config.TopicPattern))
        {
            return null;
        }

        try
        {
            // Parse topic pattern like "{Enterprise}/{Site}/{Area}/{WorkCenter}"
            var pattern = config.TopicPattern;
            var hierarchicalPath = new HierarchicalPath();

            // Extract placeholders from pattern
            var placeholderRegex = new Regex(@"\{([^}]+)\}");
            var placeholders = placeholderRegex.Matches(pattern)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            if (!placeholders.Any())
            {
                return null;
            }

            // Convert pattern to regex for matching
            var regexPattern = placeholderRegex.Replace(pattern, "([^/]+)");
            var match = Regex.Match(topic, $"^{regexPattern}");

            if (match.Success && match.Groups.Count - 1 == placeholders.Count)
            {
                // Map captured groups to hierarchy levels
                for (int i = 0; i < placeholders.Count; i++)
                {
                    var levelName = placeholders[i];
                    var value = match.Groups[i + 1].Value;
                    hierarchicalPath.SetValue(levelName, value);
                }

                return hierarchicalPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract hierarchical path from topic '{Topic}' using pattern '{Pattern}'", 
                topic, config.TopicPattern);
        }

        return null;
    }

    private object? ExtractValueFromJson(JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number => jsonElement.TryGetInt64(out var longValue) ? longValue : jsonElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => jsonElement.GetRawText()
        };
    }

    private DataPoint CreateDataPoint(MqttInputConfiguration config, string topic, object? value, HierarchicalPath? hierarchicalPath)
    {
        var dataPoint = new DataPoint
        {
            Topic = topic,
            Value = value,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["ConfigurationId"] = config.Id,
                ["ConfigurationName"] = config.Name,
                ["TopicFilter"] = config.TopicFilter,
                ["SourceType"] = "MQTT",
                ["QoS"] = config.QoS
            }
        };

        // Add hierarchical path if extracted
        if (hierarchicalPath != null)
        {
            dataPoint.Metadata["HierarchicalPath"] = hierarchicalPath;
        }

        // Add namespace if configured
        if (!string.IsNullOrEmpty(config.DefaultNamespace))
        {
            dataPoint.Metadata["DefaultNamespace"] = config.DefaultNamespace;
        }

        return dataPoint;
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Stopping MQTT configurable data service");

            // Unsubscribe from connection events and release connections
            foreach (var (connectionId, mqttService) in _activeConnections)
            {
                mqttService.DataReceived -= OnDataReceivedFromConnection;
                await _connectionManager.ReleaseConnectionAsync(connectionId, $"MqttInput_{connectionId}");
            }
            
            _activeConnections.Clear();
            _configConnectionMap.Clear();
            _activeConfigurations.Clear();
            _isRunning = false;

            _logger.LogInformation("MQTT configurable data service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT configurable data service");
        }
    }

    public Task<bool> IsRunningAsync() => Task.FromResult(_isRunning);

    public async Task<Dictionary<string, object>> GetStatusAsync()
    {
        var connectionStatuses = new List<object>();
        foreach (var (connectionId, mqttService) in _activeConnections)
        {
            connectionStatuses.Add(new
            {
                ConnectionId = connectionId,
                IsConnected = await mqttService.IsConnectedAsync()
            });
        }

        var status = new Dictionary<string, object>
        {
            ["IsRunning"] = _isRunning,
            ["ActiveConfigurations"] = _activeConfigurations.Count,
            ["ActiveConnections"] = connectionStatuses,
            ["Configurations"] = _activeConfigurations.Values.Select(c => new
            {
                c.Id,
                c.Name,
                c.IsEnabled,
                c.TopicFilter,
                c.QoS,
                c.AutoMapTopicToUNS,
                c.UseSparkplugB,
                c.DefaultNamespace
            }).ToList()
        };

        return status;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopAsync().Wait(TimeSpan.FromSeconds(10));
            _disposed = true;
        }
    }
}