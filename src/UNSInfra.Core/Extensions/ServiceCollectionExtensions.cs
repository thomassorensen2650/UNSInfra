using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UNSInfra.Core.Configuration;
using UNSInfra.Core.Repositories;
using UNSInfra.Core.Services;
using UNSInfra.Services.AutoMapping;

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
        // Register the data ingestion service manager
        services.AddSingleton<IDataIngestionServiceManager, DataIngestionServiceManager>();
        
        // Note: IDataIngestionConfigurationRepository will be registered by the storage layer
        // Don't register in-memory repository here as it would override SQLite implementation
        
        // Register the background service for managing service descriptors
        services.AddHostedService<DataIngestionServiceRegistrationService>();
        
        // Health check service will be registered by SQLite storage layer
        
        // Register auto topic mapping services
        services.AddAutoTopicMapping();
        
        return services;
    }

    /// <summary>
    /// Registers a data ingestion service descriptor type.
    /// </summary>
    /// <typeparam name="T">The service descriptor type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDataIngestionServiceDescriptor<T>(this IServiceCollection services)
        where T : class, IDataIngestionServiceDescriptor
    {
        services.AddTransient<T>();
        services.AddTransient<IDataIngestionServiceDescriptor>(provider => provider.GetRequiredService<T>());
        return services;
    }

    /// <summary>
    /// Adds auto topic mapping services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAutoTopicMapping(this IServiceCollection services)
    {
        services.AddScoped<IAutoTopicMapper, AutoTopicMapperService>();
        return services;
    }
}