using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;

namespace UNSInfra.Services.DataIngestion;

/// <summary>
/// High-performance data ingestion pipeline that coordinates stream processing
/// and bulk data operations for optimal throughput and minimal latency.
/// </summary>
public class DataIngestionPipeline : BackgroundService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataIngestionPipeline> _logger;
    private readonly StreamProcessingService _streamProcessor;
    private readonly BulkDataProcessor _bulkProcessor;
    
    // Performance monitoring
    private readonly Timer _statisticsTimer;
    private DateTime _lastStatisticsLog = DateTime.UtcNow;
    private long _totalDataPointsIngested = 0;
    private volatile bool _disposed = false;

    public DataIngestionPipeline(
        IServiceProvider serviceProvider,
        ILogger<DataIngestionPipeline> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Create stream processor with optimized settings for high throughput
        _streamProcessor = new StreamProcessingService(
            serviceProvider.GetRequiredService<ILogger<StreamProcessingService>>(),
            batchSize: 2000,      // Large batches for better throughput
            batchIntervalMs: 1000, // 1 second batching interval
            maxChannelCapacity: 50000 // Large capacity for high-volume scenarios
        );
        
        // Create bulk processor
        _bulkProcessor = new BulkDataProcessor(
            serviceProvider,
            serviceProvider.GetRequiredService<ILogger<BulkDataProcessor>>(),
            serviceProvider.GetRequiredService<UNSInfra.Services.Events.IEventBus>()
        );
        
        // Wire up the pipeline
        _streamProcessor.BatchReady += OnBatchReady;
        
        // Statistics timer - log performance metrics every 30 seconds
        _statisticsTimer = new Timer(LogStatistics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Ingests a single data point into the high-performance pipeline.
    /// Non-blocking operation with back-pressure handling.
    /// </summary>
    public bool IngestDataPoint(DataPoint dataPoint)
    {
        if (_disposed) return false;
        
        try
        {
            var success = _streamProcessor.EnqueueDataPoint(dataPoint);
            if (success)
            {
                Interlocked.Increment(ref _totalDataPointsIngested);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting data point for topic {Topic}", dataPoint.Topic);
            return false;
        }
    }

    /// <summary>
    /// Ingests multiple data points in a batch for maximum throughput.
    /// </summary>
    public async Task<int> IngestDataPointsAsync(IEnumerable<DataPoint> dataPoints)
    {
        if (_disposed) return 0;
        
        var successCount = 0;
        var dataPointsList = dataPoints.ToList();
        
        try
        {
            // Process in parallel for maximum throughput
            var tasks = dataPointsList.Select(async dataPoint =>
            {
                var success = _streamProcessor.EnqueueDataPoint(dataPoint);
                if (success)
                {
                    Interlocked.Increment(ref _totalDataPointsIngested);
                    return 1;
                }
                return 0;
            });
            
            var results = await Task.WhenAll(tasks);
            successCount = results.Sum();
            
            _logger.LogDebug("Successfully ingested {SuccessCount}/{Total} data points", 
                successCount, dataPointsList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch ingesting {Count} data points", dataPointsList.Count);
        }
        
        return successCount;
    }

    /// <summary>
    /// Gets comprehensive pipeline statistics.
    /// </summary>
    public DataIngestionStatistics GetStatistics()
    {
        var streamStats = _streamProcessor.GetStatistics();
        var bulkStats = _bulkProcessor.GetStatistics();
        
        return new DataIngestionStatistics
        {
            TotalDataPointsIngested = _totalDataPointsIngested,
            StreamProcessingStats = streamStats,
            BulkProcessingStats = bulkStats,
            PipelineUptime = DateTime.UtcNow - _lastStatisticsLog,
            OverallThroughput = CalculateOverallThroughput()
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data ingestion pipeline started");
        
        try
        {
            // Keep the service running
            while (!stoppingToken.IsCancellationRequested && !_disposed)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Data ingestion pipeline stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in data ingestion pipeline");
        }
        finally
        {
            _logger.LogInformation("Data ingestion pipeline stopped");
        }
    }

    private async void OnBatchReady(object? sender, DataBatchEventArgs e)
    {
        try
        {
            _logger.LogDebug("Processing batch {BatchId} with {Count} data points", e.BatchId, e.BatchSize);
            
            await _bulkProcessor.ProcessBatchAsync(e.DataPoints);
            
            _logger.LogDebug("Successfully processed batch {BatchId}", e.BatchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch {BatchId} with {Count} data points", 
                e.BatchId, e.BatchSize);
        }
    }

    private void LogStatistics(object? state)
    {
        if (_disposed) return;
        
        try
        {
            var stats = GetStatistics();
            
            _logger.LogInformation(
                "Pipeline Stats - Total: {Total}, Stream Batches: {StreamBatches}, " +
                "Bulk Batches: {BulkBatches}, New Topics: {NewTopics}, Throughput: {Throughput:F2}/sec",
                stats.TotalDataPointsIngested,
                stats.StreamProcessingStats.TotalBatchesProcessed,
                stats.BulkProcessingStats.TotalBatchesProcessed,
                stats.BulkProcessingStats.NewTopicsDiscovered,
                stats.OverallThroughput);
                
            // Log performance warnings if needed
            if (stats.StreamProcessingStats.ChannelUtilization > 0.8)
            {
                _logger.LogWarning("High channel utilization ({Utilization:P}) - consider increasing capacity", 
                    stats.StreamProcessingStats.ChannelUtilization);
            }
            
            if (stats.StreamProcessingStats.TotalProcessingErrors > 0)
            {
                _logger.LogWarning("Processing errors detected: {ErrorCount}", 
                    stats.StreamProcessingStats.TotalProcessingErrors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error logging pipeline statistics");
        }
    }

    private double CalculateOverallThroughput()
    {
        var uptime = DateTime.UtcNow - _lastStatisticsLog;
        return uptime.TotalSeconds > 0 ? _totalDataPointsIngested / uptime.TotalSeconds : 0;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        try
        {
            // Dispose pipeline components
            _streamProcessor?.Dispose();
            _bulkProcessor?.Dispose();
            _statisticsTimer?.Dispose();
            
            _logger.LogInformation("Data ingestion pipeline disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing data ingestion pipeline");
        }
        finally
        {
            base.Dispose();
        }
    }
}

/// <summary>
/// Comprehensive statistics for the entire data ingestion pipeline.
/// </summary>
public class DataIngestionStatistics
{
    public long TotalDataPointsIngested { get; set; }
    public StreamProcessingStatistics StreamProcessingStats { get; set; } = new();
    public BulkProcessingStatistics BulkProcessingStats { get; set; } = new();
    public TimeSpan PipelineUptime { get; set; }
    public double OverallThroughput { get; set; }
}