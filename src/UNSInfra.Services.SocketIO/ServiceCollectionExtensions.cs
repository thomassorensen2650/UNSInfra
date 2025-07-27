using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.SocketIO.Configuration;

namespace UNSInfra.Services.SocketIO;

/// <summary>
/// Extension methods for registering SocketIO services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SocketIO data service with configuration from appsettings.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configuration">The configuration instance to bind settings from</param>
    /// <param name="configurationSection">The configuration section name (default: "SocketIO")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSocketIODataService(
        this IServiceCollection services,
        IConfiguration configuration,
        string configurationSection = "SocketIO")
    {
        // Configure SocketIO settings
        services.Configure<SocketIOConfiguration>(options => 
            configuration.GetSection(configurationSection).Bind(options));

        // Register the SocketIO data service
        services.AddSingleton<SocketIODataService>();
        
        // Also register as IDataIngestionService for generic handling
        services.AddSingleton<IDataIngestionService>(provider => provider.GetRequiredService<SocketIODataService>());

        return services;
    }

    /// <summary>
    /// Adds the SocketIO data service with explicit configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configureOptions">Action to configure SocketIO options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSocketIODataService(
        this IServiceCollection services,
        Action<SocketIOConfiguration> configureOptions)
    {
        // Configure SocketIO settings
        services.Configure<SocketIOConfiguration>(configureOptions);

        // Register the SocketIO data service
        services.AddSingleton<SocketIODataService>();
        
        // Also register as IDataIngestionService for generic handling
        services.AddSingleton<IDataIngestionService>(provider => provider.GetRequiredService<SocketIODataService>());

        return services;
    }

    /// <summary>
    /// Adds the SocketIO data service with default configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSocketIODataService(
        this IServiceCollection services)
    {
        // Register the SocketIO data service with default configuration
        services.AddSingleton<SocketIODataService>();

        return services;
    }
}