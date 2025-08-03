using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Storage.SQLite;

namespace UNSInfra.Storage.SQLite.Services;

/// <summary>
/// Service to perform database health checks and diagnostics.
/// </summary>
public class DatabaseHealthCheckService
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;
    private readonly IDataIngestionConfigurationRepository _repository;
    private readonly ILogger<DatabaseHealthCheckService> _logger;

    public DatabaseHealthCheckService(
        IDbContextFactory<UNSInfraDbContext> contextFactory,
        IDataIngestionConfigurationRepository repository,
        ILogger<DatabaseHealthCheckService> logger)
    {
        _contextFactory = contextFactory;
        _repository = repository;
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
            var testId = Guid.NewGuid().ToString();
            
            // Create a test configuration
            var testConfig = new UNSInfra.Services.SocketIO.Configuration.SocketIODataIngestionConfiguration
            {
                Id = testId,
                Name = "Health Check Test Config",
                Description = "Temporary config for health check",
                Enabled = false,
                CreatedBy = "HealthCheck",
                ServerUrl = "https://test.example.com",
                EventNames = new[] { "test" },
                BaseTopicPath = "test"
            };

            // Save it
            await _repository.SaveConfigurationAsync(testConfig);
            _logger.LogInformation("✅ Write test passed - saved config {TestId}", testId);

            // Read it back
            var retrievedConfig = await _repository.GetConfigurationAsync(testId);
            if (retrievedConfig != null)
            {
                _logger.LogInformation("✅ Read test passed - retrieved config {TestId}", testId);
            }
            else
            {
                _logger.LogError("❌ Read test failed - could not retrieve config {TestId}", testId);
                throw new InvalidOperationException("Read test failed");
            }

            // Clean up
            await _repository.DeleteConfigurationAsync(testId);
            _logger.LogInformation("✅ Delete test passed - cleaned up config {TestId}", testId);
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
            var allConfigs = await _repository.GetAllConfigurationsAsync();
            _logger.LogInformation("📊 Current configuration count: {ConfigCount}", allConfigs.Count);
            
            foreach (var config in allConfigs)
            {
                _logger.LogInformation("  - {ConfigName} ({ServiceType}) - Enabled: {Enabled}", 
                    config.Name, config.ServiceType, config.Enabled);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Configuration count test failed");
            throw;
        }
    }
}