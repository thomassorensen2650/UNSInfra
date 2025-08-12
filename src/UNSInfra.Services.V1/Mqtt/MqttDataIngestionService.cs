using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using UNSInfra.Core.Repositories;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Services.V1.Configuration;

namespace UNSInfra.Services.V1.Mqtt;

/// <summary>
/// Enhanced MQTT data ingestion service that supports both subscribing to topics and publishing messages.
/// This service manages a single MQTT connection per configuration and can be used by both input and output services.
/// </summary>
public class MqttDataIngestionService : IMqttDataIngestionService
{
    private readonly ILogger<MqttDataIngestionService> _logger;
    private readonly MqttDataIngestionConfiguration _configuration;
    private readonly ITopicDiscoveryService? _topicDiscoveryService;
    
    private IManagedMqttClient? _mqttClient;
    private readonly Dictionary<string, MqttInputConfiguration> _activeSubscriptions = new();
    private readonly object _lockObject = new();
    private bool _isRunning;
    private bool _disposed;

    public event EventHandler<DataPoint>? DataReceived;

    public MqttDataIngestionService(
        ILogger<MqttDataIngestionService> logger,
        MqttDataIngestionConfiguration configuration,
        ITopicDiscoveryService? topicDiscoveryService = null)
    {
        _logger = logger;
        _configuration = configuration;
        _topicDiscoveryService = topicDiscoveryService;
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("MQTT data ingestion service '{ServiceName}' is already running", _configuration.Name);
            return;
        }

        try
        {
            _logger.LogInformation("Starting MQTT data ingestion service '{ServiceName}' connecting to {BrokerHost}:{BrokerPort}",
                _configuration.Name, _configuration.BrokerHost, _configuration.BrokerPort);

            // Create MQTT client options from configuration
            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_configuration.BrokerHost, _configuration.BrokerPort)
                .WithClientId(_configuration.ClientId)
                .WithCleanSession(_configuration.CleanSession)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_configuration.KeepAliveInterval));

            // Add authentication if provided
            if (!string.IsNullOrEmpty(_configuration.Username))
            {
                clientOptionsBuilder.WithCredentials(_configuration.Username, _configuration.Password);
            }

            // Add TLS if enabled (basic implementation)
            if (_configuration.UseTls)
            {
                clientOptionsBuilder.WithTlsOptions(o => o.UseTls());
            }

            // Add Last Will if configured
            if (!string.IsNullOrEmpty(_configuration.LastWillTopic))
            {
                clientOptionsBuilder.WithWillTopic(_configuration.LastWillTopic)
                    .WithWillPayload(_configuration.LastWillPayload ?? string.Empty)
                    .WithWillQualityOfServiceLevel((MqttQualityOfServiceLevel)_configuration.LastWillQualityOfServiceLevel)
                    .WithWillRetain(_configuration.LastWillRetain)
                    .WithWillDelayInterval((uint)_configuration.LastWillDelayInterval);
            }

            var clientOptions = clientOptionsBuilder.Build();

            // Create managed client options
            var managedOptionsBuilder = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions)
                .WithMaxPendingMessages(_configuration.MessageBufferSize);

            if (_configuration.AutoReconnect)
            {
                managedOptionsBuilder.WithAutoReconnectDelay(TimeSpan.FromSeconds(_configuration.ReconnectDelay));
            }

            var managedOptions = managedOptionsBuilder.Build();

            // Create and configure MQTT client
            _mqttClient = new MqttFactory().CreateManagedMqttClient();

            // Subscribe to events
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

            // Start the client
            await _mqttClient.StartAsync(managedOptions);

            _isRunning = true;
            _logger.LogInformation("MQTT data ingestion service '{ServiceName}' started successfully", _configuration.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT data ingestion service '{ServiceName}'", _configuration.Name);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Stopping MQTT data ingestion service '{ServiceName}'", _configuration.Name);

            lock (_lockObject)
            {
                _isRunning = false;
                _activeSubscriptions.Clear();
            }

            if (_mqttClient != null)
            {
                await _mqttClient.StopAsync();
                _mqttClient.Dispose();
                _mqttClient = null;
            }

            _logger.LogInformation("MQTT data ingestion service '{ServiceName}' stopped successfully", _configuration.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT data ingestion service '{ServiceName}'", _configuration.Name);
            throw;
        }
    }

    public async Task SubscribeToTopicAsync(string topic, HierarchicalPath path)
    {
        if (!_isRunning || _mqttClient == null)
        {
            throw new InvalidOperationException("Service is not running");
        }

        try
        {
            _logger.LogInformation("Subscribing to MQTT topic '{Topic}' in service '{ServiceName}'", topic, _configuration.Name);

            var topicFilter = new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.SubscribeAsync(new[] { topicFilter });

            // Store subscription info for message processing
            lock (_lockObject)
            {
                _activeSubscriptions[topic] = new MqttInputConfiguration
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Direct subscription to {topic}",
                    TopicFilter = topic,
                    QoS = 1,
                    IsEnabled = true
                };
            }

            _logger.LogInformation("Successfully subscribed to MQTT topic '{Topic}' in service '{ServiceName}'", topic, _configuration.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to MQTT topic '{Topic}' in service '{ServiceName}'", topic, _configuration.Name);
            throw;
        }
    }

    public async Task UnsubscribeFromTopicAsync(string topic)
    {
        if (!_isRunning || _mqttClient == null)
        {
            throw new InvalidOperationException("Service is not running");
        }

        try
        {
            _logger.LogInformation("Unsubscribing from MQTT topic '{Topic}' in service '{ServiceName}'", topic, _configuration.Name);

            await _mqttClient.UnsubscribeAsync(new[] { topic });

            lock (_lockObject)
            {
                _activeSubscriptions.Remove(topic);
            }

            _logger.LogInformation("Successfully unsubscribed from MQTT topic '{Topic}' in service '{ServiceName}'", topic, _configuration.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from MQTT topic '{Topic}' in service '{ServiceName}'", topic, _configuration.Name);
            throw;
        }
    }

    public async Task<bool> PublishAsync(string topic, byte[] payload, int qos = 1, bool retain = false)
    {
        if (!_isRunning || _mqttClient == null)
        {
            _logger.LogWarning("Cannot publish to topic '{Topic}' - service '{ServiceName}' is not running", topic, _configuration.Name);
            return false;
        }

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)Math.Min(Math.Max(qos, 0), 2))
                .WithRetainFlag(retain)
                .Build();

            await _mqttClient.EnqueueAsync(message);

            _logger.LogDebug("Published message to MQTT topic '{Topic}' via service '{ServiceName}'", topic, _configuration.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to MQTT topic '{Topic}' via service '{ServiceName}'", topic, _configuration.Name);
            return false;
        }
    }

    public async Task<bool> PublishDataPointAsync(DataPoint dataPoint, string? topic = null, int qos = 1, bool retain = false)
    {
        var targetTopic = topic ?? dataPoint.Topic;
        
        // Serialize data point as JSON
        var jsonPayload = new
        {
            value = dataPoint.Value,
            timestamp = dataPoint.Timestamp,
            metadata = dataPoint.Metadata
        };

        var json = JsonSerializer.Serialize(jsonPayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var payload = Encoding.UTF8.GetBytes(json);
        return await PublishAsync(targetTopic, payload, qos, retain);
    }

    public async Task<bool> PublishMessageAsync(MqttApplicationMessage message)
    {
        if (!_isRunning || _mqttClient == null)
        {
            _logger.LogWarning("Cannot publish message - service '{ServiceName}' is not running", _configuration.Name);
            return false;
        }

        try
        {
            await _mqttClient.EnqueueAsync(message);
            _logger.LogDebug("Published MQTT message to topic '{Topic}' via service '{ServiceName}'", message.Topic, _configuration.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT message to topic '{Topic}' via service '{ServiceName}'", message.Topic, _configuration.Name);
            return false;
        }
    }

    public async Task<bool> IsConnectedAsync()
    {
        return await Task.FromResult(_mqttClient?.IsConnected ?? false);
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            
            _logger.LogDebug("Received MQTT message on topic '{Topic}' via service '{ServiceName}': {Payload}",
                topic, _configuration.Name, payload);

            // Find matching subscription configuration
            var matchingConfig = FindMatchingSubscription(topic);
            if (matchingConfig == null)
            {
                _logger.LogDebug("No matching subscription found for topic '{Topic}' in service '{ServiceName}'", topic, _configuration.Name);
                return;
            }

            await ProcessMqttMessage(matchingConfig, topic, payload, args.ApplicationMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message on topic '{Topic}' in service '{ServiceName}'",
                args.ApplicationMessage.Topic, _configuration.Name);
        }
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _logger.LogInformation("MQTT data ingestion service '{ServiceName}' connected to broker", _configuration.Name);
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (_isRunning)
        {
            _logger.LogWarning("MQTT data ingestion service '{ServiceName}' disconnected from broker: {Reason}",
                _configuration.Name, args.Reason);
        }
        else
        {
            _logger.LogInformation("MQTT data ingestion service '{ServiceName}' disconnected from broker (expected shutdown)",
                _configuration.Name);
        }
        return Task.CompletedTask;
    }

    private MqttInputConfiguration? FindMatchingSubscription(string topic)
    {
        lock (_lockObject)
        {
            // Find the first subscription whose topic filter matches the received topic
            foreach (var subscription in _activeSubscriptions.Values)
            {
                if (TopicMatches(topic, subscription.TopicFilter))
                {
                    return subscription;
                }
            }
        }

        return null;
    }

    private static bool TopicMatches(string topic, string topicFilter)
    {
        // Convert MQTT wildcards to regex
        var pattern = topicFilter
            .Replace("+", "[^/]+")  // Single level wildcard
            .Replace("#", ".*");    // Multi level wildcard

        return Regex.IsMatch(topic, $"^{pattern}$", RegexOptions.IgnoreCase);
    }

    private async Task ProcessMqttMessage(MqttInputConfiguration config, string topic, string payload, MqttApplicationMessage message)
    {
        try
        {
            object? dataValue = payload;

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

            var dataPoint = new DataPoint
            {
                Topic = topic,
                Value = dataValue,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["ConfigurationId"] = _configuration.Id,
                    ["ConfigurationName"] = _configuration.Name,
                    ["ServiceName"] = _configuration.Name,
                    ["TopicFilter"] = config.TopicFilter,
                    ["SourceType"] = "MQTT",
                    ["QoS"] = message.QualityOfServiceLevel,
                    ["Retain"] = message.Retain
                }
            };

            DataReceived?.Invoke(this, dataPoint);

            _logger.LogDebug("Successfully processed MQTT message for topic '{Topic}' in service '{ServiceName}'",
                topic, _configuration.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message for topic '{Topic}' in service '{ServiceName}'",
                topic, _configuration.Name);
        }
    }

    private static object? ExtractValueFromJson(JsonElement jsonElement)
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

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                StopAsync().Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MQTT data ingestion service '{ServiceName}'", _configuration.Name);
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}