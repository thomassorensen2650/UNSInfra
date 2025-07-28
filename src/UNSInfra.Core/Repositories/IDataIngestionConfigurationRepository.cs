using UNSInfra.Core.Configuration;

namespace UNSInfra.Core.Repositories;

/// <summary>
/// Repository for managing data ingestion service configurations.
/// Handles persistence and retrieval of configuration data.
/// </summary>
public interface IDataIngestionConfigurationRepository
{
    /// <summary>
    /// Gets all configurations.
    /// </summary>
    /// <returns>List of all configurations</returns>
    Task<List<IDataIngestionConfiguration>> GetAllConfigurationsAsync();

    /// <summary>
    /// Gets configurations filtered by service type.
    /// </summary>
    /// <param name="serviceType">The service type to filter by</param>
    /// <returns>List of configurations for the specified service type</returns>
    Task<List<IDataIngestionConfiguration>> GetConfigurationsByTypeAsync(string serviceType);

    /// <summary>
    /// Gets a configuration by its ID.
    /// </summary>
    /// <param name="id">The configuration ID</param>
    /// <returns>The configuration, or null if not found</returns>
    Task<IDataIngestionConfiguration?> GetConfigurationAsync(string id);

    /// <summary>
    /// Gets all enabled configurations.
    /// </summary>
    /// <returns>List of enabled configurations</returns>
    Task<List<IDataIngestionConfiguration>> GetEnabledConfigurationsAsync();

    /// <summary>
    /// Saves a configuration (insert or update).
    /// </summary>
    /// <param name="configuration">The configuration to save</param>
    /// <returns>The saved configuration with updated timestamps</returns>
    Task<IDataIngestionConfiguration> SaveConfigurationAsync(IDataIngestionConfiguration configuration);

    /// <summary>
    /// Deletes a configuration.
    /// </summary>
    /// <param name="id">The configuration ID to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteConfigurationAsync(string id);

    /// <summary>
    /// Checks if a configuration name is unique within a service type.
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <param name="serviceType">The service type</param>
    /// <param name="excludeId">Configuration ID to exclude from the check (for updates)</param>
    /// <returns>True if the name is unique</returns>
    Task<bool> IsNameUniqueAsync(string name, string serviceType, string? excludeId = null);

    /// <summary>
    /// Event fired when a configuration is added, updated, or deleted.
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
}

/// <summary>
/// Event arguments for configuration change events.
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change that occurred.
    /// </summary>
    public ConfigurationChangeType ChangeType { get; set; }

    /// <summary>
    /// The configuration that was changed.
    /// </summary>
    public IDataIngestionConfiguration Configuration { get; set; } = null!;

    /// <summary>
    /// The previous configuration (for updates).
    /// </summary>
    public IDataIngestionConfiguration? PreviousConfiguration { get; set; }
}

/// <summary>
/// Types of configuration changes.
/// </summary>
public enum ConfigurationChangeType
{
    Added,
    Updated,
    Deleted
}