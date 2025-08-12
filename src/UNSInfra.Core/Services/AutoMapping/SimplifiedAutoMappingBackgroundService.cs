using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.Events;

namespace UNSInfra.Services.AutoMapping;

/// <summary>
/// Background service that integrates with the existing DataIngestionBackgroundService to 
/// automatically map new topics using the SimplifiedAutoMapperService.
/// </summary>
public class SimplifiedAutoMappingBackgroundService : BackgroundService
{
    private readonly SimplifiedAutoMapperService _autoMapper;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SimplifiedAutoMappingBackgroundService> _logger;
    
    // Queue for processing topics that need auto-mapping
    private readonly Queue<TopicInfo> _topicsToProcess = new();
    private readonly object _queueLock = new object();
    
    // Performance settings
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(2); // Process every 2 seconds
    private readonly int _maxBatchSize = 50; // Process up to 50 topics per batch
    
    // Statistics
    private int _processedTopics = 0;
    private int _mappedTopics = 0;
    private int _failedTopics = 0;

    public SimplifiedAutoMappingBackgroundService(
        SimplifiedAutoMapperService autoMapper,
        IEventBus eventBus,
        ILogger<SimplifiedAutoMappingBackgroundService> logger)
    {
        _autoMapper = autoMapper;
        _eventBus = eventBus;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting SimplifiedAutoMapping background service");
        
        // Initialize the namespace cache
        await _autoMapper.InitializeCacheAsync();
        
        // Subscribe to new topic events
        _eventBus.Subscribe<TopicAddedEvent>(OnTopicAdded);
        _eventBus.Subscribe<NamespaceStructureChangedEvent>(OnNamespaceStructureChanged);
        
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SimplifiedAutoMapping background service is running");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTopicBatch(stoppingToken);
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SimplifiedAutoMapping background service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait longer on error
            }
        }
    }

    /// <summary>
    /// Handle new topics being added - queue them for auto-mapping
    /// </summary>
    private Task OnTopicAdded(TopicAddedEvent topicEvent)
    {
        // Create TopicInfo from the event data
        var topicInfo = new TopicInfo
        {
            Topic = topicEvent.Topic,
            SourceType = topicEvent.SourceType,
            NSPath = topicEvent.Path?.GetFullPath(), // Get the actual path string from HierarchicalPath
            CreatedAt = topicEvent.CreatedAt
        };

        // Only process topics that don't already have a namespace mapping
        if (string.IsNullOrEmpty(topicInfo.NSPath))
        {
            lock (_queueLock)
            {
                _topicsToProcess.Enqueue(topicInfo);
            }
            
            _logger.LogDebug("Queued topic '{Topic}' for auto-mapping", topicInfo.Topic);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle namespace structure changes - refresh the cache
    /// </summary>
    private async Task OnNamespaceStructureChanged(NamespaceStructureChangedEvent namespaceEvent)
    {
        _logger.LogInformation("Namespace structure changed, refreshing auto-mapper cache");
        await _autoMapper.RefreshCacheAsync();
    }

    /// <summary>
    /// Process a batch of topics for auto-mapping
    /// </summary>
    private async Task ProcessTopicBatch(CancellationToken cancellationToken)
    {
        var batch = new List<TopicInfo>();
        
        // Get a batch of topics to process
        lock (_queueLock)
        {
            while (_topicsToProcess.Count > 0 && batch.Count < _maxBatchSize)
            {
                batch.Add(_topicsToProcess.Dequeue());
            }
        }
        
        if (batch.Count == 0)
        {
            return; // Nothing to process
        }
        
        _logger.LogDebug("Processing batch of {Count} topics for auto-mapping", batch.Count);
        var startTime = DateTime.UtcNow;
        
        foreach (var topic in batch)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            try
            {
                // Try to map the topic to a UNS namespace
                var namespacePath = _autoMapper.TryMapTopic(topic.Topic);
                
                _processedTopics++;
                
                if (!string.IsNullOrEmpty(namespacePath))
                {
                    // Publish successful mapping event
                    var mappingEvent = new TopicAutoMappedEvent(
                        Topic: topic.Topic,
                        SourceType: topic.SourceType,
                        MappedNamespace: namespacePath,
                        Confidence: 1.0, // Simplified mapper is binary - either matches or doesn't
                        TopicConfiguration: null // Will be created by another service if needed
                    );
                    
                    await _eventBus.PublishAsync(mappingEvent);
                    
                    _mappedTopics++;
                    _logger.LogDebug("Auto-mapped topic '{Topic}' to namespace '{Namespace}'", 
                        topic.Topic, namespacePath);
                }
                else
                {
                    // Publish failed mapping event
                    var failedEvent = new TopicAutoMappingFailedEvent(
                        Topic: topic.Topic,
                        SourceType: topic.SourceType,
                        Reason: "No matching namespace found in UNS structure"
                    );
                    
                    await _eventBus.PublishAsync(failedEvent);
                    
                    _failedTopics++;
                    _logger.LogTrace("Failed to auto-map topic '{Topic}' - no matching namespace found", 
                        topic.Topic);
                }
            }
            catch (Exception ex)
            {
                _failedTopics++;
                _logger.LogError(ex, "Error processing topic '{Topic}' for auto-mapping", topic.Topic);
                
                // Publish error event
                var errorEvent = new TopicAutoMappingFailedEvent(
                    Topic: topic.Topic,
                    SourceType: topic.SourceType,
                    Reason: $"Processing error: {ex.Message}"
                );
                
                await _eventBus.PublishAsync(errorEvent);
            }
        }
        
        var duration = DateTime.UtcNow - startTime;
        if (batch.Count > 0)
        {
            _logger.LogInformation("Processed {Count} topics in {Duration}ms - Mapped: {Mapped}, Failed: {Failed}", 
                batch.Count, duration.TotalMilliseconds, _mappedTopics, _failedTopics);
        }
        
        // Log performance stats periodically
        if (_processedTopics > 0 && _processedTopics % 100 == 0)
        {
            var stats = _autoMapper.GetStats();
            var successRate = (double)_mappedTopics / _processedTopics;
            
            _logger.LogInformation("AutoMapping stats - Processed: {Processed}, Success rate: {SuccessRate:P1}, " +
                "Cache hits: {Hits}, Cache misses: {Misses}, Hit ratio: {HitRatio:P1}", 
                _processedTopics, successRate, stats.CacheHits, stats.CacheMisses, stats.HitRatio);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping SimplifiedAutoMapping background service");
        
        // Process any remaining topics in the queue
        if (_topicsToProcess.Count > 0)
        {
            _logger.LogInformation("Processing {Count} remaining topics before shutdown", _topicsToProcess.Count);
            await ProcessTopicBatch(cancellationToken);
        }
        
        // Unsubscribe from events
        _eventBus.Unsubscribe<TopicAddedEvent>(OnTopicAdded);
        _eventBus.Unsubscribe<NamespaceStructureChangedEvent>(OnNamespaceStructureChanged);
        
        // Final statistics
        var stats = _autoMapper.GetStats();
        var successRate = _processedTopics > 0 ? (double)_mappedTopics / _processedTopics : 0.0;
        
        _logger.LogInformation("SimplifiedAutoMapping service stopped. Final stats - " +
            "Processed: {Processed}, Mapped: {Mapped}, Failed: {Failed}, Success rate: {SuccessRate:P1}",
            _processedTopics, _mappedTopics, _failedTopics, successRate);
        
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Manually queue a topic for mapping (primarily for testing purposes)
    /// </summary>
    /// <param name="topicInfo">The topic information to queue for mapping</param>
    public void QueueTopicForMapping(TopicInfo topicInfo)
    {
        lock (_queueLock)
        {
            _topicsToProcess.Enqueue(topicInfo);
        }
        
        _logger.LogDebug("Manually queued topic '{Topic}' for auto-mapping", topicInfo.Topic);
    }
}