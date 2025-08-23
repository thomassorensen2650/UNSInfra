using Microsoft.Extensions.DependencyInjection;
using UNSInfra.Services.AutoMapping;
using UNSInfra.Services.DataIngestion;
using UNSInfra.Core.Services.Caching;

namespace UNSInfra.Core.Extensions;

/// <summary>
/// Extension methods for registering UNS Infrastructure core services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core UNS Infrastructure services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddUNSInfrastructureCore(this IServiceCollection services)
    {
        // Old data ingestion services removed - moved to ConnectionSDK system
        
        // Register data storage service for persisting incoming data
        services.AddHostedService<DataStorageBackgroundService>();
        
        // Register multi-level caching system
        services.AddSingleton<MultiLevelCacheManager>();
        services.AddHostedService<MultiLevelCacheManager>(provider => provider.GetRequiredService<MultiLevelCacheManager>());
        
        // Register auto topic mapping services
        services.AddAutoTopicMapping();
        
        return services;
    }

    // Old data ingestion service descriptor registration removed - moved to ConnectionSDK system

    /// <summary>
    /// Adds the simplified, high-performance auto topic mapping service to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAutoTopicMapping(this IServiceCollection services)
    {
        // Add the simplified, high-performance auto-mapper
        services.AddSingleton<SimplifiedAutoMapperService>();
        
        // Add the background service for processing auto-mapping
        services.AddHostedService<SimplifiedAutoMappingBackgroundService>();
        
        return services;
    }
}