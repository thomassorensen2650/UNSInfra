using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocketIOClient;
using UNSInfra.Core.Services.DataIngestion;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.AutoMapping;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.SocketIO.Configuration;
using UNSInfra.Services.TopicDiscovery;

namespace UNSInfra.Services.SocketIO;

/// <summary>
/// SocketIO data ingestion service with auto topic mapping support.
/// Automatically maps incoming topics to existing UNS tree structures.
/// </summary>
public class SocketIOAutoMappableDataService : IAutoMappableDataIngestionService
{
    private readonly ILogger<SocketIOAutoMappableDataService> _logger;
    private readonly ITopicDiscoveryService? _topicDiscoveryService;
    private readonly IAutoTopicMapper? _autoTopicMapper;
    private readonly IServiceProvider? _serviceProvider;
    private readonly SocketIODataIngestionConfiguration _config;
    private SocketIOClient.SocketIO? _socketClient;
    private bool _isRunning;
    private bool _disposed;
    private readonly AutoMappingStatistics _autoMappingStats = new();

    /// <summary>
    /// Event raised when new data is received from the Socket.IO connection.
    /// </summary>
    public event EventHandler<DataPoint>? DataReceived;

    /// <summary>
    /// Event fired when a topic is successfully auto-mapped.
    /// </summary>
    public event EventHandler<TopicAutoMappedEventArgs>? TopicAutoMapped;

    /// <summary>
    /// Event fired when auto mapping fails for a topic.
    /// </summary>
    public event EventHandler<AutoMappingFailedEventArgs>? AutoMappingFailed;

    /// <summary>
    /// Gets the auto topic mapper configuration for this service.
    /// </summary>
    public AutoTopicMapperConfiguration? AutoMapperConfiguration => _config.AutoMapperConfiguration;

    /// <summary>
    /// Initializes a new instance of the SocketIOAutoMappableDataService.
    /// </summary>
    /// <param name="logger">Logger for this service</param>
    /// <param name="topicDiscoveryService">Service for discovering and mapping unknown topics</param>
    /// <param name="autoTopicMapper">Service for auto mapping topics</param>
    /// <param name="serviceProvider">Service provider for resolving scoped dependencies</param>
    /// <param name="config">Configuration for this service instance</param>
    public SocketIOAutoMappableDataService(
        ILogger<SocketIOAutoMappableDataService> logger,
        ITopicDiscoveryService topicDiscoveryService,
        IAutoTopicMapper autoTopicMapper,
        IServiceProvider serviceProvider,
        SocketIODataIngestionConfiguration config)
    {
        _logger = logger;
        _topicDiscoveryService = topicDiscoveryService;
        _autoTopicMapper = autoTopicMapper;
        _serviceProvider = serviceProvider;
        _config = config;

        // Set up default auto mapper configuration if none provided
        if (_config.AutoMapperConfiguration == null)
        {
            _config.AutoMapperConfiguration = CreateDefaultAutoMapperConfiguration();
        }
    }

    /// <summary>
    /// Starts the Socket.IO connection and begins data ingestion.
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("SocketIOAutoMappableDataService is already running");
            return;
        }

        try
        {
            _logger.LogInformation("Starting SocketIO auto-mappable data service connection to {Url}", _config.ServerUrl);
            
            var legacyConfig = _config.ToLegacyConfiguration();
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

            _logger.LogInformation("SocketIO auto-mappable data service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SocketIO auto-mappable data service");
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
            _logger.LogWarning("SocketIOAutoMappableDataService is not running");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping SocketIO auto-mappable data service");

            if (_socketClient != null)
            {
                await _socketClient.DisconnectAsync();
                _socketClient.Dispose();
                _socketClient = null;
            }

            _isRunning = false;
            _logger.LogInformation("SocketIO auto-mappable data service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping SocketIO auto-mappable data service");
            throw;
        }
    }

    /// <summary>
    /// Updates the auto topic mapper configuration for this service.
    /// </summary>
    /// <param name="configuration">The new auto mapper configuration</param>
    public async Task UpdateAutoMapperConfigurationAsync(AutoTopicMapperConfiguration? configuration)
    {
        _config.AutoMapperConfiguration = configuration;
        _logger.LogInformation("Auto mapper configuration updated. Enabled: {Enabled}", 
            configuration?.Enabled ?? false);
    }

    /// <summary>
    /// Gets statistics about auto mapping operations.
    /// </summary>
    /// <returns>Auto mapping statistics</returns>
    public AutoMappingStatistics GetAutoMappingStatistics()
    {
        return _autoMappingStats;
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
        var fullPath = string.IsNullOrEmpty(currentPath) ? basePath : $"{basePath}/{currentPath}";

        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in jsonElement.EnumerateObject())
                {
                    var newPath = string.IsNullOrEmpty(currentPath) ? property.Name : $"{currentPath}/{property.Name}";
                    await ProcessJsonDataAsync(property.Value, basePath, eventName, newPath);
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
                // This is a leaf value - create a topic and try auto mapping
                await CreateTopicWithAutoMappingAsync(fullPath, jsonElement, eventName);
                break;
        }
    }

    private async Task CreateTopicWithAutoMappingAsync(string topic, JsonElement jsonValue, string eventName)
    {
        _autoMappingStats.TotalTopicsProcessed++;

        try
        {
            // First, try auto mapping if enabled
            TopicConfiguration? topicConfig = null;
            bool wasAutoMapped = false;

            if (_config.AutoMapperConfiguration?.Enabled == true && _autoTopicMapper != null)
            {
                topicConfig = await _autoTopicMapper.TryMapTopicAsync(topic, _config.ServiceType, _config.AutoMapperConfiguration);
                
                if (topicConfig != null)
                {
                    wasAutoMapped = true;
                    _autoMappingStats.SuccessfullyMapped++;
                    _autoMappingStats.AverageConfidence = UpdateAverageConfidence(
                        _autoMappingStats.AverageConfidence, 
                        _autoMappingStats.SuccessfullyMapped,
                        (double)(topicConfig.Metadata.TryGetValue("MappingConfidence", out var conf) ? conf : 0.0));

                    // Fire auto mapping success event
                    TopicAutoMapped?.Invoke(this, new TopicAutoMappedEventArgs
                    {
                        OriginalTopic = topic,
                        MappedPath = topicConfig.NSPath,
                        Confidence = (double)(topicConfig.Metadata.TryGetValue("MappingConfidence", out var confidence) ? confidence : 0.0),
                        UsedRule = null // Could be extracted from metadata if needed
                    });

                    _logger.LogDebug("Successfully auto-mapped topic: {Topic} to {MappedPath}", topic, topicConfig.NSPath);
                }
                else
                {
                    _autoMappingStats.MappingFailed++;
                    
                    // Get suggestions for failed mapping
                    var suggestions = await _autoTopicMapper.GetAutoMappingSuggestionsAsync(topic, _config.AutoMapperConfiguration);
                    
                    // Fire auto mapping failed event
                    AutoMappingFailed?.Invoke(this, new AutoMappingFailedEventArgs
                    {
                        Topic = topic,
                        Reason = "No suitable UNS path found with sufficient confidence",
                        UsedFallback = true,
                        Suggestions = suggestions
                    });

                    _logger.LogDebug("Auto mapping failed for topic: {Topic}. Found {SuggestionCount} suggestions", 
                        topic, suggestions.Count);
                }
            }

            // If auto mapping failed or is disabled, fall back to topic discovery service
            if (topicConfig == null && _topicDiscoveryService != null)
            {
                topicConfig = await _topicDiscoveryService.ResolveTopicAsync(topic, _config.ServiceType);
                _autoMappingStats.AlreadyMapped++;
            }

            // Create the data point
            var dataPoint = new DataPoint
            {
                Topic = topic,
                Value = ExtractJsonValue(jsonValue),
                Timestamp = DateTime.UtcNow,
                Source = _config.ServiceType,
                Path = topicConfig?.Path ?? new HierarchicalPath(),
                Metadata = new Dictionary<string, object>
                {
                    { "EventName", eventName },
                    { "AutoMapped", wasAutoMapped },
                    { "JsonValueKind", jsonValue.ValueKind.ToString() }
                }
            };

            // Raise the data received event
            DataReceived?.Invoke(this, dataPoint);

            if (_config.EnableDetailedLogging)
            {
                _logger.LogDebug("Created data point for topic: {Topic}, Value: {Value}, AutoMapped: {AutoMapped}", 
                    topic, dataPoint.Value, wasAutoMapped);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating topic with auto mapping for: {Topic}", topic);
            _autoMappingStats.MappingFailed++;
        }
    }

    private object ExtractJsonValue(JsonElement jsonElement)
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

    private double UpdateAverageConfidence(double currentAverage, long count, double newValue)
    {
        if (count <= 1) return newValue;
        return ((currentAverage * (count - 1)) + newValue) / count;
    }

    private AutoTopicMapperConfiguration CreateDefaultAutoMapperConfiguration()
    {
        return new AutoTopicMapperConfiguration
        {
            Enabled = false, // Disabled by default
            MinimumConfidence = 0.8,
            MaxSearchDepth = 8,
            StripPrefixes = new List<string> { "socketio/", "socketio/update/" },
            CreateMissingNodes = false,
            CaseSensitive = false,
            CustomRules = new List<AutoMappingRule>
            {
                new()
                {
                    TopicPattern = @"socketio/update/([^/]+)/([^/]+)/?.*",
                    UNSPathTemplate = "{0}/{1}",
                    Confidence = 0.9,
                    IsActive = true,
                    Description = "SocketIO update event pattern (Enterprise/OEE format)"
                }
            }
        };
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