using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;

namespace UNSInfra.Services.DataIngestion;

/// <summary>
/// High-performance stream processing service for data ingestion.
/// Processes incoming data points in batches to optimize database operations and reduce latency.
/// </summary>
public class StreamProcessingService : IDisposable
{
    private readonly ILogger<StreamProcessingService> _logger;
    private readonly Channel<DataPoint> _dataChannel;
    private readonly ChannelWriter<DataPoint> _channelWriter;
    private readonly ChannelReader<DataPoint> _channelReader;
    
    private readonly Timer _batchTimer;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private readonly ConcurrentQueue<DataPoint> _batchBuffer = new();
    
    // Configuration
    private readonly int _batchSize;
    private readonly int _batchIntervalMs;
    private readonly int _maxChannelCapacity;
    
    // Statistics
    private long _totalMessagesReceived = 0;
    private long _totalBatchesProcessed = 0;
    private long _totalProcessingErrors = 0;
    private DateTime _lastBatchTime = DateTime.UtcNow;
    private volatile bool _disposed = false;

    public StreamProcessingService(
        ILogger<StreamProcessingService> logger,
        int batchSize = 1000,
        int batchIntervalMs = 2000,
        int maxChannelCapacity = 10000)
    {
        _logger = logger;
        _batchSize = batchSize;
        _batchIntervalMs = batchIntervalMs;
        _maxChannelCapacity = maxChannelCapacity;
        
        // Create bounded channel for back-pressure handling
        var options = new BoundedChannelOptions(maxChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        
        _dataChannel = Channel.CreateBounded<DataPoint>(options);
        _channelWriter = _dataChannel.Writer;
        _channelReader = _dataChannel.Reader;
        
        // Start batch timer
        _batchTimer = new Timer(ProcessBatchOnTimer, null, _batchIntervalMs, _batchIntervalMs);
        
        // Start background processing task
        _ = Task.Run(ProcessChannelDataAsync);
    }

    /// <summary>
    /// Event fired when a batch of data points is ready for processing.
    /// </summary>
    public event EventHandler<DataBatchEventArgs>? BatchReady;

    /// <summary>
    /// Enqueues a data point for stream processing.
    /// Non-blocking operation with back-pressure handling.
    /// </summary>
    public bool EnqueueDataPoint(DataPoint dataPoint)
    {
        if (_disposed) return false;
        
        try
        {
            // Try to write to channel (non-blocking)
            var success = _channelWriter.TryWrite(dataPoint);
            if (success)
            {
                Interlocked.Increment(ref _totalMessagesReceived);
            }
            else
            {
                _logger.LogWarning("Channel is full, dropping oldest data point. Consider increasing capacity or processing speed.");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueueing data point for topic {Topic}", dataPoint.Topic);
            return false;
        }
    }

    /// <summary>
    /// Gets processing statistics.
    /// </summary>
    public StreamProcessingStatistics GetStatistics()
    {
        return new StreamProcessingStatistics
        {
            TotalMessagesReceived = _totalMessagesReceived,
            TotalBatchesProcessed = _totalBatchesProcessed,
            TotalProcessingErrors = _totalProcessingErrors,
            CurrentBatchBufferSize = _batchBuffer.Count,
            LastBatchTime = _lastBatchTime,
            ChannelUtilization = (double)_batchBuffer.Count / _maxChannelCapacity,
            AverageMessagesPerBatch = _totalBatchesProcessed > 0 ? (double)_totalMessagesReceived / _totalBatchesProcessed : 0
        };
    }

    private async Task ProcessChannelDataAsync()
    {
        try
        {
            await foreach (var dataPoint in _channelReader.ReadAllAsync())
            {
                if (_disposed) break;
                
                _batchBuffer.Enqueue(dataPoint);
                
                // Process batch if we hit the size limit
                if (_batchBuffer.Count >= _batchSize)
                {
                    await ProcessBatchAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in stream processing background task");
        }
    }

    private void ProcessBatchOnTimer(object? state)
    {
        if (_disposed || _batchBuffer.IsEmpty) return;
        
        _ = Task.Run(ProcessBatchAsync);
    }

    private async Task ProcessBatchAsync()
    {
        if (_disposed) return;
        
        await _processingLock.WaitAsync();
        try
        {
            if (_batchBuffer.IsEmpty) return;
            
            // Extract batch from buffer
            var batch = new List<DataPoint>();
            while (batch.Count < _batchSize && _batchBuffer.TryDequeue(out var dataPoint))
            {
                batch.Add(dataPoint);
            }
            
            if (batch.Count == 0) return;
            
            // Create batch event args
            var batchArgs = new DataBatchEventArgs
            {
                DataPoints = batch,
                BatchSize = batch.Count,
                ProcessedAt = DateTime.UtcNow,
                BatchId = Guid.NewGuid().ToString()
            };
            
            // Fire batch ready event
            BatchReady?.Invoke(this, batchArgs);
            
            _lastBatchTime = DateTime.UtcNow;
            Interlocked.Increment(ref _totalBatchesProcessed);
            
            _logger.LogDebug("Processed batch {BatchId} with {Count} data points", 
                batchArgs.BatchId, batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data batch");
            Interlocked.Increment(ref _totalProcessingErrors);
        }
        finally
        {
            _processingLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        // Complete the channel writer
        _channelWriter.Complete();
        
        // Process any remaining data
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessBatchAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing final batch during disposal");
            }
        });
        
        _batchTimer?.Dispose();
        _processingLock?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for data batch processing.
/// </summary>
public class DataBatchEventArgs : EventArgs
{
    public List<DataPoint> DataPoints { get; set; } = new();
    public int BatchSize { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string BatchId { get; set; } = string.Empty;
}

/// <summary>
/// Statistics about stream processing performance.
/// </summary>
public class StreamProcessingStatistics
{
    public long TotalMessagesReceived { get; set; }
    public long TotalBatchesProcessed { get; set; }
    public long TotalProcessingErrors { get; set; }
    public int CurrentBatchBufferSize { get; set; }
    public DateTime LastBatchTime { get; set; }
    public double ChannelUtilization { get; set; }
    public double AverageMessagesPerBatch { get; set; }
}