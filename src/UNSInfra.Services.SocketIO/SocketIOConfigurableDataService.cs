using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Core.Repositories;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.TopicDiscovery;

namespace UNSInfra.Services.SocketIO;

/// <summary>
/// Enhanced SocketIO data service that uses configurable input configurations
/// </summary>
public class SocketIOConfigurableDataService : IDataIngestionService
{
    private readonly ILogger<SocketIOConfigurableDataService> _logger;
    private readonly IInputOutputConfigurationRepository _configRepository;
    private readonly ITopicDiscoveryService? _topicDiscoveryService;
    private readonly Dictionary<string, SocketIOClient.SocketIO> _socketClients = new();
    private readonly Dictionary<string, SocketIOInputConfiguration> _activeConfigurations = new();
    private bool _isRunning;
    private bool _disposed;

    public event EventHandler<DataPoint>? DataReceived;

    public SocketIOConfigurableDataService(
        ILogger<SocketIOConfigurableDataService> logger,
        IInputOutputConfigurationRepository configRepository,
        ITopicDiscoveryService? topicDiscoveryService = null)
    {
        _logger = logger;
        _configRepository = configRepository;
        _topicDiscoveryService = topicDiscoveryService;
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("SocketIO configurable data service is already running");
            return;
        }

        try
        {
            _logger.LogInformation("Starting SocketIO configurable data service");

            // Load all enabled SocketIO input configurations
            var configurations = await _configRepository.GetSocketIOInputConfigurationsAsync(enabledOnly: true);
            var configList = configurations.ToList();

            if (!configList.Any())
            {
                _logger.LogWarning("No enabled SocketIO input configurations found");
                return;
            }

            _logger.LogInformation("Found {Count} enabled SocketIO input configurations", configList.Count);

            // Start a client for each configuration
            foreach (var config in configList)
            {
                await StartConfigurationAsync(config, CancellationToken.None);
            }

            _isRunning = true;
            _logger.LogInformation("SocketIO configurable data service started successfully with {Count} configurations", 
                _activeConfigurations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SocketIO configurable data service");
        }
    }

    private async Task StartConfigurationAsync(SocketIOInputConfiguration config, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting SocketIO configuration '{ConfigName}' with {EventCount} events", 
                config.Name, config.EventNames.Count);

            // Create SocketIO client (assuming server URL is in description or custom field)
            var serverUrl = ExtractServerUrl(config);
            if (string.IsNullOrEmpty(serverUrl))
            {
                _logger.LogError("No server URL found for configuration '{ConfigName}'", config.Name);
                return;
            }

            var client = new SocketIOClient.SocketIO(serverUrl);

            // Subscribe to each configured event
            foreach (var eventName in config.EventNames)
            {
                client.On(eventName, async response =>
                {
                    try
                    {
                        await ProcessEventData(config, eventName, response);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing event '{EventName}' from configuration '{ConfigName}'", 
                            eventName, config.Name);
                    }
                });

                _logger.LogDebug("Subscribed to event '{EventName}' for configuration '{ConfigName}'", 
                    eventName, config.Name);
            }

            // Connect the client
            await client.ConnectAsync();

            _socketClients[config.Id] = client;
            _activeConfigurations[config.Id] = config;

            _logger.LogInformation("Successfully started configuration '{ConfigName}' connected to {ServerUrl}", 
                config.Name, serverUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SocketIO configuration '{ConfigName}'", config.Name);
        }
    }

    private async Task ProcessEventData(SocketIOInputConfiguration config, string eventName, SocketIOResponse response)
    {
        try
        {
            // Convert response to JSON for processing
            var jsonData = response.GetValue<JsonElement>();
            var dataJson = jsonData.ToString();

            if (string.IsNullOrEmpty(dataJson))
            {
                _logger.LogDebug("Received empty data for event '{EventName}' in configuration '{ConfigName}'", 
                    eventName, config.Name);
                return;
            }

            _logger.LogDebug("Processing event '{EventName}' data: {Data}", eventName, dataJson);

            // Parse JSON for hierarchical path extraction
            var hierarchicalPath = ExtractHierarchicalPath(config, dataJson);
            var topic = ExtractTopic(config, eventName, dataJson);
            var dataValue = ExtractDataValue(config, dataJson);

            // Create data point
            var dataPoint = new DataPoint
            {
                Topic = topic,
                Value = dataValue,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["EventName"] = eventName,
                    ["ConfigurationId"] = config.Id,
                    ["ConfigurationName"] = config.Name,
                    ["SourceType"] = "SocketIO"
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

            // Raise data received event
            DataReceived?.Invoke(this, dataPoint);

            // Use topic discovery service if configured for auto-mapping
            if (config.AutoMapToUNS && _topicDiscoveryService != null)
            {
                await _topicDiscoveryService.CreateUnverifiedTopicAsync(dataPoint.Topic, "SocketIO", hierarchicalPath);
            }

            _logger.LogDebug("Successfully processed event '{EventName}' for topic '{Topic}'", eventName, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event '{EventName}' data from configuration '{ConfigName}'", 
                eventName, config.Name);
        }
    }

    private HierarchicalPath? ExtractHierarchicalPath(SocketIOInputConfiguration config, string jsonData)
    {
        if (!config.HierarchyPathMappings.Any())
        {
            return null;
        }

        try
        {
            var json = JObject.Parse(jsonData);
            var hierarchicalPath = new HierarchicalPath();
            bool hasAnyValue = false;

            foreach (var mapping in config.HierarchyPathMappings)
            {
                try
                {
                    var token = json.SelectToken(mapping.Value);
                    if (token != null)
                    {
                        hierarchicalPath.SetValue(mapping.Key, token.ToString());
                        hasAnyValue = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract hierarchical path for level '{Level}' using path '{JsonPath}'", 
                        mapping.Key, mapping.Value);
                }
            }

            return hasAnyValue ? hierarchicalPath : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON for hierarchical path extraction");
            return null;
        }
    }

    private string ExtractTopic(SocketIOInputConfiguration config, string eventName, string jsonData)
    {
        if (string.IsNullOrEmpty(config.TopicPathMapping))
        {
            // Use event name as default topic
            return $"socketio/{eventName}";
        }

        try
        {
            var json = JObject.Parse(jsonData);
            var token = json.SelectToken(config.TopicPathMapping);
            if (token != null)
            {
                return token.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract topic using path '{JsonPath}', falling back to event name", 
                config.TopicPathMapping);
        }

        return $"socketio/{eventName}";
    }

    private object? ExtractDataValue(SocketIOInputConfiguration config, string jsonData)
    {
        var valuePath = config.DataValuePathMapping ?? "$.value";

        try
        {
            var json = JObject.Parse(jsonData);
            var token = json.SelectToken(valuePath);
            
            if (token == null)
            {
                // If no specific value path found, return the entire JSON
                return jsonData;
            }

            // Convert token to appropriate .NET type
            return token.Type switch
            {
                JTokenType.Integer => token.Value<long>(),
                JTokenType.Float => token.Value<double>(),
                JTokenType.Boolean => token.Value<bool>(),
                JTokenType.Date => token.Value<DateTime>(),
                JTokenType.String => token.Value<string>(),
                _ => token.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract data value using path '{JsonPath}', returning raw JSON", valuePath);
            return jsonData;
        }
    }

    private string? ExtractServerUrl(SocketIOInputConfiguration config)
    {
        // Try to extract server URL from description (simple approach)
        // In a real implementation, you might want to add a ServerUrl property to the configuration
        if (!string.IsNullOrEmpty(config.Description) && 
            (config.Description.StartsWith("http://") || config.Description.StartsWith("https://")))
        {
            return config.Description.Split(' ')[0]; // Take first URL-like part
        }

        // Default fallback (should be configured properly in real usage)
        return "http://localhost:3000";
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Stopping SocketIO configurable data service");

            // Disconnect all clients
            var disconnectTasks = _socketClients.Values.Select(client => client.DisconnectAsync());
            await Task.WhenAll(disconnectTasks);

            // Dispose all clients
            foreach (var client in _socketClients.Values)
            {
                client.Dispose();
            }

            _socketClients.Clear();
            _activeConfigurations.Clear();
            _isRunning = false;

            _logger.LogInformation("SocketIO configurable data service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping SocketIO configurable data service");
        }
    }

    public Task<bool> IsRunningAsync() => Task.FromResult(_isRunning);

    public Task<Dictionary<string, object>> GetStatusAsync()
    {
        var status = new Dictionary<string, object>
        {
            ["IsRunning"] = _isRunning,
            ["ActiveConfigurations"] = _activeConfigurations.Count,
            ["ConnectedClients"] = _socketClients.Count,
            ["Configurations"] = _activeConfigurations.Values.Select(c => new
            {
                c.Id,
                c.Name,
                c.IsEnabled,
                EventCount = c.EventNames.Count,
                c.AutoMapToUNS,
                c.DefaultNamespace
            }).ToList()
        };

        return Task.FromResult(status);
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