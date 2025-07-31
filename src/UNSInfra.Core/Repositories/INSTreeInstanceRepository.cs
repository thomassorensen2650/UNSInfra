using UNSInfra.Models.Namespace;

namespace UNSInfra.Repositories;

/// <summary>
/// Repository for managing NS tree instances.
/// </summary>
public interface INSTreeInstanceRepository
{
    /// <summary>
    /// Gets all NS tree instances.
    /// </summary>
    /// <param name="activeOnly">Whether to return only active instances</param>
    /// <returns>Collection of NS tree instances</returns>
    Task<IEnumerable<NSTreeInstance>> GetAllInstancesAsync(bool activeOnly = true);

    /// <summary>
    /// Gets an NS tree instance by ID.
    /// </summary>
    /// <param name="id">The instance ID</param>
    /// <returns>The NS tree instance if found</returns>
    Task<NSTreeInstance?> GetInstanceByIdAsync(string id);

    /// <summary>
    /// Gets child instances for a given parent.
    /// </summary>
    /// <param name="parentInstanceId">The parent instance ID, or null for root instances</param>
    /// <returns>Collection of child instances</returns>
    Task<IEnumerable<NSTreeInstance>> GetChildInstancesAsync(string? parentInstanceId);

    /// <summary>
    /// Saves an NS tree instance.
    /// </summary>
    /// <param name="instance">The instance to save</param>
    Task SaveInstanceAsync(NSTreeInstance instance);

    /// <summary>
    /// Deletes an NS tree instance and all its children.
    /// </summary>
    /// <param name="id">The instance ID to delete</param>
    Task DeleteInstanceAsync(string id);

    /// <summary>
    /// Checks if an instance can be deleted (has no children or dependent namespaces).
    /// </summary>
    /// <param name="id">The instance ID</param>
    /// <returns>True if it can be deleted safely</returns>
    Task<bool> CanDeleteInstanceAsync(string id);
}