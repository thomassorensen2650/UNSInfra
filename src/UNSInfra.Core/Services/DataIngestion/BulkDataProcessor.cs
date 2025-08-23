using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Services.Events;

namespace UNSInfra.Services.DataIngestion;

/// <summary>
/// High-performance bulk data processor that optimizes database operations
/// by batching writes and implementing intelligent topic discovery.
/// </summary>
public class BulkDataProcessor : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BulkDataProcessor> _logger;
    private readonly IEventBus _eventBus;
    
    // Topic discovery cache
    private readonly Dictionary<string, DateTime> _knownTopics = new();
    private readonly SemaphoreSlim _topicCacheLock = new(1, 1);
    
    // Statistics
    private long _totalDataPointsProcessed = 0;
    private long _totalBatchesProcessed = 0;
    private long _newTopicsDiscovered = 0;
    private long _databaseWrites = 0;
    private volatile bool _disposed = false;
    
    public BulkDataProcessor(
        IServiceProvider serviceProvider,
        ILogger<BulkDataProcessor> logger,
        IEventBus eventBus)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Processes a batch of data points with optimized bulk operations.
    /// </summary>
    public async Task ProcessBatchAsync(List<DataPoint> dataPoints)
    {
        if (_disposed || dataPoints.Count == 0) return;
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Processing batch of {Count} data points", dataPoints.Count);
            
            // Group data points by characteristics for optimized processing
            var groupedData = GroupDataPointsForOptimalProcessing(dataPoints);
            
            // Process each group
            await ProcessGroupedDataAsync(groupedData);
            
            // Update statistics
            Interlocked.Add(ref _totalDataPointsProcessed, dataPoints.Count);
            Interlocked.Increment(ref _totalBatchesProcessed);
            
            stopwatch.Stop();
            _logger.LogDebug("Processed batch of {Count} data points in {ElapsedMs}ms", 
                dataPoints.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data batch with {Count} data points", dataPoints.Count);
        }
    }

    /// <summary>
    /// Groups data points by processing characteristics for optimal batch operations.
    /// </summary>
    private Dictionary<string, List<DataPoint>> GroupDataPointsForOptimalProcessing(List<DataPoint> dataPoints)
    {
        var groups = new Dictionary<string, List<DataPoint>>();
        
        foreach (var dataPoint in dataPoints)
        {
            // Group by source system for optimized storage operations
            var groupKey = dataPoint.Source;
            
            if (!groups.ContainsKey(groupKey))
            {
                groups[groupKey] = new List<DataPoint>();
            }
            
            groups[groupKey].Add(dataPoint);
        }
        
        return groups;
    }

    /// <summary>
    /// Processes grouped data with optimized storage operations.
    /// </summary>
    private async Task ProcessGroupedDataAsync(Dictionary<string, List<DataPoint>> groupedData)
    {
        var tasks = groupedData.Select(async group =>
        {
            try
            {
                await ProcessDataGroupAsync(group.Key, group.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing data group {GroupKey} with {Count} points", 
                    group.Key, group.Value.Count);
            }
        });
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Processes a specific group of data points.
    /// </summary>
    private async Task ProcessDataGroupAsync(string groupKey, List<DataPoint> dataPoints)
    {
        using var scope = _serviceProvider.CreateScope();
        var realtimeStorage = scope.ServiceProvider.GetRequiredService<IRealtimeStorage>();
        var historicalStorage = scope.ServiceProvider.GetRequiredService<IHistoricalStorage>();
        
        // Discover new topics first
        var newTopics = await DiscoverNewTopicsAsync(dataPoints);
        
        // Bulk write to realtime storage
        await BulkWriteToRealtimeStorageAsync(realtimeStorage, dataPoints);
        
        // Bulk write to historical storage
        await BulkWriteToHistoricalStorageAsync(historicalStorage, dataPoints);
        
        // Fire events for new topics discovered
        if (newTopics.Count > 0)
        {
            await FireTopicDiscoveryEventsAsync(newTopics);
        }
    }

    /// <summary>
    /// Discovers new topics in the batch using intelligent caching.
    /// </summary>
    private async Task<List<string>> DiscoverNewTopicsAsync(List<DataPoint> dataPoints)
    {
        var newTopics = new List<string>();
        var topicsToCheck = new HashSet<string>();
        
        await _topicCacheLock.WaitAsync();
        try
        {
            // First pass: check against local cache
            foreach (var dataPoint in dataPoints)
            {
                if (!_knownTopics.ContainsKey(dataPoint.Topic))
                {
                    topicsToCheck.Add(dataPoint.Topic);
                }
            }
            
            // Second pass: check new topics against database if needed
            if (topicsToCheck.Count > 0)
            {
                using var scope = _serviceProvider.CreateScope();
                var cachedTopicService = scope.ServiceProvider.GetService<UNSInfra.Services.TopicBrowser.CachedTopicBrowserService>();
                
                if (cachedTopicService != null)
                {
                    foreach (var topic in topicsToCheck)
                    {
                        // Check if topic exists in cached service
                        var existingTopics = await cachedTopicService.GetLatestTopicStructureAsync();
                        var topicExists = existingTopics.Any(t => t.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase));
                        
                        if (!topicExists)
                        {
                            newTopics.Add(topic);
                            _logger.LogDebug("Discovered new topic: {Topic}", topic);
                        }
                        
                        // Cache the topic as known
                        _knownTopics[topic] = DateTime.UtcNow;
                    }
                }
            }
            
            if (newTopics.Count > 0)
            {
                Interlocked.Add(ref _newTopicsDiscovered, newTopics.Count);
            }
        }
        finally
        {
            _topicCacheLock.Release();
        }
        
        return newTopics;
    }

    /// <summary>
    /// Bulk writes data points to realtime storage with optimization.
    /// </summary>
    private async Task BulkWriteToRealtimeStorageAsync(IRealtimeStorage realtimeStorage, List<DataPoint> dataPoints)
    {
        try
        {
            // Group by topic for optimized writes
            var topicGroups = dataPoints.GroupBy(dp => dp.Topic);
            
            foreach (var topicGroup in topicGroups)
            {
                // For realtime storage, we typically want the latest value per topic
                var latestDataPoint = topicGroup
                    .OrderByDescending(dp => dp.Timestamp)
                    .First();
                
                await realtimeStorage.StoreAsync(latestDataPoint);
            }
            
            Interlocked.Increment(ref _databaseWrites);
            _logger.LogDebug("Bulk wrote {TopicCount} topics to realtime storage", topicGroups.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk writing to realtime storage");
        }
    }

    /// <summary>
    /// Bulk writes data points to historical storage with optimization.
    /// </summary>
    private async Task BulkWriteToHistoricalStorageAsync(IHistoricalStorage historicalStorage, List<DataPoint> dataPoints)
    {
        try
        {
            // For historical storage, we want all data points
            await historicalStorage.StoreBulkAsync(dataPoints);
            
            Interlocked.Increment(ref _databaseWrites);
            _logger.LogDebug("Bulk wrote {Count} data points to historical storage", dataPoints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk writing to historical storage");
        }
    }

    /// <summary>
    /// Fires events for newly discovered topics.
    /// </summary>
    private async Task FireTopicDiscoveryEventsAsync(List<string> newTopics)
    {
        try
        {
            var discoveryEvent = new TopicDiscoveryEvent
            {
                NewTopics = newTopics,
                DiscoveredAt = DateTime.UtcNow,
                Source = "BulkDataProcessor"
            };
            
            await _eventBus.PublishAsync(discoveryEvent);
            
            _logger.LogInformation("Published topic discovery event for {Count} new topics", newTopics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error firing topic discovery events");
        }
    }

    /// <summary>
    /// Gets processing statistics.
    /// </summary>
    public BulkProcessingStatistics GetStatistics()
    {
        return new BulkProcessingStatistics
        {
            TotalDataPointsProcessed = _totalDataPointsProcessed,
            TotalBatchesProcessed = _totalBatchesProcessed,
            NewTopicsDiscovered = _newTopicsDiscovered,
            DatabaseWrites = _databaseWrites,
            KnownTopicsCount = _knownTopics.Count,
            AverageDataPointsPerBatch = _totalBatchesProcessed > 0 ? (double)_totalDataPointsProcessed / _totalBatchesProcessed : 0
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _topicCacheLock?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event published when new topics are discovered during bulk processing.
/// </summary>
public class TopicDiscoveryEvent : IEvent
{
    public List<string> NewTopics { get; set; } = new();
    public DateTime DiscoveredAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EventId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Statistics about bulk processing performance.
/// </summary>
public class BulkProcessingStatistics
{
    public long TotalDataPointsProcessed { get; set; }
    public long TotalBatchesProcessed { get; set; }
    public long NewTopicsDiscovered { get; set; }
    public long DatabaseWrites { get; set; }
    public int KnownTopicsCount { get; set; }
    public double AverageDataPointsPerBatch { get; set; }
}