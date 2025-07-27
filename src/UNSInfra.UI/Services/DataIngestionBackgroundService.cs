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
    private const int BatchSize = 100;
    private const int ProcessingIntervalMs = 250;

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
            
            if (_dataQueue.Count % 1000 == 0)
            {
                _logger.LogDebug("Data queue size: {QueueSize}", _dataQueue.Count);
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

            // Process the batch
            foreach (var dataPoint in batch)
            {
                try
                {
                    // Store in realtime storage
                    await _realtimeStorage.StoreAsync(dataPoint);
                    
                    // Store in historical storage  
                    await _historicalStorage.StoreAsync(dataPoint);

                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error storing data for topic: {Topic}", dataPoint.Topic);
                }
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
            }
            _logger.LogInformation("Loaded {Count} existing topics for tracking", _knownTopics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading existing topics");
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