using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SocketIOClient;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Base;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Services.SocketIO.Models;

namespace UNSInfra.Services.SocketIO.Connections;

/// <summary>
/// Production SocketIO connection implementation using SocketIOClient
/// </summary>
public class SocketIOConnection : BaseDataConnection
{
    private SocketIOConnectionConfiguration? _connectionConfig;
    private readonly Dictionary<string, SocketIOInputConfiguration> _inputs = new();
    private readonly Dictionary<string, SocketIOOutputConfiguration> _outputs = new();
    private readonly Dictionary<string, object?> _lastEmittedValues = new();
    private readonly Dictionary<string, DateTime> _lastEmitTimes = new();

    private SocketIOClient.SocketIO? _socketClient;
    private bool _isConnected;

    /// <summary>
    /// Initializes a new SocketIO connection
    /// </summary>
    public SocketIOConnection(string connectionId, string name, ILogger<SocketIOConnection> logger) 
        : base(connectionId, name, logger)
    {
    }

    /// <inheritdoc />
    public override UNSInfra.ConnectionSDK.Models.ValidationResult ValidateConfiguration(object configuration)
    {
        if (configuration is not SocketIOConnectionConfiguration config)
        {
            return UNSInfra.ConnectionSDK.Models.ValidationResult.Failure("Configuration must be of type SocketIOConnectionConfiguration");
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.ServerUrl))
            errors.Add("ServerUrl is required");

        if (!Uri.TryCreate(config.ServerUrl, UriKind.Absolute, out _))
            errors.Add("ServerUrl must be a valid URL");

        if (config.ConnectionTimeoutSeconds < 1 || config.ConnectionTimeoutSeconds > 300)
            errors.Add("Connection timeout must be between 1 and 300 seconds");

        if (config.ReconnectionAttempts < 0 || config.ReconnectionAttempts > 20)
            errors.Add("Reconnection attempts must be between 0 and 20");

        if (config.ReconnectionDelaySeconds < 1 || config.ReconnectionDelaySeconds > 60)
            errors.Add("Reconnection delay must be between 1 and 60 seconds");

        return errors.Any() ? UNSInfra.ConnectionSDK.Models.ValidationResult.Failure(errors) : UNSInfra.ConnectionSDK.Models.ValidationResult.Success();
    }

    /// <inheritdoc />
    protected override async Task<bool> OnInitializeAsync(object configuration, CancellationToken cancellationToken)
    {
        _connectionConfig = (SocketIOConnectionConfiguration)configuration;
        
        Logger.LogInformation("Initialized SocketIO connection to {ServerUrl}", 
            _connectionConfig.ServerUrl);
        
        return await Task.FromResult(true);
    }

    /// <inheritdoc />
    protected override async Task<bool> OnStartAsync(CancellationToken cancellationToken)
    {
        if (_connectionConfig == null)
            return false;

        try
        {
            Logger.LogInformation("Connecting to SocketIO server {ServerUrl}", 
                _connectionConfig.ServerUrl);

            // Create SocketIO client options
            var options = new SocketIOOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(_connectionConfig.ConnectionTimeoutSeconds),
                Reconnection = _connectionConfig.EnableReconnection,
                ReconnectionAttempts = _connectionConfig.ReconnectionAttempts,
                ReconnectionDelay = (int)TimeSpan.FromSeconds(_connectionConfig.ReconnectionDelaySeconds).TotalMilliseconds,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
            };

            // Add auth token if provided
            if (!string.IsNullOrEmpty(_connectionConfig.AuthToken))
            {
                options.Auth = new { token = _connectionConfig.AuthToken };
            }

            // Note: Query parameters and extra headers parsing removed for simplicity
            // These can be added later if needed

            // Create client
            _socketClient = new SocketIOClient.SocketIO(_connectionConfig.ServerUrl, options);
            
            // Subscribe to connection events
            _socketClient.OnConnected += (sender, e) => OnSocketConnected(sender, e);
            _socketClient.OnDisconnected += (sender, e) => OnSocketDisconnected(sender, e);
            _socketClient.OnError += (sender, e) => OnSocketError(sender, e);
            _socketClient.OnReconnectAttempt += (sender, attempt) => OnSocketReconnectAttempt(sender, attempt);
            _socketClient.OnReconnected += (sender, attemptNumber) => OnSocketReconnected(sender, attemptNumber);

            // Connect to the server
            await _socketClient.ConnectAsync();

            // Wait for connection with timeout
            var connectionTimeout = TimeSpan.FromSeconds(_connectionConfig.ConnectionTimeoutSeconds);
            var startTime = DateTime.UtcNow;
            
            while (!_socketClient.Connected && DateTime.UtcNow - startTime < connectionTimeout)
            {
                await Task.Delay(100, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _isConnected = _socketClient.Connected;

            if (_isConnected)
            {
                Logger.LogInformation("Successfully connected to SocketIO server");
                
                // Subscribe to all configured inputs
                await SubscribeToConfiguredInputs();
                
                return true;
            }
            else
            {
                Logger.LogError("Failed to connect to SocketIO server within timeout");
                UpdateStatus(ConnectionStatus.Error, "Connection timeout");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to SocketIO server");
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnStopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_socketClient != null)
            {
                if (_socketClient.Connected)
                {
                    await _socketClient.DisconnectAsync();
                }
                
                // Note: Event handlers were added as lambda expressions, so we can't directly unsubscribe
                // The disposal of the client will handle cleanup
                
                _socketClient.Dispose();
                _socketClient = null;
            }

            _isConnected = false;
            Logger.LogInformation("Disconnected from SocketIO server");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disconnecting from SocketIO server");
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnConfigureInputAsync(object inputConfig, CancellationToken cancellationToken)
    {
        if (inputConfig is not SocketIOInputConfiguration config)
            return false;

        try
        {
            if (!config.IsEnabled)
            {
                Logger.LogInformation("Input {InputId} is disabled, skipping configuration", config.Id);
                return true;
            }

            Logger.LogInformation("Configuring SocketIO input {InputId} for events: {EventNames}", 
                config.Id, string.Join(", ", config.EventNames));

            lock (_lockObject)
            {
                _inputs[config.Id] = config;
            }

            // Subscribe to events if connected
            if (_isConnected && _socketClient != null)
            {
                await SubscribeToEvents(config);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error configuring SocketIO input {InputId}", config.Id);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnRemoveInputAsync(string inputId, CancellationToken cancellationToken)
    {
        try
        {
            SocketIOInputConfiguration? config;
            lock (_lockObject)
            {
                _inputs.TryGetValue(inputId, out config);
                _inputs.Remove(inputId);
            }

            if (config != null)
            {
                Logger.LogInformation("Removed SocketIO input {InputId}", inputId);
                // Note: SocketIO client doesn't have a direct way to unsubscribe from specific events
                // They would need to be re-configured by stopping and starting the connection
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing SocketIO input {InputId}", inputId);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnConfigureOutputAsync(object outputConfig, CancellationToken cancellationToken)
    {
        if (outputConfig is not SocketIOOutputConfiguration config)
            return false;

        try
        {
            Logger.LogInformation("Configured SocketIO output {OutputId} for event: {EventName}", 
                config.Id, config.EventName);

            lock (_lockObject)
            {
                _outputs[config.Id] = config;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error configuring SocketIO output {OutputId}", config.Id);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnRemoveOutputAsync(string outputId, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Removing SocketIO output {OutputId}", outputId);

            lock (_lockObject)
            {
                _outputs.Remove(outputId);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing SocketIO output {OutputId}", outputId);
            return false;
        }
    }

    /// <inheritdoc />
    public override async Task<bool> SendDataAsync(DataPoint dataPoint, string? outputId = null, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _socketClient == null)
        {
            Logger.LogWarning("Cannot send data - SocketIO client not connected");
            return false;
        }

        try
        {
            var applicableOutputs = GetApplicableOutputs(dataPoint, outputId);

            foreach (var output in applicableOutputs)
            {
                if (ShouldEmitData(output, dataPoint))
                {
                    await EmitToOutput(output, dataPoint, cancellationToken);
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
        return ((SocketIOInputConfiguration)inputConfig).Id;
    }

    /// <inheritdoc />
    protected override string ExtractOutputId(object outputConfig)
    {
        return ((SocketIOOutputConfiguration)outputConfig).Id;
    }

    private async Task SubscribeToConfiguredInputs()
    {
        List<SocketIOInputConfiguration> inputs;
        lock (_lockObject)
        {
            inputs = _inputs.Values.Where(i => i.IsEnabled).ToList();
        }

        Logger.LogInformation("Subscribing to {Count} enabled inputs", inputs.Count);

        foreach (var input in inputs)
        {
            await SubscribeToEvents(input);
        }
    }

    private async Task SubscribeToEvents(SocketIOInputConfiguration config)
    {
        if (_socketClient == null) return;

        try
        {
            if (config.ListenForAllEvents || !config.EventNames.Any())
            {
                // Listen for any event
                _socketClient.OnAny(async (eventName, response) =>
                {
                    await ProcessSocketIOEvent(eventName, response, config);
                });
                
                Logger.LogDebug("Subscribed to all events for input {InputId}", config.Id);
            }
            else
            {
                // Listen for specific events
                foreach (var eventName in config.EventNames)
                {
                    _socketClient.On(eventName, async (response) =>
                    {
                        await ProcessSocketIOEvent(eventName, response, config);
                    });
                    
                    Logger.LogDebug("Subscribed to event {EventName} for input {InputId}", eventName, config.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to subscribe to events for input {InputId}", config.Id);
        }
    }

    private async Task ProcessSocketIOEvent(string eventName, SocketIOResponse response, SocketIOInputConfiguration config)
    {
        try
        {
            if (_connectionConfig?.EnableDetailedLogging == true)
            {
                Logger.LogDebug("Processing event '{EventName}' for input {InputId}", eventName, config.Id);
            }

            var data = response?.GetValue<object>(0);
            if (data == null)
            {
                if (_connectionConfig?.EnableDetailedLogging == true)
                {
                    Logger.LogDebug("No data available for event '{EventName}'", eventName);
                }
                return;
            }

            var jsonString = data.ToString();
            if (string.IsNullOrEmpty(jsonString))
                return;

            // Parse the JSON and create data points
            await ProcessJsonMessage(jsonString, eventName, config);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing SocketIO event '{EventName}' for input {InputId}", eventName, config.Id);
        }
    }

    private async Task ProcessJsonMessage(string jsonMessage, string eventName, SocketIOInputConfiguration config)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonMessage);
            var rootElement = document.RootElement;
            
            // Process the JSON recursively and create topics for each path
            var basePath = $"{Name}/{eventName}";
            await ProcessJsonElement(rootElement, basePath, config);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse JSON message from event '{EventName}': {Message}", eventName, jsonMessage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing JSON message from event '{EventName}'", eventName);
        }
    }

    private async Task ProcessJsonElement(JsonElement element, string currentPath, SocketIOInputConfiguration config)
    {
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (IsValueTimestampPayload(element))
                    {
                        await CreateDataPoint(currentPath, element, config);    
                    }
                    else 
                    {
                        foreach (var property in element.EnumerateObject())
                        {
                            var newPath = $"{currentPath}/{property.Name}";
                            await ProcessJsonElement(property.Value, newPath, config);
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    for (int i = 0; i < element.GetArrayLength(); i++)
                    {
                        var arrayElement = element[i];
                        var newPath = $"{currentPath}[{i}]";
                        await ProcessJsonElement(arrayElement, newPath, config);
                    }
                    break;

                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    // This is a leaf value - create a data point
                    await CreateDataPoint(currentPath, element, config);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing JSON element at path: {Path}", currentPath);
        }
    }

    private async Task CreateDataPoint(string topic, JsonElement value, SocketIOInputConfiguration config)
    {
        try
        {
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

            // Create the data point
            var dataPoint = new DataPoint
            {
                Topic = topic,
                Value = dataValue,
                Timestamp = DateTime.UtcNow,
                Quality = "Good",
                ConnectionId = ConnectionId,
                SourceSystem = "SocketIO",
                Metadata = new Dictionary<string, object>
                {
                    ["InputId"] = config.Id,
                    ["ServerUrl"] = _connectionConfig?.ServerUrl ?? "",
                    ["DataFormat"] = config.DataFormat.ToString(),
                    ["ValueType"] = value.ValueKind.ToString(),
                    ["Protocol"] = "Socket.IO"
                }
            };

            if (_connectionConfig?.EnableDetailedLogging == true)
            {
                Logger.LogDebug("Created data point for topic {Topic} with value {Value}", topic, dataValue);
            }

            // Raise data received event
            OnDataReceived(dataPoint, config.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating data point for topic: {Topic}", topic);
        }
    }

    private static bool IsValueTimestampPayload(JsonElement jsonElement)
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

    private List<SocketIOOutputConfiguration> GetApplicableOutputs(DataPoint dataPoint, string? outputId)
    {
        lock (_lockObject)
        {
            if (!string.IsNullOrEmpty(outputId))
            {
                return _outputs.TryGetValue(outputId, out var output) ? new List<SocketIOOutputConfiguration> { output } : new();
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

    private bool ShouldEmitData(SocketIOOutputConfiguration output, DataPoint dataPoint)
    {
        var key = $"{output.Id}:{dataPoint.Topic}";

        // Check if value has changed (if EmitOnChange is enabled)
        if (output.EmitOnChange)
        {
            if (_lastEmittedValues.TryGetValue(key, out var lastValue))
            {
                var valuesEqual = lastValue == null && dataPoint.Value == null ||
                                 (lastValue != null && lastValue.Equals(dataPoint.Value));

                if (valuesEqual)
                {
                    Logger.LogDebug("Skipping emit for topic {Topic} - value unchanged", dataPoint.Topic);
                    return false;
                }
            }
        }

        // Check minimum emit interval
        if (_lastEmitTimes.TryGetValue(key, out var lastEmit))
        {
            var timeSinceLastEmit = DateTime.UtcNow - lastEmit;
            if (timeSinceLastEmit.TotalMilliseconds < output.MinEmitIntervalMs)
            {
                Logger.LogDebug("Rate limiting emit for topic {Topic}", dataPoint.Topic);
                return false;
            }
        }

        return true;
    }

    private async Task EmitToOutput(SocketIOOutputConfiguration output, DataPoint dataPoint, CancellationToken cancellationToken)
    {
        if (_socketClient == null) return;

        try
        {
            // Build payload
            var payload = BuildPayload(output, dataPoint);

            // Emit the event
            await _socketClient.EmitAsync(output.EventName, payload);

            Logger.LogDebug("Emitted to SocketIO event {EventName}: {Payload}", 
                output.EventName, JsonSerializer.Serialize(payload));

            // Track emitted value and time
            var key = $"{output.Id}:{dataPoint.Topic}";
            _lastEmittedValues[key] = dataPoint.Value;
            _lastEmitTimes[key] = DateTime.UtcNow;

            IncrementDataSent();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error emitting to SocketIO output {OutputId}", output.Id);
            throw;
        }
    }

    private object BuildPayload(SocketIOOutputConfiguration output, DataPoint dataPoint)
    {
        return output.DataFormat switch
        {
            SocketIODataFormat.Raw => dataPoint.Value ?? "",
            SocketIODataFormat.Json => BuildJsonPayload(output, dataPoint),
            _ => BuildJsonPayload(output, dataPoint)
        };
    }

    private object BuildJsonPayload(SocketIOOutputConfiguration output, DataPoint dataPoint)
    {
        var payload = new Dictionary<string, object?>
        {
            ["topic"] = dataPoint.Topic,
            ["value"] = dataPoint.Value
        };

        if (output.IncludeTimestamp)
            payload["timestamp"] = dataPoint.Timestamp;

        if (output.IncludeQuality)
            payload["quality"] = dataPoint.Quality;

        return payload;
    }

    private void OnSocketConnected(object sender, EventArgs e)
    {
        _isConnected = true;
        UpdateStatus(ConnectionStatus.Connected, "Connected to SocketIO server");
        Logger.LogInformation("SocketIO client connected");
    }

    private void OnSocketDisconnected(object sender, string e)
    {
        _isConnected = false;
        UpdateStatus(ConnectionStatus.Disconnected, $"Disconnected from SocketIO server: {e}");
        Logger.LogWarning("SocketIO client disconnected: {Reason}", e);
    }

    private void OnSocketError(object sender, string e)
    {
        UpdateStatus(ConnectionStatus.Error, $"SocketIO error: {e}");
        Logger.LogError("SocketIO client error: {Error}", e);
    }

    private void OnSocketReconnectAttempt(object sender, int attempt)
    {
        Logger.LogInformation("SocketIO reconnection attempt {Attempt}", attempt);
    }

    private void OnSocketReconnected(object sender, int attemptNumber)
    {
        _isConnected = true;
        Logger.LogInformation("SocketIO client reconnected after {Attempts} attempts", attemptNumber);
    }
}