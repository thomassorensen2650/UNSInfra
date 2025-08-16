using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Core.Repositories;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Services.DataIngestion.Mock;

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
    private readonly MqttConnectionManager _connectionManager;
    private readonly object? _sparkplugBEncoder;
    private readonly Dictionary<string, MqttOutputConfiguration> _activeConfigurations = new();
    private readonly Dictionary<string, IMqttDataIngestionService> _activeConnections = new();
    private readonly Dictionary<string, string> _configConnectionMap = new();
    private readonly Dictionary<string, DateTime> _lastPublishTimes = new();
    private readonly Dictionary<string, object?> _lastPublishedValues = new();
    private bool _isRunning;
    private bool _disposed;

    // Event handler for data changes
    private readonly Dictionary<string, TaskCompletionSource<bool>> _dataSubscriptions = new();

    public MqttDataExportService(
        ILogger<MqttDataExportService> logger,
        IInputOutputConfigurationRepository configRepository,
        ITopicConfigurationRepository topicConfigurationRepository,
        IRealtimeStorage realtimeStorage,
        MqttConnectionManager connectionManager,
        object? sparkplugBEncoder = null)
    {
        _logger = logger;
        _configRepository = configRepository;
        _topicConfigurationRepository = topicConfigurationRepository;
        _realtimeStorage = realtimeStorage;
        _connectionManager = connectionManager;
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

            // Group configurations by connection ID and setup each connection
            var configsByConnection = dataConfigs.GroupBy(c => c.ConnectionId).ToList();
            
            foreach (var connectionGroup in configsByConnection)
            {
                var connectionId = connectionGroup.Key;
                if (string.IsNullOrEmpty(connectionId))
                {
                    _logger.LogError("MQTT data export configuration does not have a ConnectionId specified");
                    continue;
                }

                // Get or create shared MQTT connection
                var mqttService = await _connectionManager.GetOrCreateConnectionAsync(connectionId, $"DataExport_{Guid.NewGuid():N}");
                if (mqttService == null)
                {
                    _logger.LogError("Could not create MQTT connection for configuration ID: {ConnectionId}", connectionId);
                    continue;
                }

                _activeConnections[connectionId] = mqttService;
                
                // Setup configurations using this connection
                foreach (var config in connectionGroup)
                {
                    _configConnectionMap[config.Id] = connectionId;
                    _activeConfigurations[config.Id] = config;
                }
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

        // Check if value has actually changed
        if (_lastPublishedValues.TryGetValue(key, out var lastValue))
        {
            // Compare current value with last published value
            var valuesAreEqual = lastValue == null && dataPoint.Value == null ||
                                (lastValue != null && lastValue.Equals(dataPoint.Value));
            
            if (valuesAreEqual)
            {
                // Value hasn't changed, don't republish
                _logger.LogDebug("Skipping publish for topic '{Topic}' - value unchanged: {Value}", 
                    topic, dataPoint.Value);
                return false;
            }
        }

        // Value has changed (or this is first publish), check minimum publish interval for rate limiting
        if (_lastPublishTimes.TryGetValue(key, out var lastPublish))
        {
            var timeSinceLastPublish = DateTime.UtcNow - lastPublish;
            var minInterval = TimeSpan.FromMilliseconds(exportConfig.MinPublishIntervalMs);
            
            if (timeSinceLastPublish < minInterval)
            {
                _logger.LogDebug("Rate limiting publish for topic '{Topic}' - last publish was {TimeSince}ms ago, minimum interval is {MinInterval}ms", 
                    topic, timeSinceLastPublish.TotalMilliseconds, exportConfig.MinPublishIntervalMs);
                return false;
            }
        }

        _logger.LogDebug("Publishing data for topic '{Topic}' - value changed from {OldValue} to {NewValue}", 
            topic, lastValue, dataPoint.Value);
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
            
            // Message will be published directly using the service

            // Get the MQTT service for this configuration and publish
            if (_configConnectionMap.TryGetValue(config.Id, out var connectionId) &&
                _activeConnections.TryGetValue(connectionId, out var mqttService))
            {
                await mqttService.PublishAsync(mqttTopic, payload, config.QoS, config.Retain);
                
                // Update last publish time and value
                var key = $"{config.Id}:{topicConfig.Topic}";
                _lastPublishTimes[key] = DateTime.UtcNow;
                _lastPublishedValues[key] = dataPoint.Value;

                _logger.LogDebug("Published data for topic '{Topic}' to MQTT topic '{MqttTopic}' using configuration '{ConfigName}'",
                    topicConfig.Topic, mqttTopic, config.Name);
            }
            else
            {
                _logger.LogWarning("No MQTT connection available for configuration '{ConfigId}'", config.Id);
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

            // Release MQTT connections
            foreach (var (configId, connectionId) in _configConnectionMap)
            {
                await _connectionManager.ReleaseConnectionAsync(connectionId, $"DataExport_{configId}");
            }
            
            _activeConnections.Clear();
            _configConnectionMap.Clear();
            _activeConfigurations.Clear();
            _lastPublishTimes.Clear();
            _lastPublishedValues.Clear();

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

    public async Task<Dictionary<string, object>> GetStatusAsync()
    {
        var connectionStatuses = new List<object>();
        bool anyConnected = false;
        
        foreach (var (connectionId, mqttService) in _activeConnections)
        {
            var isConnected = await mqttService.IsConnectedAsync();
            connectionStatuses.Add(new
            {
                ConnectionId = connectionId,
                IsConnected = isConnected
            });
            
            if (isConnected)
                anyConnected = true;
        }

        var status = new Dictionary<string, object>
        {
            ["IsRunning"] = _isRunning,
            ["MqttConnected"] = anyConnected,
            ["ActiveConfigurations"] = _activeConfigurations.Count,
            ["TrackedTopics"] = _lastPublishTimes.Count,
            ["ActiveConnections"] = connectionStatuses,
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