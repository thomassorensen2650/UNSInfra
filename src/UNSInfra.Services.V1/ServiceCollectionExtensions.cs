using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.V1.Configuration;
using UNSInfra.Services.V1.Mqtt;
using UNSInfra.Services.V1.SparkplugB;

namespace UNSInfra.Services.V1;

/// <summary>
/// Extension methods for registering UNS Infrastructure V1 services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the production MQTT data service and related components to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configuration">The configuration instance to bind MQTT settings from</param>
    /// <param name="configurationSection">The configuration section name for MQTT settings (default: "Mqtt")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMqttDataService(
        this IServiceCollection services,
        IConfiguration configuration,
        string configurationSection = "Mqtt")
    {
        // Configure MQTT settings
        services.Configure<MqttConfiguration>(options => 
            configuration.GetSection(configurationSection).Bind(options));

        // Register Sparkplug B decoder
        services.AddSingleton<SparkplugBDecoder>();

        // Register the MQTT data service
        services.AddSingleton<IMqttDataService, MqttDataService>();
        
        // Also register as IDataIngestionService for generic handling
        services.AddSingleton<IDataIngestionService>(provider => provider.GetRequiredService<IMqttDataService>());

        return services;
    }

    /// <summary>
    /// Adds the production MQTT data service with explicit configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configureOptions">Action to configure MQTT options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMqttDataService(
        this IServiceCollection services,
        Action<MqttConfiguration> configureOptions)
    {
        // Configure MQTT settings
        services.Configure<MqttConfiguration>(configureOptions);

        // Register Sparkplug B decoder
        services.AddSingleton<SparkplugBDecoder>();

        // Register the MQTT data service
        services.AddSingleton<IMqttDataService, MqttDataService>();
        
        // Also register as IDataIngestionService for generic handling
        services.AddSingleton<IDataIngestionService>(provider => provider.GetRequiredService<IMqttDataService>());

        return services;
    }

    /// <summary>
    /// Adds all UNS Infrastructure V1 services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configuration">The configuration instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddUNSInfrastructureV1Services(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add MQTT data service
        services.AddMqttDataService(configuration);

        // Add other V1 services as they are implemented
        // services.AddKafkaDataService(configuration);
        // services.AddOpcUaDataService(configuration);

        return services;
    }
}