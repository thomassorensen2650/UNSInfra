using UNSInfra.Models.Configuration;

namespace UNSInfra.Core.Repositories;

/// <summary>
/// Repository interface for managing input/output configurations
/// </summary>
public interface IInputOutputConfigurationRepository
{
    /// <summary>
    /// Gets all input/output configurations
    /// </summary>
    /// <param name="serviceType">Filter by service type (optional)</param>
    /// <param name="type">Filter by input/output type (optional)</param>
    /// <param name="enabledOnly">Whether to return only enabled configurations</param>
    Task<IEnumerable<InputOutputConfiguration>> GetAllConfigurationsAsync(
        string? serviceType = null, 
        InputOutputType? type = null, 
        bool enabledOnly = false);

    /// <summary>
    /// Gets a specific configuration by ID
    /// </summary>
    /// <param name="id">Configuration ID</param>
    Task<InputOutputConfiguration?> GetConfigurationByIdAsync(string id);

    /// <summary>
    /// Gets all input configurations for a service type
    /// </summary>
    /// <param name="serviceType">Service type (MQTT, SocketIO)</param>
    /// <param name="enabledOnly">Whether to return only enabled configurations</param>
    Task<IEnumerable<InputConfiguration>> GetInputConfigurationsAsync(string serviceType, bool enabledOnly = true);

    /// <summary>
    /// Gets all output configurations for a service type
    /// </summary>
    /// <param name="serviceType">Service type (MQTT, SocketIO)</param>
    /// <param name="enabledOnly">Whether to return only enabled configurations</param>
    Task<IEnumerable<OutputConfiguration>> GetOutputConfigurationsAsync(string serviceType, bool enabledOnly = true);

    /// <summary>
    /// Gets all SocketIO input configurations
    /// </summary>
    /// <param name="enabledOnly">Whether to return only enabled configurations</param>
    Task<IEnumerable<SocketIOInputConfiguration>> GetSocketIOInputConfigurationsAsync(bool enabledOnly = true);

    /// <summary>
    /// Gets all MQTT input configurations
    /// </summary>
    /// <param name="enabledOnly">Whether to return only enabled configurations</param>
    Task<IEnumerable<MqttInputConfiguration>> GetMqttInputConfigurationsAsync(bool enabledOnly = true);

    /// <summary>
    /// Gets all MQTT output configurations
    /// </summary>
    /// <param name="enabledOnly">Whether to return only enabled configurations</param>
    Task<IEnumerable<MqttOutputConfiguration>> GetMqttOutputConfigurationsAsync(bool enabledOnly = true);

    /// <summary>
    /// Saves an input/output configuration
    /// </summary>
    /// <param name="configuration">Configuration to save</param>
    Task SaveConfigurationAsync(InputOutputConfiguration configuration);

    /// <summary>
    /// Deletes an input/output configuration
    /// </summary>
    /// <param name="id">Configuration ID to delete</param>
    Task<bool> DeleteConfigurationAsync(string id);

    /// <summary>
    /// Enables or disables a configuration
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="isEnabled">Whether to enable the configuration</param>
    Task<bool> SetConfigurationEnabledAsync(string id, bool isEnabled);

    /// <summary>
    /// Gets configurations that need to be processed (enabled and valid)
    /// </summary>
    /// <param name="serviceType">Service type filter</param>
    /// <param name="type">Input/Output type filter</param>
    Task<IEnumerable<InputOutputConfiguration>> GetActiveConfigurationsAsync(
        string? serviceType = null, 
        InputOutputType? type = null);

    /// <summary>
    /// Gets all configurations for a specific connection
    /// </summary>
    /// <param name="connectionId">Connection ID to filter by</param>
    /// <param name="enabledOnly">Whether to return only enabled configurations</param>
    Task<IEnumerable<InputOutputConfiguration>> GetConfigurationsByConnectionIdAsync(
        string connectionId, 
        bool enabledOnly = false);
}