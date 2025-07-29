using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Repositories;

/// <summary>
/// Repository interface for managing hierarchy configurations.
/// </summary>
public interface IHierarchyConfigurationRepository
{
    /// <summary>
    /// Gets the currently active hierarchy configuration.
    /// </summary>
    /// <returns>The active hierarchy configuration, or null if none is set</returns>
    Task<HierarchyConfiguration?> GetActiveConfigurationAsync();

    /// <summary>
    /// Gets all available hierarchy configurations.
    /// </summary>
    /// <returns>Collection of all hierarchy configurations</returns>
    Task<IEnumerable<HierarchyConfiguration>> GetAllConfigurationsAsync();

    /// <summary>
    /// Gets a hierarchy configuration by its ID.
    /// </summary>
    /// <param name="id">The configuration ID</param>
    /// <returns>The hierarchy configuration if found, null otherwise</returns>
    Task<HierarchyConfiguration?> GetConfigurationByIdAsync(string id);

    /// <summary>
    /// Saves a hierarchy configuration.
    /// </summary>
    /// <param name="configuration">The configuration to save</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveConfigurationAsync(HierarchyConfiguration configuration);

    /// <summary>
    /// Deletes a hierarchy configuration by its ID.
    /// </summary>
    /// <param name="id">The configuration ID to delete</param>
    /// <returns>True if deleted successfully, false if not found or cannot be deleted</returns>
    Task<bool> DeleteConfigurationAsync(string id);

    /// <summary>
    /// Sets a hierarchy configuration as the active one.
    /// </summary>
    /// <param name="id">The configuration ID to activate</param>
    /// <returns>True if activated successfully, false if not found</returns>
    Task<bool> SetActiveConfigurationAsync(string id);

    /// <summary>
    /// Creates the default ISA-S95 hierarchy configuration if it doesn't exist.
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    Task EnsureDefaultConfigurationAsync();
}