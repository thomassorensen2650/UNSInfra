namespace UNSInfra.ConnectionSDK.Abstractions;

/// <summary>
/// Registry for managing available connection types
/// </summary>
public interface IConnectionRegistry
{
    /// <summary>
    /// Registers a connection descriptor
    /// </summary>
    /// <param name="descriptor">Connection descriptor to register</param>
    void RegisterConnection(IConnectionDescriptor descriptor);

    /// <summary>
    /// Gets all registered connection descriptors
    /// </summary>
    /// <returns>Collection of connection descriptors</returns>
    IEnumerable<IConnectionDescriptor> GetAllDescriptors();

    /// <summary>
    /// Gets a connection descriptor by type
    /// </summary>
    /// <param name="connectionType">Connection type identifier</param>
    /// <returns>Connection descriptor, or null if not found</returns>
    IConnectionDescriptor? GetDescriptor(string connectionType);

    /// <summary>
    /// Gets connection descriptors by category
    /// </summary>
    /// <param name="category">Category name</param>
    /// <returns>Collection of connection descriptors in the category</returns>
    IEnumerable<IConnectionDescriptor> GetDescriptorsByCategory(string category);

    /// <summary>
    /// Checks if a connection type is registered
    /// </summary>
    /// <param name="connectionType">Connection type identifier</param>
    /// <returns>True if the connection type is registered</returns>
    bool IsRegistered(string connectionType);
}