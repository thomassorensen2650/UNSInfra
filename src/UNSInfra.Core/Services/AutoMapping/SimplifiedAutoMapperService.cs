using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Repositories;
using UNSInfra.Services;

namespace UNSInfra.Services.AutoMapping;

/// <summary>
/// Simplified, high-performance auto-mapper that matches incoming topics with existing UNS namespaces.
/// Example: "socket/virtualfactory/Enterprise1/KPI/MyKPI" matches "Enterprise1/KPI/MyKPI" in UNS.
/// </summary>
public class SimplifiedAutoMapperService : IDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SimplifiedAutoMapperService> _logger;
    
    // High-performance cache of UNS namespace paths for fast lookup
    private readonly Dictionary<string, string> _namespaceCache = new(); // Key: namespace path, Value: full NS path
    private readonly HashSet<string> _mappedTopics = new(); // Cache of already mapped topics to avoid duplicate work
    private readonly object _cacheLock = new object();
    
    // Performance counters
    private int _cacheHits = 0;
    private int _cacheMisses = 0;
    
    public SimplifiedAutoMapperService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SimplifiedAutoMapperService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the namespace cache by loading all UNS paths.
    /// Should be called once at startup and when UNS structure changes.
    /// </summary>
    public virtual async Task InitializeCacheAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var namespaceService = scope.ServiceProvider.GetService<INamespaceStructureService>();
        if (namespaceService == null)
        {
            _logger.LogError("INamespaceStructureService not found in service provider");
            return;
        }
        
        try
        {
            var startTime = DateTime.UtcNow;
            var namespaces = await namespaceService.GetNamespaceStructureAsync();
            
            lock (_cacheLock)
            {
                _namespaceCache.Clear();
                
                // Build a flat cache of all namespace paths for O(1) lookup
                foreach (var ns in namespaces)
                {
                    AddNamespaceToCache(ns, ns.Name);
                }
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Initialized namespace cache with {Count} paths in {Duration}ms", 
                    _namespaceCache.Count, duration.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize namespace cache");
        }
    }
    
    /// <summary>
    /// Try to map an incoming topic to an existing UNS namespace.
    /// Returns the namespace path if a match is found, null otherwise.
    /// </summary>
    public virtual string? TryMapTopic(string topic)
    {
        if (string.IsNullOrEmpty(topic))
            return null;
            
        // Check if we've already processed this topic
        lock (_cacheLock)
        {
            if (_mappedTopics.Contains(topic))
            {
                _cacheHits++;
                return null; // Already processed, avoid duplicate work
            }
        }
        
        // Extract the potential namespace path from the topic
        // Example: "socket/virtualfactory/Enterprise1/KPI/MyKPI" -> ["Enterprise1/KPI/MyKPI", "KPI/MyKPI", "MyKPI"]
        var candidatePaths = ExtractCandidatePaths(topic);
        
        string? matchedNamespace = null;
        lock (_cacheLock)
        {
            // Find the longest matching namespace path (most specific match)
            foreach (var candidatePath in candidatePaths.OrderByDescending(p => p.Length))
            {
                if (_namespaceCache.TryGetValue(candidatePath, out var namespacePath))
                {
                    matchedNamespace = namespacePath;
                    _cacheHits++;
                    break;
                }
            }
            
            if (matchedNamespace == null)
            {
                _cacheMisses++;
            }
            
            // Mark topic as processed regardless of whether we found a match
            _mappedTopics.Add(topic);
        }
        
        if (matchedNamespace != null)
        {
            _logger.LogInformation("Mapped topic '{Topic}' to namespace '{Namespace}'", topic, matchedNamespace);
        }
        
        return matchedNamespace;
    }
    
    /// <summary>
    /// Called when UNS structure changes to refresh the cache.
    /// </summary>
    public virtual async Task RefreshCacheAsync()
    {
        _logger.LogInformation("Refreshing namespace cache due to UNS structure change");
        await InitializeCacheAsync();
        
        // Clear the mapped topics cache since namespace structure changed
        lock (_cacheLock)
        {
            _mappedTopics.Clear();
        }
    }
    
    /// <summary>
    /// Get performance statistics for monitoring.
    /// </summary>
    public virtual (int CacheHits, int CacheMisses, int CacheSize, double HitRatio) GetStats()
    {
        lock (_cacheLock)
        {
            var total = _cacheHits + _cacheMisses;
            var hitRatio = total > 0 ? (double)_cacheHits / total : 0.0;
            return (_cacheHits, _cacheMisses, _namespaceCache.Count, hitRatio);
        }
    }
    
    /// <summary>
    /// Extract potential namespace paths from a topic by removing source prefixes.
    /// Example: "socket/virtualfactory/Enterprise1/KPI/MyKPI" -> ["Enterprise1/KPI/MyKPI", "KPI/MyKPI", "MyKPI"]
    /// </summary>
    private List<string> ExtractCandidatePaths(string topic)
    {
        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries).SkipLast(1).ToArray();
        var candidates = new List<string>();
        
        // first is connection type (MQTT), second could be operation (SocketIO updated etc.)
        // Start from index 1 to skip single-part topics which are unlikely to be namespace matches
        for (int i = 1; i <= Math.Min(2, parts.Length); i++)
        {
            var candidatePath = string.Join("/", parts.Skip(i));
            if (!string.IsNullOrEmpty(candidatePath))
            {
                candidates.Add(candidatePath);
            }
        }
        
        return candidates;
    }
    
    /// <summary>
    /// Recursively add namespace and its children to the cache.
    /// </summary>
    private void AddNamespaceToCache(NSTreeNode node, string currentPath)
    {
     
        if (node.Namespace is not null)
        {
            // We only allow data in namespaces
            // Add this namespace path to cache
            _namespaceCache[currentPath] = currentPath;
        }
        
        // Recursively add children
        foreach (var child in node.Children)
        {
            var childPath = string.IsNullOrEmpty(currentPath) ? child.Name : $"{currentPath}/{child.Name}";
            AddNamespaceToCache(child, childPath);
        }
    }
    
    public void Dispose()
    {
        var stats = GetStats();
        _logger.LogInformation("AutoMapper disposed. Final stats - Hits: {Hits}, Misses: {Misses}, Hit Ratio: {HitRatio:P1}", 
            stats.CacheHits, stats.CacheMisses, stats.HitRatio);
    }
}