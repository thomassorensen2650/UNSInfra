using Microsoft.Extensions.DependencyInjection;
using UNSInfra.Abstractions;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Services;
using UNSInfra.Core.Repositories;
using UNSInfra.Services;

namespace UNSInfra.Extensions;

/// <summary>
/// Extension methods for registering connection services
/// </summary>
public static class ConnectionServiceCollectionExtensions
{
    /// <summary>
    /// Adds connection services to the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddConnectionServices(this IServiceCollection services)
    {
        // Register connection configuration repository
        // Note: The repository is registered by AddConfigurableStorage() based on storage provider config
        // Don't register here to avoid conflicts with SQLite registration

        // Register core connection services
        services.AddSingleton<IConnectionRegistry, ConnectionRegistry>();
        
        // Register ConnectionManager as a singleton that serves both IConnectionManager and IHostedService
        services.AddSingleton<ConnectionManager>();
        services.AddSingleton<IConnectionManager>(provider => provider.GetRequiredService<ConnectionManager>());
        services.AddHostedService<ConnectionManager>(provider => 
            provider.GetRequiredService<ConnectionManager>());

        return services;
    }

    /// <summary>
    /// Registers all connection descriptors with the connection registry
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    public static void RegisterConnectionTypes(this IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<IConnectionRegistry>();
        var descriptors = serviceProvider.GetServices<IConnectionDescriptor>();

        foreach (var descriptor in descriptors)
        {
            registry.RegisterConnection(descriptor);
        }
    }
}