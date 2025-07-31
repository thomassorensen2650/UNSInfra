using System.Collections.Concurrent;
using System.Threading.Channels;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Data;
using UNSInfra.Repositories;
using UNSInfra.Services.Events;
using UNSInfra.Services.DataIngestion;

namespace UNSInfra.UI.Services;

/// <summary>
/// Event-driven background service that processes data without blocking the UI thread.
/// Uses high-performance queues and batch processing for optimal performance.
/// </summary>
public class EventDrivenDataIngestionBackgroundService : BackgroundService
{
    private readonly IEnumerable<IDataIngestionService> _dataIngestionServices;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EventDrivenDataIngestionBackgroundService> _logger;
    
    // High-performance queues for different types of operations
    private readonly Channel<DataPoint> _dataChannel = Channel.CreateUnbounded<DataPoint>();
    private readonly Channel<(string Topic, HierarchicalPath Path, string SourceType)> _newTopicChannel = Channel.CreateUnbounded<(string, HierarchicalPath, string)>();
    
    private readonly ConcurrentDictionary<string, bool> _knownTopics = new();
    private readonly ConcurrentDictionary<string, bool> _verifiedTopics = new();
    private readonly Timer _maintenanceTimer;
    private readonly Timer _verificationCacheTimer;
    
    private const int DataBatchSize = 100; // Reduced from 1000 to lower memory pressure
    private const int TopicBatchSize = 25; // Reduced from 100 to lower memory pressure
    private static readonly int MaxConcurrentTasks = Environment.ProcessorCount;

    public EventDrivenDataIngestionBackgroundService(
        IEnumerable<IDataIngestionService> dataIngestionServices,
        IServiceScopeFactory serviceScopeFactory,
        IEventBus eventBus,
        ILogger<EventDrivenDataIngestionBackgroundService> logger)
    {
        _dataIngestionServices = dataIngestionServices;
        _serviceScopeFactory = serviceScopeFactory;
        _eventBus = eventBus;
        _logger = logger;

        // Initialize timers for maintenance tasks
        _maintenanceTimer = new Timer(RunMaintenance, null, TimeSpan.FromHours(1), TimeSpan.FromHours(6));
        _verificationCacheTimer = new Timer(RefreshVerificationCache, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize the system
        await InitializeAsync();

        // Start parallel processing tasks
        var tasks = new[]
        {
            ProcessDataAsync(stoppingToken),
            ProcessNewTopicsAsync(stoppingToken),
            MonitorDataIngestionServicesAsync(stoppingToken)
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Data ingestion service is shutting down");
        }
    }

    private async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing event-driven data ingestion service");

        try
        {
            // Load existing topics into cache
            await RefreshTopicCaches();

            // Initialize event-driven topic browser service if available  
            if (_eventBus is not null)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var topicConfigurationRepository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
                
                var existingTopics = await topicConfigurationRepository.GetAllTopicConfigurationsAsync();
                var topicInfos = existingTopics.Select(config => new TopicInfo
                {
                    Topic = config.Topic,
                    Path = config.Path,
                    IsVerified = config.IsVerified,
                    IsActive = config.IsActive,
                    SourceType = config.SourceType,
                    CreatedAt = config.CreatedAt,
                    ModifiedAt = config.ModifiedAt,
                    Description = config.Description,
                    Metadata = config.Metadata,
                    NSPath = config.NSPath
                });

                // Initialize the event-driven topic browser service's cache
                var eventDrivenTopicBrowser = scope.ServiceProvider.GetService<EventDrivenTopicBrowserService>();
                if (eventDrivenTopicBrowser != null)
                {
                    await eventDrivenTopicBrowser.InitializeCacheAsync(topicInfos);
                    _logger.LogInformation("Initialized EventDrivenTopicBrowserService cache with {Count} topics", existingTopics.Count());
                }

                // Publish bulk initialization event for any other subscribers
                await _eventBus.PublishAsync(new BulkTopicsAddedEvent(
                    existingTopics.Select(t => (t.Topic, t.Path, t.IsVerified, t.CreatedAt)).ToList(),
                    "initialization"
                ));
            }

            // Start data ingestion services
            foreach (var service in _dataIngestionServices)
            {
                service.DataReceived += OnDataReceived;
                await service.StartAsync();
            }

            _logger.LogInformation("Event-driven data ingestion service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize data ingestion service");
            throw;
        }
    }

    private async Task ProcessDataAsync(CancellationToken cancellationToken)
    {
        var reader = _dataChannel.Reader;
        var dataBatch = new List<DataPoint>(DataBatchSize);

        try
        {
            await foreach (var dataPoint in reader.ReadAllAsync(cancellationToken))
            {
                dataBatch.Add(dataPoint);

                // Process in batches for better performance
                if (dataBatch.Count >= DataBatchSize)
                {
                    await ProcessDataBatch(dataBatch);
                    dataBatch.Clear();
                }
            }

            // Process remaining items
            if (dataBatch.Count > 0)
            {
                await ProcessDataBatch(dataBatch);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in data processing loop");
        }
    }

    private async Task ProcessDataBatch(List<DataPoint> batch)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var realtimeStorage = scope.ServiceProvider.GetRequiredService<IRealtimeStorage>();
            var historicalStorage = scope.ServiceProvider.GetRequiredService<IHistoricalStorage>();

            // Process storage operations in parallel
            var storageTasks = new[]
            {
                Task.Run(async () =>
                {
                    foreach (var dataPoint in batch)
                    {
                        await realtimeStorage.StoreAsync(dataPoint);
                    }
                }),
                Task.Run(async () =>
                {
                    foreach (var dataPoint in batch)
                    {
                        await historicalStorage.StoreAsync(dataPoint);
                    }
                })
            };

            await Task.WhenAll(storageTasks);

            // Force garbage collection every 10 batches to reduce memory pressure
            if (batch.Count > 50 && batch.GetHashCode() % 10 == 0)
            {
                GC.Collect(0, GCCollectionMode.Optimized);
            }

            // Publish data update events (throttled to avoid overwhelming SignalR)
            // Only publish events for the latest data per topic in this batch to reduce SignalR load
            var latestPerTopic = batch
                .GroupBy(dp => dp.Topic)
                .Select(g => g.OrderByDescending(dp => dp.Timestamp).First())
                .Take(50); // Limit to 50 events per batch to prevent SignalR overload

            foreach (var dataPoint in latestPerTopic)
            {
                await _eventBus.PublishAsync(new TopicDataUpdatedEvent(dataPoint.Topic, dataPoint, "unknown"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data batch of {Count} items", batch.Count);
        }
    }

    private async Task ProcessNewTopicsAsync(CancellationToken cancellationToken)
    {
        var reader = _newTopicChannel.Reader;
        var topicBatch = new List<(string Topic, HierarchicalPath Path, string SourceType)>(TopicBatchSize);

        try
        {
            await foreach (var newTopic in reader.ReadAllAsync(cancellationToken))
            {
                topicBatch.Add(newTopic);

                if (topicBatch.Count >= TopicBatchSize)
                {
                    await ProcessNewTopicsBatch(topicBatch);
                    topicBatch.Clear();
                }
            }

            if (topicBatch.Count > 0)
            {
                await ProcessNewTopicsBatch(topicBatch);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in new topics processing loop");
        }
    }

    private async Task ProcessNewTopicsBatch(List<(string Topic, HierarchicalPath Path, string SourceType)> batch)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var topicConfigurationRepository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();

            var now = DateTime.UtcNow;
            var configurations = new List<TopicConfiguration>();

            foreach (var (topic, path, sourceType) in batch)
            {
                var configuration = new TopicConfiguration
                {
                    Id = Guid.NewGuid().ToString(),
                    Topic = topic,
                    Path = path,
                    IsVerified = false,
                    IsActive = true,
                    SourceType = sourceType,
                    CreatedAt = now,
                    ModifiedAt = now,
                    Description = $"Auto-discovered from {sourceType}",
                    Metadata = new Dictionary<string, object>
                    {
                        { "discovery_method", "auto" },
                        { "source", sourceType }
                    }
                };

                configurations.Add(configuration);
            }

            // Batch save to database
            foreach (var config in configurations)
            {
                try
                {
                    await topicConfigurationRepository.SaveTopicConfigurationAsync(config);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save topic configuration for {Topic}", config.Topic);
                }
            }

            // Publish bulk event for UI updates
            var topicData = configurations.Select(c => (c.Topic, c.Path, c.IsVerified, c.CreatedAt)).ToList();
            await _eventBus.PublishAsync(new BulkTopicsAddedEvent(topicData, batch.First().SourceType));

            _logger.LogDebug("Processed batch of {Count} new topics", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing new topics batch of {Count} items", batch.Count);
        }
    }

    private async Task MonitorDataIngestionServicesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Monitor service health and restart if needed
                foreach (var service in _dataIngestionServices)
                {
                    // Add service monitoring logic here if needed
                }

                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in service monitoring");
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }

    private void OnDataReceived(object? sender, DataPoint dataPoint)
    {
        try
        {
            // Non-blocking: just add to queue
            if (!_dataChannel.Writer.TryWrite(dataPoint))
            {
                _logger.LogWarning("Data channel is full, dropping data point for topic {Topic}", dataPoint.Topic);
            }

            // Check for new topics (non-blocking)
            if (_knownTopics.TryAdd(dataPoint.Topic, true))
            {
                var path = new HierarchicalPath(); // We'll need to determine this from the data source
                var sourceType = sender?.GetType().Name?.Replace("DataService", "") ?? "Unknown";
                
                if (!_newTopicChannel.Writer.TryWrite((dataPoint.Topic, path, sourceType)))
                {
                    _logger.LogWarning("New topic channel is full, dropping new topic {Topic}", dataPoint.Topic);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling data received event for topic {Topic}", dataPoint.Topic);
        }
    }

    private async Task RefreshTopicCaches()
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var topicConfigurationRepository = scope.ServiceProvider.GetRequiredService<ITopicConfigurationRepository>();
            
            var existingTopics = await topicConfigurationRepository.GetAllTopicConfigurationsAsync();
            
            _knownTopics.Clear();
            _verifiedTopics.Clear();

            foreach (var topic in existingTopics)
            {
                _knownTopics.TryAdd(topic.Topic, true);
                if (topic.IsVerified)
                {
                    _verifiedTopics.TryAdd(topic.Topic, true);
                }
            }

            _logger.LogDebug("Refreshed topic caches with {Count} topics", existingTopics.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing topic caches");
        }
    }

    private async void RefreshVerificationCache(object? state)
    {
        try
        {
            await RefreshTopicCaches();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in verification cache refresh timer");
        }
    }

    private async void RunMaintenance(object? state)
    {
        try
        {
            _logger.LogInformation("Running maintenance tasks");
            
            using var scope = _serviceScopeFactory.CreateScope();
            var historicalStorage = scope.ServiceProvider.GetRequiredService<IHistoricalStorage>();
            
            // Archive old data
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            await historicalStorage.ArchiveAsync(cutoffTime);
            
            _logger.LogInformation("Maintenance tasks completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running maintenance tasks");
        }
    }

    public override void Dispose()
    {
        _dataChannel.Writer.Complete();
        _newTopicChannel.Writer.Complete();
        _maintenanceTimer?.Dispose();
        _verificationCacheTimer?.Dispose();
        
        foreach (var service in _dataIngestionServices)
        {
            service.DataReceived -= OnDataReceived;
        }

        base.Dispose();
    }
}