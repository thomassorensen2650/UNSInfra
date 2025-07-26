using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Services.V1.Configuration;
using UNSInfra.Services.V1.SparkplugB;

namespace UNSInfra.Services.V1.Mqtt;

/// <summary>
/// Production MQTT data service implementation with full broker connectivity,
/// SSL/TLS support, authentication, and Sparkplug B message decoding.
/// </summary>
public class MqttDataService : IMqttDataService, IDisposable
{
    private readonly ILogger<MqttDataService> _logger;
    private readonly MqttConfiguration _config;
    private readonly ITopicDiscoveryService _topicDiscoveryService;
    private readonly SparkplugBDecoder _sparkplugBDecoder;
    private readonly Dictionary<string, HierarchicalPath> _subscriptions = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IManagedMqttClient? _mqttClient;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Event raised when new data is received from the MQTT broker.
    /// </summary>
    public event EventHandler<DataPoint>? DataReceived;

    /// <summary>
    /// Initializes a new instance of the MqttDataService.
    /// </summary>
    /// <param name="config">MQTT configuration options</param>
    /// <param name="topicDiscoveryService">Service for discovering and mapping unknown topics</param>
    /// <param name="sparkplugBDecoder">Decoder for Sparkplug B messages</param>
    /// <param name="logger">Logger instance</param>
    public MqttDataService(
        IOptions<MqttConfiguration> config,
        ITopicDiscoveryService topicDiscoveryService,
        SparkplugBDecoder sparkplugBDecoder,
        ILogger<MqttDataService> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _topicDiscoveryService = topicDiscoveryService ?? throw new ArgumentNullException(nameof(topicDiscoveryService));
        _sparkplugBDecoder = sparkplugBDecoder ?? throw new ArgumentNullException(nameof(sparkplugBDecoder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the MQTT service and establishes connection to the broker.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("MQTT service is already running");
            return;
        }

        try
        {
            _logger.LogInformation("Starting MQTT service with broker {BrokerHost}:{BrokerPort}", 
                _config.BrokerHost, _config.BrokerPort);

            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateManagedMqttClient();

            var clientOptions = CreateClientOptions();
            var managedOptions = CreateManagedOptions(clientOptions);

            // Set up event handlers
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

            await _mqttClient.StartAsync(managedOptions);
            _isRunning = true;

            _logger.LogInformation("MQTT service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT service");
            throw;
        }
    }

    /// <summary>
    /// Stops the MQTT service and disconnects from the broker.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StopAsync()
    {
        if (!_isRunning || _mqttClient == null)
        {
            _logger.LogWarning("MQTT service is not running");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping MQTT service");

            _cancellationTokenSource.Cancel();
            await _mqttClient.StopAsync();
            _isRunning = false;

            _logger.LogInformation("MQTT service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT service");
            throw;
        }
    }

    /// <summary>
    /// Subscribes to an MQTT topic with explicit path mapping.
    /// </summary>
    /// <param name="topic">The MQTT topic to subscribe to</param>
    /// <param name="path">The ISA-S95 hierarchical path for data from this topic</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task SubscribeToTopicAsync(string topic, HierarchicalPath path)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be null or empty", nameof(topic));

        if (path == null)
            throw new ArgumentNullException(nameof(path));

        try
        {
            _subscriptions[topic] = path;

            if (_mqttClient?.IsConnected == true)
            {
                var subscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => f.WithTopic(topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                    .Build();

                await _mqttClient.SubscribeAsync(subscribeOptions.TopicFilters);
                _logger.LogInformation("Subscribed to topic: {Topic}", topic);
            }
            else
            {
                _logger.LogInformation("Topic {Topic} queued for subscription when client connects", topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to topic: {Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// Unsubscribes from an MQTT topic.
    /// </summary>
    /// <param name="topic">The MQTT topic to unsubscribe from</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UnsubscribeFromTopicAsync(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be null or empty", nameof(topic));

        try
        {
            _subscriptions.Remove(topic);

            if (_mqttClient?.IsConnected == true)
            {
                var unsubscribeOptions = mqttFactory.CreateUnsubscribeOptionsBuilder()
                    .WithTopicFilter(topic)
                    .Build();

                await _mqttClient.UnsubscribeAsync(unsubscribeOptions.TopicFilters);
                _logger.LogInformation("Unsubscribed from topic: {Topic}", topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from topic: {Topic}", topic);
            throw;
        }
    }

    private static readonly MqttFactory mqttFactory = new MqttFactory();

    /// <summary>
    /// Creates MQTT client options from configuration.
    /// </summary>
    private MqttClientOptions CreateClientOptions()
    {
        var optionsBuilder = mqttFactory.CreateClientOptionsBuilder()
            .WithClientId(_config.ClientId)
            .WithTcpServer(_config.BrokerHost, _config.BrokerPort)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_config.KeepAliveInterval))
            .WithTimeout(TimeSpan.FromSeconds(_config.ConnectionTimeout));

        if (_config.CleanSession)
        {
            optionsBuilder.WithCleanSession();
        }

        // Configure authentication
        if (!string.IsNullOrEmpty(_config.Username))
        {
            optionsBuilder.WithCredentials(_config.Username, _config.Password);
        }

        // Configure TLS
        if (_config.UseTls && _config.TlsConfiguration != null)
        {
            optionsBuilder.WithTlsOptions(o =>
            {
                o.UseTls();
                
                if (_config.TlsConfiguration.AllowUntrustedCertificates)
                {
                    o.WithAllowUntrustedCertificates();
                }
                
                if (_config.TlsConfiguration.IgnoreCertificateChainErrors)
                {
                    o.WithIgnoreCertificateChainErrors();
                }
                
                if (_config.TlsConfiguration.IgnoreCertificateRevocationErrors)
                {
                    o.WithIgnoreCertificateRevocationErrors();
                }

                // Load client certificate if specified
                if (!string.IsNullOrEmpty(_config.TlsConfiguration.ClientCertificatePath))
                {
                    var clientCert = new X509Certificate2(
                        _config.TlsConfiguration.ClientCertificatePath,
                        _config.TlsConfiguration.ClientCertificatePassword);
                    o.WithClientCertificates(new List<X509Certificate2> { clientCert });
                }

                // Load CA certificate if specified
                if (!string.IsNullOrEmpty(_config.TlsConfiguration.CaCertificatePath))
                {
                    var caCert = new X509Certificate2(_config.TlsConfiguration.CaCertificatePath);
                    o.WithCertificateValidationHandler(context =>
                    {
                        // Custom validation can be implemented here
                        return true;
                    });
                }
            });
        }

        // Configure Last Will and Testament
        if (_config.LastWillConfiguration != null && !string.IsNullOrEmpty(_config.LastWillConfiguration.Topic))
        {
            optionsBuilder.WithWillTopic(_config.LastWillConfiguration.Topic)
                          .WithWillPayload(_config.LastWillConfiguration.Payload)
                          .WithWillQualityOfServiceLevel((MqttQualityOfServiceLevel)_config.LastWillConfiguration.QualityOfServiceLevel)
                          .WithWillRetain(_config.LastWillConfiguration.Retain);

            if (_config.LastWillConfiguration.DelayInterval > 0)
            {
                optionsBuilder.WithWillDelayInterval((uint)_config.LastWillConfiguration.DelayInterval);
            }
        }

        return optionsBuilder.Build();
    }

    /// <summary>
    /// Creates managed MQTT client options.
    /// </summary>
    private ManagedMqttClientOptions CreateManagedOptions(MqttClientOptions clientOptions)
    {
        return new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(clientOptions)
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(_config.ReconnectDelay))
            .WithMaxPendingMessages(_config.MessageBufferSize)
            .Build();
    }

    /// <summary>
    /// Handles MQTT client connected event.
    /// </summary>
    private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _logger.LogInformation("Connected to MQTT broker: {ResultCode}", args.ConnectResult.ResultCode);

        // Subscribe to all configured topics
        if (_subscriptions.Any())
        {
            var subscribeOptionsBuilder = mqttFactory.CreateSubscribeOptionsBuilder();
            
            foreach (var subscription in _subscriptions.Keys)
            {
                subscribeOptionsBuilder.WithTopicFilter(f => f.WithTopic(subscription)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce));
            }

            var subscribeOptions = subscribeOptionsBuilder.Build();
            await _mqttClient!.SubscribeAsync(subscribeOptions.TopicFilters);
            
            _logger.LogInformation("Subscribed to {Count} topics", _subscriptions.Count);
        }
    }

    /// <summary>
    /// Handles MQTT client disconnected event.
    /// </summary>
    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("Disconnected from MQTT broker: {Reason}", args.Reason);
        
        if (_config.AutoReconnect && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _logger.LogInformation("Auto-reconnect is enabled, will attempt to reconnect");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles incoming MQTT messages.
    /// </summary>
    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            if (_config.EnableDetailedLogging)
            {
                _logger.LogDebug("Received message on topic: {Topic}", args.ApplicationMessage.Topic);
            }

            var topic = args.ApplicationMessage.Topic;
            var payload = args.ApplicationMessage.PayloadSegment.ToArray();

            // Try to decode as Sparkplug B first
            if (IsSparkplugBTopic(topic))
            {
                var sparkplugDataPoints = _sparkplugBDecoder.DecodeMessage(topic, payload);
                foreach (var dataPoint in sparkplugDataPoints)
                {
                    DataReceived?.Invoke(this, dataPoint);
                }
                return;
            }

            // Fall back to regular MQTT message handling
            await HandleRegularMqttMessage(topic, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message for topic: {Topic}", args.ApplicationMessage.Topic);
        }
    }

    /// <summary>
    /// Handles regular (non-Sparkplug B) MQTT messages.
    /// </summary>
    private async Task HandleRegularMqttMessage(string topic, byte[] payload)
    {
        HierarchicalPath? path = null;

        // Check if we have explicit subscription
        if (_subscriptions.TryGetValue(topic, out var explicitPath))
        {
            path = explicitPath;
        }
        else
        {
            // Try to resolve using topic discovery
            var configuration = await _topicDiscoveryService.ResolveTopicAsync(topic, "MQTT");
            if (configuration != null)
            {
                path = configuration.Path;
                
                if (!configuration.IsVerified)
                {
                    _logger.LogWarning("Received data for unverified topic '{Topic}'", topic);
                }
            }
            else
            {
                // Create unverified configuration for completely unknown topic
                configuration = await _topicDiscoveryService.CreateUnverifiedTopicAsync(topic, "MQTT");
                path = configuration.Path;
                _logger.LogInformation("Created unverified configuration for new topic '{Topic}'", topic);
            }
        }

        if (path != null)
        {
            // Try to parse payload as JSON, fall back to string, then bytes
            object value;
            try
            {
                var payloadString = Encoding.UTF8.GetString(payload);
                value = System.Text.Json.JsonSerializer.Deserialize<object>(payloadString) ?? payloadString;
            }
            catch
            {
                // If not valid JSON, try as string
                try
                {
                    value = Encoding.UTF8.GetString(payload);
                }
                catch
                {
                    // If not valid UTF-8, store as byte array
                    value = payload;
                }
            }

            var dataPoint = new DataPoint
            {
                Topic = topic,
                Path = path,
                Value = value,
                Source = "MQTT",
                Timestamp = DateTime.UtcNow
            };

            DataReceived?.Invoke(this, dataPoint);
        }
    }

    /// <summary>
    /// Determines if a topic follows Sparkplug B naming convention.
    /// </summary>
    private static bool IsSparkplugBTopic(string topic)
    {
        return topic.StartsWith("spBv1.0/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Disposes the MQTT service and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _cancellationTokenSource.Cancel();
            _mqttClient?.StopAsync().GetAwaiter().GetResult();
            _mqttClient?.Dispose();
            _cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MQTT service");
        }
        finally
        {
            _disposed = true;
        }
    }
}