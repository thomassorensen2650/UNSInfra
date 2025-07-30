using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.SQLite.Mappers;

namespace UNSInfra.Storage.SQLite.Storage;

/// <summary>
/// SQLite-based implementation of realtime storage with in-memory caching for performance.
/// Combines the persistence of SQLite with the speed of in-memory storage for latest values.
/// </summary>
public class SQLiteRealtimeStorage : IRealtimeStorage
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;
    private readonly ILogger<SQLiteRealtimeStorage> _logger;
    private readonly ConcurrentDictionary<string, DataPoint> _latestValues = new();

    /// <summary>
    /// Initializes a new instance of the SQLiteRealtimeStorage class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    /// <param name="logger">Logger instance.</param>
    public SQLiteRealtimeStorage(IDbContextFactory<UNSInfraDbContext> contextFactory, ILogger<SQLiteRealtimeStorage> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        
        // Load existing latest values from database on startup (now singleton, so only once)
        _ = Task.Run(LoadLatestValuesAsync);
    }

    /// <inheritdoc />
    public async Task StoreAsync(DataPoint dataPoint)
    {
        // Store in memory cache for fast retrieval
        _latestValues.AddOrUpdate(dataPoint.Topic, dataPoint, (key, oldValue) => 
            dataPoint.Timestamp > oldValue.Timestamp ? dataPoint : oldValue);

        // Also store in database for persistence with retry logic
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                
                // Remove old entry for this topic if it exists (keep only latest)
                var existingEntity = await context.DataPoints
                    .Where(dp => dp.Topic == dataPoint.Topic)
                    .OrderByDescending(dp => dp.Timestamp)
                    .FirstOrDefaultAsync();

                if (existingEntity != null)
                {
                    // Update existing entity instead of removing and adding
                    var entity = dataPoint.ToEntity();
                    entity.Id = existingEntity.Id; // Preserve the ID
                    context.Entry(existingEntity).CurrentValues.SetValues(entity);
                }
                else
                {
                    // Add new entry
                    var entity = dataPoint.ToEntity();
                    context.DataPoints.Add(entity);
                }
                
                await context.SaveChangesAsync();
                break; // Success, exit retry loop
            }
            catch (Exception ex) when (attempt < maxRetries && 
                (ex.Message.Contains("database is locked") || ex.Message.Contains("disposed") || ex.Message.Contains("second operation")))
            {
                // Retry with exponential backoff for database concurrency issues
                await Task.Delay(attempt * 50);
                _logger.LogDebug("Retry {Attempt} for storing realtime data (topic: {Topic}): {Message}", attempt, dataPoint.Topic, ex.Message);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - we don't want to break the real-time flow
                _logger.LogWarning(ex, "Error persisting realtime data to SQLite (topic: {Topic}): {Message}", dataPoint.Topic, ex.Message);
                break; // Don't retry for other types of errors
            }
        }
    }

    /// <inheritdoc />
    public Task<DataPoint?> GetLatestAsync(string topic)
    {
        _latestValues.TryGetValue(topic, out var dataPoint);
        return Task.FromResult(dataPoint);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DataPoint>> GetLatestByPathAsync(HierarchicalPath path)
    {
        var pathString = path.GetFullPath();
        
        // First try in-memory cache
        var memoryResults = _latestValues.Values
            .Where(dp => dp.Path.GetFullPath().StartsWith(pathString))
            .ToList();

        if (memoryResults.Any())
        {
            return memoryResults;
        }

        // Fallback to database if not in memory
        using var context = _contextFactory.CreateDbContext();
        var entities = await context.DataPoints
            .Where(dp => dp.PathValuesJson.Contains(pathString))
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string topic)
    {
        // Remove from memory cache
        _latestValues.TryRemove(topic, out _);

        // Remove from database
        using var context = _contextFactory.CreateDbContext();
        var entities = await context.DataPoints
            .Where(dp => dp.Topic == topic)
            .ToListAsync();

        if (entities.Any())
        {
            context.DataPoints.RemoveRange(entities);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Loads the latest values from the database into memory on startup.
    /// </summary>
    private async Task LoadLatestValuesAsync()
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            
            // Get the most recent data point for each topic
            var latestEntities = await context.DataPoints
                .GroupBy(dp => dp.Topic)
                .Select(g => g.OrderByDescending(dp => dp.Timestamp).First())
                .ToListAsync();

            foreach (var entity in latestEntities)
            {
                var dataPoint = entity.ToModel();
                _latestValues.TryAdd(dataPoint.Topic, dataPoint);
            }

            _logger.LogInformation("Loaded {Count} latest values from SQLite into memory cache", _latestValues.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading latest values from SQLite: {Message}", ex.Message);
        }
    }
}