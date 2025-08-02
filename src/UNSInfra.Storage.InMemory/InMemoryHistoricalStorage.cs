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
        // Check per-topic limit
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
        
        // Check global limit
        if (_options.MaxTotalValues > 0 && _totalDataPointCount > _options.MaxTotalValues)
        {
            CleanupOldestDataPoints();
        }
    }

    private void CleanupOldestDataPoints()
    {
        var excessCount = _totalDataPointCount - _options.MaxTotalValues;
        if (excessCount <= 0) return;

        var removedCount = 0;
        var allDataPoints = new List<(string topic, DataPoint dataPoint)>();
        
        // Collect all data points with their topics
        foreach (var kvp in _storageByTopic)
        {
            var tempList = new List<DataPoint>();
            while (kvp.Value.TryDequeue(out var dataPoint))
            {
                tempList.Add(dataPoint);
            }
            
            foreach (var dp in tempList)
            {
                allDataPoints.Add((kvp.Key, dp));
            }
        }
        
        // Sort by timestamp (oldest first)
        allDataPoints.Sort((a, b) => a.dataPoint.Timestamp.CompareTo(b.dataPoint.Timestamp));
        
        // Remove excess count from the beginning (oldest)
        var toKeep = allDataPoints.Skip((int)excessCount).ToList();
        removedCount = (int)excessCount;
        
        // Clear all queues
        foreach (var kvp in _storageByTopic)
        {
            while (kvp.Value.TryDequeue(out var _)) { }
        }
        
        // Re-populate with kept data points
        foreach (var (topic, dataPoint) in toKeep)
        {
            var queue = _storageByTopic.GetOrAdd(topic, _ => new ConcurrentQueue<DataPoint>());
            queue.Enqueue(dataPoint);
        }
        
        Interlocked.Add(ref _totalDataPointCount, -removedCount);
    }
}
