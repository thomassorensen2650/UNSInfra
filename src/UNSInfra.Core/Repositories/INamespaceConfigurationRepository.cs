using UNSInfra.Models.Namespace;

namespace UNSInfra.Repositories;

/// <summary>
/// Repository interface for managing namespace configurations.
/// </summary>
public interface INamespaceConfigurationRepository
{
    /// <summary>
    /// Gets a namespace configuration by its ID.
    /// </summary>
    /// <param name="id">The namespace configuration ID</param>
    /// <returns>The namespace configuration, or null if not found</returns>
    Task<NamespaceConfiguration?> GetNamespaceConfigurationAsync(string id);

    /// <summary>
    /// Gets a namespace configuration by its name and hierarchical path.
    /// </summary>
    /// <param name="name">The namespace name</param>
    /// <param name="hierarchicalPath">The hierarchical path</param>
    /// <returns>The namespace configuration, or null if not found</returns>
    Task<NamespaceConfiguration?> GetNamespaceConfigurationByNameAndPathAsync(string name, string hierarchicalPath);

    /// <summary>
    /// Gets all namespace configurations.
    /// </summary>
    /// <param name="activeOnly">Whether to return only active configurations</param>
    /// <returns>A collection of namespace configurations</returns>
    Task<IEnumerable<NamespaceConfiguration>> GetAllNamespaceConfigurationsAsync(bool activeOnly = true);

    /// <summary>
    /// Gets namespace configurations by type.
    /// </summary>
    /// <param name="type">The namespace type to filter by</param>
    /// <param name="activeOnly">Whether to return only active configurations</param>
    /// <returns>A collection of namespace configurations</returns>
    Task<IEnumerable<NamespaceConfiguration>> GetNamespaceConfigurationsByTypeAsync(NamespaceType type, bool activeOnly = true);

    /// <summary>
    /// Finds the matching namespace configuration for a given topic path.
    /// </summary>
    /// <param name="topicPath">The topic path to match</param>
    /// <returns>The matching namespace configuration, or null if none found</returns>
    Task<NamespaceConfiguration?> FindMatchingNamespaceAsync(string topicPath);

    /// <summary>
    /// Saves a namespace configuration (insert or update).
    /// </summary>
    /// <param name="configuration">The namespace configuration to save</param>
    /// <returns>A task representing the async operation</returns>
    Task SaveNamespaceConfigurationAsync(NamespaceConfiguration configuration);

    /// <summary>
    /// Deletes a namespace configuration.
    /// </summary>
    /// <param name="id">The ID of the configuration to delete</param>
    /// <returns>A task representing the async operation</returns>
    Task DeleteNamespaceConfigurationAsync(string id);

    /// <summary>
    /// Checks if a topic path pattern conflicts with existing namespace configurations.
    /// </summary>
    /// <param name="topicPathPattern">The pattern to check</param>
    /// <param name="excludeId">Configuration ID to exclude from conflict check (for updates)</param>
    /// <returns>True if there's a conflict, false otherwise</returns>
    Task<bool> HasConflictingPatternAsync(string topicPathPattern, string? excludeId = null);
}