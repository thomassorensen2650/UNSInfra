using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocketIOClient;
using UNSInfra.Models.Data;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Services.SocketIO.Configuration;

namespace UNSInfra.Services.SocketIO;

/// <summary>
/// Data ingestion service that connects to Socket.IO servers and parses JSON data into individual topics.
/// Generic Socket.IO client that can connect to any Socket.IO server and create topics based on JSON paths.
/// </summary>
public class SocketIODataService : IDataIngestionService
{
    private readonly ILogger<SocketIODataService> _logger;
    private readonly ITopicDiscoveryService _topicDiscoveryService;
    private readonly SocketIOConfiguration _config;
    private SocketIOClient.SocketIO? _socketClient;
    private bool _isRunning;

    /// <summary>
    /// Event raised when new data is received from the Socket.IO connection.
    /// </summary>
    public event EventHandler<DataPoint>? DataReceived;

    /// <summary>
    /// Initializes a new instance of the SocketIODataService.
    /// </summary>
    /// <param name="logger">Logger for this service</param>
    /// <param name="topicDiscoveryService">Service for discovering and mapping unknown topics</param>
    /// <param name="config">Configuration options for the service</param>
    public SocketIODataService(
        ILogger<SocketIODataService> logger,
        ITopicDiscoveryService topicDiscoveryService,
        IOptions<SocketIOConfiguration> config)
    {
        _logger = logger;
        _topicDiscoveryService = topicDiscoveryService;
        _config = config.Value;
    }

    /// <summary>
    /// Initializes a new instance of the SocketIODataService with default configuration.
    /// </summary>
    /// <param name="logger">Logger for this service</param>
    /// <param name="topicDiscoveryService">Service for discovering and mapping unknown topics</param>
    public SocketIODataService(
        ILogger<SocketIODataService> logger,
        ITopicDiscoveryService topicDiscoveryService)
    {
        _logger = logger;
        _topicDiscoveryService = topicDiscoveryService;
        _config = new SocketIOConfiguration();
    }

    /// <summary>
    /// Starts the Socket.IO connection and begins data ingestion.
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("SocketIODataService is already running");
            return;
        }

        try
        {
            _logger.LogInformation("Starting SocketIO data service connection to {Url}", _config.ServerUrl);
            
            _socketClient = new SocketIOClient.SocketIO(_config.ServerUrl, new SocketIOOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(_config.ConnectionTimeoutSeconds),
                Reconnection = _config.EnableReconnection,
                ReconnectionAttempts = _config.ReconnectionAttempts,
                ReconnectionDelay = (int)TimeSpan.FromSeconds(_config.ReconnectionDelaySeconds).TotalMilliseconds,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
            });

            // Set up event handlers
            SetupEventHandlers();
            
            await _socketClient.ConnectAsync();
            _isRunning = true;
            
            _logger.LogInformation("Successfully connected to Socket.IO server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SocketIO data service");
            await StopAsync();
            // Don't rethrow to allow other services to continue
        }
    }

    /// <summary>
    /// Stops the Socket.IO connection and data ingestion.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Stopping SocketIO data service");
            
            if (_socketClient?.Connected == true)
            {
                await _socketClient.DisconnectAsync();
            }
            
            _socketClient?.Dispose();
            _socketClient = null;
            
            _isRunning = false;
            _logger.LogInformation("SocketIO data service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping SocketIO data service");
        }
    }

    /// <summary>
    /// Sets up Socket.IO event handlers for connection, disconnection, and data events.
    /// </summary>
    private void SetupEventHandlers()
    {
        if (_socketClient == null) return;

        // Connection events
        _socketClient.OnConnected += (sender, e) =>
        {
            _logger.LogInformation("Socket.IO connected to server");
        };

        _socketClient.OnDisconnected += (sender, e) =>
        {
            _logger.LogWarning("Socket.IO disconnected from server: {Reason}", e);
            _isRunning = false;
        };

        _socketClient.OnError += (sender, e) =>
        {
            _logger.LogError("Socket.IO error: {Error}", e);
        };

        _socketClient.OnReconnectAttempt += (sender, attempt) =>
        {
            _logger.LogInformation("Socket.IO reconnection attempt {Attempt}", attempt);
        };

        _socketClient.OnReconnected += (sender, e) =>
        {
            _logger.LogInformation("Socket.IO reconnected to server");
            _isRunning = true;
        };

        // Data events - listen for common event names
        SetupDataEventHandlers();
    }

    /// <summary>
    /// Sets up event handlers for data events from the Socket.IO server.
    /// </summary>
    private void SetupDataEventHandlers()
    {
        if (_socketClient == null) return;

        // Use configured event names or default to common ones
        var eventNames = _config.EventNames?.Length > 0 
            ? _config.EventNames 
            : new[] { "update", "data", "factory-data", "sensor-data", "update", "message", "namespace-update" };

        foreach (var eventName in eventNames)
        {
            _socketClient.On(eventName, async (response) =>
            {
                try
                {
                    if (_config.EnableDetailedLogging)
                    {
                        _logger.LogDebug("Received '{EventName}' event from Socket.IO server", eventName);
                    }
                    await ProcessSocketIOEvent(eventName, response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing '{EventName}' event", eventName);
                }
            });
        }

        // Listen for any event (fallback) - only if no specific events configured
        if (_config.EventNames?.Length == 0)
        {
            _socketClient.OnAny(async (eventName, response) =>
            {
                try
                {
                    if (_config.EnableDetailedLogging)
                    {
                        _logger.LogDebug("Received unknown event '{EventName}' from Socket.IO server", eventName);
                    }
                    await ProcessSocketIOEvent(eventName, response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing unknown event '{EventName}'", eventName);
                }
            });
        }
    }

    /// <summary>
    /// Processes a Socket.IO event and extracts data points.
    /// </summary>
    private async Task ProcessSocketIOEvent(string eventName, SocketIOResponse response)
    {
        try
        {
            if (response != null)
            {
                try
                {
                    var data = response.GetValue<object>(0);
                
                    if (data != null)
                    {
                        var jsonString = data.ToString();
                        if (!string.IsNullOrEmpty(jsonString))
                        {
                            if (_config.EnableDetailedLogging)
                            {
                                _logger.LogDebug("Processing event '{EventName}' with data: {Data}", eventName, jsonString);
                            }
                            await ProcessJsonMessage(jsonString, eventName);
                        }
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    // No data available at index 0, which is normal for some events
                    if (_config.EnableDetailedLogging)
                    {
                        _logger.LogDebug("No data available for event '{EventName}'", eventName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Socket.IO event '{EventName}'", eventName);
        }
    }

    /// <summary>
    /// Processes a JSON message by extracting individual data points and creating topics.
    /// </summary>
    private async Task ProcessJsonMessage(string jsonMessage, string eventName = "data")
    {
        try
        {
            if (_config.EnableDetailedLogging)
            {
                _logger.LogDebug("Processing JSON message from event '{EventName}': {Message}", eventName, jsonMessage);
            }
            
            using var document = JsonDocument.Parse(jsonMessage);
            var rootElement = document.RootElement;
            
            // Process the JSON recursively and create topics for each path
            var basePath = $"{_config.BaseTopicPath}/{eventName}";
            await ProcessJsonElement(rootElement, basePath);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON message from event '{EventName}': {Message}", eventName, jsonMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing JSON message from event '{EventName}'", eventName);
        }
    }

    /// <summary>
    /// Recursively processes JSON elements and creates topics for leaf values.
    /// </summary>
    private async Task ProcessJsonElement(JsonElement element, string currentPath)
    {
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var newPath = $"{currentPath}/{property.Name}";
                        await ProcessJsonElement(property.Value, newPath);
                    }
                    break;

                case JsonValueKind.Array:
                    for (int i = 0; i < element.GetArrayLength(); i++)
                    {
                        var arrayElement = element[i];
                        var newPath = $"{currentPath}[{i}]";
                        await ProcessJsonElement(arrayElement, newPath);
                    }
                    break;

                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    // This is a leaf value - create a data point
                    await CreateDataPoint(currentPath, element);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing JSON element at path: {Path}", currentPath);
        }
    }

    /// <summary>
    /// Creates a data point for a leaf value and raises the DataReceived event.
    /// </summary>
    private async Task CreateDataPoint(string topic, JsonElement value)
    {
        try
        {
            // Get or create topic configuration
            var topicConfig = await _topicDiscoveryService.ResolveTopicAsync(topic, "SocketIO");
            if (topicConfig == null)
            {
                _logger.LogWarning("Could not resolve topic configuration for: {Topic}", topic);
                return;
            }

            // Extract the actual value based on JSON type
            object? dataValue = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetInt64(out var longVal) ? longVal : value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => value.ToString()
            };

            // Unescape Unicode escape sequences in string values (like \u0022 for quotes)
            if (dataValue is string stringValue && stringValue.Contains("\\u"))
            {
                try
                {
                    dataValue = System.Text.RegularExpressions.Regex.Unescape(stringValue);
                }
                catch (Exception unescapeEx)
                {
                    _logger.LogWarning(unescapeEx, "Failed to unescape Unicode characters in value: {Value}", stringValue);
                    // Keep original value if unescaping fails
                }
            }

            // Create the data point
            var dataPoint = new DataPoint
            {
                Topic = topic,
                Path = topicConfig.Path,
                Value = dataValue,
                Timestamp = DateTime.UtcNow,
                Source = "SocketIO",
                Metadata = new Dictionary<string, object>
                {
                    { "ServerUrl", _config.ServerUrl },
                    { "JsonPath", topic },
                    { "ValueType", value.ValueKind.ToString() },
                    { "Protocol", "Socket.IO" }
                }
            };

            if (_config.EnableDetailedLogging)
            {
                _logger.LogDebug("Created data point for topic {Topic} with value {Value}", topic, dataValue);
            }

            // Raise the event
            DataReceived?.Invoke(this, dataPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating data point for topic: {Topic}", topic);
        }
    }

    /// <summary>
    /// Disposes of resources used by the service.
    /// </summary>
    public void Dispose()
    {
        StopAsync().Wait(5000); // Wait up to 5 seconds for graceful shutdown
    }
}