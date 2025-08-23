using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.Models.Data;
using UNSInfra.Services.Events;

namespace UNSInfra.Services.DataIngestion;

/// <summary>
/// Adapter that bridges connection data events with the high-performance data ingestion pipeline.
/// This allows existing connections to benefit from the new stream processing capabilities.
/// </summary>
public class ConnectionDataIngestionAdapter : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConnectionDataIngestionAdapter> _logger;
    private readonly IEventBus _eventBus;
    private DataIngestionPipeline? _pipeline;
    
    // Statistics
    private long _totalEventsReceived = 0;
    private long _totalEventsIngested = 0;
    private long _totalEventsFailed = 0;
    private volatile bool _disposed = false;

    public ConnectionDataIngestionAdapter(
        IServiceProvider serviceProvider,
        ILogger<ConnectionDataIngestionAdapter> logger,
        IEventBus eventBus)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _eventBus = eventBus;
        
        // Subscribe to connection data events
        SubscribeToConnectionEvents();
    }

    /// <summary>
    /// Gets the data ingestion pipeline instance, creating it lazily.
    /// </summary>
    private DataIngestionPipeline GetPipeline()
    {
        if (_pipeline == null)
        {
            _pipeline = _serviceProvider.GetService<DataIngestionPipeline>();
            if (_pipeline == null)
            {
                _logger.LogWarning("DataIngestionPipeline service not found. High-performance ingestion disabled.");
            }
        }
        return _pipeline;
    }

    /// <summary>
    /// Subscribes to connection data events to integrate with the ingestion pipeline.
    /// </summary>
    private void SubscribeToConnectionEvents()
    {
        try
        {
            // Subscribe to data received events from connections
            _eventBus.Subscribe<ConnectionDataReceivedEvent>(OnConnectionDataReceived);
            
            _logger.LogDebug("Subscribed to connection data events for high-performance ingestion");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to connection data events");
        }
    }

    /// <summary>
    /// Handles data received events from connections.
    /// </summary>
    private async Task OnConnectionDataReceived(ConnectionDataReceivedEvent eventData)
    {
        if (_disposed) return;
        
        Interlocked.Increment(ref _totalEventsReceived);
        
        try
        {
            var pipeline = GetPipeline();
            if (pipeline == null)
            {
                Interlocked.Increment(ref _totalEventsFailed);
                return;
            }
            
            // Convert connection data to DataPoint format
            var dataPoint = ConvertToDataPoint(eventData);
            
            // Ingest into high-performance pipeline
            var success = pipeline.IngestDataPoint(dataPoint);
            
            if (success)
            {
                Interlocked.Increment(ref _totalEventsIngested);
                _logger.LogTrace("Successfully ingested data point for topic {Topic} from connection {ConnectionId}", 
                    dataPoint.Topic, eventData.ConnectionId);
            }
            else
            {
                Interlocked.Increment(ref _totalEventsFailed);
                _logger.LogWarning("Failed to ingest data point for topic {Topic} from connection {ConnectionId} - pipeline may be overloaded", 
                    eventData.Topic, eventData.ConnectionId);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalEventsFailed);
            _logger.LogError(ex, "Error processing connection data event for topic {Topic}", eventData.Topic);
        }
    }

    /// <summary>
    /// Converts connection data event to standardized DataPoint format.
    /// </summary>
    private DataPoint ConvertToDataPoint(ConnectionDataReceivedEvent eventData)
    {
        return new DataPoint
        {
            Topic = eventData.Topic,
            Value = eventData.Value,
            Timestamp = eventData.DataTimestamp,
            Source = eventData.SourceSystem ?? "Unknown",
            Metadata = eventData.Metadata ?? new Dictionary<string, object>()
            {
                ["Quality"] = eventData.Quality ?? "Good",
                ["ConnectionId"] = eventData.ConnectionId
            }
        };
    }

    /// <summary>
    /// Gets adapter statistics.
    /// </summary>
    public ConnectionAdapterStatistics GetStatistics()
    {
        return new ConnectionAdapterStatistics
        {
            TotalEventsReceived = _totalEventsReceived,
            TotalEventsIngested = _totalEventsIngested,
            TotalEventsFailed = _totalEventsFailed,
            SuccessRate = _totalEventsReceived > 0 ? (double)_totalEventsIngested / _totalEventsReceived : 0
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        try
        {
            // Unsubscribe from events
            _eventBus.Unsubscribe<ConnectionDataReceivedEvent>(OnConnectionDataReceived);
            
            _logger.LogInformation("Connection data ingestion adapter disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing connection data ingestion adapter");
        }
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event published when connection data is received and needs to be ingested.
/// </summary>
public class ConnectionDataReceivedEvent : IEvent
{
    public string Topic { get; set; } = string.Empty;
    public object? Value { get; set; }
    public DateTime DataTimestamp { get; set; }
    public string? Quality { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string? SourceSystem { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EventId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Statistics about the connection data adapter performance.
/// </summary>
public class ConnectionAdapterStatistics
{
    public long TotalEventsReceived { get; set; }
    public long TotalEventsIngested { get; set; }
    public long TotalEventsFailed { get; set; }
    public double SuccessRate { get; set; }
}