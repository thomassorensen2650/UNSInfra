using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Abstractions;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.TopicBrowser.Events;
using UNSInfra.Storage.Abstractions;

namespace UNSInfra.Services.TopicBrowser;

/// <summary>
/// High-performance cached implementation of the topic browser service.
/// Loads topic structure on startup and maintains it via events to minimize database calls.
/// </summary>
public class CachedTopicBrowserService : ITopicBrowserService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CachedTopicBrowserService> _logger;
    private readonly IConnectionManager? _connectionManager;
    
    // Thread-safe caching - includes BOTH configured and discovered topics
    private readonly ConcurrentDictionary<string, TopicInfo> _topicCache = new();
    private readonly ConcurrentDictionary<string, TopicInfo> _discoveredTopicCache = new();
    private readonly ConcurrentDictionary<string, List<TopicInfo>> _namespaceIndex = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    // Cache management
    private DateTime _lastFullRefresh = DateTime.MinValue;
    private const int FULL_REFRESH_MINUTES = 60; // Safety net fallback
    private const int STALE_CACHE_MINUTES = 5; // Consider cache stale after this
    private volatile bool _isInitialized = false;
    private volatile bool _disposed = false;
    
    // Statistics
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private long _databaseCalls = 0;

    public CachedTopicBrowserService(
        IServiceProvider serviceProvider,
        ILogger<CachedTopicBrowserService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Try to get connection manager (optional dependency to avoid circular references)
        _connectionManager = serviceProvider.GetService<IConnectionManager>();
        if (_connectionManager != null)
        {
            _connectionManager.DataReceived += OnDataReceived;
            _logger.LogDebug("Subscribed to connection manager data events for topic discovery");
        }
        else
        {
            _logger.LogWarning("ConnectionManager not available - discovered topic cache will not be populated from data events");
        }
    }

    #region Events
    
    /// <summary>
    /// Event fired when topic structure changes occur.
    /// </summary>
    public event EventHandler<TopicStructureChangedEventArgs>? TopicStructureChanged;
    
    /// <summary>
    /// Event fired when a new topic is added to the system.
    /// </summary>
    public event EventHandler<TopicAddedEventArgs>? TopicAdded;
    
    /// <summary>
    /// Event fired when topic data is updated.
    /// </summary>
    public event EventHandler<TopicDataUpdatedEventArgs>? TopicDataUpdated;
    
    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the cache by loading all topics from the database.
    /// Should be called once on application startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CachedTopicBrowserService));
        
        await _cacheLock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            
            _logger.LogInformation("Initializing topic cache...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            await RefreshCacheInternal();
            
            _isInitialized = true;
            _lastFullRefresh = DateTime.UtcNow;
            
            stopwatch.Stop();
            _logger.LogInformation("Topic cache initialized with {Count} topics in {ElapsedMs}ms", 
                _topicCache.Count, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Gets the latest topic structure from cache. Initializes cache if not already done.
    /// </summary>
    public async Task<IEnumerable<TopicInfo>> GetLatestTopicStructureAsync()
    {
        await EnsureInitializedAsync();
        
        // Check if we need a safety refresh
        if (ShouldPerformSafetyRefresh())
        {
            _logger.LogInformation("Performing safety refresh of topic cache");
            await RefreshCacheAsync();
        }
        
        Interlocked.Increment(ref _cacheHits);
        
        // Merge configured topics with discovered topics
        var allTopics = new List<TopicInfo>();
        allTopics.AddRange(_topicCache.Values);
        allTopics.AddRange(_discoveredTopicCache.Values.Where(d => !_topicCache.ContainsKey(d.Topic)));
        
        return allTopics; // Return a snapshot with both configured and discovered topics
    }

    /// <summary>
    /// Gets new topics added since the specified time from cache.
    /// </summary>
    public async Task<IEnumerable<TopicInfo>> GetNewTopicsAsync(DateTime lastCheckTime)
    {
        await EnsureInitializedAsync();
        
        // Check both configured and discovered topics
        var newTopics = new List<TopicInfo>();
        
        newTopics.AddRange(_topicCache.Values.Where(t => t.CreatedAt > lastCheckTime));
        newTopics.AddRange(_discoveredTopicCache.Values
            .Where(d => d.CreatedAt > lastCheckTime && !_topicCache.ContainsKey(d.Topic)));
            
        Interlocked.Increment(ref _cacheHits);
        return newTopics;
    }

    /// <summary>
    /// Gets topics for a specific namespace from cache with fast index lookup.
    /// </summary>
    public async Task<IEnumerable<TopicInfo>> GetTopicsForNamespaceAsync(string namespacePath)
    {
        await EnsureInitializedAsync();
        
        if (_namespaceIndex.TryGetValue(namespacePath, out var topics))
        {
            Interlocked.Increment(ref _cacheHits);
            return topics.ToList(); // Return a snapshot
        }
        
        // Fallback to linear search if index is stale
        var fallbackTopics = _topicCache.Values
            .Where(t => !string.IsNullOrEmpty(t.NSPath) && 
                       t.NSPath.Equals(namespacePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        // Update index
        _namespaceIndex[namespacePath] = fallbackTopics;
        
        Interlocked.Increment(ref _cacheHits);
        return fallbackTopics;
    }

    /// <summary>
    /// Gets data for a topic (not cached, always goes to storage).
    /// </summary>
    public async Task<UNSInfra.Models.Data.DataPoint?> GetDataForTopicAsync(string topic)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var realtimeStorage = scope.ServiceProvider.GetRequiredService<IRealtimeStorage>();
            return await realtimeStorage.GetLatestAsync(topic);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get data for topic {Topic}", topic);
            return null;
        }
    }

    /// <summary>
    /// Gets topic configuration from cache first, then database if needed.
    /// </summary>
    public async Task<TopicConfiguration?> GetTopicConfigurationAsync(string topic)
    {
        await EnsureInitializedAsync();
        
        // Check cache first
        if (_topicCache.TryGetValue(topic, out var cachedTopic))
        {
            // Convert TopicInfo back to TopicConfiguration
            var config = new TopicConfiguration
            {
                Topic = cachedTopic.Topic,
                Path = cachedTopic.Path,
                IsActive = cachedTopic.IsActive,
                SourceType = cachedTopic.SourceType,
                CreatedAt = cachedTopic.CreatedAt,
                ModifiedAt = cachedTopic.ModifiedAt,
                Description = cachedTopic.Description,
                Metadata = cachedTopic.Metadata,
                NSPath = cachedTopic.NSPath,
                UNSName = cachedTopic.UNSName
            };
            
            Interlocked.Increment(ref _cacheHits);
            return config;
        }
        
        // Cache miss - go to database
        Interlocked.Increment(ref _cacheMisses);
        Interlocked.Increment(ref _databaseCalls);
        
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
        return await repository.GetTopicConfigurationAsync(topic);
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// Manually refreshes the entire cache from the database.
    /// Use sparingly - prefer event-driven updates.
    /// </summary>
    public async Task RefreshCacheAsync()
    {
        if (_disposed) return;
        
        await _cacheLock.WaitAsync();
        try
        {
            await RefreshCacheInternal();
            _lastFullRefresh = DateTime.UtcNow;
            
            // Notify subscribers
            TopicStructureChanged?.Invoke(this, new TopicStructureChangedEventArgs
            {
                ChangeType = TopicChangeType.FullRefresh,
                Metadata = new Dictionary<string, object>
                {
                    ["CacheSize"] = _topicCache.Count,
                    ["RefreshReason"] = "Manual"
                }
            });
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Updates a single topic in the cache. Call this when a topic is modified.
    /// </summary>
    public async Task UpdateTopicInCacheAsync(string topic)
    {
        if (_disposed) return;
        
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
        var config = await repository.GetTopicConfigurationAsync(topic);
        if (config == null)
        {
            // Topic was deleted
            await RemoveTopicFromCacheAsync(topic);
            return;
        }
        
        var topicInfo = MapConfigurationToTopicInfo(config);
        
        await _cacheLock.WaitAsync();
        try
        {
            var isNewTopic = !_topicCache.ContainsKey(topic);
            _topicCache[topic] = topicInfo;
            
            // Update namespace index
            UpdateNamespaceIndex(topicInfo);
            
            // Fire appropriate event
            if (isNewTopic)
            {
                OnTopicAdded(topicInfo);
                TopicStructureChanged?.Invoke(this, new TopicStructureChangedEventArgs
                {
                    ChangeType = TopicChangeType.TopicsAdded,
                    AffectedTopics = new[] { topic }
                });
            }
            else
            {
                TopicStructureChanged?.Invoke(this, new TopicStructureChangedEventArgs
                {
                    ChangeType = TopicChangeType.TopicsUpdated,
                    AffectedTopics = new[] { topic },
                    AffectedNamespace = topicInfo.NSPath
                });
            }
        }
        finally
        {
            _cacheLock.Release();
        }
        
        Interlocked.Increment(ref _databaseCalls);
    }

    /// <summary>
    /// Removes a topic from the cache.
    /// </summary>
    public async Task RemoveTopicFromCacheAsync(string topic)
    {
        if (_disposed) return;
        
        await _cacheLock.WaitAsync();
        try
        {
            if (_topicCache.TryRemove(topic, out var removedTopic))
            {
                // Update namespace index
                if (!string.IsNullOrEmpty(removedTopic.NSPath) && 
                    _namespaceIndex.TryGetValue(removedTopic.NSPath, out var namespaceTopics))
                {
                    namespaceTopics.RemoveAll(t => t.Topic == topic);
                }
                
                TopicStructureChanged?.Invoke(this, new TopicStructureChangedEventArgs
                {
                    ChangeType = TopicChangeType.TopicsRemoved,
                    AffectedTopics = new[] { topic },
                    AffectedNamespace = removedTopic.NSPath
                });
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Updates multiple topics that were auto-mapped to namespaces.
    /// </summary>
    public async Task UpdateAutoMappedTopicsAsync(IEnumerable<string> topics, string namespacePath)
    {
        if (_disposed) return;
        
        var updatedTopics = new List<string>();
        
        await _cacheLock.WaitAsync();
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
            
            foreach (var topic in topics)
            {
                var config = await repository.GetTopicConfigurationAsync(topic);
                if (config != null)
                {
                    var topicInfo = MapConfigurationToTopicInfo(config);
                    _topicCache[topic] = topicInfo;
                    UpdateNamespaceIndex(topicInfo);
                    updatedTopics.Add(topic);
                }
            }
        }
        finally
        {
            _cacheLock.Release();
        }
        
        if (updatedTopics.Any())
        {
            TopicStructureChanged?.Invoke(this, new TopicStructureChangedEventArgs
            {
                ChangeType = TopicChangeType.TopicsAutoMapped,
                AffectedTopics = updatedTopics,
                AffectedNamespace = namespacePath,
                Metadata = new Dictionary<string, object>
                {
                    ["AutoMappedCount"] = updatedTopics.Count
                }
            });
        }
        
        Interlocked.Add(ref _databaseCalls, updatedTopics.Count);
    }

    #endregion

    #region Statistics and Diagnostics

    /// <summary>
    /// Gets cache performance statistics.
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        return new CacheStatistics
        {
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            DatabaseCalls = _databaseCalls,
            CachedTopicCount = _topicCache.Count,
            DiscoveredTopicCount = _discoveredTopicCache.Count,
            TotalTopicCount = _topicCache.Count + _discoveredTopicCache.Count - 
                             _topicCache.Keys.Intersect(_discoveredTopicCache.Keys).Count(), // Avoid double counting
            NamespaceIndexCount = _namespaceIndex.Count,
            LastFullRefresh = _lastFullRefresh,
            IsInitialized = _isInitialized,
            HitRate = _cacheHits + _cacheMisses > 0 ? (double)_cacheHits / (_cacheHits + _cacheMisses) : 0
        };
    }

    #endregion

    #region Private Methods

    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }
    }

    private async Task RefreshCacheInternal()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
        var configurations = await repository.GetAllTopicConfigurationsAsync();
        
        _topicCache.Clear();
        _namespaceIndex.Clear();
        
        foreach (var config in configurations)
        {
            var topicInfo = MapConfigurationToTopicInfo(config);
            _topicCache[config.Topic] = topicInfo;
            UpdateNamespaceIndex(topicInfo);
        }
        
        Interlocked.Increment(ref _databaseCalls);
    }

    private void UpdateNamespaceIndex(TopicInfo topicInfo)
    {
        if (string.IsNullOrEmpty(topicInfo.NSPath)) return;
        
        _namespaceIndex.AddOrUpdate(
            topicInfo.NSPath,
            new List<TopicInfo> { topicInfo },
            (key, existing) =>
            {
                existing.RemoveAll(t => t.Topic == topicInfo.Topic);
                existing.Add(topicInfo);
                return existing;
            });
    }

    private bool ShouldPerformSafetyRefresh()
    {
        return DateTime.UtcNow - _lastFullRefresh > TimeSpan.FromMinutes(FULL_REFRESH_MINUTES);
    }

    private static TopicInfo MapConfigurationToTopicInfo(TopicConfiguration config)
    {
        return new TopicInfo
        {
            Topic = config.Topic,
            Path = config.Path,
            IsActive = config.IsActive,
            SourceType = config.SourceType,
            CreatedAt = config.CreatedAt,
            ModifiedAt = config.ModifiedAt,
            Description = config.Description,
            Metadata = config.Metadata,
            NSPath = config.NSPath,
            UNSName = config.UNSName
        };
    }

    private void OnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        try
        {
            var dataPoint = e.DataPoint;
            var topic = dataPoint.Topic;
            
            // Skip if this topic is already in the configured cache (configured topics take precedence)
            if (_topicCache.ContainsKey(topic))
                return;
            
            // Skip if already in discovered cache
            if (_discoveredTopicCache.ContainsKey(topic))
                return;
                
            // Create TopicInfo from discovered data point
            var topicInfo = new TopicInfo
            {
                Topic = topic,
                Path = new HierarchicalPath(), // Empty hierarchical path for discovered topics
                IsActive = true, // Discovered topics are considered active by default
                SourceType = "Discovered", // Mark as discovered
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                Description = $"Auto-discovered topic from data connection",
                Metadata = new Dictionary<string, object>
                {
                    ["InputId"] = e.InputId,
                    ["DiscoveryTimestamp"] = DateTime.UtcNow,
                    ["DataType"] = dataPoint.Value?.GetType().Name ?? "Unknown",
                    ["Timestamp"] = dataPoint.Timestamp
                },
                NSPath = string.Empty, // No namespace path until mapped
                UNSName = string.Empty // No UNS name until mapped
            };
            
            // Add to discovered cache
            _discoveredTopicCache.TryAdd(topic, topicInfo);
            
            _logger.LogDebug("Discovered new topic from data events: {Topic}", topic);
            
            // Fire topic added event
            OnTopicAdded(topicInfo);
            TopicStructureChanged?.Invoke(this, new TopicStructureChangedEventArgs
            {
                ChangeType = TopicChangeType.TopicsAdded,
                AffectedTopics = new[] { topic },
                Metadata = new Dictionary<string, object>
                {
                    ["DiscoverySource"] = "DataConnection",
                    ["InputId"] = e.InputId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing discovered topic from data connection");
        }
    }

    #endregion

    #region Legacy Event Methods (for compatibility)

    public async Task VerifyTopicAsync(string topic, string verifiedBy)
    {
        var config = await GetTopicConfigurationAsync(topic);
        if (config != null)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
            await repository.VerifyTopicConfigurationAsync(config.Id, verifiedBy);
            await UpdateTopicInCacheAsync(topic);
        }
    }

    public async Task UpdateTopicConfigurationAsync(TopicConfiguration configuration)
    {
        configuration.ModifiedAt = DateTime.UtcNow;
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
        await repository.SaveTopicConfigurationAsync(configuration);
        await UpdateTopicInCacheAsync(configuration.Topic);
    }

    public void NotifyTopicAdded(TopicInfo topicInfo)
    {
        OnTopicAdded(topicInfo);
    }

    public void NotifyTopicDataUpdated(string topic, UNSInfra.Models.Data.DataPoint dataPoint)
    {
        OnTopicDataUpdated(topic, dataPoint);
    }

    protected virtual void OnTopicAdded(TopicInfo topicInfo)
    {
        TopicAdded?.Invoke(this, new TopicAddedEventArgs { TopicInfo = topicInfo });
    }

    protected virtual void OnTopicDataUpdated(string topic, UNSInfra.Models.Data.DataPoint dataPoint)
    {
        TopicDataUpdated?.Invoke(this, new TopicDataUpdatedEventArgs 
        { 
            Topic = topic, 
            DataPoint = dataPoint 
        });
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        // Unsubscribe from connection manager events
        if (_connectionManager != null)
        {
            _connectionManager.DataReceived -= OnDataReceived;
        }
        
        _cacheLock?.Dispose();
        
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Cache performance statistics.
/// </summary>
public class CacheStatistics
{
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public long DatabaseCalls { get; set; }
    public int CachedTopicCount { get; set; }
    public int DiscoveredTopicCount { get; set; }
    public int TotalTopicCount { get; set; }
    public int NamespaceIndexCount { get; set; }
    public DateTime LastFullRefresh { get; set; }
    public bool IsInitialized { get; set; }
    public double HitRate { get; set; }
}