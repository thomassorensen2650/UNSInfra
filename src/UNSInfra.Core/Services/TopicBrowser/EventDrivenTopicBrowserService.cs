using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.Events;
using UNSInfra.Storage.Abstractions;

namespace UNSInfra.Services.TopicBrowser;

/// <summary>
/// Event-driven topic browser service that maintains an in-memory cache 
/// and updates via events instead of polling the database
/// </summary>
public class EventDrivenTopicBrowserService : ITopicBrowserService, IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IRealtimeStorage? _realtimeStorage;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ConcurrentDictionary<string, TopicInfo> _topicCache = new();
    private readonly ConcurrentDictionary<string, DataPoint> _latestDataCache = new();
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private bool _isInitialized = false;
    private bool _disposed = false;

    public EventDrivenTopicBrowserService(IEventBus eventBus, IServiceScopeFactory serviceScopeFactory, IRealtimeStorage? realtimeStorage = null)
    {
        _eventBus = eventBus;
        _realtimeStorage = realtimeStorage;
        _serviceScopeFactory = serviceScopeFactory;
        
        // Subscribe to events
        _eventBus.Subscribe<TopicAddedEvent>(OnTopicAdded);
        _eventBus.Subscribe<TopicDataUpdatedEvent>(OnTopicDataUpdated);
        _eventBus.Subscribe<TopicVerifiedEvent>(OnTopicVerified);
        _eventBus.Subscribe<TopicConfigurationUpdatedEvent>(OnTopicConfigurationUpdated);
        _eventBus.Subscribe<BulkTopicsAddedEvent>(OnBulkTopicsAdded);
    }

    /// <summary>
    /// Event that fires when a new topic is added to the system.
    /// </summary>
    public event EventHandler<TopicAddedEventArgs>? TopicAdded;

    /// <summary>
    /// Event that fires when topic data is updated.
    /// </summary>
    public event EventHandler<TopicDataUpdatedEventArgs>? TopicDataUpdated;

    /// <inheritdoc />
    public async Task<IEnumerable<TopicInfo>> GetLatestTopicStructureAsync()
    {
        await EnsureInitializedAsync();
        return _topicCache.Values.ToList(); // Return a copy for thread safety
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TopicInfo>> GetNewTopicsAsync(DateTime lastCheckTime)
    {
        await EnsureInitializedAsync();
        return _topicCache.Values
            .Where(t => t.CreatedAt > lastCheckTime)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DataPoint?> GetDataForTopicAsync(string topic)
    {
        await EnsureInitializedAsync();
        
        // First check the cache
        if (_latestDataCache.TryGetValue(topic, out var dataPoint))
        {
            return dataPoint;
        }
        
        // If not in cache and we have realtime storage, query it as fallback
        if (_realtimeStorage != null)
        {
            var storageData = await _realtimeStorage.GetLatestAsync(topic);
            if (storageData != null)
            {
                // Cache it for future use
                _latestDataCache.TryAdd(topic, storageData);
                return storageData;
            }
        }
        
        return null;
    }

    /// <inheritdoc />
    public async Task<TopicConfiguration?> GetTopicConfigurationAsync(string topic)
    {
        await EnsureInitializedAsync();
        
        if (!_topicCache.TryGetValue(topic, out var topicInfo))
            return null;

        // Convert TopicInfo back to TopicConfiguration
        return new TopicConfiguration
        {
            Id = $"config_{topic}",
            Topic = topicInfo.Topic,
            Path = topicInfo.Path,
            IsVerified = topicInfo.IsVerified,
            IsActive = topicInfo.IsActive,
            SourceType = topicInfo.SourceType,
            CreatedAt = topicInfo.CreatedAt,
            ModifiedAt = topicInfo.ModifiedAt,
            Description = topicInfo.Description,
            Metadata = topicInfo.Metadata,
            NSPath = topicInfo.NSPath
        };
    }

    /// <inheritdoc />
    public async Task VerifyTopicAsync(string topic, string verifiedBy)
    {
        // Publish event instead of directly updating database
        await _eventBus.PublishAsync(new TopicVerifiedEvent(topic, verifiedBy, DateTime.UtcNow));
    }

    /// <inheritdoc />
    public async Task UpdateTopicConfigurationAsync(TopicConfiguration configuration)
    {
        // For the EventDriven service, we need to update both the database and the cache
        // Get a scoped repository to persist the changes
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
        
        // Save to database first
        await repository.SaveTopicConfigurationAsync(configuration);
        
        // Update the cache with the new configuration
        if (_topicCache.TryGetValue(configuration.Topic, out var existingTopic))
        {
            var updatedTopic = new TopicInfo
            {
                Topic = configuration.Topic,
                Path = configuration.Path,
                IsVerified = configuration.IsVerified,
                IsActive = configuration.IsActive,
                SourceType = configuration.SourceType,
                CreatedAt = configuration.CreatedAt,
                ModifiedAt = configuration.ModifiedAt,
                Description = configuration.Description,
                Metadata = configuration.Metadata,
                NSPath = configuration.NSPath
            };
            
            _topicCache.TryUpdate(configuration.Topic, updatedTopic, existingTopic);
            
            // Publish event for other subscribers
            await _eventBus.PublishAsync(new TopicConfigurationUpdatedEvent(
                configuration.Topic,
                existingTopic.Path,
                configuration.Path,
                "user"
            ));
        }
    }

    /// <summary>
    /// Initializes the cache from an external data source (called by DataIngestionBackgroundService)
    /// </summary>
    public async Task InitializeCacheAsync(IEnumerable<TopicInfo> initialTopics)
    {
        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            foreach (var topic in initialTopics)
            {
                _topicCache.TryAdd(topic.Topic, topic);
            }

            _isInitialized = true;
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        // For simple cache lookups like GetDataForTopicAsync, we don't need to wait for full initialization
        // The cache will be populated incrementally as data arrives via events
        // This removes the 100ms delay that was causing "Loading..." to show
        return;
    }

    private async Task OnTopicAdded(TopicAddedEvent eventData)
    {
        var topicInfo = new TopicInfo
        {
            Topic = eventData.Topic,
            Path = eventData.Path,
            IsVerified = eventData.IsVerified,
            IsActive = true,
            SourceType = eventData.SourceType,
            CreatedAt = eventData.CreatedAt,
            ModifiedAt = eventData.CreatedAt,
            Description = "",
            Metadata = new Dictionary<string, object>()
        };

        _topicCache.TryAdd(eventData.Topic, topicInfo);
        
        // Notify UI
        TopicAdded?.Invoke(this, new TopicAddedEventArgs { TopicInfo = topicInfo });
    }

    private async Task OnTopicDataUpdated(TopicDataUpdatedEvent eventData)
    {
        _latestDataCache.AddOrUpdate(eventData.Topic, eventData.DataPoint, (key, existing) => eventData.DataPoint);
        
        // Notify UI
        TopicDataUpdated?.Invoke(this, new TopicDataUpdatedEventArgs
        {
            Topic = eventData.Topic,
            DataPoint = eventData.DataPoint
        });
    }

    private async Task OnTopicVerified(TopicVerifiedEvent eventData)
    {
        if (_topicCache.TryGetValue(eventData.Topic, out var topicInfo))
        {
            var updatedTopic = new TopicInfo
            {
                Topic = topicInfo.Topic,
                Path = topicInfo.Path,
                IsVerified = true,
                IsActive = topicInfo.IsActive,
                SourceType = topicInfo.SourceType,
                CreatedAt = topicInfo.CreatedAt,
                ModifiedAt = eventData.VerifiedAt,
                Description = topicInfo.Description,
                Metadata = topicInfo.Metadata
            };
            _topicCache.TryUpdate(eventData.Topic, updatedTopic, topicInfo);
        }
    }

    private async Task OnTopicConfigurationUpdated(TopicConfigurationUpdatedEvent eventData)
    {
        if (_topicCache.TryGetValue(eventData.Topic, out var topicInfo))
        {
            var updatedTopic = new TopicInfo
            {
                Topic = topicInfo.Topic,
                Path = eventData.NewPath,
                IsVerified = topicInfo.IsVerified,
                IsActive = topicInfo.IsActive,
                SourceType = topicInfo.SourceType,
                CreatedAt = topicInfo.CreatedAt,
                ModifiedAt = eventData.Timestamp,
                Description = topicInfo.Description,
                Metadata = topicInfo.Metadata
            };
            _topicCache.TryUpdate(eventData.Topic, updatedTopic, topicInfo);
        }
    }

    private async Task OnBulkTopicsAdded(BulkTopicsAddedEvent eventData)
    {
        var addedTopics = new List<TopicInfo>();

        foreach (var (topic, path, isVerified, createdAt) in eventData.Topics)
        {
            var topicInfo = new TopicInfo
            {
                Topic = topic,
                Path = path,
                IsVerified = isVerified,
                IsActive = true,
                SourceType = eventData.SourceType,
                CreatedAt = createdAt,
                ModifiedAt = createdAt,
                Description = "",
                Metadata = new Dictionary<string, object>()
            };

            if (_topicCache.TryAdd(topic, topicInfo))
            {
                addedTopics.Add(topicInfo);
            }
        }

        // Notify UI about all added topics
        foreach (var topicInfo in addedTopics)
        {
            TopicAdded?.Invoke(this, new TopicAddedEventArgs { TopicInfo = topicInfo });
        }
    }

    /// <summary>
    /// Notifies the service that a new topic has been added.
    /// This method publishes an event instead of directly modifying state.
    /// </summary>
    public void NotifyTopicAdded(TopicInfo topicInfo)
    {
        _ = Task.Run(async () =>
        {
            await _eventBus.PublishAsync(new TopicAddedEvent(
                topicInfo.Topic,
                topicInfo.Path,
                topicInfo.SourceType,
                topicInfo.IsVerified,
                topicInfo.CreatedAt
            ));
        });
    }

    /// <summary>
    /// Notifies the service that topic data has been updated.
    /// This method publishes an event instead of directly modifying state.
    /// </summary>
    public void NotifyTopicDataUpdated(string topic, DataPoint dataPoint)
    {
        _ = Task.Run(async () =>
        {
            await _eventBus.PublishAsync(new TopicDataUpdatedEvent(topic, dataPoint, "unknown"));
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _initializationSemaphore?.Dispose();
        _disposed = true;
    }
}