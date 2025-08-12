using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UNSInfra.Core.Repositories;
using UNSInfra.Services.V1.Background;
using UNSInfra.Services.V1.Mqtt;
using UNSInfra.Services.SocketIO;

namespace UNSInfra.Services.V1.Extensions;

/// <summary>
/// Extension methods for registering input/output services
/// </summary>
public static class InputOutputServiceCollectionExtensions
{
    /// <summary>
    /// Adds input/output configuration services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInputOutputServices(this IServiceCollection services)
    {
        // Register repository only if not already registered (allows storage provider to override)
        services.TryAddScoped<IInputOutputConfigurationRepository, InMemoryInputOutputConfigurationRepository>();

        // Register MQTT connection manager as singleton (shared across all services)
        services.AddSingleton<MqttConnectionManager>();

        // Register input services
        services.AddScoped<SocketIOConfigurableDataService>();
        services.AddScoped<MqttConfigurableDataService>();

        // Register output services
        services.AddScoped<MqttModelExportService>();
        services.AddScoped<MqttDataExportService>();

        // Register background service coordinator
        services.AddHostedService<InputOutputBackgroundService>();

        return services;
    }

    /// <summary>
    /// Adds input/output configuration services with custom repository implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="repositoryImplementation">Custom repository implementation factory</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInputOutputServices<TRepository>(
        this IServiceCollection services,
        Func<IServiceProvider, TRepository> repositoryImplementation)
        where TRepository : class, IInputOutputConfigurationRepository
    {
        // Register custom repository
        services.AddSingleton<IInputOutputConfigurationRepository>(repositoryImplementation);

        // Register MQTT connection manager as singleton (shared across all services)
        services.AddSingleton<MqttConnectionManager>();

        // Register input services
        services.AddScoped<SocketIOConfigurableDataService>();
        services.AddScoped<MqttConfigurableDataService>();

        // Register output services
        services.AddScoped<MqttModelExportService>();
        services.AddScoped<MqttDataExportService>();

        // Register background service coordinator
        services.AddHostedService<InputOutputBackgroundService>();

        return services;
    }

    /// <summary>
    /// Adds only input services (for scenarios where you only want data ingestion)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInputServices(this IServiceCollection services)
    {
        // Register repository if not already registered
        services.TryAddSingleton<IInputOutputConfigurationRepository, InMemoryInputOutputConfigurationRepository>();

        // Register input services only
        services.AddScoped<SocketIOConfigurableDataService>();
        services.AddScoped<MqttConfigurableDataService>();

        return services;
    }

    /// <summary>
    /// Adds only output services (for scenarios where you only want data export)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOutputServices(this IServiceCollection services)
    {
        // Register repository if not already registered
        services.TryAddSingleton<IInputOutputConfigurationRepository, InMemoryInputOutputConfigurationRepository>();

        // Register MQTT connection manager as singleton (shared across all services)
        services.AddSingleton<MqttConnectionManager>();

        // Register output services only
        services.AddScoped<MqttModelExportService>();
        services.AddScoped<MqttDataExportService>();

        return services;
    }

}