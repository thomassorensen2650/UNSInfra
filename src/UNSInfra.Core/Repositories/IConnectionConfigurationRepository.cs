using UNSInfra.Models;

namespace UNSInfra.Core.Repositories;

/// <summary>
/// Repository interface for managing connection configurations
/// </summary>
public interface IConnectionConfigurationRepository
{
    /// <summary>
    /// Gets all connection configurations
    /// </summary>
    /// <param name="enabledOnly">Whether to return only enabled connections</param>
    Task<IEnumerable<ConnectionConfiguration>> GetAllConnectionsAsync(bool enabledOnly = false);

    /// <summary>
    /// Gets a specific connection configuration by ID
    /// </summary>
    /// <param name="id">Connection ID</param>
    Task<ConnectionConfiguration?> GetConnectionByIdAsync(string id);

    /// <summary>
    /// Gets connections by type
    /// </summary>
    /// <param name="connectionType">Connection type (e.g., "mqtt", "socketio")</param>
    /// <param name="enabledOnly">Whether to return only enabled connections</param>
    Task<IEnumerable<ConnectionConfiguration>> GetConnectionsByTypeAsync(string connectionType, bool enabledOnly = false);

    /// <summary>
    /// Saves a connection configuration
    /// </summary>
    /// <param name="connection">Connection to save</param>
    Task SaveConnectionAsync(ConnectionConfiguration connection);

    /// <summary>
    /// Deletes a connection configuration
    /// </summary>
    /// <param name="id">Connection ID to delete</param>
    Task<bool> DeleteConnectionAsync(string id);

    /// <summary>
    /// Enables or disables a connection
    /// </summary>
    /// <param name="id">Connection ID</param>
    /// <param name="isEnabled">Whether to enable the connection</param>
    Task<bool> SetConnectionEnabledAsync(string id, bool isEnabled);

    /// <summary>
    /// Gets connections that should auto-start on application startup
    /// </summary>
    Task<IEnumerable<ConnectionConfiguration>> GetAutoStartConnectionsAsync();
}