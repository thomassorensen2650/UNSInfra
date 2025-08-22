using Microsoft.Extensions.DependencyInjection;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.Services.V1.Connections;

namespace UNSInfra.Services.V1.Extensions;

/// <summary>
/// Extension methods for registering Services.V1 components
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the production MQTT connection implementation
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddProductionMqttConnection(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionDescriptor, MqttConnectionDescriptor>();
        return services;
    }
}