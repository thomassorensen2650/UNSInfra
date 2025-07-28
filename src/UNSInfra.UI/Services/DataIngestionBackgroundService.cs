using System.Collections.Concurrent;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Data;
using UNSInfra.Repositories;

namespace UNSInfra.UI.Services;

/// <summary>
/// Background service that manages all data ingestion sources and routes data to storage and topic browser.
/// Includes throttling to handle high-volume topic streams efficiently from multiple data sources.
/// </summary>
public class DataIngestionBackgroundService : BackgroundService
{
    private readonly IEnumerable<IDataIngestionService> _dataIngestionServices;
    private readonly IRealtimeStorage _realtimeStorage;
    private readonly IHistoricalStorage _historicalStorage;
    private readonly ITopicBrowserService _topicBrowserService;
    private readonly ITopicConfigurationRepository _topicConfigurationRepository;
    private readonly ILogger<DataIngestionBackgroundService> _logger;
    private readonly ConcurrentQueue<DataPoint> _dataQueue = new();
    private readonly Timer _processTimer;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private readonly ConcurrentDictionary<string, bool> _knownTopics = new();
    private readonly ConcurrentDictionary<string, bool> _verifiedTopics = new();
    private DateTime _lastVerificationCacheUpdate = DateTime.MinValue;
    private DateTime _lastCleanupTime = DateTime.MinValue;
    private const int BatchSize = 500; // Increased for better throughput with 100k topics
    private const int ProcessingIntervalMs = 500; // Slightly longer to allow larger batches
    private const int VerificationCacheRefreshMinutes = 5;
    private const int CleanupIntervalHours = 6;
    private const int RealtimeDataRetentionHours = 24; // Keep 24 hours of realtime data

    public DataIngestionBackgroundService(
        IEnumerable<IDataIngestionService> dataIngestionServices,
        IRealtimeStorage realtimeStorage,
        IHistoricalStorage historicalStorage,
        ITopicBrowserService topicBrowserService,
        ITopicConfigurationRepository topicConfigurationRepository,
        ILogger<DataIngestionBackgroundService> logger)
    {
        _dataIngestionServices = dataIngestionServices;
        _realtimeStorage = realtimeStorage;
        _historicalStorage = historicalStorage;
        _topicBrowserService = topicBrowserService;
        _topicConfigurationRepository = topicConfigurationRepository;
        _logger = logger;
        
        // Initialize the batch processing timer
        _processTimer = new Timer(ProcessDataBatch, null, ProcessingIntervalMs, ProcessingIntervalMs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var serviceCount = _dataIngestionServices.Count();
            _logger.LogInformation("Starting data ingestion background service with {ServiceCount} data sources", serviceCount);

            // Subscribe to data received events for all services
            foreach (var service in _dataIngestionServices)
            {
                service.DataReceived += OnDataReceived;
                _logger.LogInformation("Subscribed to data events from {ServiceType}", service.GetType().Name);
            }

            // Start all data ingestion services
            foreach (var service in _dataIngestionServices)
            {
                await service.StartAsync();
                _logger.LogInformation("Started {ServiceType}", service.GetType().Name);
            }

            // Subscribe to all MQTT topics using wildcard (specific to MQTT service)
            await SubscribeToMqttTopics();

            // Load existing topics to track which ones are new
            await LoadExistingTopics();

            _logger.LogInformation("Data ingestion background service started successfully with {ServiceCount} sources", serviceCount);

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Data ingestion background service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in data ingestion background service");
            throw;
        }
    }

    private async Task SubscribeToMqttTopics()
    {
        try
        {
            // Find MQTT service and subscribe to wildcard topics
            var mqttService = _dataIngestionServices.OfType<IMqttDataService>().FirstOrDefault();
            if (mqttService != null)
            {
                // Subscribe to all topics using the wildcard "#"
                // This will receive all messages published to the broker
                var wildcardPath = new HierarchicalPath 
                { 
                    Enterprise = "mqtt", 
                    Site = "broker", 
                    Area = "all", 
                    WorkCenter = "topics", 
                    WorkUnit = "", 
                    Property = "" 
                };

                await mqttService.SubscribeToTopicAsync("test", wildcardPath);
                _logger.LogInformation("Subscribed to MQTT topics using wildcard");
            }
            else
            {
                _logger.LogInformation("No MQTT service found, skipping topic subscription");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to MQTT topics");
        }
    }

    private void OnDataReceived(object? sender, DataPoint dataPoint)
    {
        try
        {
            // Queue the data point for batch processing instead of immediate processing
            _dataQueue.Enqueue(dataPoint);
            
            if (_dataQueue.Count % 5000 == 0) // Log less frequently for high volume
            {
                _logger.LogInformation("High volume: Data queue size: {QueueSize}", _dataQueue.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing received data for topic: {Topic} from source: {Source}", dataPoint.Topic, dataPoint.Source);
        }
    }

    private async void ProcessDataBatch(object? state)
    {
        if (!await _processingLock.WaitAsync(100)) // Don't wait long if already processing
            return;

        try
        {
            var processedCount = 0;
            var batch = new List<DataPoint>();

            // Dequeue up to BatchSize items
            while (batch.Count < BatchSize && _dataQueue.TryDequeue(out var dataPoint))
            {
                batch.Add(dataPoint);
            }

            if (batch.Count == 0)
                return;

            _logger.LogDebug("Processing batch of {Count} data points", batch.Count);

            // Check for new topics and collect them
            var newTopics = new List<DataPoint>();
            foreach (var dataPoint in batch)
            {
                if (_knownTopics.TryAdd(dataPoint.Topic, true))
                {
                    newTopics.Add(dataPoint);
                    _logger.LogDebug("Discovered new topic: {Topic}", dataPoint.Topic);
                }
            }

            // Refresh verification cache periodically
            await RefreshVerificationCacheIfNeeded();
            
            // Run cleanup periodically to prevent storage bloat
            await RunCleanupIfNeeded();

            // Process the batch - separate verified and unverified for efficient batch operations
            var verifiedBatch = new List<DataPoint>();
            var unverifiedBatch = new List<DataPoint>();
            
            foreach (var dataPoint in batch)
            {
                if (_verifiedTopics.ContainsKey(dataPoint.Topic))
                    verifiedBatch.Add(dataPoint);
                else
                    unverifiedBatch.Add(dataPoint);
            }

            try
            {
                // Batch store all data points in realtime storage
                if (_realtimeStorage is IBatchStorage batchRealtimeStorage)
                {
                    await batchRealtimeStorage.StoreBatchAsync(batch);
                }
                else
                {
                    // Fallback to individual storage calls
                    foreach (var dataPoint in batch)
                    {
                        await _realtimeStorage.StoreAsync(dataPoint);
                    }
                }

                // Batch store only verified topics in historical storage
                if (verifiedBatch.Count > 0)
                {
                    if (_historicalStorage is IBatchStorage batchHistoricalStorage)
                    {
                        await batchHistoricalStorage.StoreBatchAsync(verifiedBatch);
                    }
                    else
                    {
                        // Fallback to individual storage calls
                        foreach (var dataPoint in verifiedBatch)
                        {
                            await _historicalStorage.StoreAsync(dataPoint);
                        }
                    }
                    _logger.LogTrace("Historical data stored for {Count} verified topics", verifiedBatch.Count);
                }

                if (unverifiedBatch.Count > 0)
                {
                    _logger.LogTrace("Skipped historical storage for {Count} unverified topics", unverifiedBatch.Count);
                }

                processedCount = batch.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing batch of {Count} data points", batch.Count);
            }

            // Notify about new topics first
            if (_topicBrowserService is TopicBrowserService browserService && newTopics.Count > 0)
            {
                try
                {
                    foreach (var dataPoint in newTopics)
                    {
                        // Get the topic configuration that was created by the data ingestion service
                        var topicConfig = await _topicConfigurationRepository.GetTopicConfigurationAsync(dataPoint.Topic);
                        if (topicConfig != null)
                        {
                            var topicInfo = new TopicInfo
                            {
                                Topic = topicConfig.Topic,
                                Path = topicConfig.Path,
                                IsVerified = topicConfig.IsVerified,
                                IsActive = topicConfig.IsActive,
                                SourceType = topicConfig.SourceType,
                                CreatedAt = topicConfig.CreatedAt,
                                ModifiedAt = topicConfig.ModifiedAt,
                                Description = topicConfig.Description,
                                Metadata = topicConfig.Metadata
                            };
                            
                            browserService.NotifyTopicAdded(topicInfo);
                            _logger.LogDebug("Notified UI about new topic: {Topic}", dataPoint.Topic);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error notifying about new topics");
                }
            }

            // Notify topic browser about data updates (throttled to once per batch)
            if (batch.Count > 0)
            {
                try
                {
                    // Only notify about the latest update per topic in this batch
                    var latestPerTopic = batch
                        .GroupBy(dp => dp.Topic)
                        .Select(g => g.OrderByDescending(dp => dp.Timestamp).First());

                    foreach (var dataPoint in latestPerTopic)
                    {
                        if (_topicBrowserService is TopicBrowserService service)
                        {
                            service.NotifyTopicDataUpdated(dataPoint.Topic, dataPoint);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error notifying topic browser service");
                }
            }

            if (processedCount > 0)
            {
                _logger.LogDebug("Successfully processed {ProcessedCount} data points", processedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch processing");
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task LoadExistingTopics()
    {
        try
        {
            var existingTopics = await _topicConfigurationRepository.GetAllTopicConfigurationsAsync();
            foreach (var topic in existingTopics)
            {
                _knownTopics.TryAdd(topic.Topic, true);
                
                // Initialize verified topics cache
                if (topic.IsVerified)
                {
                    _verifiedTopics.TryAdd(topic.Topic, true);
                }
            }
            _lastVerificationCacheUpdate = DateTime.UtcNow;
            _logger.LogInformation("Loaded {Count} existing topics ({VerifiedCount} verified) for tracking", 
                _knownTopics.Count, _verifiedTopics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading existing topics");
        }
    }

    private async Task RefreshVerificationCacheIfNeeded()
    {
        if (DateTime.UtcNow - _lastVerificationCacheUpdate > TimeSpan.FromMinutes(VerificationCacheRefreshMinutes))
        {
            try
            {
                var allTopics = await _topicConfigurationRepository.GetAllTopicConfigurationsAsync();
                var newVerifiedTopics = new ConcurrentDictionary<string, bool>();
                
                foreach (var topic in allTopics.Where(t => t.IsVerified))
                {
                    newVerifiedTopics.TryAdd(topic.Topic, true);
                }
                
                _verifiedTopics.Clear();
                foreach (var kvp in newVerifiedTopics)
                {
                    _verifiedTopics.TryAdd(kvp.Key, kvp.Value);
                }
                
                _lastVerificationCacheUpdate = DateTime.UtcNow;
                _logger.LogDebug("Refreshed verification cache: {VerifiedCount} verified topics", _verifiedTopics.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing verification cache");
            }
        }
    }

    private async Task RunCleanupIfNeeded()
    {
        if (DateTime.UtcNow - _lastCleanupTime > TimeSpan.FromHours(CleanupIntervalHours))
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-RealtimeDataRetentionHours);
                
                // Clean up old realtime data (keep only last 24 hours for unverified topics)
                if (_realtimeStorage is ICleanableStorage cleanableRealtime)
                {
                    await cleanableRealtime.CleanupOldDataAsync(cutoffTime);
                    _logger.LogInformation("Cleaned up realtime data older than {CutoffTime}", cutoffTime);
                }
                
                // Archive old historical data (this should be configurable per deployment)
                var archiveCutoffTime = DateTime.UtcNow.AddDays(-30); // Keep 30 days by default
                await _historicalStorage.ArchiveAsync(archiveCutoffTime);
                
                _lastCleanupTime = DateTime.UtcNow;
                _logger.LogInformation("Completed storage cleanup cycle");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during storage cleanup");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping data ingestion background service");

        try
        {
            // Stop the timer
            await _processTimer.DisposeAsync();
            
            // Unsubscribe from events for all services
            foreach (var service in _dataIngestionServices)
            {
                service.DataReceived -= OnDataReceived;
                _logger.LogInformation("Unsubscribed from {ServiceType}", service.GetType().Name);
            }

            // Process any remaining queued data
            await ProcessRemainingData();

            // Stop all data ingestion services
            foreach (var service in _dataIngestionServices)
            {
                await service.StopAsync();
                _logger.LogInformation("Stopped {ServiceType}", service.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping data ingestion services");
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Data ingestion background service stopped");
    }

    private async Task ProcessRemainingData()
    {
        _logger.LogInformation("Processing remaining {Count} queued data points", _dataQueue.Count);
        
        while (_dataQueue.TryDequeue(out var dataPoint))
        {
            try
            {
                await _realtimeStorage.StoreAsync(dataPoint);
                await _historicalStorage.StoreAsync(dataPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing remaining data for topic: {Topic}", dataPoint.Topic);
            }
        }
    }

    public override void Dispose()
    {
        _processTimer?.Dispose();
        _processingLock?.Dispose();
        
        // Unsubscribe from all data ingestion services
        foreach (var service in _dataIngestionServices)
        {
            service.DataReceived -= OnDataReceived;
        }
        
        base.Dispose();
    }
}