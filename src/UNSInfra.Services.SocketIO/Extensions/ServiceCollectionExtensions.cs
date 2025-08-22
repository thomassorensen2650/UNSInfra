using Microsoft.Extensions.DependencyInjection;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.Services.SocketIO.Connections;

namespace UNSInfra.Services.SocketIO.Extensions;

/// <summary>
/// Extension methods for registering SocketIO connection components
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the production SocketIO connection implementation
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddProductionSocketIOConnection(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionDescriptor, SocketIOConnectionDescriptor>();
        return services;
    }
}