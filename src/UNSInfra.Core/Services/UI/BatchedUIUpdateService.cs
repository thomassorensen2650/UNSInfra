using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace UNSInfra.Services.UI;

/// <summary>
/// Service that batches UI updates to prevent excessive DOM thrashing and improve performance.
/// Instead of immediate updates, it collects update requests and flushes them in batches.
/// </summary>
public class BatchedUIUpdateService : IDisposable
{
    private readonly ILogger<BatchedUIUpdateService> _logger;
    private readonly ConcurrentDictionary<string, UIUpdateRequest> _pendingUpdates = new();
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    
    // Configuration
    private readonly int _batchIntervalMs;
    private readonly int _maxBatchSize;
    
    // Statistics
    private long _totalUpdatesRequested = 0;
    private long _totalBatchesFlushed = 0;
    private DateTime _lastFlush = DateTime.UtcNow;
    
    private volatile bool _disposed = false;

    public BatchedUIUpdateService(ILogger<BatchedUIUpdateService> logger, int batchIntervalMs = 1000, int maxBatchSize = 100)
    {
        _logger = logger;
        _batchIntervalMs = batchIntervalMs;
        _maxBatchSize = maxBatchSize;
        
        // Create timer but don't start it yet
        _flushTimer = new Timer(FlushPendingUpdates, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Event fired when a batch of UI updates should be processed.
    /// </summary>
    public event EventHandler<UIUpdateBatchEventArgs>? UpdateBatchReady;

    /// <summary>
    /// Schedules a UI component for update. Updates are batched and flushed periodically.
    /// </summary>
    /// <param name="componentId">Unique identifier for the UI component</param>
    /// <param name="updateType">Type of update being requested</param>
    /// <param name="metadata">Additional metadata about the update</param>
    public void ScheduleUpdate(string componentId, UIUpdateType updateType, Dictionary<string, object>? metadata = null)
    {
        if (_disposed) return;
        
        var updateRequest = new UIUpdateRequest
        {
            ComponentId = componentId,
            UpdateType = updateType,
            Metadata = metadata ?? new Dictionary<string, object>(),
            RequestedAt = DateTime.UtcNow
        };
        
        // Add or update the pending request (later requests override earlier ones for same component)
        _pendingUpdates.AddOrUpdate(componentId, updateRequest, (key, existing) => updateRequest);
        
        Interlocked.Increment(ref _totalUpdatesRequested);
        
        // Start or restart the flush timer
        _flushTimer.Change(_batchIntervalMs, Timeout.Infinite);
        
        // Check if we should flush immediately due to batch size
        if (_pendingUpdates.Count >= _maxBatchSize)
        {
            _ = Task.Run(FlushPendingUpdatesAsync);
        }
    }

    /// <summary>
    /// Forces an immediate flush of all pending updates.
    /// </summary>
    public async Task FlushImmediateAsync()
    {
        if (_disposed) return;
        
        await FlushPendingUpdatesAsync();
    }

    /// <summary>
    /// Gets statistics about the batching service performance.
    /// </summary>
    public UIUpdateStatistics GetStatistics()
    {
        return new UIUpdateStatistics
        {
            TotalUpdatesRequested = _totalUpdatesRequested,
            TotalBatchesFlushed = _totalBatchesFlushed,
            PendingUpdatesCount = _pendingUpdates.Count,
            LastFlushTime = _lastFlush,
            AverageUpdatesPerBatch = _totalBatchesFlushed > 0 ? (double)_totalUpdatesRequested / _totalBatchesFlushed : 0
        };
    }

    private void FlushPendingUpdates(object? state)
    {
        _ = Task.Run(FlushPendingUpdatesAsync);
    }

    private async Task FlushPendingUpdatesAsync()
    {
        if (_disposed || _pendingUpdates.IsEmpty) return;
        
        await _flushLock.WaitAsync();
        try
        {
            if (_pendingUpdates.IsEmpty) return;
            
            // Extract all pending updates
            var updates = new List<UIUpdateRequest>();
            var keys = _pendingUpdates.Keys.ToList();
            
            foreach (var key in keys)
            {
                if (_pendingUpdates.TryRemove(key, out var update))
                {
                    updates.Add(update);
                }
            }
            
            if (updates.Count == 0) return;
            
            // Group updates by type for more efficient processing
            var groupedUpdates = updates
                .GroupBy(u => u.UpdateType)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // Fire the batch update event
            var batchArgs = new UIUpdateBatchEventArgs
            {
                Updates = updates,
                GroupedUpdates = groupedUpdates,
                BatchSize = updates.Count,
                ProcessedAt = DateTime.UtcNow
            };
            
            UpdateBatchReady?.Invoke(this, batchArgs);
            
            _lastFlush = DateTime.UtcNow;
            Interlocked.Increment(ref _totalBatchesFlushed);
            
            _logger.LogDebug("Flushed UI update batch with {Count} updates", updates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing UI update batch");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        // Flush any remaining updates
        _ = Task.Run(FlushPendingUpdatesAsync);
        
        _flushTimer?.Dispose();
        _flushLock?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a UI update request.
/// </summary>
public class UIUpdateRequest
{
    public string ComponentId { get; set; } = string.Empty;
    public UIUpdateType UpdateType { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime RequestedAt { get; set; }
}

/// <summary>
/// Event arguments for UI update batches.
/// </summary>
public class UIUpdateBatchEventArgs : EventArgs
{
    public List<UIUpdateRequest> Updates { get; set; } = new();
    public Dictionary<UIUpdateType, List<UIUpdateRequest>> GroupedUpdates { get; set; } = new();
    public int BatchSize { get; set; }
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Types of UI updates that can be batched.
/// </summary>
public enum UIUpdateType
{
    /// <summary>
    /// Topic structure or tree needs to be refreshed.
    /// </summary>
    TopicTreeRefresh,
    
    /// <summary>
    /// Statistics or counters need to be updated.
    /// </summary>
    StatisticsUpdate,
    
    /// <summary>
    /// Topic data values have been updated.
    /// </summary>
    TopicDataUpdate,
    
    /// <summary>
    /// Namespace assignments have changed.
    /// </summary>
    NamespaceUpdate,
    
    /// <summary>
    /// General UI component refresh.
    /// </summary>
    ComponentRefresh
}

/// <summary>
/// Statistics about UI update batching performance.
/// </summary>
public class UIUpdateStatistics
{
    public long TotalUpdatesRequested { get; set; }
    public long TotalBatchesFlushed { get; set; }
    public int PendingUpdatesCount { get; set; }
    public DateTime LastFlushTime { get; set; }
    public double AverageUpdatesPerBatch { get; set; }
}