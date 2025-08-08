using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using UNSInfra.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;

namespace UNSInfra.Storage.InMemory;

public class InMemoryHistoricalStorage : IHistoricalStorage
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DataPoint>> _storageByTopic = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly InMemoryHistoricalStorageOptions _options;
    private long _totalDataPointCount = 0;
    private long _insertionsSinceLastCleanup = 0;
    private DateTime _lastCleanupTime = DateTime.UtcNow;

    public InMemoryHistoricalStorage(IOptions<HistoricalStorageConfiguration> configuration)
    {
        _options = configuration.Value.InMemory;
    }

    public Task StoreAsync(DataPoint dataPoint)
    {
        var topic = dataPoint.Topic;
        
        _lock.EnterWriteLock();
        try
        {
            // Get or create queue for this topic
            var queue = _storageByTopic.GetOrAdd(topic, _ => new ConcurrentQueue<DataPoint>());
            
            // Add the data point
            queue.Enqueue(dataPoint);
            Interlocked.Increment(ref _totalDataPointCount);
            
            // Cleanup if needed
            if (_options.AutoCleanup)
            {
                CleanupIfNeeded(topic, queue);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DataPoint>> GetHistoryAsync(string topic, DateTime from, DateTime to)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_storageByTopic.TryGetValue(topic, out var queue))
            {
                return Task.FromResult(Enumerable.Empty<DataPoint>());
            }

            var results = queue
                .Where(dp => dp.Timestamp >= from && dp.Timestamp <= to)
                .OrderBy(dp => dp.Timestamp)
                .ToList();
            
            return Task.FromResult(results.AsEnumerable());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<IEnumerable<DataPoint>> GetHistoryByPathAsync(HierarchicalPath path, DateTime from, DateTime to)
    {
        var pathStr = path.GetFullPath();
        
        _lock.EnterReadLock();
        try
        {
            var results = new List<DataPoint>();
            
            foreach (var kvp in _storageByTopic)
            {
                var topicResults = kvp.Value
                    .Where(dp => dp.Path.GetFullPath().StartsWith(pathStr) && 
                                 dp.Timestamp >= from && dp.Timestamp <= to)
                    .ToList();
                results.AddRange(topicResults);
            }
            
            return Task.FromResult(results.OrderBy(dp => dp.Timestamp).AsEnumerable());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task ArchiveAsync(DateTime before)
    {
        _lock.EnterWriteLock();
        try
        {
            var removedCount = 0;
            
            foreach (var kvp in _storageByTopic)
            {
                var queue = kvp.Value;
                var tempList = new List<DataPoint>();
                
                // Dequeue all items and keep only those newer than the cutoff
                while (queue.TryDequeue(out var dataPoint))
                {
                    if (dataPoint.Timestamp >= before)
                    {
                        tempList.Add(dataPoint);
                    }
                    else
                    {
                        removedCount++;
                    }
                }
                
                // Re-enqueue the kept items
                foreach (var dataPoint in tempList)
                {
                    queue.Enqueue(dataPoint);
                }
            }
            
            Interlocked.Add(ref _totalDataPointCount, -removedCount);
            
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void CleanupIfNeeded(string topic, ConcurrentQueue<DataPoint> queue)
    {
        Interlocked.Increment(ref _insertionsSinceLastCleanup);
        
        // Check per-topic limit - this is efficient since it only affects one queue
        if (_options.MaxValuesPerDataPoint > 0 && queue.Count > _options.MaxValuesPerDataPoint)
        {
            var excessCount = queue.Count - _options.MaxValuesPerDataPoint;
            for (int i = 0; i < excessCount; i++)
            {
                if (queue.TryDequeue(out var _))
                {
                    Interlocked.Decrement(ref _totalDataPointCount);
                }
            }
        }
        
        // Check global limit with intelligent triggering
        if (_options.MaxTotalValues > 0 && ShouldTriggerGlobalCleanup())
        {
            CleanupOldestDataPointsOptimized();
        }
    }

    private bool ShouldTriggerGlobalCleanup()
    {
        var currentCount = _totalDataPointCount;
        var maxValues = _options.MaxTotalValues;
        
        // Only trigger cleanup if we're significantly over the limit to avoid constant cleanup
        var cleanupThreshold = maxValues * 1.2; // 20% over limit
        
        if (currentCount <= cleanupThreshold)
            return false;
            
        // Also add time-based throttling - don't cleanup too frequently
        var timeSinceLastCleanup = DateTime.UtcNow - _lastCleanupTime;
        var minTimeBetweenCleanups = TimeSpan.FromSeconds(10); // At most every 10 seconds
        
        if (timeSinceLastCleanup < minTimeBetweenCleanups)
            return false;
            
        // Or insertion-based throttling - only after significant activity
        var minInsertionsBetweenCleanups = Math.Max(1000, maxValues / 100); // At least 1000 or 1% of max
        
        return _insertionsSinceLastCleanup >= minInsertionsBetweenCleanups;
    }

    private void CleanupOldestDataPointsOptimized()
    {
        var currentCount = _totalDataPointCount;
        var targetCount = _options.MaxTotalValues;
        var excessCount = currentCount - targetCount;
        
        if (excessCount <= 0) return;

        
        _lastCleanupTime = DateTime.UtcNow;
        Interlocked.Exchange(ref _insertionsSinceLastCleanup, 0);
        
        var removedCount = 0;
        var topicsToRemoveFrom = new List<(string topic, ConcurrentQueue<DataPoint> queue, int count)>();
        
        // Calculate how many items to remove from each topic proportionally
        foreach (var kvp in _storageByTopic)
        {
            var topicCount = kvp.Value.Count;
            if (topicCount > 0)
            {
                topicsToRemoveFrom.Add((kvp.Key, kvp.Value, topicCount));
            }
        }
        
        if (!topicsToRemoveFrom.Any()) return;
        
        // Remove items proportionally from each topic, starting with those that have the most items
        var totalItems = topicsToRemoveFrom.Sum(t => t.count);
        var remaining = excessCount;
        
        // Sort by count descending to remove from largest topics first
        topicsToRemoveFrom.Sort((a, b) => b.count.CompareTo(a.count));
        
        foreach (var (topic, queue, count) in topicsToRemoveFrom)
        {
            if (remaining <= 0) break;
            
            // Calculate how many to remove from this topic (proportional to its size)
            var proportionalRemoval = Math.Min(remaining, (int)Math.Ceiling((double)count * excessCount / totalItems));
            
            // But also ensure we don't remove more than half of any topic's data to maintain some history
            var maxRemovalForTopic = Math.Max(1, count / 2);
            var actualRemoval = Math.Min(proportionalRemoval, maxRemovalForTopic);
            
            // Remove oldest items from this topic
            for (int i = 0; i < actualRemoval && queue.TryDequeue(out var _); i++)
            {
                removedCount++;
                remaining--;
            }
        }
        
        Interlocked.Add(ref _totalDataPointCount, -removedCount);
    }

    private void CleanupOldestDataPoints()
    {
        // Legacy method kept for compatibility - redirect to optimized version
        CleanupOldestDataPointsOptimized();
    }
}
