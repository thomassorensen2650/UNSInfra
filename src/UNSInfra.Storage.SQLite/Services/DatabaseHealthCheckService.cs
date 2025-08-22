using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UNSInfra.Storage.SQLite;

namespace UNSInfra.Storage.SQLite.Services;

/// <summary>
/// Service to perform database health checks and diagnostics.
/// </summary>
public class DatabaseHealthCheckService
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;
    private readonly ILogger<DatabaseHealthCheckService> _logger;

    public DatabaseHealthCheckService(
        IDbContextFactory<UNSInfraDbContext> contextFactory,
        ILogger<DatabaseHealthCheckService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Performs comprehensive database health checks.
    /// </summary>
    public async Task PerformHealthCheckAsync()
    {
        try
        {
            _logger.LogInformation("Starting database health check...");

            // Test 1: Basic connectivity
            await TestDatabaseConnectivity();

            // Test 2: Table existence
            await TestTableExistence();

            // Test 3: Write/Read test
            await TestWriteReadCapability();

            // Test 4: Configuration count
            await TestConfigurationCount();

            _logger.LogInformation("Database health check completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            throw;
        }
    }

    private async Task TestDatabaseConnectivity()
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.Database.OpenConnectionAsync();
            _logger.LogInformation("✅ Database connectivity test passed");
            
            var connectionString = context.Database.GetConnectionString();
            _logger.LogInformation("Database connection string: {ConnectionString}", connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Database connectivity test failed");
            throw;
        }
    }

    private async Task TestTableExistence()
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var tableExists = await context.DataIngestionConfigurations.AnyAsync();
            _logger.LogInformation("✅ DataIngestionConfigurations table exists and is queryable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ DataIngestionConfigurations table test failed");
            throw;
        }
    }

    private async Task TestWriteReadCapability()
    {
        try
        {
            // Skip write/read test as data ingestion has moved to the ConnectionSDK system
            // The data ingestion configuration repository is now deprecated
            _logger.LogInformation("⚠️  Write/Read test skipped - data ingestion moved to ConnectionSDK system");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Write/Read capability test failed");
            throw;
        }
    }

    private async Task TestConfigurationCount()
    {
        try
        {
            // Skip configuration count test as data ingestion has moved to the ConnectionSDK system
            // The data ingestion configuration repository is now deprecated
            _logger.LogInformation("⚠️  Configuration count test skipped - data ingestion moved to ConnectionSDK system");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Configuration count test failed");
            throw;
        }
    }
}