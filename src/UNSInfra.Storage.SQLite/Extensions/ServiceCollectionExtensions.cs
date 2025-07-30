using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.SQLite.Repositories;
using UNSInfra.Storage.SQLite.Storage;

namespace UNSInfra.Storage.SQLite.Extensions;

/// <summary>
/// Extension methods for registering SQLite storage services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQLite storage services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQLite connection string. If null, uses in-memory database.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddSQLiteStorage(this IServiceCollection services, string? connectionString = null)
    {
        // Add DbContextFactory for thread-safe context creation
        services.AddDbContextFactory<UNSInfraDbContext>(options =>
        {
            string finalConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                // Use SQLite with a file-based database
                var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "UNSInfra", "unsinfra.db");
                
                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
                
                // Use optimized settings for better concurrency and reduced locking
                finalConnectionString = $"Data Source={dbPath};Cache=Shared;Pooling=True;";
            }
            else
            {
                finalConnectionString = connectionString;
            }
            
            options.UseSqlite(finalConnectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
            
            // Enable sensitive data logging in development
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
            
            // Reduce EF Core logging verbosity
            options.ConfigureWarnings(warnings => warnings.Log(
                (Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted, Microsoft.Extensions.Logging.LogLevel.Debug),
                (Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuting, Microsoft.Extensions.Logging.LogLevel.Debug),
                (Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandCreated, Microsoft.Extensions.Logging.LogLevel.Debug),
                (Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandCreating, Microsoft.Extensions.Logging.LogLevel.Debug),
                (Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.QueryExecutionPlanned, Microsoft.Extensions.Logging.LogLevel.Debug)
            ));
        });

        // Also add regular DbContext for non-concurrent operations (like initialization)
        services.AddDbContext<UNSInfraDbContext>(options =>
        {
            string finalConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "UNSInfra", "unsinfra.db");
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
                finalConnectionString = $"Data Source={dbPath};Cache=Shared;Pooling=True;";
            }
            else
            {
                finalConnectionString = connectionString;
            }
            
            options.UseSqlite(finalConnectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
            
            // Reduce EF Core logging verbosity
            options.ConfigureWarnings(warnings => warnings.Log(
                (Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted, Microsoft.Extensions.Logging.LogLevel.Debug),
                (Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuting, Microsoft.Extensions.Logging.LogLevel.Debug),
                (Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandCreated, Microsoft.Extensions.Logging.LogLevel.Debug),
                (Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandCreating, Microsoft.Extensions.Logging.LogLevel.Debug),
                (Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.QueryExecutionPlanned, Microsoft.Extensions.Logging.LogLevel.Debug)
            ));
        });

        // Add repositories
        services.AddScoped<IHierarchyConfigurationRepository, SQLiteHierarchyConfigurationRepository>();
        services.AddScoped<ITopicConfigurationRepository, SQLiteTopicConfigurationRepository>();
        services.AddScoped<ISchemaRepository, SQLiteSchemaRepository>();
        services.AddScoped<INamespaceConfigurationRepository, SQLiteNamespaceConfigurationRepository>();

        // Add storage services - Realtime storage as singleton for in-memory cache, Historical as scoped
        services.AddSingleton<IRealtimeStorage, SQLiteRealtimeStorage>();
        services.AddScoped<IHistoricalStorage, SQLiteHistoricalStorage>();

        return services;
    }

    /// <summary>
    /// Ensures the SQLite database is created and initialized.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task InitializeSQLiteDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UNSInfraDbContext>();
        
        // Create database if it doesn't exist
        await context.Database.EnsureCreatedAsync();
        
        // Enable WAL mode for better concurrency
        try
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size=1000;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA temp_store=memory;");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not set SQLite PRAGMA settings: {ex.Message}");
        }
        
        // Initialize default hierarchy configuration
        var hierarchyRepo = scope.ServiceProvider.GetRequiredService<IHierarchyConfigurationRepository>();
        await hierarchyRepo.EnsureDefaultConfigurationAsync();
    }

}