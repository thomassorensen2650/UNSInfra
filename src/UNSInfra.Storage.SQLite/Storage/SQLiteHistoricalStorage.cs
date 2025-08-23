using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.SQLite.Mappers;

namespace UNSInfra.Storage.SQLite.Storage;

/// <summary>
/// SQLite-based implementation of historical storage.
/// </summary>
public class SQLiteHistoricalStorage : IHistoricalStorage
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;
    private readonly ILogger<SQLiteHistoricalStorage> _logger;

    /// <summary>
    /// Initializes a new instance of the SQLiteHistoricalStorage class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    /// <param name="logger">Logger instance.</param>
    public SQLiteHistoricalStorage(IDbContextFactory<UNSInfraDbContext> contextFactory, ILogger<SQLiteHistoricalStorage> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StoreAsync(DataPoint dataPoint)
    {
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var entity = dataPoint.ToEntity();
                context.DataPoints.Add(entity);
                await context.SaveChangesAsync();
                break; // Success, exit retry loop
            }
            catch (Exception ex) when (attempt < maxRetries && 
                (ex.Message.Contains("database is locked") || ex.Message.Contains("disposed") || ex.Message.Contains("second operation")))
            {
                // Retry with exponential backoff for database concurrency issues
                await Task.Delay(attempt * 50);
                _logger.LogDebug("Retry {Attempt} for storing historical data (topic: {Topic}): {Message}", attempt, dataPoint.Topic, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error storing historical data (topic: {Topic}): {Message}", dataPoint.Topic, ex.Message);
                throw; // Re-throw for non-recoverable errors
            }
        }
    }

    /// <inheritdoc />
    public async Task StoreBulkAsync(IEnumerable<DataPoint> dataPoints)
    {
        var dataPointsList = dataPoints.ToList();
        if (dataPointsList.Count == 0) return;

        const int maxRetries = 3;
        const int batchSize = 1000; // Process in batches to avoid memory issues
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                
                // Process in batches for better performance
                for (int i = 0; i < dataPointsList.Count; i += batchSize)
                {
                    var batch = dataPointsList.Skip(i).Take(batchSize);
                    var entities = batch.Select(dp => dp.ToEntity()).ToList();
                    
                    context.DataPoints.AddRange(entities);
                }
                
                await context.SaveChangesAsync();
                
                _logger.LogDebug("Bulk stored {Count} data points to historical storage", dataPointsList.Count);
                break; // Success, exit retry loop
            }
            catch (Exception ex) when (attempt < maxRetries && 
                (ex.Message.Contains("database is locked") || ex.Message.Contains("disposed") || ex.Message.Contains("second operation")))
            {
                // Retry with exponential backoff for database concurrency issues
                await Task.Delay(attempt * 100);
                _logger.LogDebug("Retry {Attempt} for bulk storing historical data ({Count} points): {Message}", 
                    attempt, dataPointsList.Count, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk store {Count} data points to historical storage", dataPointsList.Count);
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DataPoint>> GetHistoryAsync(string topic, DateTime startTime, DateTime endTime)
    {
        using var context = _contextFactory.CreateDbContext();
        var entities = await context.DataPoints
            .Where(dp => dp.Topic == topic && 
                        dp.Timestamp >= startTime && 
                        dp.Timestamp <= endTime)
            .OrderBy(dp => dp.Timestamp)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DataPoint>> GetHistoryByPathAsync(HierarchicalPath path, DateTime startTime, DateTime endTime)
    {
        var pathString = path.GetFullPath();
        
        using var context = _contextFactory.CreateDbContext();
        var entities = await context.DataPoints
            .Where(dp => dp.PathValuesJson.Contains(pathString) && 
                        dp.Timestamp >= startTime && 
                        dp.Timestamp <= endTime)
            .OrderBy(dp => dp.Timestamp)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DataPoint>> GetLatestHistoryAsync(string topic, int count)
    {
        using var context = _contextFactory.CreateDbContext();
        var entities = await context.DataPoints
            .Where(dp => dp.Topic == topic)
            .OrderByDescending(dp => dp.Timestamp)
            .Take(count)
            .ToListAsync();

        return entities.Select(e => e.ToModel()).Reverse(); // Return in chronological order
    }

    /// <inheritdoc />
    public async Task<bool> DeleteHistoryAsync(string topic, DateTime? before = null)
    {
        using var context = _contextFactory.CreateDbContext();
        IQueryable<Entities.DataPointEntity> query = context.DataPoints
            .Where(dp => dp.Topic == topic);

        if (before.HasValue)
        {
            query = query.Where(dp => dp.Timestamp < before.Value);
        }

        var entities = await query.ToListAsync();

        if (entities.Any())
        {
            context.DataPoints.RemoveRange(entities);
            await context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<long> GetHistoryCountAsync(string topic)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.DataPoints
            .CountAsync(dp => dp.Topic == topic);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetOldestTimestampAsync(string topic)
    {
        using var context = _contextFactory.CreateDbContext();
        var oldestEntity = await context.DataPoints
            .Where(dp => dp.Topic == topic)
            .OrderBy(dp => dp.Timestamp)
            .FirstOrDefaultAsync();

        return oldestEntity?.Timestamp;
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLatestTimestampAsync(string topic)
    {
        using var context = _contextFactory.CreateDbContext();
        var latestEntity = await context.DataPoints
            .Where(dp => dp.Topic == topic)
            .OrderByDescending(dp => dp.Timestamp)
            .FirstOrDefaultAsync();

        return latestEntity?.Timestamp;
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(DateTime before)
    {
        using var context = _contextFactory.CreateDbContext();
        var entitiesToArchive = await context.DataPoints
            .Where(dp => dp.Timestamp < before)
            .ToListAsync();
            
        if (entitiesToArchive.Any())
        {
            // In a real implementation, you might move these to an archive table
            // For now, we'll just delete them
            context.DataPoints.RemoveRange(entitiesToArchive);
            await context.SaveChangesAsync();
        }
    }
}