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
        // Register repository (use in-memory by default, can be overridden)
        services.AddSingleton<IInputOutputConfigurationRepository, InMemoryInputOutputConfigurationRepository>();

        // Register input services
        services.AddSingleton<SocketIOConfigurableDataService>();
        services.AddSingleton<MqttConfigurableDataService>();

        // Register output services
        services.AddSingleton<MqttModelExportService>();
        services.AddSingleton<MqttDataExportService>();

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

        // Register input services
        services.AddSingleton<SocketIOConfigurableDataService>();
        services.AddSingleton<MqttConfigurableDataService>();

        // Register output services
        services.AddSingleton<MqttModelExportService>();
        services.AddSingleton<MqttDataExportService>();

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
        services.AddSingleton<SocketIOConfigurableDataService>();
        services.AddSingleton<MqttConfigurableDataService>();

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

        // Register output services only
        services.AddSingleton<MqttModelExportService>();
        services.AddSingleton<MqttDataExportService>();

        return services;
    }

}