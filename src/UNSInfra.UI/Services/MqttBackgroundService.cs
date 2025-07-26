using System.Collections.Concurrent;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Data;

namespace UNSInfra.UI.Services;

/// <summary>
/// Background service that manages MQTT connection and routes data to storage and topic browser.
/// Includes throttling to handle high-volume topic streams efficiently.
/// </summary>
public class MqttBackgroundService : BackgroundService
{
    private readonly IMqttDataService _mqttDataService;
    private readonly IRealtimeStorage _realtimeStorage;
    private readonly IHistoricalStorage _historicalStorage;
    private readonly ITopicBrowserService _topicBrowserService;
    private readonly ILogger<MqttBackgroundService> _logger;
    private readonly ConcurrentQueue<DataPoint> _dataQueue = new();
    private readonly Timer _processTimer;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private const int BatchSize = 100;
    private const int ProcessingIntervalMs = 250;

    public MqttBackgroundService(
        IMqttDataService mqttDataService,
        IRealtimeStorage realtimeStorage,
        IHistoricalStorage historicalStorage,
        ITopicBrowserService topicBrowserService,
        ILogger<MqttBackgroundService> logger)
    {
        _mqttDataService = mqttDataService;
        _realtimeStorage = realtimeStorage;
        _historicalStorage = historicalStorage;
        _topicBrowserService = topicBrowserService;
        _logger = logger;
        
        // Initialize the batch processing timer
        _processTimer = new Timer(ProcessDataBatch, null, ProcessingIntervalMs, ProcessingIntervalMs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting MQTT background service");

            // Subscribe to data received events
            _mqttDataService.DataReceived += OnDataReceived;

            // Start the MQTT service
            await _mqttDataService.StartAsync();

            // Subscribe to all topics using wildcard
            await SubscribeToAllTopics();

            _logger.LogInformation("MQTT background service started successfully");

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MQTT background service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MQTT background service");
            throw;
        }
    }

    private async Task SubscribeToAllTopics()
    {
        try
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

            await _mqttDataService.SubscribeToTopicAsync("#", wildcardPath);
            _logger.LogInformation("Subscribed to all topics using wildcard '#'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to wildcard topic");
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
            _logger.LogError(ex, "Error queuing received data for topic: {Topic}", dataPoint.Topic);
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

            // Notify topic browser about updates (throttled to once per batch)
            if (_topicBrowserService is TopicBrowserService browserService && batch.Count > 0)
            {
                try
                {
                    // Only notify about the latest update per topic in this batch
                    var latestPerTopic = batch
                        .GroupBy(dp => dp.Topic)
                        .Select(g => g.OrderByDescending(dp => dp.Timestamp).First());

                    foreach (var dataPoint in latestPerTopic)
                    {
                        browserService.NotifyTopicDataUpdated(dataPoint.Topic, dataPoint);
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MQTT background service");

        try
        {
            // Stop the timer
            await _processTimer.DisposeAsync();
            
            // Unsubscribe from events
            _mqttDataService.DataReceived -= OnDataReceived;

            // Process any remaining queued data
            await ProcessRemainingData();

            // Stop the MQTT service
            await _mqttDataService.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT service");
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("MQTT background service stopped");
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
        _mqttDataService.DataReceived -= OnDataReceived;
        base.Dispose();
    }
}