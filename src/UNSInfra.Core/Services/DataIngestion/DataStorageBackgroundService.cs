using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Services.Events;
using UNSInfra.Storage.Abstractions;

namespace UNSInfra.Services.DataIngestion;

/// <summary>
/// Background service that subscribes to TopicDataUpdatedEvent and stores data in storage systems.
/// This ensures that all incoming data is persisted to both realtime and historical storage.
/// </summary>
public class DataStorageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<DataStorageBackgroundService> _logger;

    public DataStorageBackgroundService(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        ILogger<DataStorageBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DataStorage background service");
        
        // Subscribe to data events
        _eventBus.Subscribe<TopicDataUpdatedEvent>(OnTopicDataUpdated);
        
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataStorage background service is running");
        
        // This service is event-driven, so we just wait for cancellation
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
    }

    /// <summary>
    /// Handle topic data updates by storing them in both realtime and historical storage
    /// </summary>
    private async Task OnTopicDataUpdated(TopicDataUpdatedEvent eventData)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            // Get storage services
            var realtimeStorage = scope.ServiceProvider.GetService<IRealtimeStorage>();
            var historicalStorage = scope.ServiceProvider.GetService<IHistoricalStorage>();
            
            // Store in realtime storage (for latest values)
            if (realtimeStorage != null)
            {
                await realtimeStorage.StoreAsync(eventData.DataPoint);
                _logger.LogTrace("Stored data point for topic {Topic} in realtime storage", eventData.Topic);
            }
            
            // Store in historical storage (for time series data)
            if (historicalStorage != null)
            {
                await historicalStorage.StoreAsync(eventData.DataPoint);
                _logger.LogTrace("Stored data point for topic {Topic} in historical storage", eventData.Topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing data for topic {Topic}", eventData.Topic);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DataStorage background service");
        
        // Unsubscribe from events
        _eventBus.Unsubscribe<TopicDataUpdatedEvent>(OnTopicDataUpdated);
        
        await base.StopAsync(cancellationToken);
    }
}