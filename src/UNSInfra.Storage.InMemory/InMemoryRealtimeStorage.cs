namespace UNSInfra.Storage.InMemory;

using System.Collections.Concurrent;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;

// ==================== STORAGE IMPLEMENTATIONS ====================
public class InMemoryRealtimeStorage : IRealtimeStorage, IDisposable
{
    private readonly ConcurrentDictionary<string, DataPoint> _storage = new();
    
    // Index for efficient hierarchical path lookups
    // Key: hierarchical path string, Value: set of topics at that path
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _pathIndex = new();
    
    // Concurrent HashSet implementation for thread-safe operations
    private class ConcurrentHashSet<T> : IDisposable where T : notnull
    {
        private readonly HashSet<T> _hashSet = new();
        private readonly ReaderWriterLockSlim _lock = new();

        public ConcurrentHashSet()
        {
        }

        public ConcurrentHashSet(T initialItem)
        {
            _hashSet.Add(initialItem);
        }

        public void Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                _hashSet.Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Remove(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                return _hashSet.Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            _lock.EnterReadLock();
            try
            {
                return _hashSet.Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public List<T> ToList()
        {
            _lock.EnterReadLock();
            try
            {
                return _hashSet.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _hashSet.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }

    public Task StoreAsync(DataPoint dataPoint)
    {
        // Store the data point
        var oldDataPoint = _storage.AddOrUpdate(dataPoint.Topic, dataPoint, (key, existing) => dataPoint);
        
        // Update the path index
        var pathStr = dataPoint.Path.GetFullPath();
        
        // Remove from old path if it existed and path changed
        if (oldDataPoint != null && oldDataPoint != dataPoint)
        {
            var oldPathStr = oldDataPoint.Path.GetFullPath();
            if (oldPathStr != pathStr && _pathIndex.TryGetValue(oldPathStr, out var oldTopicSet))
            {
                oldTopicSet.Remove(dataPoint.Topic);
                // Clean up empty path entries
                if (oldTopicSet.Count == 0)
                {
                    _pathIndex.TryRemove(oldPathStr, out _);
                    oldTopicSet.Dispose();
                }
            }
        }
        
        // Add to new path index
        _pathIndex.AddOrUpdate(
            pathStr,
            _ => new ConcurrentHashSet<string>(dataPoint.Topic),
            (_, existingSet) => 
            {
                existingSet.Add(dataPoint.Topic);
                return existingSet;
            });
            
        return Task.CompletedTask;
    }

    public Task<DataPoint?> GetLatestAsync(string topic)
    {
        _storage.TryGetValue(topic, out var dataPoint);
        return Task.FromResult(dataPoint);
    }

    public Task<IEnumerable<DataPoint>> GetLatestByPathAsync(HierarchicalPath path)
    {
        var pathStr = path.GetFullPath();
        var results = new List<DataPoint>();
        
        // Use index for exact path matches (O(1) lookup)
        if (_pathIndex.TryGetValue(pathStr, out var exactTopics))
        {
            var topics = exactTopics.ToList();
            foreach (var topic in topics)
            {
                if (_storage.TryGetValue(topic, out var dataPoint))
                {
                    results.Add(dataPoint);
                }
            }
        }
        
        // For hierarchical searches (path prefix matching), we need to check all indexed paths
        // This is still more efficient than scanning all data points
        if (!string.IsNullOrEmpty(pathStr))
        {
            foreach (var indexedPath in _pathIndex.Keys)
            {
                // Skip exact matches (already processed above)
                if (indexedPath == pathStr) continue;
                
                // Check if this indexed path starts with our search path (hierarchical match)
                if (indexedPath.StartsWith(pathStr))
                {
                    if (_pathIndex.TryGetValue(indexedPath, out var topics))
                    {
                        var topicList = topics.ToList();
                        foreach (var topic in topicList)
                        {
                            if (_storage.TryGetValue(topic, out var dataPoint))
                            {
                                results.Add(dataPoint);
                            }
                        }
                    }
                }
            }
        }
        
        return Task.FromResult(results.AsEnumerable());
    }

    public Task<IEnumerable<string>> GetAllTopicsAsync()
    {
        return Task.FromResult(_storage.Keys.AsEnumerable());
    }

    public Task DeleteAsync(string id)
    {
        // Find and remove the data point, maintaining the index
        foreach (var kvp in _storage)
        {
            if (kvp.Value.Id == id)
            {
                var topic = kvp.Key;
                var dataPoint = kvp.Value;
                
                // Remove from main storage
                if (_storage.TryRemove(topic, out _))
                {
                    // Remove from path index
                    var pathStr = dataPoint.Path.GetFullPath();
                    if (_pathIndex.TryGetValue(pathStr, out var topicSet))
                    {
                        topicSet.Remove(topic);
                        
                        // Clean up empty path entries
                        if (topicSet.Count == 0)
                        {
                            _pathIndex.TryRemove(pathStr, out _);
                            topicSet.Dispose();
                        }
                    }
                }
                break;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Optimized delete by topic (more efficient than delete by ID).
    /// </summary>
    public Task DeleteByTopicAsync(string topic)
    {
        if (_storage.TryRemove(topic, out var dataPoint))
        {
            // Remove from path index
            var pathStr = dataPoint.Path.GetFullPath();
            if (_pathIndex.TryGetValue(pathStr, out var topicSet))
            {
                topicSet.Remove(topic);
                
                // Clean up empty path entries
                if (topicSet.Count == 0)
                {
                    _pathIndex.TryRemove(pathStr, out _);
                    topicSet.Dispose();
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets statistics about the storage and index performance.
    /// </summary>
    public (int TotalDataPoints, int IndexedPaths, double AverageTopicsPerPath) GetStatistics()
    {
        var totalDataPoints = _storage.Count;
        var indexedPaths = _pathIndex.Count;
        var averageTopicsPerPath = indexedPaths > 0 
            ? _pathIndex.Values.Average(set => set.Count) 
            : 0.0;
            
        return (totalDataPoints, indexedPaths, averageTopicsPerPath);
    }

    public void Dispose()
    {
        // Dispose all ConcurrentHashSet instances in the path index
        foreach (var topicSet in _pathIndex.Values)
        {
            topicSet.Dispose();
        }
        _pathIndex.Clear();
        _storage.Clear();
    }
}