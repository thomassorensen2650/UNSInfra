using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Core.Repositories;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Services.V1.SparkplugB;

namespace UNSInfra.Services.V1.Mqtt;

/// <summary>
/// Service for exporting UNS data to MQTT
/// </summary>
public class MqttDataExportService : IDisposable
{
    private readonly ILogger<MqttDataExportService> _logger;
    private readonly IInputOutputConfigurationRepository _configRepository;
    private readonly ITopicConfigurationRepository _topicConfigurationRepository;
    private readonly IRealtimeStorage _realtimeStorage;
    private readonly object? _sparkplugBEncoder;
    private IManagedMqttClient? _mqttClient;
    private readonly Dictionary<string, MqttOutputConfiguration> _activeConfigurations = new();
    private readonly Dictionary<string, DateTime> _lastPublishTimes = new();
    private bool _isRunning;
    private bool _disposed;

    // Event handler for data changes
    private readonly Dictionary<string, TaskCompletionSource<bool>> _dataSubscriptions = new();

    public MqttDataExportService(
        ILogger<MqttDataExportService> logger,
        IInputOutputConfigurationRepository configRepository,
        ITopicConfigurationRepository topicConfigurationRepository,
        IRealtimeStorage realtimeStorage,
        object? sparkplugBEncoder = null)
    {
        _logger = logger;
        _configRepository = configRepository;
        _topicConfigurationRepository = topicConfigurationRepository;
        _realtimeStorage = realtimeStorage;
        _sparkplugBEncoder = sparkplugBEncoder;
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("MQTT data export service is already running");
            return true;
        }

        try
        {
            _logger.LogInformation("Starting MQTT data export service");

            // Load enabled MQTT output configurations for data export
            var configurations = await _configRepository.GetMqttOutputConfigurationsAsync(enabledOnly: true);
            var dataConfigs = configurations
                .Where(c => c.OutputType == MqttOutputType.Data || c.OutputType == MqttOutputType.Both)
                .ToList();

            if (!dataConfigs.Any())
            {
                _logger.LogWarning("No enabled MQTT data export configurations found");
                return false;
            }

            _logger.LogInformation("Found {Count} enabled MQTT data export configurations", dataConfigs.Count);

            // Create MQTT client
            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("localhost", 1883) // Should be configurable
                .WithClientId($"UNSDataExporter_{Guid.NewGuid():N}")
                .WithCleanSession()
                .Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions)
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            await _mqttClient.StartAsync(managedOptions);

            // Setup configurations
            foreach (var config in dataConfigs)
            {
                _activeConfigurations[config.Id] = config;
            }

            // Start data change monitoring
            await StartDataChangeMonitoring();

            _isRunning = true;
            _logger.LogInformation("MQTT data export service started successfully with {Count} configurations", 
                dataConfigs.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT data export service");
            return false;
        }
    }

    private async Task StartDataChangeMonitoring()
    {
        // In a real implementation, you'd want to subscribe to data change events from the storage layer
        // For now, we'll implement a polling approach
        _ = Task.Run(async () =>
        {
            while (_isRunning && !_disposed)
            {
                try
                {
                    await CheckForDataChanges();
                    await Task.Delay(TimeSpan.FromSeconds(1)); // Check every second
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in data change monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(5)); // Wait longer on error
                }
            }
        });
    }

    private async Task CheckForDataChanges()
    {
        try
        {
            // Get all topic configurations that might have data
            var topicConfigs = await _topicConfigurationRepository.GetAllTopicConfigurationsAsync();
            
            foreach (var topicConfig in topicConfigs)
            {
                // Check if we should export data for this topic
                var applicableConfigs = _activeConfigurations.Values
                    .Where(c => ShouldExportTopic(c, topicConfig))
                    .ToList();

                if (!applicableConfigs.Any())
                    continue;

                // Get latest data for the topic
                var latestData = await _realtimeStorage.GetLatestAsync(topicConfig.Topic);
                if (latestData == null)
                    continue;

                // Check if data is recent enough
                foreach (var config in applicableConfigs)
                {
                    if (config.DataExportConfig == null)
                        continue;

                    var maxAge = TimeSpan.FromMinutes(config.DataExportConfig.MaxDataAgeMinutes);
                    if (DateTime.UtcNow - latestData.Timestamp > maxAge)
                        continue;

                    // Check if we should publish based on timing constraints
                    if (config.DataExportConfig.PublishOnChange && 
                        ShouldPublishData(config, topicConfig.Topic, latestData))
                    {
                        await PublishDataPoint(config, topicConfig, latestData);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for data changes");
        }
    }

    private bool ShouldExportTopic(MqttOutputConfiguration config, UNSInfra.Models.Hierarchy.TopicConfiguration topicConfig)
    {
        var exportConfig = config.DataExportConfig;
        if (exportConfig == null)
            return false;

        // Apply namespace filter
        if (exportConfig.NamespaceFilter.Any())
        {
            var topicNamespace = topicConfig.NSPath ?? "";
            if (!exportConfig.NamespaceFilter.Any(ns => 
                topicNamespace.Contains(ns, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // Apply topic filter
        if (exportConfig.TopicFilter.Any())
        {
            var topicName = topicConfig.Topic;
            if (!exportConfig.TopicFilter.Any(filter => TopicMatches(topicName, filter)))
            {
                return false;
            }
        }

        return true;
    }

    private bool TopicMatches(string topic, string pattern)
    {
        // Convert wildcards to regex
        var regexPattern = pattern
            .Replace("*", ".*")
            .Replace("+", "[^/]+")
            .Replace("#", ".*");

        return Regex.IsMatch(topic, $"^{regexPattern}$", RegexOptions.IgnoreCase);
    }

    private bool ShouldPublishData(MqttOutputConfiguration config, string topic, DataPoint dataPoint)
    {
        var exportConfig = config.DataExportConfig!;
        var key = $"{config.Id}:{topic}";

        // Check minimum publish interval
        if (_lastPublishTimes.TryGetValue(key, out var lastPublish))
        {
            var timeSinceLastPublish = DateTime.UtcNow - lastPublish;
            var minInterval = TimeSpan.FromMilliseconds(exportConfig.MinPublishIntervalMs);
            
            if (timeSinceLastPublish < minInterval)
            {
                return false;
            }
        }

        return true;
    }

    private async Task PublishDataPoint(
        MqttOutputConfiguration config, 
        UNSInfra.Models.Hierarchy.TopicConfiguration topicConfig, 
        DataPoint dataPoint)
    {
        try
        {
            var exportConfig = config.DataExportConfig!;
            
            // Determine the MQTT topic to publish to
            var mqttTopic = DetermineMqttTopic(config, topicConfig, dataPoint);
            
            // Build payload based on format
            var payload = BuildPayload(config, dataPoint);
            
            // Create MQTT message
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(mqttTopic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)config.QoS)
                .WithRetainFlag(config.Retain)
                .Build();

            // Publish the message
            if (_mqttClient != null)
            {
                await _mqttClient.EnqueueAsync(message);
                
                // Update last publish time
                var key = $"{config.Id}:{topicConfig.Topic}";
                _lastPublishTimes[key] = DateTime.UtcNow;

                _logger.LogDebug("Published data for topic '{Topic}' to MQTT topic '{MqttTopic}' using configuration '{ConfigName}'",
                    topicConfig.Topic, mqttTopic, config.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing data for topic '{Topic}' using configuration '{ConfigName}'",
                topicConfig.Topic, config.Name);
        }
    }

    private string DetermineMqttTopic(
        MqttOutputConfiguration config, 
        UNSInfra.Models.Hierarchy.TopicConfiguration topicConfig, 
        DataPoint dataPoint)
    {
        var exportConfig = config.DataExportConfig!;
        var topicParts = new List<string>();

        // Add prefix if configured
        if (!string.IsNullOrEmpty(config.TopicPrefix))
        {
            topicParts.Add(config.TopicPrefix);
        }

        // Use UNS path or original topic
        if (exportConfig.UseUNSPathAsTopic && topicConfig.Path != null)
        {
            // Build UNS hierarchical path
            var unsPath = topicConfig.Path.GetFullPath();
            if (!string.IsNullOrEmpty(unsPath))
            {
                topicParts.Add(unsPath);
            }
            
            // Add UNS name if available
            if (!string.IsNullOrEmpty(topicConfig.UNSName))
            {
                topicParts.Add(topicConfig.UNSName);
            }
        }
        else
        {
            // Use original topic name
            topicParts.Add(dataPoint.Topic);
        }

        return string.Join("/", topicParts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private byte[] BuildPayload(MqttOutputConfiguration config, DataPoint dataPoint)
    {
        var exportConfig = config.DataExportConfig!;

        return exportConfig.DataFormat switch
        {
            MqttDataFormat.Raw => BuildRawPayload(dataPoint),
            MqttDataFormat.Json => BuildJsonPayload(exportConfig, dataPoint),
            MqttDataFormat.SparkplugB => BuildSparkplugBPayload(dataPoint),
            _ => BuildJsonPayload(exportConfig, dataPoint)
        };
    }

    private byte[] BuildRawPayload(DataPoint dataPoint)
    {
        var value = dataPoint.Value?.ToString() ?? "";
        return Encoding.UTF8.GetBytes(value);
    }

    private byte[] BuildJsonPayload(MqttDataExportConfiguration exportConfig, DataPoint dataPoint)
    {
        var payload = new Dictionary<string, object?>();

        // Always include the value
        payload["value"] = dataPoint.Value;

        // Include timestamp if configured
        if (exportConfig.IncludeTimestamp)
        {
            payload["timestamp"] = dataPoint.Timestamp;
        }

        // Include quality/source information if configured
        if (exportConfig.IncludeQuality)
        {
            payload["quality"] = "Good"; // Simplified - could be enhanced
            
            if (dataPoint.Metadata.ContainsKey("SourceType"))
            {
                payload["source"] = dataPoint.Metadata["SourceType"];
            }
        }

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return Encoding.UTF8.GetBytes(json);
    }

    private byte[] BuildSparkplugBPayload(DataPoint dataPoint)
    {
        if (_sparkplugBEncoder == null)
        {
            _logger.LogWarning("Sparkplug B encoder not available, falling back to JSON");
            return BuildJsonPayload(new MqttDataExportConfiguration { IncludeTimestamp = true }, dataPoint);
        }

        try
        {
            // For now, fall back to JSON since SparkplugBEncoder is not available
            // This can be enhanced when the Sparkplug B implementation is complete
            _logger.LogInformation("Sparkplug B encoding requested but not fully implemented, falling back to JSON");
            return BuildJsonPayload(new MqttDataExportConfiguration { IncludeTimestamp = true }, dataPoint);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to encode Sparkplug B payload, falling back to JSON");
            return BuildJsonPayload(new MqttDataExportConfiguration { IncludeTimestamp = true }, dataPoint);
        }
    }

    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return true;
        }

        try
        {
            _logger.LogInformation("Stopping MQTT data export service");

            _isRunning = false;

            // Stop MQTT client
            if (_mqttClient != null)
            {
                await _mqttClient.StopAsync();
                _mqttClient.Dispose();
                _mqttClient = null;
            }

            _activeConfigurations.Clear();
            _lastPublishTimes.Clear();

            _logger.LogInformation("MQTT data export service stopped successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT data export service");
            return false;
        }
    }

    public Task<bool> IsRunningAsync() => Task.FromResult(_isRunning);

    public Task<Dictionary<string, object>> GetStatusAsync()
    {
        var status = new Dictionary<string, object>
        {
            ["IsRunning"] = _isRunning,
            ["MqttConnected"] = _mqttClient?.IsConnected ?? false,
            ["ActiveConfigurations"] = _activeConfigurations.Count,
            ["TrackedTopics"] = _lastPublishTimes.Count,
            ["Configurations"] = _activeConfigurations.Values.Select(c => new
            {
                c.Id,
                c.Name,
                c.IsEnabled,
                c.OutputType,
                DataConfig = c.DataExportConfig != null ? new
                {
                    c.DataExportConfig.PublishOnChange,
                    c.DataExportConfig.MinPublishIntervalMs,
                    c.DataExportConfig.MaxDataAgeMinutes,
                    c.DataExportConfig.DataFormat,
                    c.DataExportConfig.UseUNSPathAsTopic,
                    NamespaceFilterCount = c.DataExportConfig.NamespaceFilter.Count,
                    TopicFilterCount = c.DataExportConfig.TopicFilter.Count
                } : null
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