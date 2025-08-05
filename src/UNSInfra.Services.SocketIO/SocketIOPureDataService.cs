using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocketIOClient;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.SocketIO.Configuration;

namespace UNSInfra.Services.SocketIO;

/// <summary>
/// Pure SocketIO data ingestion service that only handles data collection.
/// Auto-mapping is handled by the separate GenericAutoMappingService.
/// </summary>
public class SocketIOPureDataService : IDataIngestionService
{
    private readonly ILogger<SocketIOPureDataService> _logger;
    private readonly SocketIODataIngestionConfiguration _config;
    private SocketIOClient.SocketIO? _socketClient;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Event raised when new data is received from the Socket.IO connection.
    /// </summary>
    public event EventHandler<DataPoint>? DataReceived;

    /// <summary>
    /// Initializes a new instance of the SocketIOPureDataService.
    /// </summary>
    /// <param name="logger">Logger for this service</param>
    /// <param name="config">Configuration for this service instance</param>
    public SocketIOPureDataService(
        ILogger<SocketIOPureDataService> logger,
        SocketIODataIngestionConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Starts the Socket.IO connection and begins data ingestion.
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("SocketIOPureDataService is already running");
            return;
        }

        try
        {
            _logger.LogInformation("Starting SocketIO pure data service connection to {Url}", _config.ServerUrl);
            
            _socketClient = new SocketIOClient.SocketIO(_config.ServerUrl, new SocketIOOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(_config.ConnectionTimeoutSeconds),
                Reconnection = _config.EnableReconnection,
                ReconnectionAttempts = _config.ReconnectionAttempts,
                ReconnectionDelay = (int)TimeSpan.FromSeconds(_config.ReconnectionDelaySeconds).TotalMilliseconds,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
            });

            // Set up event handlers for configured events
            foreach (var eventName in _config.EventNames)
            {
                _socketClient.On(eventName, async (data) =>
                {
                    try
                    {
                        await ProcessReceivedDataAsync(eventName, data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Socket.IO event: {EventName}", eventName);
                    }
                });
            }

            // Set up general connection event handlers
            _socketClient.OnConnected += async (sender, e) =>
            {
                _logger.LogInformation("Connected to Socket.IO server: {Url}", _config.ServerUrl);
            };

            _socketClient.OnDisconnected += async (sender, e) =>
            {
                _logger.LogWarning("Disconnected from Socket.IO server: {Url}", _config.ServerUrl);
            };

            _socketClient.OnError += async (sender, e) =>
            {
                _logger.LogError("Socket.IO connection error: {Error}", e);
            };

            await _socketClient.ConnectAsync();
            _isRunning = true;

            _logger.LogInformation("SocketIO pure data service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SocketIO pure data service");
            _isRunning = false;
            throw;
        }
    }

    /// <summary>
    /// Stops the Socket.IO connection and data ingestion.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            _logger.LogWarning("SocketIOPureDataService is not running");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping SocketIO pure data service");

            if (_socketClient != null)
            {
                await _socketClient.DisconnectAsync();
                _socketClient.Dispose();
                _socketClient = null;
            }

            _isRunning = false;
            _logger.LogInformation("SocketIO pure data service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping SocketIO pure data service");
            throw;
        }
    }

    private async Task ProcessReceivedDataAsync(string eventName, SocketIOResponse data)
    {
        try
        {
            var jsonData = data.GetValue<JsonElement>();
            
            if (_config.EnableDetailedLogging)
            {
                _logger.LogDebug("Received Socket.IO event: {EventName}, Data: {Data}", eventName, jsonData);
            }

            // Parse JSON and create topics for each leaf value
            await ProcessJsonDataAsync(jsonData, _config.BaseTopicPath, eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received data for event: {EventName}", eventName);
        }
    }

    private async Task ProcessJsonDataAsync(JsonElement jsonElement, string basePath, string eventName, string currentPath = "")
    {
        // Build the full path with proper handling of empty strings and event names
        var pathParts = new List<string>();
        
        // Add base path if not empty
        if (!string.IsNullOrEmpty(basePath))
        {
            pathParts.Add(basePath);
        }
        
        // Add event name if not empty (this was missing!)
        if (!string.IsNullOrEmpty(eventName))
        {
            pathParts.Add(eventName);
        }
        
        // Add current path if not empty
        if (!string.IsNullOrEmpty(currentPath))
        {
            pathParts.Add(currentPath);
        }
        
        var fullPath = string.Join("/", pathParts);

        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Object:
                // Check if this object has exactly "value" and "timestamp" properties - if so, treat it as a payload
                if (IsValueTimestampPayload(jsonElement))
                {
                    // This object contains a value/timestamp payload - create a topic for the entire object
                    await CreateTopicAsync(fullPath, jsonElement, eventName);
                }
                else
                {
                    // Regular object - recurse into properties
                    foreach (var property in jsonElement.EnumerateObject())
                    {
                        var propertyName = property.Name;
                        var newPath = string.IsNullOrEmpty(currentPath) ? propertyName : $"{currentPath}/{propertyName}";
                        
                        // Check for path duplication - if the property name already exists in the base path,
                        // we might want to skip adding it again to avoid "Enterprise/Enterprise" scenarios
                        var shouldSkipDuplication = ShouldSkipDuplicatePathSegment(basePath, eventName, propertyName, currentPath);
                        
                        if (shouldSkipDuplication)
                        {
                            // Skip this level and process the property's children directly
                            await ProcessJsonDataAsync(property.Value, basePath, eventName, currentPath);
                        }
                        else
                        {
                            await ProcessJsonDataAsync(property.Value, basePath, eventName, newPath);
                        }
                    }
                }
                break;

            case JsonValueKind.Array:
                for (int i = 0; i < jsonElement.GetArrayLength(); i++)
                {
                    var newPath = string.IsNullOrEmpty(currentPath) ? i.ToString() : $"{currentPath}/{i}";
                    await ProcessJsonDataAsync(jsonElement[i], basePath, eventName, newPath);
                }
                break;

            default:
                // This is a leaf value - create a topic
                await CreateTopicAsync(fullPath, jsonElement, eventName);
                break;
        }
    }

    private async Task CreateTopicAsync(string topic, JsonElement jsonValue, string eventName)
    {
        try
        {
            // Create the data point - handle value/timestamp payload specially
            var dataPoint = new DataPoint
            {
                Topic = topic,
                Value = ExtractJsonValue(jsonValue),
                Timestamp = ExtractTimestamp(jsonValue),
                Source = _config.ServiceType,
                Path = new HierarchicalPath(), // Will be set by auto-mapping service if configured
                Metadata = new Dictionary<string, object>
                {
                    { "EventName", eventName },
                    { "JsonValueKind", jsonValue.ValueKind.ToString() },
                    { "IsValueTimestampPayload", IsValueTimestampPayload(jsonValue) },
                    { "SourceConfiguration", _config.Name }
                }
            };

            // Raise the data received event - this will be picked up by EventDrivenDataIngestionBackgroundService
            DataReceived?.Invoke(this, dataPoint);

            if (_config.EnableDetailedLogging)
            {
                _logger.LogDebug("Created data point for topic: {Topic}, Value: {Value}", 
                    topic, dataPoint.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating data point for topic: {Topic}", topic);
        }
    }

    /// <summary>
    /// Determines if we should skip adding a path segment to avoid duplication.
    /// For example, if basePath is "Enterprise" and we encounter a property named "Enterprise",
    /// we should skip it to avoid "Enterprise/Enterprise".
    /// </summary>
    private bool ShouldSkipDuplicatePathSegment(string basePath, string eventName, string propertyName, string currentPath)
    {
        // Only check for duplication at the root level (when currentPath is empty)
        if (!string.IsNullOrEmpty(currentPath))
        {
            return false;
        }

        // Build the expected path segments that will be in the final path
        var existingSegments = new List<string>();
        
        if (!string.IsNullOrEmpty(basePath))
        {
            existingSegments.AddRange(basePath.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }
        
        if (!string.IsNullOrEmpty(eventName))
        {
            existingSegments.AddRange(eventName.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }

        // Check if the property name would create a duplication
        return existingSegments.Contains(propertyName, StringComparer.OrdinalIgnoreCase);
    }

    private object ExtractJsonValue(JsonElement jsonElement)
    {
        // If this is a value/timestamp payload, extract the "value" property
        if (IsValueTimestampPayload(jsonElement))
        {
            var valueProperty = jsonElement.EnumerateObject()
                .FirstOrDefault(p => p.Name.Equals("value", StringComparison.OrdinalIgnoreCase));
            
            if (valueProperty.Value.ValueKind != JsonValueKind.Undefined)
            {
                return ExtractPrimitiveValue(valueProperty.Value);
            }
        }

        // For non-payload objects or primitives, extract as usual
        return ExtractPrimitiveValue(jsonElement);
    }

    private object ExtractPrimitiveValue(JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
            JsonValueKind.Number => jsonElement.TryGetDouble(out var doubleValue) ? doubleValue : jsonElement.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => jsonElement.GetRawText()
        };
    }

    /// <summary>
    /// Extracts the timestamp from a JSON element. If it's a value/timestamp payload,
    /// uses the timestamp property; otherwise uses current UTC time.
    /// </summary>
    /// <param name="jsonElement">The JSON element to extract timestamp from</param>
    /// <returns>The timestamp to use for the data point</returns>
    private DateTime ExtractTimestamp(JsonElement jsonElement)
    {
        // If this is a value/timestamp payload, try to extract the timestamp
        if (IsValueTimestampPayload(jsonElement))
        {
            var timestampProperty = jsonElement.EnumerateObject()
                .FirstOrDefault(p => p.Name.Equals("timestamp", StringComparison.OrdinalIgnoreCase));
            
            if (timestampProperty.Value.ValueKind != JsonValueKind.Undefined)
            {
                // Try to parse timestamp in various formats
                if (timestampProperty.Value.ValueKind == JsonValueKind.String)
                {
                    var timestampString = timestampProperty.Value.GetString();
                    if (!string.IsNullOrEmpty(timestampString))
                    {
                        // Try ISO 8601 format first
                        if (DateTime.TryParse(timestampString, out var parsedDateTime))
                        {
                            return parsedDateTime.ToUniversalTime();
                        }
                        
                        // Try Unix timestamp (seconds)
                        if (long.TryParse(timestampString, out var unixSeconds))
                        {
                            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                        }
                    }
                }
                else if (timestampProperty.Value.ValueKind == JsonValueKind.Number)
                {
                    // Numeric timestamp - assume Unix seconds or milliseconds
                    if (timestampProperty.Value.TryGetInt64(out var numericTimestamp))
                    {
                        // If the number is very large, assume milliseconds; otherwise assume seconds
                        if (numericTimestamp > 1000000000000) // Larger than 2001 in seconds, so likely milliseconds
                        {
                            return DateTimeOffset.FromUnixTimeMilliseconds(numericTimestamp).UtcDateTime;
                        }
                        else
                        {
                            return DateTimeOffset.FromUnixTimeSeconds(numericTimestamp).UtcDateTime;
                        }
                    }
                }
            }
        }

        // Default to current UTC time if timestamp cannot be extracted
        return DateTime.UtcNow;
    }

    /// <summary>
    /// Determines if a JSON object represents a value/timestamp payload pattern.
    /// Returns true if the object has exactly "value" and "timestamp" properties.
    /// </summary>
    /// <param name="jsonElement">The JSON object to check</param>
    /// <returns>True if this is a value/timestamp payload object</returns>
    private bool IsValueTimestampPayload(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != JsonValueKind.Object)
            return false;

        var properties = jsonElement.EnumerateObject().ToList();
        
        // Must have exactly 2 properties
        if (properties.Count != 2)
            return false;

        // Must have "value" and "timestamp" properties (case-insensitive)
        var propertyNames = properties.Select(p => p.Name.ToLowerInvariant()).ToHashSet();
        return propertyNames.Contains("value") && propertyNames.Contains("timestamp");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopAsync().Wait(TimeSpan.FromSeconds(5));
            _disposed = true;
        }
    }
}