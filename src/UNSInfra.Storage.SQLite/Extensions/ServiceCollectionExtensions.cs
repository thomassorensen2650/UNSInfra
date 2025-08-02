using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UNSInfra.Core.Configuration;
using UNSInfra.Core.Repositories;
using UNSInfra.Configuration;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.InMemory;
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
        services.AddScoped<INSTreeInstanceRepository, SQLiteNSTreeInstanceRepository>();
        services.AddScoped<IDataIngestionConfigurationRepository, SQLiteDataIngestionConfigurationRepository>();

        // Add storage services - Realtime storage as singleton for in-memory cache, Historical as scoped
        services.AddSingleton<IRealtimeStorage, SQLiteRealtimeStorage>();
        services.AddScoped<IHistoricalStorage, SQLiteHistoricalStorage>();

        return services;
    }

    /// <summary>
    /// Adds configurable storage services based on appsettings.json configuration.
    /// IRealtimeStorage is always InMemory for performance, but IHistoricalStorage uses the configured provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration provider.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddConfigurableStorage(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind storage configuration
        var storageConfig = new StorageConfiguration();
        configuration.GetSection("Storage").Bind(storageConfig);
        services.Configure<StorageConfiguration>(options => configuration.GetSection("Storage").Bind(options));

        // Bind historical storage configuration
        var historicalStorageConfig = new HistoricalStorageConfiguration();
        configuration.GetSection("HistoricalStorage").Bind(historicalStorageConfig);
        services.Configure<HistoricalStorageConfiguration>(options => configuration.GetSection("HistoricalStorage").Bind(options));

        // Always use InMemory for realtime storage (performance requirement)
        services.AddSingleton<IRealtimeStorage, InMemoryRealtimeStorage>();

        // Configure repositories and database context based on main storage configuration
        switch (storageConfig.Provider.ToUpperInvariant())
        {
            case "SQLITE":
                services.AddSQLiteRepositoriesAndContext(storageConfig);
                break;
            case "INMEMORY":
                // Add in-memory repository implementations
                services.AddScoped<IHierarchyConfigurationRepository, InMemoryHierarchyConfigurationRepository>();
                services.AddScoped<ITopicConfigurationRepository, InMemoryTopicConfigurationRepository>();
                services.AddScoped<ISchemaRepository, InMemorySchemaRepository>();
                services.AddScoped<IDataIngestionConfigurationRepository, InMemoryDataIngestionConfigurationRepository>();
                // Add SQLite repositories for namespace and NSTree only (these don't have InMemory implementations yet)
                services.AddSQLiteDbContextOnly(storageConfig);
                services.AddScoped<INamespaceConfigurationRepository, SQLiteNamespaceConfigurationRepository>();
                services.AddScoped<INSTreeInstanceRepository, SQLiteNSTreeInstanceRepository>();
                break;
            default:
                throw new InvalidOperationException($"Unsupported storage provider: {storageConfig.Provider}. Supported providers: SQLite, InMemory");
        }

        // Configure historical storage based on the HistoricalStorage configuration
        if (!historicalStorageConfig.Enabled)
        {
            // If historical storage is disabled, use a no-op implementation
            services.AddSingleton<IHistoricalStorage, NoOpHistoricalStorage>();
        }
        else
        {
            switch (historicalStorageConfig.StorageType)
            {
                case HistoricalStorageType.SQLite:
                    // Only add historical storage, repositories are already registered above
                    services.AddScoped<IHistoricalStorage, SQLiteHistoricalStorage>();
                    break;
                case HistoricalStorageType.InMemory:
                    services.AddSingleton<IHistoricalStorage, InMemoryHistoricalStorage>();
                    break;
                case HistoricalStorageType.None:
                    services.AddSingleton<IHistoricalStorage, NoOpHistoricalStorage>();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported historical storage type: {historicalStorageConfig.StorageType}. Supported types: SQLite, InMemory, None");
            }
        }

        return services;
    }

    /// <summary>
    /// Adds SQLite database context only (without repositories).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storageConfig">The storage configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    private static IServiceCollection AddSQLiteDbContextOnly(this IServiceCollection services, StorageConfiguration storageConfig)
    {
        var connectionString = storageConfig.ConnectionString;
        var commandTimeout = storageConfig.CommandTimeoutSeconds;

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
                sqliteOptions.CommandTimeout(commandTimeout);
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
                sqliteOptions.CommandTimeout(commandTimeout);
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

        return services;
    }

    /// <summary>
    /// Adds SQLite repositories and database context.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storageConfig">The storage configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    private static IServiceCollection AddSQLiteRepositoriesAndContext(this IServiceCollection services, StorageConfiguration storageConfig)
    {
        var connectionString = storageConfig.ConnectionString;
        var commandTimeout = storageConfig.CommandTimeoutSeconds;

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
                sqliteOptions.CommandTimeout(commandTimeout);
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
                sqliteOptions.CommandTimeout(commandTimeout);
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
        services.AddScoped<INSTreeInstanceRepository, SQLiteNSTreeInstanceRepository>();
        services.AddScoped<IDataIngestionConfigurationRepository, SQLiteDataIngestionConfigurationRepository>();

        return services;
    }

    /// <summary>
    /// Adds SQLite storage specifically for historical data only.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storageConfig">The storage configuration.</param>
    /// <param name="historicalStorageConfig">The historical storage configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    private static IServiceCollection AddSQLiteHistoricalStorage(this IServiceCollection services, StorageConfiguration storageConfig, HistoricalStorageConfiguration historicalStorageConfig)
    {
        // Use historical storage connection string if provided, otherwise fallback to storage config
        var connectionString = !string.IsNullOrEmpty(historicalStorageConfig.ConnectionString) 
            ? historicalStorageConfig.ConnectionString 
            : storageConfig.ConnectionString;
        var commandTimeout = storageConfig.CommandTimeoutSeconds;

        // Add DbContextFactory for thread-safe context creation
        services.AddDbContextFactory<UNSInfraDbContext>(options =>
        {
            string finalConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                // Use SQLite with a file-based database
                var dbPath = !string.IsNullOrEmpty(historicalStorageConfig.SQLite.DatabasePath)
                    ? historicalStorageConfig.SQLite.DatabasePath
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                        "UNSInfra", "historical-data.db");
                
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
                sqliteOptions.CommandTimeout(commandTimeout);
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
                sqliteOptions.CommandTimeout(commandTimeout);
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
        services.AddScoped<INSTreeInstanceRepository, SQLiteNSTreeInstanceRepository>();
        services.AddScoped<IDataIngestionConfigurationRepository, SQLiteDataIngestionConfigurationRepository>();

        // Add historical storage as scoped
        services.AddScoped<IHistoricalStorage, SQLiteHistoricalStorage>();

        return services;
    }

    /// <summary>
    /// Initializes the configured storage provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="configuration">The configuration provider.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task InitializeConfigurableStorageAsync(this IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var storageConfig = new StorageConfiguration();
        configuration.GetSection("Storage").Bind(storageConfig);

        switch (storageConfig.Provider.ToUpperInvariant())
        {
            case "SQLITE":
                await serviceProvider.InitializeSQLiteDatabaseAsync(storageConfig);
                break;
            case "INMEMORY":
                // No initialization needed for in-memory storage
                break;
            default:
                throw new InvalidOperationException($"Unsupported storage provider: {storageConfig.Provider}");
        }
    }

    /// <summary>
    /// Initializes SQLite database with configuration options.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="storageConfig">The storage configuration.</param>
    /// <returns>A task representing the async operation.</returns>
    private static async Task InitializeSQLiteDatabaseAsync(this IServiceProvider serviceProvider, StorageConfiguration storageConfig)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UNSInfraDbContext>();
        
        // Create database if it doesn't exist
        await context.Database.EnsureCreatedAsync();
        
        // Apply configuration-based SQLite settings
        try
        {
            if (storageConfig.EnableWalMode)
            {
                await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            }
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size=" + storageConfig.CacheSize + ";");
            await context.Database.ExecuteSqlRawAsync("PRAGMA temp_store=memory;");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not set SQLite PRAGMA settings: {ex.Message}");
        }
        
        // Initialize default hierarchy configuration
        var hierarchyRepo = scope.ServiceProvider.GetRequiredService<IHierarchyConfigurationRepository>();
        await hierarchyRepo.EnsureDefaultConfigurationAsync();
        
        // Initialize default namespace configurations
        var namespaceRepo = scope.ServiceProvider.GetRequiredService<INamespaceConfigurationRepository>();
        await namespaceRepo.EnsureDefaultConfigurationAsync();
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
        
        // Initialize default namespace configurations
        var namespaceRepo = scope.ServiceProvider.GetRequiredService<INamespaceConfigurationRepository>();
        await namespaceRepo.EnsureDefaultConfigurationAsync();
    }

}