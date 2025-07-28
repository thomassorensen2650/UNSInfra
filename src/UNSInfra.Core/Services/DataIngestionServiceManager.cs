using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Configuration;
using UNSInfra.Core.Repositories;
using UNSInfra.Models.Data;
using UNSInfra.Services.DataIngestion.Mock;

namespace UNSInfra.Core.Services;

/// <summary>
/// Manages the lifecycle of data ingestion services based on configuration.
/// Handles dynamic creation, starting, stopping, and monitoring of services.
/// </summary>
public class DataIngestionServiceManager : IDataIngestionServiceManager, IDisposable
{
    private readonly ILogger<DataIngestionServiceManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataIngestionConfigurationRepository _configurationRepository;
    private readonly Dictionary<string, IDataIngestionServiceDescriptor> _serviceDescriptors = new();
    private readonly Dictionary<string, IDataIngestionService> _runningServices = new();
    private readonly Dictionary<string, ServiceStatus> _serviceStatuses = new();
    private readonly object _lock = new object();
    private bool _disposed = false;

    /// <summary>
    /// Event fired when a service status changes.
    /// </summary>
    public event EventHandler<ServiceStatusChangedEventArgs>? ServiceStatusChanged;

    /// <summary>
    /// Event fired when data is received from any managed service.
    /// </summary>
    public event EventHandler<ServiceDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Initializes a new instance of the DataIngestionServiceManager.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <param name="configurationRepository">Repository for configuration persistence</param>
    public DataIngestionServiceManager(
        ILogger<DataIngestionServiceManager> logger,
        IServiceProvider serviceProvider,
        IDataIngestionConfigurationRepository configurationRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));

        // Subscribe to configuration changes
        _configurationRepository.ConfigurationChanged += OnConfigurationChanged;
    }

    /// <summary>
    /// Gets all registered service descriptors.
    /// </summary>
    /// <returns>List of available service types</returns>
    public List<IDataIngestionServiceDescriptor> GetAvailableServiceTypes()
    {
        lock (_lock)
        {
            return _serviceDescriptors.Values.ToList();
        }
    }

    /// <summary>
    /// Gets a service descriptor by type.
    /// </summary>
    /// <param name="serviceType">The service type identifier</param>
    /// <returns>The service descriptor, or null if not found</returns>
    public IDataIngestionServiceDescriptor? GetServiceDescriptor(string serviceType)
    {
        lock (_lock)
        {
            return _serviceDescriptors.TryGetValue(serviceType, out var descriptor) ? descriptor : null;
        }
    }

    /// <summary>
    /// Registers a new service type descriptor.
    /// </summary>
    /// <param name="descriptor">The service descriptor to register</param>
    public void RegisterServiceType(IDataIngestionServiceDescriptor descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        lock (_lock)
        {
            _serviceDescriptors[descriptor.ServiceType] = descriptor;
            _logger.LogInformation("Registered service type: {ServiceType}", descriptor.ServiceType);
        }
    }

    /// <summary>
    /// Gets all currently managed service instances.
    /// </summary>
    /// <returns>Dictionary of configuration ID to service instance</returns>
    public Dictionary<string, IDataIngestionService> GetRunningServices()
    {
        lock (_lock)
        {
            return new Dictionary<string, IDataIngestionService>(_runningServices);
        }
    }

    /// <summary>
    /// Gets the status of all managed services.
    /// </summary>
    /// <returns>Dictionary of configuration ID to service status</returns>
    public Dictionary<string, ServiceStatus> GetServicesStatus()
    {
        lock (_lock)
        {
            return new Dictionary<string, ServiceStatus>(_serviceStatuses);
        }
    }

    /// <summary>
    /// Gets the status of a specific service.
    /// </summary>
    /// <param name="configurationId">The configuration ID</param>
    /// <returns>The service status, or null if not found</returns>
    public ServiceStatus? GetServiceStatus(string configurationId)
    {
        lock (_lock)
        {
            return _serviceStatuses.TryGetValue(configurationId, out var status) ? status : null;
        }
    }

    /// <summary>
    /// Starts a service from a configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use</param>
    /// <returns>True if started successfully</returns>
    public async Task<bool> StartServiceAsync(IDataIngestionConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        if (!configuration.Enabled)
        {
            _logger.LogInformation("Configuration {ConfigurationId} is disabled, skipping start", configuration.Id);
            return false;
        }

        try
        {
            // Validate configuration
            var validationErrors = configuration.Validate();
            if (validationErrors.Any())
            {
                _logger.LogWarning("Configuration {ConfigurationId} has validation errors: {Errors}", 
                    configuration.Id, string.Join(", ", validationErrors));
                UpdateServiceStatus(configuration.Id, ConnectionStatus.Error, "Configuration validation failed: " + string.Join(", ", validationErrors));
                return false;
            }

            // Get service descriptor
            var descriptor = GetServiceDescriptor(configuration.ServiceType);
            if (descriptor == null)
            {
                _logger.LogError("No service descriptor found for type: {ServiceType}", configuration.ServiceType);
                UpdateServiceStatus(configuration.Id, ConnectionStatus.Error, $"Unknown service type: {configuration.ServiceType}");
                return false;
            }

            // Stop existing service if running
            await StopServiceAsync(configuration.Id);

            // Update status to connecting
            UpdateServiceStatus(configuration.Id, ConnectionStatus.Connecting, "Starting service...");

            // Create service instance
            var service = descriptor.CreateService(configuration, _serviceProvider);
            
            // Subscribe to data events
            service.DataReceived += (sender, dataPoint) =>
            {
                DataReceived?.Invoke(this, new ServiceDataReceivedEventArgs
                {
                    ConfigurationId = configuration.Id,
                    DataPoint = dataPoint,
                    Service = service
                });
            };

            // Start the service
            await service.StartAsync();

            // Store the running service
            lock (_lock)
            {
                _runningServices[configuration.Id] = service;
            }

            UpdateServiceStatus(configuration.Id, ConnectionStatus.Connected, "Service started successfully");
            _logger.LogInformation("Started service {ConfigurationId} ({ServiceType})", configuration.Id, configuration.ServiceType);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service {ConfigurationId}", configuration.Id);
            UpdateServiceStatus(configuration.Id, ConnectionStatus.Error, $"Start failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stops a service by configuration ID.
    /// </summary>
    /// <param name="configurationId">The configuration ID</param>
    /// <returns>True if stopped successfully</returns>
    public async Task<bool> StopServiceAsync(string configurationId)
    {
        if (string.IsNullOrEmpty(configurationId))
            throw new ArgumentException("Configuration ID cannot be null or empty", nameof(configurationId));

        try
        {
            IDataIngestionService? service;
            
            lock (_lock)
            {
                if (!_runningServices.TryGetValue(configurationId, out service))
                {
                    _logger.LogInformation("Service {ConfigurationId} is not running", configurationId);
                    return true;
                }
            }

            UpdateServiceStatus(configurationId, ConnectionStatus.Stopping, "Stopping service...");

            // Stop the service
            await service.StopAsync();

            // Dispose if disposable
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Remove from running services
            lock (_lock)
            {
                _runningServices.Remove(configurationId);
            }

            UpdateServiceStatus(configurationId, ConnectionStatus.Disconnected, "Service stopped");
            _logger.LogInformation("Stopped service {ConfigurationId}", configurationId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service {ConfigurationId}", configurationId);
            UpdateServiceStatus(configurationId, ConnectionStatus.Error, $"Stop failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restarts a service by configuration ID.
    /// </summary>
    /// <param name="configurationId">The configuration ID</param>
    /// <returns>True if restarted successfully</returns>
    public async Task<bool> RestartServiceAsync(string configurationId)
    {
        try
        {
            // Get the configuration
            var configurations = await _configurationRepository.GetAllConfigurationsAsync();
            var configuration = configurations.FirstOrDefault(c => c.Id == configurationId);
            
            if (configuration == null)
            {
                _logger.LogWarning("Configuration {ConfigurationId} not found for restart", configurationId);
                return false;
            }

            // Stop and start the service
            await StopServiceAsync(configurationId);
            return await StartServiceAsync(configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart service {ConfigurationId}", configurationId);
            return false;
        }
    }

    /// <summary>
    /// Updates a running service with a new configuration.
    /// </summary>
    /// <param name="configuration">The updated configuration</param>
    /// <returns>True if updated successfully</returns>
    public async Task<bool> UpdateServiceAsync(IDataIngestionConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        try
        {
            // For now, we'll implement update as stop and start
            // In the future, we could add hot-reload capabilities for specific configuration changes
            _logger.LogInformation("Updating service {ConfigurationId} by restarting", configuration.Id);
            
            await StopServiceAsync(configuration.Id);
            
            if (configuration.Enabled)
            {
                return await StartServiceAsync(configuration);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update service {ConfigurationId}", configuration.Id);
            return false;
        }
    }

    /// <summary>
    /// Loads and starts all enabled configurations.
    /// Called during application startup.
    /// </summary>
    /// <returns>Number of services started</returns>
    public async Task<int> LoadAndStartEnabledServicesAsync()
    {
        try
        {
            _logger.LogInformation("Loading and starting enabled services...");
            
            var configurations = await _configurationRepository.GetAllConfigurationsAsync();
            var enabledConfigurations = configurations.Where(c => c.Enabled).ToList();
            
            var startedCount = 0;
            
            foreach (var configuration in enabledConfigurations)
            {
                try
                {
                    if (await StartServiceAsync(configuration))
                    {
                        startedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start service {ConfigurationId} during startup", configuration.Id);
                }
            }
            
            _logger.LogInformation("Started {StartedCount} of {EnabledCount} enabled services", startedCount, enabledConfigurations.Count);
            return startedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load and start enabled services");
            return 0;
        }
    }

    /// <summary>
    /// Stops all running services.
    /// Called during application shutdown.
    /// </summary>
    /// <returns>Number of services stopped</returns>
    public async Task<int> StopAllServicesAsync()
    {
        try
        {
            _logger.LogInformation("Stopping all running services...");
            
            var runningServiceIds = GetRunningServices().Keys.ToList();
            var stoppedCount = 0;
            
            foreach (var configurationId in runningServiceIds)
            {
                try
                {
                    if (await StopServiceAsync(configurationId))
                    {
                        stoppedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop service {ConfigurationId} during shutdown", configurationId);
                }
            }
            
            _logger.LogInformation("Stopped {StoppedCount} of {RunningCount} running services", stoppedCount, runningServiceIds.Count);
            return stoppedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop all running services");
            return 0;
        }
    }

    /// <summary>
    /// Updates the status of a service and fires the status changed event.
    /// </summary>
    private void UpdateServiceStatus(string configurationId, ConnectionStatus status, string? message = null)
    {
        ServiceStatus? previousStatus;
        ServiceStatus newStatus;
        
        lock (_lock)
        {
            _serviceStatuses.TryGetValue(configurationId, out previousStatus);
            
            newStatus = new ServiceStatus
            {
                ConfigurationId = configurationId,
                Status = status,
                Message = message,
                LastUpdated = DateTime.UtcNow,
                ConnectionAttempts = previousStatus?.ConnectionAttempts ?? 0,
                DataPointsReceived = previousStatus?.DataPointsReceived ?? 0,
                BytesReceived = previousStatus?.BytesReceived ?? 0,
                MessageRate = previousStatus?.MessageRate ?? 0.0,
                ThroughputBytesPerSecond = previousStatus?.ThroughputBytesPerSecond ?? 0.0
            };
            
            // Increment connection attempts when connecting
            if (status == ConnectionStatus.Connecting)
            {
                newStatus.ConnectionAttempts++;
            }
            
            _serviceStatuses[configurationId] = newStatus;
        }
        
        // Fire event outside of lock
        ServiceStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs
        {
            ConfigurationId = configurationId,
            Status = newStatus,
            PreviousStatus = previousStatus
        });
    }

    /// <summary>
    /// Handles configuration changes from the repository.
    /// </summary>
    private async void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Configuration changed: {ConfigurationId}, Action: {Action}", e.Configuration.Id, e.ChangeType);
            
            switch (e.ChangeType)
            {
                case ConfigurationChangeType.Added:
                case ConfigurationChangeType.Updated:
                    if (e.Configuration != null)
                    {
                        await UpdateServiceAsync(e.Configuration);
                    }
                    break;
                    
                case ConfigurationChangeType.Deleted:
                    await StopServiceAsync(e.Configuration.Id);
                    lock (_lock)
                    {
                        _serviceStatuses.Remove(e.Configuration.Id);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling configuration change for {ConfigurationId}", e.Configuration.Id);
        }
    }

    /// <summary>
    /// Disposes the service manager and stops all services.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                StopAllServicesAsync().GetAwaiter().GetResult();
                _configurationRepository.ConfigurationChanged -= OnConfigurationChanged;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}