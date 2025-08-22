using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Base;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Services.V1.Models;

namespace UNSInfra.Services.V1.Connections;

/// <summary>
/// Production MQTT connection implementation using MQTTnet
/// </summary>
public class MqttConnection : BaseDataConnection
{
    private MqttConnectionConfiguration? _connectionConfig;
    private readonly Dictionary<string, MqttInputConfiguration> _inputs = new();
    private readonly Dictionary<string, MqttOutputConfiguration> _outputs = new();
    private readonly Dictionary<string, object?> _lastPublishedValues = new();
    private readonly Dictionary<string, DateTime> _lastPublishTimes = new();

    private IManagedMqttClient? _mqttClient;
    private bool _isConnected;

    /// <summary>
    /// Initializes a new MQTT connection
    /// </summary>
    public MqttConnection(string connectionId, string name, ILogger<MqttConnection> logger) 
        : base(connectionId, name, logger)
    {
    }

    /// <inheritdoc />
    public override UNSInfra.ConnectionSDK.Models.ValidationResult ValidateConfiguration(object configuration)
    {
        if (configuration is not MqttConnectionConfiguration config)
        {
            return UNSInfra.ConnectionSDK.Models.ValidationResult.Failure("Configuration must be of type MqttConnectionConfiguration");
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.Host))
            errors.Add("Host is required");

        if (config.Port < 1 || config.Port > 65535)
            errors.Add("Port must be between 1 and 65535");

        if (config.TimeoutSeconds < 1 || config.TimeoutSeconds > 300)
            errors.Add("Timeout must be between 1 and 300 seconds");

        if (config.KeepAliveSeconds < 10 || config.KeepAliveSeconds > 3600)
            errors.Add("Keep alive must be between 10 and 3600 seconds");

        return errors.Any() ? UNSInfra.ConnectionSDK.Models.ValidationResult.Failure(errors) : UNSInfra.ConnectionSDK.Models.ValidationResult.Success();
    }

    /// <inheritdoc />
    protected override async Task<bool> OnInitializeAsync(object configuration, CancellationToken cancellationToken)
    {
        _connectionConfig = (MqttConnectionConfiguration)configuration;
        
        Logger.LogInformation("Initialized MQTT connection to {Host}:{Port}", 
            _connectionConfig.Host, _connectionConfig.Port);
        
        return await Task.FromResult(true);
    }

    /// <inheritdoc />
    protected override async Task<bool> OnStartAsync(CancellationToken cancellationToken)
    {
        if (_connectionConfig == null)
            return false;

        try
        {
            Logger.LogInformation("Connecting to MQTT broker {Host}:{Port}", 
                _connectionConfig.Host, _connectionConfig.Port);

            // Create MQTT client options
            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_connectionConfig.Host, _connectionConfig.Port)
                .WithTimeout(TimeSpan.FromSeconds(_connectionConfig.TimeoutSeconds))
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_connectionConfig.KeepAliveSeconds))
                .WithCleanSession(_connectionConfig.CleanSession);

            if (!string.IsNullOrEmpty(_connectionConfig.ClientId))
            {
                clientOptions.WithClientId(_connectionConfig.ClientId);
            }

            if (!string.IsNullOrEmpty(_connectionConfig.Username))
            {
                clientOptions.WithCredentials(_connectionConfig.Username, _connectionConfig.Password);
            }

            if (_connectionConfig.UseTls)
            {
                clientOptions.WithTlsOptions(o => o.UseTls());
            }

            // Create managed client options
            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions.Build())
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .Build();

            // Create managed client
            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            
            // Subscribe to events
            _mqttClient.ApplicationMessageReceivedAsync += OnMqttMessageReceived;
            _mqttClient.ConnectedAsync += OnMqttConnected;
            _mqttClient.DisconnectedAsync += OnMqttDisconnected;

            // Start the client
            await _mqttClient.StartAsync(managedOptions);

            // Wait for connection with timeout
            var connectionTimeout = TimeSpan.FromSeconds(_connectionConfig.TimeoutSeconds);
            var startTime = DateTime.UtcNow;
            
            while (!_mqttClient.IsConnected && DateTime.UtcNow - startTime < connectionTimeout)
            {
                await Task.Delay(100, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _isConnected = _mqttClient.IsConnected;

            if (_isConnected)
            {
                Logger.LogInformation("Successfully connected to MQTT broker");
                
                // Subscribe to all configured inputs
                await SubscribeToConfiguredInputs();
                
                return true;
            }
            else
            {
                Logger.LogError("Failed to connect to MQTT broker within timeout");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to MQTT broker");
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnStopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_mqttClient != null)
            {
                await _mqttClient.StopAsync();
                _mqttClient.ApplicationMessageReceivedAsync -= OnMqttMessageReceived;
                _mqttClient.ConnectedAsync -= OnMqttConnected;
                _mqttClient.DisconnectedAsync -= OnMqttDisconnected;
                _mqttClient.Dispose();
                _mqttClient = null;
            }

            _isConnected = false;
            Logger.LogInformation("Disconnected from MQTT broker");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disconnecting from MQTT broker");
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnConfigureInputAsync(object inputConfig, CancellationToken cancellationToken)
    {
        if (inputConfig is not MqttInputConfiguration config)
            return false;

        try
        {
            if (!config.IsEnabled)
            {
                Logger.LogInformation("Input {InputId} is disabled, skipping configuration", config.Id);
                return true;
            }

            Logger.LogInformation("Configuring MQTT input {InputId} for topic pattern: {TopicPattern} (QoS: {QoS})", 
                config.Id, config.TopicPattern, config.QualityOfService);

            lock (_lockObject)
            {
                _inputs[config.Id] = config;
            }

            // Subscribe to the topic if connected
            if (_isConnected && _mqttClient != null)
            {
                await SubscribeToTopic(config);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error configuring MQTT input {InputId}", config.Id);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnRemoveInputAsync(string inputId, CancellationToken cancellationToken)
    {
        try
        {
            MqttInputConfiguration? config;
            lock (_lockObject)
            {
                _inputs.TryGetValue(inputId, out config);
                _inputs.Remove(inputId);
            }

            if (config != null && _isConnected && _mqttClient != null)
            {
                await _mqttClient.UnsubscribeAsync(config.TopicPattern);
                Logger.LogInformation("Unsubscribed from MQTT topic {TopicPattern} for input {InputId}", 
                    config.TopicPattern, inputId);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing MQTT input {InputId}", inputId);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnConfigureOutputAsync(object outputConfig, CancellationToken cancellationToken)
    {
        if (outputConfig is not MqttOutputConfiguration config)
            return false;

        try
        {
            Logger.LogInformation("Configured MQTT output {OutputId} for topic pattern: {TopicPattern}", 
                config.Id, config.TopicPattern);

            lock (_lockObject)
            {
                _outputs[config.Id] = config;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error configuring MQTT output {OutputId}", config.Id);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnRemoveOutputAsync(string outputId, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Removing MQTT output {OutputId}", outputId);

            lock (_lockObject)
            {
                _outputs.Remove(outputId);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing MQTT output {OutputId}", outputId);
            return false;
        }
    }

    /// <inheritdoc />
    public override async Task<bool> SendDataAsync(DataPoint dataPoint, string? outputId = null, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _mqttClient == null)
        {
            Logger.LogWarning("Cannot send data - MQTT client not connected");
            return false;
        }

        try
        {
            var applicableOutputs = GetApplicableOutputs(dataPoint, outputId);

            foreach (var output in applicableOutputs)
            {
                if (ShouldPublishData(output, dataPoint))
                {
                    await PublishToOutput(output, dataPoint, cancellationToken);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending data point {Topic}", dataPoint.Topic);
            return false;
        }
    }

    /// <inheritdoc />
    protected override string ExtractInputId(object inputConfig)
    {
        return ((MqttInputConfiguration)inputConfig).Id;
    }

    /// <inheritdoc />
    protected override string ExtractOutputId(object outputConfig)
    {
        return ((MqttOutputConfiguration)outputConfig).Id;
    }

    private async Task SubscribeToConfiguredInputs()
    {
        List<MqttInputConfiguration> inputs;
        lock (_lockObject)
        {
            inputs = _inputs.Values.Where(i => i.IsEnabled).ToList();
        }

        foreach (var input in inputs)
        {
            await SubscribeToTopic(input);
        }
    }

    private async Task SubscribeToTopic(MqttInputConfiguration config)
    {
        if (_mqttClient == null) return;

        try
        {
            var subscribeOptions = new MqttTopicFilterBuilder()
                .WithTopic(config.TopicPattern)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)config.QualityOfService)
                .Build();

            await _mqttClient.SubscribeAsync(new[] { subscribeOptions });
            
            Logger.LogDebug("Subscribed to MQTT topic {TopicPattern} with QoS {QoS}", 
                config.TopicPattern, config.QualityOfService);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to subscribe to topic {TopicPattern}", config.TopicPattern);
        }
    }

    private Task OnMqttConnected(MqttClientConnectedEventArgs e)
    {
        _isConnected = true;
        Logger.LogInformation("MQTT client connected");
        return Task.CompletedTask;
    }

    private Task OnMqttDisconnected(MqttClientDisconnectedEventArgs e)
    {
        _isConnected = false;
        Logger.LogWarning("MQTT client disconnected: {Reason}", e.Reason);
        return Task.CompletedTask;
    }

    private Task OnMqttMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.PayloadSegment.Array != null 
                ? Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment.Array, 
                    e.ApplicationMessage.PayloadSegment.Offset, 
                    e.ApplicationMessage.PayloadSegment.Count)
                : string.Empty;

            Logger.LogDebug("Received MQTT message on topic {Topic}: {Payload}", topic, payload);

            // Find matching input configuration
            var matchingInput = FindMatchingInput(topic);
            if (matchingInput == null)
            {
                Logger.LogDebug("No matching input configuration for topic {Topic}", topic);
                return Task.CompletedTask;
            }

            // Parse the payload based on configuration
            var value = ParsePayload(payload, matchingInput.PayloadFormat);

            // Create data point
            var dataPoint = new DataPoint
            {
                Topic = topic,
                Value = value,
                Timestamp = DateTime.UtcNow,
                Quality = "Good",
                ConnectionId = ConnectionId,
                SourceSystem = "MQTT",
                Metadata = new Dictionary<string, object>
                {
                    ["InputId"] = matchingInput.Id,
                    ["PayloadFormat"] = matchingInput.PayloadFormat.ToString(),
                    ["QoS"] = (int)e.ApplicationMessage.QualityOfServiceLevel,
                    ["Retain"] = e.ApplicationMessage.Retain
                }
            };

            // Raise data received event
            OnDataReceived(dataPoint, matchingInput.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing MQTT message on topic {Topic}", e.ApplicationMessage.Topic);
        }

        return Task.CompletedTask;
    }

    private MqttInputConfiguration? FindMatchingInput(string topic)
    {
        lock (_lockObject)
        {
            return _inputs.Values
                .Where(input => input.IsEnabled && TopicMatches(topic, input.TopicPattern))
                .FirstOrDefault();
        }
    }

    private static bool TopicMatches(string topic, string pattern)
    {
        // Convert MQTT wildcards to regex
        var regexPattern = pattern
            .Replace("+", "[^/]+")  // + matches any single level
            .Replace("#", ".*");     // # matches any number of levels

        regexPattern = "^" + regexPattern + "$";
        
        return Regex.IsMatch(topic, regexPattern);
    }

    private object? ParsePayload(string payload, PayloadFormat format)
    {
        try
        {
            return format switch
            {
                PayloadFormat.Json => JsonSerializer.Deserialize<object>(payload),
                PayloadFormat.Raw => payload,
                PayloadFormat.Auto => TryParseAsJson(payload) ?? payload,
                _ => payload
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse payload as {Format}, returning as string", format);
            return payload;
        }
    }

    private object? TryParseAsJson(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<object>(payload);
        }
        catch
        {
            return null;
        }
    }

    private List<MqttOutputConfiguration> GetApplicableOutputs(DataPoint dataPoint, string? outputId)
    {
        lock (_lockObject)
        {
            if (!string.IsNullOrEmpty(outputId))
            {
                return _outputs.TryGetValue(outputId, out var output) ? new List<MqttOutputConfiguration> { output } : new();
            }

            return _outputs.Values
                .Where(o => o.IsEnabled && MatchesTopicFilters(dataPoint.Topic, o.TopicFilters))
                .ToList();
        }
    }

    private bool MatchesTopicFilters(string topic, List<string> filters)
    {
        if (!filters.Any())
            return true;

        return filters.Any(filter => 
            string.IsNullOrWhiteSpace(filter) || 
            topic.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private bool ShouldPublishData(MqttOutputConfiguration output, DataPoint dataPoint)
    {
        var key = $"{output.Id}:{dataPoint.Topic}";

        // Check if value has changed (if PublishOnChange is enabled)
        if (output.PublishOnChange)
        {
            if (_lastPublishedValues.TryGetValue(key, out var lastValue))
            {
                var valuesEqual = lastValue == null && dataPoint.Value == null ||
                                 (lastValue != null && lastValue.Equals(dataPoint.Value));

                if (valuesEqual)
                {
                    Logger.LogDebug("Skipping publish for topic {Topic} - value unchanged", dataPoint.Topic);
                    return false;
                }
            }
        }

        // Check minimum publish interval
        if (_lastPublishTimes.TryGetValue(key, out var lastPublish))
        {
            var timeSinceLastPublish = DateTime.UtcNow - lastPublish;
            if (timeSinceLastPublish.TotalMilliseconds < output.MinPublishIntervalMs)
            {
                Logger.LogDebug("Rate limiting publish for topic {Topic}", dataPoint.Topic);
                return false;
            }
        }

        return true;
    }

    private async Task PublishToOutput(MqttOutputConfiguration output, DataPoint dataPoint, CancellationToken cancellationToken)
    {
        if (_mqttClient == null) return;

        try
        {
            // Build MQTT topic from pattern
            var mqttTopic = BuildMqttTopic(output.TopicPattern, dataPoint);

            // Build payload
            var payload = BuildPayload(output, dataPoint);

            // Create MQTT message
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(mqttTopic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)output.QualityOfService)
                .WithRetainFlag(output.Retain)
                .Build();

            // Publish message
            await _mqttClient.EnqueueAsync(message);

            Logger.LogDebug("Published to MQTT topic {MqttTopic}: {Payload}", 
                mqttTopic, Encoding.UTF8.GetString(payload));

            // Track published value and time
            var key = $"{output.Id}:{dataPoint.Topic}";
            _lastPublishedValues[key] = dataPoint.Value;
            _lastPublishTimes[key] = DateTime.UtcNow;

            IncrementDataSent();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error publishing to MQTT output {OutputId}", output.Id);
            throw;
        }
    }

    private string BuildMqttTopic(string pattern, DataPoint dataPoint)
    {
        // Simple placeholder replacement - could be more sophisticated
        return pattern
            .Replace("{topic}", dataPoint.Topic)
            .Replace("{unsPath}", dataPoint.Topic.Replace("/", "_"))
            .Replace("{connectionId}", ConnectionId);
    }

    private byte[] BuildPayload(MqttOutputConfiguration output, DataPoint dataPoint)
    {
        return output.PayloadFormat switch
        {
            PayloadFormat.Raw => Encoding.UTF8.GetBytes(dataPoint.Value?.ToString() ?? ""),
            PayloadFormat.Json => BuildJsonPayload(output, dataPoint),
            _ => BuildJsonPayload(output, dataPoint)
        };
    }

    private byte[] BuildJsonPayload(MqttOutputConfiguration output, DataPoint dataPoint)
    {
        var payload = new Dictionary<string, object?>
        {
            ["value"] = dataPoint.Value
        };

        if (output.IncludeTimestamp)
            payload["timestamp"] = dataPoint.Timestamp;

        if (output.IncludeQuality)
            payload["quality"] = dataPoint.Quality;

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return Encoding.UTF8.GetBytes(json);
    }
}