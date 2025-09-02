using Microsoft.Extensions.DependencyInjection;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.Services.OPCUA.Connections;

namespace UNSInfra.Services.OPCUA.Extensions;

/// <summary>
/// Extension methods for registering OPC UA services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OPC UA connection services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOPCUAConnection(this IServiceCollection services)
    {
        // Register the OPC UA connection descriptor
        services.AddSingleton<IConnectionDescriptor, OPCUAConnectionDescriptor>();
        
        return services;
    }
}