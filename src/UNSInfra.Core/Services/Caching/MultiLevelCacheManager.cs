using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Services.Events;
using UNSInfra.Models.Data;
using UNSInfra.Services.TopicBrowser;

namespace UNSInfra.Core.Services.Caching;

/// <summary>
/// Multi-level cache manager with cache warming strategies and eviction policies.
/// Provides L1 (memory), L2 (compressed memory), and L3 (warm/cold) cache tiers.
/// </summary>
public class MultiLevelCacheManager : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<MultiLevelCacheManager> _logger;
    
    // L1 Cache - Hot data (frequently accessed)
    private readonly ConcurrentDictionary<string, CacheItem<TopicInfo>> _l1TopicCache = new();
    private readonly ConcurrentDictionary<string, CacheItem<DataPoint>> _l1DataCache = new();
    
    // L2 Cache - Warm data (moderately accessed)
    private readonly ConcurrentDictionary<string, CompressedCacheItem> _l2Cache = new();
    
    // L3 Cache - Cold data (rarely accessed but kept for warming)
    private readonly ConcurrentDictionary<string, ColdCacheItem> _l3Cache = new();
    
    // Cache statistics and configuration
    private readonly CacheStatistics _stats = new();
    private readonly Timer _maintenanceTimer;
    private readonly Timer _warmingTimer;
    private readonly Timer _statisticsTimer;
    
    // Configuration
    private readonly TimeSpan _l1EvictionTime = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _l2EvictionTime = TimeSpan.FromHours(2);
    private readonly TimeSpan _l3EvictionTime = TimeSpan.FromHours(24);
    private readonly int _maxL1Items = 10000;
    private readonly int _maxL2Items = 50000;
    private readonly int _maxL3Items = 100000;

    public MultiLevelCacheManager(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        ILogger<MultiLevelCacheManager> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
        
        // Initialize maintenance timers
        _maintenanceTimer = new Timer(PerformMaintenance, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        _warmingTimer = new Timer(PerformCacheWarming, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10));
        _statisticsTimer = new Timer(LogStatistics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        
        // Subscribe to events for cache invalidation
        _eventBus.Subscribe<TopicAddedEvent>(OnTopicAdded);
        _eventBus.Subscribe<TopicDataUpdatedEvent>(OnTopicDataUpdated);
        _eventBus.Subscribe<TopicConfigurationUpdatedEvent>(OnTopicConfigurationUpdated);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Multi-level cache manager started");
        
        // Perform initial cache warming
        await PerformInitialCacheWarming();
        
        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        
        _logger.LogInformation("Multi-level cache manager stopped");
    }

    /// <summary>
    /// Get topic info with multi-level cache fallback
    /// </summary>
    public async Task<TopicInfo?> GetTopicAsync(string topic)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // L1 Cache check
        if (_l1TopicCache.TryGetValue(topic, out var l1Item) && !l1Item.IsExpired(_l1EvictionTime))
        {
            l1Item.UpdateAccess();
            Interlocked.Increment(ref _stats.L1Hits);
            _logger.LogTrace("L1 cache hit for topic {Topic} in {ElapsedMs}ms", topic, stopwatch.ElapsedMilliseconds);
            return l1Item.Value;
        }
        
        // L2 Cache check
        if (_l2Cache.TryGetValue($"topic:{topic}", out var l2Item) && !l2Item.IsExpired(_l2EvictionTime))
        {
            var decompressed = l2Item.Decompress<TopicInfo>();
            if (decompressed != null)
            {
                // Promote to L1
                _l1TopicCache.TryAdd(topic, new CacheItem<TopicInfo>(decompressed));
                Interlocked.Increment(ref _stats.L2Hits);
                _logger.LogTrace("L2 cache hit for topic {Topic}, promoted to L1 in {ElapsedMs}ms", topic, stopwatch.ElapsedMilliseconds);
                return decompressed;
            }
        }
        
        // L3 Cache check and database fallback
        TopicInfo? result = null;
        if (_l3Cache.TryGetValue($"topic:{topic}", out var l3Item) && !l3Item.IsExpired(_l3EvictionTime))
        {
            result = l3Item.GetMetadata<TopicInfo>("TopicInfo");
            Interlocked.Increment(ref _stats.L3Hits);
        }
        
        if (result == null)
        {
            // Database fallback
            result = await GetTopicFromDatabase(topic);
            Interlocked.Increment(ref _stats.DatabaseQueries);
        }
        
        if (result != null)
        {
            // Store in all cache levels
            _l1TopicCache.TryAdd(topic, new CacheItem<TopicInfo>(result));
            _l2Cache.TryAdd($"topic:{topic}", new CompressedCacheItem(result));
            _l3Cache.TryAdd($"topic:{topic}", new ColdCacheItem().SetMetadata("TopicInfo", result));
        }
        
        Interlocked.Increment(ref _stats.CacheMisses);
        _logger.LogTrace("Cache miss for topic {Topic}, database query in {ElapsedMs}ms", topic, stopwatch.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    /// Get data point with multi-level cache fallback
    /// </summary>
    public async Task<DataPoint?> GetDataAsync(string topic)
    {
        // Similar multi-level approach for data points
        if (_l1DataCache.TryGetValue(topic, out var l1Item) && !l1Item.IsExpired(_l1EvictionTime))
        {
            l1Item.UpdateAccess();
            Interlocked.Increment(ref _stats.L1Hits);
            return l1Item.Value;
        }
        
        // Database fallback for data
        var result = await GetDataFromDatabase(topic);
        if (result != null)
        {
            _l1DataCache.TryAdd(topic, new CacheItem<DataPoint>(result));
        }
        
        return result;
    }

    /// <summary>
    /// Perform initial cache warming by loading frequently accessed data
    /// </summary>
    private async Task PerformInitialCacheWarming()
    {
        try
        {
            _logger.LogInformation("Starting initial cache warming");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            using var scope = _serviceProvider.CreateScope();
            var topicBrowserService = scope.ServiceProvider.GetService<ITopicBrowserService>();
            
            if (topicBrowserService != null)
            {
                // Warm L1 cache with most recent topics
                var recentTopics = await topicBrowserService.GetLatestTopicStructureAsync();
                var topicList = recentTopics.Take(_maxL1Items / 2).ToList();
                
                foreach (var topic in topicList)
                {
                    _l1TopicCache.TryAdd(topic.Topic, new CacheItem<TopicInfo>(topic));
                    
                    // Also try to warm data cache
                    var data = await topicBrowserService.GetDataForTopicAsync(topic.Topic);
                    if (data != null)
                    {
                        _l1DataCache.TryAdd(topic.Topic, new CacheItem<DataPoint>(data));
                    }
                }
                
                stopwatch.Stop();
                _logger.LogInformation("Cache warming completed: L1={L1Count} topics, {ElapsedMs}ms", 
                    _l1TopicCache.Count, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial cache warming");
        }
    }

    /// <summary>
    /// Periodic cache maintenance - eviction and promotion
    /// </summary>
    private async void PerformMaintenance(object? state)
    {
        try
        {
            var removed = 0;
            var promoted = 0;
            var demoted = 0;
            
            // L1 Cache maintenance
            var l1ExpiredKeys = _l1TopicCache
                .Where(kvp => kvp.Value.IsExpired(_l1EvictionTime) || _l1TopicCache.Count > _maxL1Items)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var key in l1ExpiredKeys.Take(_l1TopicCache.Count - _maxL1Items + 100))
            {
                if (_l1TopicCache.TryRemove(key, out var expiredItem))
                {
                    // Demote to L2 if still warm
                    if (!expiredItem.IsExpired(TimeSpan.FromMinutes(30)))
                    {
                        _l2Cache.TryAdd($"topic:{key}", new CompressedCacheItem(expiredItem.Value));
                        demoted++;
                    }
                    removed++;
                }
            }
            
            // L2 Cache maintenance
            var l2ExpiredKeys = _l2Cache
                .Where(kvp => kvp.Value.IsExpired(_l2EvictionTime) || _l2Cache.Count > _maxL2Items)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var key in l2ExpiredKeys.Take(_l2Cache.Count - _maxL2Items + 500))
            {
                if (_l2Cache.TryRemove(key, out var expiredItem))
                {
                    // Demote to L3 if still relevant
                    if (!expiredItem.IsExpired(TimeSpan.FromHours(4)))
                    {
                        _l3Cache.TryAdd(key, new ColdCacheItem().SetMetadata("LastAccess", DateTime.UtcNow));
                    }
                    removed++;
                }
            }
            
            // L3 Cache maintenance
            var l3ExpiredKeys = _l3Cache
                .Where(kvp => kvp.Value.IsExpired(_l3EvictionTime) || _l3Cache.Count > _maxL3Items)
                .Select(kvp => kvp.Key)
                .Take(_l3Cache.Count - _maxL3Items + 1000)
                .ToList();
                
            foreach (var key in l3ExpiredKeys)
            {
                _l3Cache.TryRemove(key, out _);
                removed++;
            }
            
            if (removed > 0 || promoted > 0 || demoted > 0)
            {
                _logger.LogDebug("Cache maintenance: Removed={Removed}, Promoted={Promoted}, Demoted={Demoted}", 
                    removed, promoted, demoted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache maintenance");
        }
    }

    /// <summary>
    /// Periodic cache warming based on access patterns
    /// </summary>
    private async void PerformCacheWarming(object? state)
    {
        try
        {
            // Identify hot topics from L2/L3 caches that should be promoted to L1
            var candidatesForPromotion = _l2Cache
                .Where(kvp => kvp.Key.StartsWith("topic:") && kvp.Value.AccessCount > 5)
                .OrderByDescending(kvp => kvp.Value.AccessCount)
                .Take(100)
                .ToList();
                
            var promoted = 0;
            foreach (var candidate in candidatesForPromotion)
            {
                var topic = candidate.Key.Substring(6); // Remove "topic:" prefix
                if (!_l1TopicCache.ContainsKey(topic))
                {
                    var decompressed = candidate.Value.Decompress<TopicInfo>();
                    if (decompressed != null)
                    {
                        _l1TopicCache.TryAdd(topic, new CacheItem<TopicInfo>(decompressed));
                        promoted++;
                    }
                }
            }
            
            if (promoted > 0)
            {
                _logger.LogDebug("Cache warming promoted {Count} items to L1", promoted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache warming");
        }
    }

    /// <summary>
    /// Log cache statistics periodically
    /// </summary>
    private void LogStatistics(object? state)
    {
        var totalRequests = _stats.L1Hits + _stats.L2Hits + _stats.L3Hits + _stats.CacheMisses;
        if (totalRequests == 0) return;
        
        var l1HitRate = (double)_stats.L1Hits / totalRequests;
        var l2HitRate = (double)_stats.L2Hits / totalRequests;
        var l3HitRate = (double)_stats.L3Hits / totalRequests;
        var overallHitRate = (double)(_stats.L1Hits + _stats.L2Hits + _stats.L3Hits) / totalRequests;
        
        _logger.LogInformation("Cache Stats - L1: {L1Count} items ({L1Rate:P1}), L2: {L2Count} items ({L2Rate:P1}), " +
            "L3: {L3Count} items ({L3Rate:P1}), Overall hit rate: {OverallRate:P1}, DB queries: {DbQueries}",
            _l1TopicCache.Count + _l1DataCache.Count, l1HitRate,
            _l2Cache.Count, l2HitRate,
            _l3Cache.Count, l3HitRate,
            overallHitRate, _stats.DatabaseQueries);
    }

    // Event handlers for cache invalidation
    private Task OnTopicAdded(TopicAddedEvent eventData)
    {
        _l1TopicCache.TryAdd(eventData.Topic, new CacheItem<TopicInfo>(new TopicInfo
        {
            Topic = eventData.Topic,
            Path = eventData.Path,
            SourceType = eventData.SourceType,
            CreatedAt = eventData.CreatedAt,
            IsActive = true
        }));
        return Task.CompletedTask;
    }

    private Task OnTopicDataUpdated(TopicDataUpdatedEvent eventData)
    {
        _l1DataCache.AddOrUpdate(eventData.Topic, 
            new CacheItem<DataPoint>(eventData.DataPoint),
            (key, existing) => new CacheItem<DataPoint>(eventData.DataPoint));
        return Task.CompletedTask;
    }

    private Task OnTopicConfigurationUpdated(TopicConfigurationUpdatedEvent eventData)
    {
        // Invalidate caches for this topic
        _l1TopicCache.TryRemove(eventData.Topic, out _);
        _l2Cache.TryRemove($"topic:{eventData.Topic}", out _);
        _l3Cache.TryRemove($"topic:{eventData.Topic}", out _);
        return Task.CompletedTask;
    }

    // Database fallback methods
    private async Task<TopicInfo?> GetTopicFromDatabase(string topic)
    {
        using var scope = _serviceProvider.CreateScope();
        var topicBrowserService = scope.ServiceProvider.GetService<ITopicBrowserService>();
        
        if (topicBrowserService != null)
        {
            var allTopics = await topicBrowserService.GetLatestTopicStructureAsync();
            return allTopics.FirstOrDefault(t => t.Topic == topic);
        }
        
        return null;
    }

    private async Task<DataPoint?> GetDataFromDatabase(string topic)
    {
        using var scope = _serviceProvider.CreateScope();
        var topicBrowserService = scope.ServiceProvider.GetService<ITopicBrowserService>();
        return await topicBrowserService?.GetDataForTopicAsync(topic)!;
    }

    public override void Dispose()
    {
        _maintenanceTimer?.Dispose();
        _warmingTimer?.Dispose();
        _statisticsTimer?.Dispose();
        
        _eventBus.Unsubscribe<TopicAddedEvent>(OnTopicAdded);
        _eventBus.Unsubscribe<TopicDataUpdatedEvent>(OnTopicDataUpdated);
        _eventBus.Unsubscribe<TopicConfigurationUpdatedEvent>(OnTopicConfigurationUpdated);
        
        base.Dispose();
    }
}

/// <summary>
/// Cache item with access tracking and expiration
/// </summary>
public class CacheItem<T>
{
    public T Value { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastAccessed { get; private set; }
    public long AccessCount { get; private set; }

    public CacheItem(T value)
    {
        Value = value;
        CreatedAt = DateTime.UtcNow;
        LastAccessed = DateTime.UtcNow;
        AccessCount = 1;
    }

    public void UpdateAccess()
    {
        LastAccessed = DateTime.UtcNow;
        AccessCount++;
    }

    public bool IsExpired(TimeSpan maxAge) => DateTime.UtcNow - LastAccessed > maxAge;
}

/// <summary>
/// Compressed cache item for L2 cache
/// </summary>
public class CompressedCacheItem
{
    public byte[] CompressedData { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastAccessed { get; private set; }
    public long AccessCount { get; private set; }

    public CompressedCacheItem(object data)
    {
        CompressedData = CompressData(data);
        CreatedAt = DateTime.UtcNow;
        LastAccessed = DateTime.UtcNow;
        AccessCount = 1;
    }

    public T? Decompress<T>()
    {
        LastAccessed = DateTime.UtcNow;
        AccessCount++;
        return DecompressData<T>(CompressedData);
    }

    public bool IsExpired(TimeSpan maxAge) => DateTime.UtcNow - LastAccessed > maxAge;

    private static byte[] CompressData(object data)
    {
        // Simple JSON compression for demo - in production use more efficient serialization
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    private static T? DecompressData<T>(byte[] data)
    {
        var json = System.Text.Encoding.UTF8.GetString(data);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }
}

/// <summary>
/// Cold cache item for L3 cache (metadata only)
/// </summary>
public class ColdCacheItem
{
    public DateTime CreatedAt { get; }
    public DateTime LastAccessed { get; private set; }
    private readonly Dictionary<string, object> _metadata = new();

    public ColdCacheItem()
    {
        CreatedAt = DateTime.UtcNow;
        LastAccessed = DateTime.UtcNow;
    }

    public ColdCacheItem SetMetadata<T>(string key, T value)
    {
        _metadata[key] = value!;
        return this;
    }

    public T? GetMetadata<T>(string key)
    {
        LastAccessed = DateTime.UtcNow;
        return _metadata.TryGetValue(key, out var value) ? (T)value : default;
    }

    public bool IsExpired(TimeSpan maxAge) => DateTime.UtcNow - LastAccessed > maxAge;
}

/// <summary>
/// Cache statistics for monitoring
/// </summary>
public class CacheStatistics
{
    public long L1Hits;
    public long L2Hits;
    public long L3Hits;
    public long CacheMisses;
    public long DatabaseQueries;
}