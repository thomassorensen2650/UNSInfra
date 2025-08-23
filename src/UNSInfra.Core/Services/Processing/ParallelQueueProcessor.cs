using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Services.Events;

namespace UNSInfra.Core.Services.Processing;

/// <summary>
/// High-performance parallel queue processor with multiple processing lanes,
/// priority queues, and adaptive load balancing.
/// </summary>
public class ParallelQueueProcessor<T> : BackgroundService where T : class
{
    private readonly ILogger<ParallelQueueProcessor<T>> _logger;
    private readonly Func<T, CancellationToken, Task> _processor;
    private readonly ParallelProcessorOptions _options;
    
    // Multi-lane processing channels
    private readonly Channel<T>[] _processingLanes;
    private readonly Task[] _processingTasks;
    private readonly SemaphoreSlim[] _laneSemaphores;
    
    // Priority queue for high-priority items
    private readonly Channel<T> _priorityQueue;
    private Task _priorityProcessingTask = Task.CompletedTask;
    
    // Load balancing and statistics
    private readonly long[] _laneWorkloads;
    private readonly long[] _laneProcessedCounts;
    private readonly Timer _statisticsTimer;
    private readonly Timer _loadBalancingTimer;
    
    // Performance metrics
    private long _totalProcessed;
    private long _totalErrors;
    private long _queuedItems;
    private DateTime _lastStatisticsLog = DateTime.UtcNow;

    public ParallelQueueProcessor(
        Func<T, CancellationToken, Task> processor,
        ParallelProcessorOptions? options = null,
        ILogger<ParallelQueueProcessor<T>>? logger = null)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _options = options ?? new ParallelProcessorOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ParallelQueueProcessor<T>>.Instance;
        
        // Initialize processing lanes
        var channelOptions = new BoundedChannelOptions(_options.LaneCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        
        _processingLanes = new Channel<T>[_options.ProcessingLanes];
        _processingTasks = new Task[_options.ProcessingLanes];
        _laneSemaphores = new SemaphoreSlim[_options.ProcessingLanes];
        _laneWorkloads = new long[_options.ProcessingLanes];
        _laneProcessedCounts = new long[_options.ProcessingLanes];
        
        for (int i = 0; i < _options.ProcessingLanes; i++)
        {
            _processingLanes[i] = Channel.CreateBounded<T>(channelOptions);
            _laneSemaphores[i] = new SemaphoreSlim(_options.MaxConcurrentPerLane, _options.MaxConcurrentPerLane);
        }
        
        // Initialize priority queue
        _priorityQueue = Channel.CreateBounded<T>(channelOptions);
        
        // Initialize timers
        _statisticsTimer = new Timer(LogStatistics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        _loadBalancingTimer = new Timer(RebalanceWorkload, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Enqueue item for processing with automatic load balancing
    /// </summary>
    public async Task EnqueueAsync(T item, bool highPriority = false, CancellationToken cancellationToken = default)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        
        Interlocked.Increment(ref _queuedItems);
        
        if (highPriority)
        {
            await _priorityQueue.Writer.WriteAsync(item, cancellationToken);
            _logger.LogTrace("Enqueued high-priority item");
        }
        else
        {
            // Find the lane with the lowest workload
            var selectedLane = GetOptimalLane();
            await _processingLanes[selectedLane].Writer.WriteAsync(item, cancellationToken);
            Interlocked.Increment(ref _laneWorkloads[selectedLane]);
            
            _logger.LogTrace("Enqueued item to lane {Lane}", selectedLane);
        }
    }

    /// <summary>
    /// Enqueue multiple items efficiently
    /// </summary>
    public async Task EnqueueBatchAsync(IEnumerable<T> items, bool highPriority = false, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return;
        
        if (highPriority)
        {
            foreach (var item in itemList)
            {
                await _priorityQueue.Writer.WriteAsync(item, cancellationToken);
            }
            _logger.LogDebug("Enqueued {Count} high-priority items", itemList.Count);
        }
        else
        {
            // Distribute items across lanes for optimal load balancing
            for (int i = 0; i < itemList.Count; i++)
            {
                var laneIndex = i % _options.ProcessingLanes;
                await _processingLanes[laneIndex].Writer.WriteAsync(itemList[i], cancellationToken);
                Interlocked.Increment(ref _laneWorkloads[laneIndex]);
            }
            _logger.LogDebug("Distributed {Count} items across {Lanes} lanes", itemList.Count, _options.ProcessingLanes);
        }
        
        Interlocked.Add(ref _queuedItems, itemList.Count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting parallel queue processor with {Lanes} lanes, {MaxConcurrent} max concurrent per lane",
            _options.ProcessingLanes, _options.MaxConcurrentPerLane);
        
        // Start priority processing task
        _priorityProcessingTask = ProcessPriorityQueueAsync(stoppingToken);
        
        // Start processing tasks for each lane
        for (int i = 0; i < _options.ProcessingLanes; i++)
        {
            var laneIndex = i;
            _processingTasks[i] = ProcessLaneAsync(laneIndex, stoppingToken);
        }
        
        // Wait for all processing tasks to complete
        var allTasks = new List<Task> { _priorityProcessingTask };
        allTasks.AddRange(_processingTasks);
        
        await Task.WhenAll(allTasks);
        
        _logger.LogInformation("Parallel queue processor stopped");
    }

    /// <summary>
    /// Process priority queue with higher concurrency
    /// </summary>
    private async Task ProcessPriorityQueueAsync(CancellationToken cancellationToken)
    {
        var prioritySemaphore = new SemaphoreSlim(_options.MaxConcurrentPerLane * 2, _options.MaxConcurrentPerLane * 2);
        var tasks = new List<Task>();
        
        await foreach (var item in _priorityQueue.Reader.ReadAllAsync(cancellationToken))
        {
            await prioritySemaphore.WaitAsync(cancellationToken);
            
            var task = ProcessItemAsync(item, prioritySemaphore, -1, cancellationToken);
            tasks.Add(task);
            
            // Clean up completed tasks to prevent memory buildup
            tasks.RemoveAll(t => t.IsCompleted);
        }
        
        // Wait for remaining tasks to complete
        await Task.WhenAll(tasks);
        prioritySemaphore.Dispose();
    }

    /// <summary>
    /// Process items in a specific lane
    /// </summary>
    private async Task ProcessLaneAsync(int laneIndex, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        
        await foreach (var item in _processingLanes[laneIndex].Reader.ReadAllAsync(cancellationToken))
        {
            await _laneSemaphores[laneIndex].WaitAsync(cancellationToken);
            
            var task = ProcessItemAsync(item, _laneSemaphores[laneIndex], laneIndex, cancellationToken);
            tasks.Add(task);
            
            // Clean up completed tasks
            tasks.RemoveAll(t => t.IsCompleted);
        }
        
        // Wait for remaining tasks in this lane to complete
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Process individual item with error handling and metrics
    /// </summary>
    private async Task ProcessItemAsync(T item, SemaphoreSlim semaphore, int laneIndex, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            await _processor(item, cancellationToken);
            
            Interlocked.Increment(ref _totalProcessed);
            if (laneIndex >= 0)
            {
                Interlocked.Increment(ref _laneProcessedCounts[laneIndex]);
                Interlocked.Decrement(ref _laneWorkloads[laneIndex]);
            }
            Interlocked.Decrement(ref _queuedItems);
            
            _logger.LogTrace("Processed item in {ElapsedMs}ms on lane {Lane}", 
                stopwatch.ElapsedMilliseconds, laneIndex >= 0 ? laneIndex : "priority");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalErrors);
            if (laneIndex >= 0)
            {
                Interlocked.Decrement(ref _laneWorkloads[laneIndex]);
            }
            Interlocked.Decrement(ref _queuedItems);
            
            _logger.LogError(ex, "Error processing item on lane {Lane} after {ElapsedMs}ms", 
                laneIndex >= 0 ? laneIndex : "priority", stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Get optimal lane based on current workloads
    /// </summary>
    private int GetOptimalLane()
    {
        var minWorkload = _laneWorkloads[0];
        var optimalLane = 0;
        
        for (int i = 1; i < _options.ProcessingLanes; i++)
        {
            if (_laneWorkloads[i] < minWorkload)
            {
                minWorkload = _laneWorkloads[i];
                optimalLane = i;
            }
        }
        
        return optimalLane;
    }

    /// <summary>
    /// Periodic load balancing and workload rebalancing
    /// </summary>
    private void RebalanceWorkload(object? state)
    {
        try
        {
            // Find lanes with significant workload imbalance
            var maxWorkload = _laneWorkloads.Max();
            var minWorkload = _laneWorkloads.Min();
            var imbalanceThreshold = Math.Max(10, maxWorkload / 4); // 25% imbalance threshold
            
            if (maxWorkload - minWorkload > imbalanceThreshold)
            {
                _logger.LogDebug("Workload imbalance detected: Max={Max}, Min={Min}, rebalancing may be needed", 
                    maxWorkload, minWorkload);
                
                // In a production system, you might implement workload redistribution here
                // For now, we just log the imbalance for monitoring
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during workload rebalancing");
        }
    }

    /// <summary>
    /// Log processing statistics
    /// </summary>
    private void LogStatistics(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastStatisticsLog;
            var totalProcessed = _totalProcessed;
            var totalErrors = _totalErrors;
            var queuedItems = _queuedItems;
            
            var throughput = elapsed.TotalSeconds > 0 ? totalProcessed / elapsed.TotalSeconds : 0;
            var errorRate = totalProcessed > 0 ? (double)totalErrors / (totalProcessed + totalErrors) : 0;
            
            _logger.LogInformation("Queue Stats - Processed: {Processed}, Queued: {Queued}, " +
                "Throughput: {Throughput:F1}/sec, Error rate: {ErrorRate:P2}",
                totalProcessed, queuedItems, throughput, errorRate);
            
            // Log per-lane statistics
            for (int i = 0; i < _options.ProcessingLanes; i++)
            {
                _logger.LogDebug("Lane {Lane} - Workload: {Workload}, Processed: {Processed}",
                    i, _laneWorkloads[i], _laneProcessedCounts[i]);
            }
            
            _lastStatisticsLog = now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging statistics");
        }
    }

    /// <summary>
    /// Get current processing statistics
    /// </summary>
    public ProcessingStatistics GetStatistics()
    {
        return new ProcessingStatistics
        {
            TotalProcessed = _totalProcessed,
            TotalErrors = _totalErrors,
            QueuedItems = _queuedItems,
            ProcessingLanes = _options.ProcessingLanes,
            LaneWorkloads = _laneWorkloads.ToArray(),
            LaneProcessedCounts = _laneProcessedCounts.ToArray()
        };
    }

    public override void Dispose()
    {
        _statisticsTimer?.Dispose();
        _loadBalancingTimer?.Dispose();
        
        // Close all channels
        for (int i = 0; i < _processingLanes.Length; i++)
        {
            _processingLanes[i].Writer.TryComplete();
            _laneSemaphores[i].Dispose();
        }
        _priorityQueue.Writer.TryComplete();
        
        base.Dispose();
    }
}

/// <summary>
/// Configuration options for parallel queue processor
/// </summary>
public class ParallelProcessorOptions
{
    /// <summary>
    /// Number of parallel processing lanes (default: CPU count)
    /// </summary>
    public int ProcessingLanes { get; set; } = Environment.ProcessorCount;
    
    /// <summary>
    /// Maximum concurrent items per lane (default: 4)
    /// </summary>
    public int MaxConcurrentPerLane { get; set; } = 4;
    
    /// <summary>
    /// Capacity of each processing lane queue (default: 1000)
    /// </summary>
    public int LaneCapacity { get; set; } = 1000;
    
    /// <summary>
    /// Enable adaptive load balancing (default: true)
    /// </summary>
    public bool EnableLoadBalancing { get; set; } = true;
    
    /// <summary>
    /// Statistics logging interval in seconds (default: 30)
    /// </summary>
    public int StatisticsIntervalSeconds { get; set; } = 30;
}

/// <summary>
/// Processing statistics for monitoring
/// </summary>
public class ProcessingStatistics
{
    public long TotalProcessed { get; set; }
    public long TotalErrors { get; set; }
    public long QueuedItems { get; set; }
    public int ProcessingLanes { get; set; }
    public long[] LaneWorkloads { get; set; } = Array.Empty<long>();
    public long[] LaneProcessedCounts { get; set; } = Array.Empty<long>();
    
    public double ErrorRate => TotalProcessed + TotalErrors > 0 ? 
        (double)TotalErrors / (TotalProcessed + TotalErrors) : 0;
    
    public double AverageWorkloadPerLane => ProcessingLanes > 0 ? 
        LaneWorkloads.Average() : 0;
}