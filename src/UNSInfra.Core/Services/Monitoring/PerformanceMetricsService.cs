using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Services.Events;

namespace UNSInfra.Core.Services.Monitoring;

/// <summary>
/// Comprehensive performance monitoring service that collects and analyzes
/// system metrics, performance counters, and application-specific metrics.
/// </summary>
public class PerformanceMetricsService : BackgroundService
{
    private readonly ILogger<PerformanceMetricsService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    
    // Performance counters
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly PerformanceCounter _diskCounter;
    
    // Application metrics
    private readonly ConcurrentDictionary<string, MetricValue> _applicationMetrics = new();
    private readonly ConcurrentDictionary<string, List<double>> _performanceSamples = new();
    private readonly ConcurrentQueue<PerformanceEvent> _performanceEvents = new();
    
    // Timers for different monitoring intervals
    private readonly Timer _systemMetricsTimer;
    private readonly Timer _applicationMetricsTimer;
    private readonly Timer _alertsTimer;
    private readonly Timer _cleanupTimer;
    
    // Thresholds for alerts
    private readonly AlertThresholds _alertThresholds;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public PerformanceMetricsService(
        ILogger<PerformanceMetricsService> logger,
        IServiceProvider serviceProvider,
        IEventBus eventBus)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        
        // Initialize performance counters
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        
        // Try to initialize disk counter (may not be available on all systems)
        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize disk performance counter");
            _diskCounter = null!;
        }
        
        // Initialize alert thresholds
        _alertThresholds = new AlertThresholds();
        
        // Initialize timers
        _systemMetricsTimer = new Timer(CollectSystemMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        _applicationMetricsTimer = new Timer(CollectApplicationMetrics, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        _alertsTimer = new Timer(CheckAlerts, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Performance metrics service started");
        
        // Subscribe to application events for metrics
        _eventBus.Subscribe<TopicDataUpdatedEvent>(OnTopicDataUpdated);
        _eventBus.Subscribe<TopicAddedEvent>(OnTopicAdded);
        
        // Log initial system information
        LogSystemInformation();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                
                // Process performance events
                await ProcessPerformanceEvents();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in performance metrics service execution");
            }
        }
        
        _logger.LogInformation("Performance metrics service stopped");
    }

    /// <summary>
    /// Record a performance metric
    /// </summary>
    public void RecordMetric(string name, double value, string? unit = null, Dictionary<string, object>? tags = null)
    {
        var metric = new MetricValue
        {
            Name = name,
            Value = value,
            Unit = unit ?? "count",
            Tags = tags ?? new Dictionary<string, object>(),
            Timestamp = DateTime.UtcNow
        };
        
        _applicationMetrics.AddOrUpdate(name, metric, (key, existing) => metric);
        
        // Add to performance samples for trend analysis
        _performanceSamples.AddOrUpdate(name, 
            new List<double> { value },
            (key, existing) => 
            {
                existing.Add(value);
                // Keep only last 1000 samples
                if (existing.Count > 1000)
                {
                    existing.RemoveRange(0, existing.Count - 1000);
                }
                return existing;
            });
        
        _logger.LogTrace("Recorded metric {Name}: {Value} {Unit}", name, value, metric.Unit);
    }

    /// <summary>
    /// Start a performance measurement
    /// </summary>
    public IDisposable StartMeasurement(string operationName, Dictionary<string, object>? tags = null)
    {
        return new PerformanceMeasurement(operationName, tags, this);
    }

    /// <summary>
    /// Get current performance metrics
    /// </summary>
    public PerformanceSnapshot GetPerformanceSnapshot()
    {
        var systemMetrics = GetCurrentSystemMetrics();
        var applicationMetrics = _applicationMetrics.Values.ToList();
        var uptime = DateTime.UtcNow - _startTime;
        
        return new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Uptime = uptime,
            SystemMetrics = systemMetrics,
            ApplicationMetrics = applicationMetrics,
            TrendData = GetTrendData()
        };
    }

    /// <summary>
    /// Get trend data for key metrics
    /// </summary>
    public Dictionary<string, TrendData> GetTrendData()
    {
        var trends = new Dictionary<string, TrendData>();
        
        foreach (var kvp in _performanceSamples)
        {
            var samples = kvp.Value.ToList();
            if (samples.Count > 1)
            {
                trends[kvp.Key] = new TrendData
                {
                    Average = samples.Average(),
                    Minimum = samples.Min(),
                    Maximum = samples.Max(),
                    SampleCount = samples.Count,
                    Trend = CalculateTrend(samples)
                };
            }
        }
        
        return trends;
    }

    private void CollectSystemMetrics(object? state)
    {
        try
        {
            // CPU usage
            var cpuUsage = _cpuCounter.NextValue();
            RecordMetric("system.cpu.usage", cpuUsage, "percent");
            
            // Memory usage
            var availableMemory = _memoryCounter.NextValue();
            var process = Process.GetCurrentProcess();
            var processMemory = process.WorkingSet64 / 1024.0 / 1024.0; // Convert to MB
            
            RecordMetric("system.memory.available", availableMemory, "MB");
            RecordMetric("system.memory.process", processMemory, "MB");
            
            // Disk usage
            if (_diskCounter != null)
            {
                var diskUsage = _diskCounter.NextValue();
                RecordMetric("system.disk.usage", diskUsage, "percent");
            }
            
            // Thread count
            RecordMetric("system.threads", process.Threads.Count, "count");
            
            // GC information
            RecordMetric("system.gc.gen0", GC.CollectionCount(0), "count");
            RecordMetric("system.gc.gen1", GC.CollectionCount(1), "count");
            RecordMetric("system.gc.gen2", GC.CollectionCount(2), "count");
            RecordMetric("system.gc.memory", GC.GetTotalMemory(false) / 1024.0 / 1024.0, "MB");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting system metrics");
        }
    }

    private async void CollectApplicationMetrics(object? state)
    {
        try
        {
            // Collect metrics from various services
            await CollectCacheMetrics();
            await CollectQueueMetrics();
            await CollectConnectionMetrics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting application metrics");
        }
    }

    private async Task CollectCacheMetrics()
    {
        try
        {
            // Get metrics from multi-level cache if available
            using var scope = _serviceProvider.CreateScope();
            var cacheManager = scope.ServiceProvider.GetService<Caching.MultiLevelCacheManager>();
            
            if (cacheManager != null)
            {
                // Cache metrics would be collected here if the cache manager exposed statistics
                RecordMetric("cache.status", 1, "status");
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not collect cache metrics");
        }
    }

    private async Task CollectQueueMetrics()
    {
        try
        {
            // Collect queue processing metrics
            var eventQueueSize = _performanceEvents.Count;
            RecordMetric("queue.events.size", eventQueueSize, "count");
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not collect queue metrics");
        }
    }

    private async Task CollectConnectionMetrics()
    {
        try
        {
            // Connection metrics would be collected here
            // This is a placeholder for future connection monitoring
            RecordMetric("connections.active", 0, "count");
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not collect connection metrics");
        }
    }

    private void CheckAlerts(object? state)
    {
        try
        {
            var snapshot = GetPerformanceSnapshot();
            
            // Check CPU usage alert
            if (snapshot.SystemMetrics.CpuUsage > _alertThresholds.CpuUsageThreshold)
            {
                LogAlert("High CPU Usage", $"CPU usage is {snapshot.SystemMetrics.CpuUsage:F1}%", AlertLevel.Warning);
            }
            
            // Check memory usage alert
            if (snapshot.SystemMetrics.MemoryUsage > _alertThresholds.MemoryUsageThreshold)
            {
                LogAlert("High Memory Usage", $"Process memory usage is {snapshot.SystemMetrics.MemoryUsage:F1} MB", AlertLevel.Warning);
            }
            
            // Check error rates from trends
            var trends = snapshot.TrendData;
            foreach (var trend in trends.Where(t => t.Key.Contains("error")))
            {
                if (trend.Value.Average > _alertThresholds.ErrorRateThreshold)
                {
                    LogAlert("High Error Rate", $"{trend.Key} error rate is {trend.Value.Average:F2}", AlertLevel.Error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alerts");
        }
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            // Clean up old performance events
            var cutoff = DateTime.UtcNow.AddHours(-1);
            var eventsToRemove = new List<PerformanceEvent>();
            
            while (_performanceEvents.TryPeek(out var evt) && evt.Timestamp < cutoff)
            {
                if (_performanceEvents.TryDequeue(out var removedEvent))
                {
                    eventsToRemove.Add(removedEvent);
                }
            }
            
            if (eventsToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} old performance events", eventsToRemove.Count);
            }
            
            // Clean up old metric samples to prevent memory growth
            foreach (var kvp in _performanceSamples.ToList())
            {
                if (kvp.Value.Count > 2000)
                {
                    kvp.Value.RemoveRange(0, kvp.Value.Count - 1000);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }

    private async Task ProcessPerformanceEvents()
    {
        var processedCount = 0;
        
        while (_performanceEvents.TryDequeue(out var evt) && processedCount < 100)
        {
            try
            {
                // Process performance event (analytics, alerting, etc.)
                await ProcessPerformanceEvent(evt);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing performance event");
            }
        }
        
        if (processedCount > 0)
        {
            _logger.LogTrace("Processed {Count} performance events", processedCount);
        }
    }

    private async Task ProcessPerformanceEvent(PerformanceEvent evt)
    {
        // Implementation for processing performance events
        // Could include anomaly detection, alerting, etc.
        await Task.CompletedTask;
    }

    private void LogSystemInformation()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            _logger.LogInformation("System Info - OS: {OS}, Processors: {Processors}, Process: {ProcessName}, PID: {PID}",
                Environment.OSVersion.VersionString,
                Environment.ProcessorCount,
                process.ProcessName,
                process.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not log system information");
        }
    }

    private SystemMetrics GetCurrentSystemMetrics()
    {
        var metrics = _applicationMetrics.Values
            .Where(m => m.Name.StartsWith("system."))
            .ToList();
            
        return new SystemMetrics
        {
            CpuUsage = metrics.FirstOrDefault(m => m.Name == "system.cpu.usage")?.Value ?? 0,
            MemoryUsage = metrics.FirstOrDefault(m => m.Name == "system.memory.process")?.Value ?? 0,
            ThreadCount = (int)(metrics.FirstOrDefault(m => m.Name == "system.threads")?.Value ?? 0),
            GCMemory = metrics.FirstOrDefault(m => m.Name == "system.gc.memory")?.Value ?? 0
        };
    }

    private double CalculateTrend(List<double> samples)
    {
        if (samples.Count < 2) return 0;
        
        // Simple linear trend calculation
        var n = samples.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;
        
        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += samples[i];
            sumXY += i * samples[i];
            sumX2 += i * i;
        }
        
        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return slope;
    }

    private void LogAlert(string title, string message, AlertLevel level)
    {
        var logLevel = level switch
        {
            AlertLevel.Info => LogLevel.Information,
            AlertLevel.Warning => LogLevel.Warning,
            AlertLevel.Error => LogLevel.Error,
            AlertLevel.Critical => LogLevel.Critical,
            _ => LogLevel.Information
        };
        
        _logger.Log(logLevel, "ALERT: {Title} - {Message}", title, message);
        
        // Could also send to external alerting system here
    }

    // Event handlers
    private Task OnTopicDataUpdated(TopicDataUpdatedEvent eventData)
    {
        RecordMetric("topics.data_updates", 1, "count");
        _performanceEvents.Enqueue(new PerformanceEvent
        {
            Type = "TopicDataUpdated",
            Timestamp = DateTime.UtcNow,
            Data = new Dictionary<string, object> { ["topic"] = eventData.Topic }
        });
        return Task.CompletedTask;
    }

    private Task OnTopicAdded(TopicAddedEvent eventData)
    {
        RecordMetric("topics.added", 1, "count");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _systemMetricsTimer?.Dispose();
        _applicationMetricsTimer?.Dispose();
        _alertsTimer?.Dispose();
        _cleanupTimer?.Dispose();
        
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
        _diskCounter?.Dispose();
        
        _eventBus.Unsubscribe<TopicDataUpdatedEvent>(OnTopicDataUpdated);
        _eventBus.Unsubscribe<TopicAddedEvent>(OnTopicAdded);
        
        base.Dispose();
    }
}

// Supporting classes and enums
public class MetricValue
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = "count";
    public Dictionary<string, object> Tags { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public TimeSpan Uptime { get; set; }
    public SystemMetrics SystemMetrics { get; set; } = new();
    public List<MetricValue> ApplicationMetrics { get; set; } = new();
    public Dictionary<string, TrendData> TrendData { get; set; } = new();
}

public class SystemMetrics
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public int ThreadCount { get; set; }
    public double GCMemory { get; set; }
}

public class TrendData
{
    public double Average { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public int SampleCount { get; set; }
    public double Trend { get; set; } // Slope of trend line
}

public class PerformanceEvent
{
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public class AlertThresholds
{
    public double CpuUsageThreshold { get; set; } = 80.0; // 80%
    public double MemoryUsageThreshold { get; set; } = 1024.0; // 1GB
    public double ErrorRateThreshold { get; set; } = 0.05; // 5%
}

public enum AlertLevel
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Performance measurement helper that automatically records duration
/// </summary>
public class PerformanceMeasurement : IDisposable
{
    private readonly string _operationName;
    private readonly Dictionary<string, object>? _tags;
    private readonly PerformanceMetricsService _metricsService;
    private readonly Stopwatch _stopwatch;

    public PerformanceMeasurement(string operationName, Dictionary<string, object>? tags, PerformanceMetricsService metricsService)
    {
        _operationName = operationName;
        _tags = tags;
        _metricsService = metricsService;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _metricsService.RecordMetric($"operation.{_operationName}.duration", 
            _stopwatch.ElapsedMilliseconds, "ms", _tags);
    }
}