using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Configuration;
using UNSInfra.Core.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Core.Services;

/// <summary>
/// Background service that registers data ingestion service descriptors at startup
/// and manages their lifecycle.
/// </summary>
public class DataIngestionServiceRegistrationService : BackgroundService
{
    private readonly ILogger<DataIngestionServiceRegistrationService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataIngestionServiceManager _serviceManager;

    /// <summary>
    /// Initializes a new instance of the DataIngestionServiceRegistrationService.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="serviceProvider">Service provider for resolving descriptors</param>
    /// <param name="serviceManager">Service manager to register descriptors with</param>
    public DataIngestionServiceRegistrationService(
        ILogger<DataIngestionServiceRegistrationService> logger,
        IServiceProvider serviceProvider,
        IDataIngestionServiceManager serviceManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        
        // Subscribe to data events to store received data
        _serviceManager.DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Executes the background service.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for stopping the service</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting data ingestion service registration...");

            // Register all available service descriptors
            await RegisterServiceDescriptors();

            // Create default configurations if none exist
            await CreateDefaultConfigurationsIfNeeded();

            // Load and start enabled configurations
            var startedCount = await _serviceManager.LoadAndStartEnabledServicesAsync();
            _logger.LogInformation("Started {StartedCount} enabled data ingestion services", startedCount);

            // Subscribe to MQTT wildcard topics
            await SubscribeToMqttTopics();

            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("Data ingestion service registration stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in data ingestion service registration");
            throw;
        }
    }

    /// <summary>
    /// Stops the background service and shuts down all running services.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping all data ingestion services...");
        
        try
        {
            var stoppedCount = await _serviceManager.StopAllServicesAsync();
            _logger.LogInformation("Stopped {StoppedCount} data ingestion services", stoppedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping data ingestion services");
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Registers all available service descriptors with the service manager.
    /// </summary>
    private Task RegisterServiceDescriptors()
    {
        var registeredCount = 0;

        try
        {
            // Get all registered service descriptors
            var serviceDescriptors = _serviceProvider.GetServices<IDataIngestionServiceDescriptor>();
            
            foreach (var descriptor in serviceDescriptors)
            {
                try
                {
                    _serviceManager.RegisterServiceType(descriptor);
                    registeredCount++;
                    _logger.LogInformation("Registered {ServiceType} service descriptor", descriptor.ServiceType);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register {ServiceType} service descriptor", descriptor.ServiceType);
                }
            }

            _logger.LogInformation("Registered {RegisteredCount} service descriptors", registeredCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering service descriptors");
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates default configurations if none exist, based on registered service descriptors.
    /// </summary>
    private async Task CreateDefaultConfigurationsIfNeeded()
    {
        try
        {
            var repository = _serviceProvider.GetRequiredService<IDataIngestionConfigurationRepository>();
            var existingConfigurations = await repository.GetAllConfigurationsAsync();
            
            // If no configurations exist, create defaults for each registered service type
            if (!existingConfigurations.Any())
            {
                _logger.LogInformation("No existing configurations found. Creating default configurations...");
                
                var serviceDescriptors = _serviceProvider.GetServices<IDataIngestionServiceDescriptor>();
                
                foreach (var descriptor in serviceDescriptors)
                {
                    try
                    {
                        var defaultConfig = descriptor.CreateDefaultConfiguration();
                        
                        // Create production-ready configurations
                        if (descriptor.ServiceType == "MQTT")
                        {
                            // Enable and configure MQTT to match legacy appsettings.json
                            defaultConfig.Enabled = false;
                            defaultConfig.Name = "MQTT Broker (test.mosquitto.org)";
                            defaultConfig.Description = "MQTT connection to test.mosquitto.org for real-time data ingestion";
                            ConfigureMqttDefaults(defaultConfig);
                        }
                        else if (descriptor.ServiceType == "SocketIO")
                        {
                            // Enable and configure SocketIO to match legacy appsettings.json
                            defaultConfig.Enabled = true;
                            defaultConfig.Name = "Virtual Factory SocketIO";
                            defaultConfig.Description = "Socket.IO connection to virtualfactory.online for real-time data streaming";
                            ConfigureSocketIODefaults(defaultConfig);
                        }
                        else
                        {
                            // For other service types, keep them disabled
                            defaultConfig.Enabled = false;
                            defaultConfig.Name = $"Default {descriptor.DisplayName}";
                            defaultConfig.Description = $"Default configuration for {descriptor.DisplayName} - Edit and enable to use";
                        }
                        
                        await repository.SaveConfigurationAsync(defaultConfig);
                        _logger.LogInformation("Created default configuration for {ServiceType}", descriptor.ServiceType);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create default configuration for {ServiceType}", descriptor.ServiceType);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default configurations");
        }
    }

    /// <summary>
    /// Configures MQTT defaults to match legacy appsettings.json.
    /// </summary>
    private void ConfigureMqttDefaults(IDataIngestionConfiguration config)
    {
        // Use reflection to set properties since we can't reference specific implementation types
        var configType = config.GetType();
        
        var properties = new Dictionary<string, object?>
        {
            { "BrokerHost", "test.mosquitto.org" },
            { "BrokerPort", 1883 },
            { "UseTls", false },
            { "ClientId", "UNSInfra-UI-Client" },
            { "Username", "" },
            { "Password", "" },
            { "KeepAliveInterval", 60 },
            { "ConnectionTimeout", 30 },
            { "CleanSession", true },
            { "MaxReconnectAttempts", 10 },
            { "ReconnectDelay", 5 },
            { "AutoReconnect", true },
            { "MessageBufferSize", 1000 },
            { "EnableDetailedLogging", true },
            { "ClientCertificatePath", "" },
            { "ClientCertificatePassword", "" },
            { "CaCertificatePath", "" },
            { "AllowUntrustedCertificates", false },
            { "IgnoreCertificateChainErrors", false },
            { "IgnoreCertificateRevocationErrors", false },
            { "TlsVersion", "1.2" },
            { "LastWillTopic", "uns/status/ui-client" },
            { "LastWillPayload", "{\"status\": \"offline\", \"timestamp\": \"{{timestamp}}\"}" },
            { "LastWillQualityOfServiceLevel", 1 },
            { "LastWillRetain", true },
            { "LastWillDelayInterval", 0 }
        };

        foreach (var (propertyName, value) in properties)
        {
            var property = configType.GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(config, value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set MQTT property {PropertyName} to {Value}", propertyName, value);
                }
            }
        }
    }

    /// <summary>
    /// Configures SocketIO defaults to match legacy appsettings.json.
    /// </summary>
    private void ConfigureSocketIODefaults(IDataIngestionConfiguration config)
    {
        // Use reflection to set properties since we can't reference specific implementation types
        var configType = config.GetType();
        
        var properties = new Dictionary<string, object?>
        {
            { "ServerUrl", "https://virtualfactory.online:3000" },
            { "ConnectionTimeoutSeconds", 10 },
            { "EnableReconnection", true },
            { "ReconnectionAttempts", 5 },
            { "ReconnectionDelaySeconds", 2 },
            { "EventNames", new[] { "update" } },
            { "BaseTopicPath", "virtualfactory" },
            { "EnableDetailedLogging", true }
        };

        foreach (var (propertyName, value) in properties)
        {
            var property = configType.GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(config, value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set SocketIO property {PropertyName} to {Value}", propertyName, value);
                }
            }
        }
    }

    /// <summary>
    /// Handles data received events from the service manager by storing the data in storage services.
    /// </summary>
    private async void OnDataReceived(object? sender, ServiceDataReceivedEventArgs e)
    {
        try
        {
            // Use Task.Run to avoid blocking the event handler and ensure proper async context
            await Task.Run(async () =>
            {
                try
                {
                    // Create a scope to resolve scoped storage services
                    using var scope = _serviceProvider.CreateScope();
                    var realtimeStorage = scope.ServiceProvider.GetRequiredService<IRealtimeStorage>();
                    var historicalStorage = scope.ServiceProvider.GetRequiredService<IHistoricalStorage>();
                    var topicBrowserService = scope.ServiceProvider.GetRequiredService<ITopicBrowserService>();

                    // Store in both realtime and historical storage with retry logic
                    const int maxRetries = 3;
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            // Store in both realtime and historical storage
                            await realtimeStorage.StoreAsync(e.DataPoint);
                            await historicalStorage.StoreAsync(e.DataPoint);
                            break; // Success, exit retry loop
                        }
                        catch (Exception storageEx) when (attempt < maxRetries && 
                            (storageEx.Message.Contains("database is locked") || storageEx.Message.Contains("disposed")))
                        {
                            _logger.LogWarning("Storage attempt {Attempt} failed for topic {Topic}, retrying in {Delay}ms: {Error}", 
                                attempt, e.DataPoint.Topic, attempt * 100, storageEx.Message);
                            await Task.Delay(attempt * 100); // Exponential backoff
                        }
                    }

                    // Notify topic browser service about the data update
                    if (topicBrowserService is TopicBrowserService concreteBrowserService)
                    {
                        concreteBrowserService.NotifyTopicDataUpdated(e.DataPoint.Topic, e.DataPoint);
                    }

                    _logger.LogDebug("Stored data for topic {Topic} from service {ConfigurationId}", 
                        e.DataPoint.Topic, e.ConfigurationId);
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Error in data storage task for topic {Topic}", e.DataPoint.Topic);
                    throw; // Re-throw for outer catch
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing data for topic {Topic}", e.DataPoint.Topic);
        }
    }

    /// <summary>
    /// Subscribes MQTT services to wildcard topics to receive all messages.
    /// </summary>
    private async Task SubscribeToMqttTopics()
    {
        try
        {
            // Get all running MQTT services
            var runningServices = _serviceManager.GetRunningServices();
            var mqttServices = runningServices.Where(kvp => kvp.Value is IMqttDataService)
                                              .Select(kvp => kvp.Value as IMqttDataService)
                                              .Where(s => s != null);

            foreach (var mqttService in mqttServices)
            {
                if (mqttService != null)
                {
                    // Subscribe to wildcard topic to receive all MQTT messages
                    var wildcardPath = new HierarchicalPath();
                    wildcardPath.SetValue("Enterprise", "mqtt");
                    wildcardPath.SetValue("Site", "broker");
                    wildcardPath.SetValue("Area", "all");
                    wildcardPath.SetValue("WorkCenter", "topics");

                    await mqttService.SubscribeToTopicAsync("#", wildcardPath);
                    _logger.LogInformation("Subscribed MQTT service to wildcard topic '#' for all messages");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to MQTT wildcard topics");
        }
    }
}