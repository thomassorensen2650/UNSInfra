using UNSInfra.Core.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Services.DataIngestion.Mock;

namespace UNSInfra.Core.Services;

/// <summary>
/// Manages the lifecycle of data ingestion services based on configuration.
/// Handles dynamic creation, starting, stopping, and monitoring of services.
/// </summary>
public interface IDataIngestionServiceManager
{
    /// <summary>
    /// Gets all registered service descriptors.
    /// </summary>
    /// <returns>List of available service types</returns>
    List<IDataIngestionServiceDescriptor> GetAvailableServiceTypes();

    /// <summary>
    /// Gets a service descriptor by type.
    /// </summary>
    /// <param name="serviceType">The service type identifier</param>
    /// <returns>The service descriptor, or null if not found</returns>
    IDataIngestionServiceDescriptor? GetServiceDescriptor(string serviceType);

    /// <summary>
    /// Registers a new service type descriptor.
    /// </summary>
    /// <param name="descriptor">The service descriptor to register</param>
    void RegisterServiceType(IDataIngestionServiceDescriptor descriptor);

    /// <summary>
    /// Gets all currently managed service instances.
    /// </summary>
    /// <returns>Dictionary of configuration ID to service instance</returns>
    Dictionary<string, IDataIngestionService> GetRunningServices();

    /// <summary>
    /// Gets the status of all managed services.
    /// </summary>
    /// <returns>Dictionary of configuration ID to service status</returns>
    Dictionary<string, ServiceStatus> GetServicesStatus();

    /// <summary>
    /// Gets the status of a specific service.
    /// </summary>
    /// <param name="configurationId">The configuration ID</param>
    /// <returns>The service status, or null if not found</returns>
    ServiceStatus? GetServiceStatus(string configurationId);

    /// <summary>
    /// Starts a service from a configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use</param>
    /// <returns>True if started successfully</returns>
    Task<bool> StartServiceAsync(IDataIngestionConfiguration configuration);

    /// <summary>
    /// Stops a service by configuration ID.
    /// </summary>
    /// <param name="configurationId">The configuration ID</param>
    /// <returns>True if stopped successfully</returns>
    Task<bool> StopServiceAsync(string configurationId);

    /// <summary>
    /// Restarts a service by configuration ID.
    /// </summary>
    /// <param name="configurationId">The configuration ID</param>
    /// <returns>True if restarted successfully</returns>
    Task<bool> RestartServiceAsync(string configurationId);

    /// <summary>
    /// Updates a running service with a new configuration.
    /// </summary>
    /// <param name="configuration">The updated configuration</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateServiceAsync(IDataIngestionConfiguration configuration);

    /// <summary>
    /// Loads and starts all enabled configurations.
    /// Called during application startup.
    /// </summary>
    /// <returns>Number of services started</returns>
    Task<int> LoadAndStartEnabledServicesAsync();

    /// <summary>
    /// Stops all running services.
    /// Called during application shutdown.
    /// </summary>
    /// <returns>Number of services stopped</returns>
    Task<int> StopAllServicesAsync();

    /// <summary>
    /// Event fired when a service status changes.
    /// </summary>
    event EventHandler<ServiceStatusChangedEventArgs>? ServiceStatusChanged;

    /// <summary>
    /// Event fired when data is received from any managed service.
    /// </summary>
    event EventHandler<ServiceDataReceivedEventArgs>? DataReceived;
}

/// <summary>
/// Event arguments for service status change events.
/// </summary>
public class ServiceStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// The configuration ID of the service.
    /// </summary>
    public string ConfigurationId { get; set; } = string.Empty;

    /// <summary>
    /// The new status of the service.
    /// </summary>
    public ServiceStatus Status { get; set; } = null!;

    /// <summary>
    /// The previous status of the service.
    /// </summary>
    public ServiceStatus? PreviousStatus { get; set; }
}

/// <summary>
/// Event arguments for data received events.
/// </summary>
public class ServiceDataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// The configuration ID of the service that received the data.
    /// </summary>
    public string ConfigurationId { get; set; } = string.Empty;

    /// <summary>
    /// The data point that was received.
    /// </summary>
    public DataPoint DataPoint { get; set; } = null!;

    /// <summary>
    /// The service instance that received the data.
    /// </summary>
    public IDataIngestionService Service { get; set; } = null!;
}