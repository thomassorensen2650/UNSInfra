using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Models;

namespace UNSInfra.Abstractions;

/// <summary>
/// Interface for managing data connections and their lifecycle
/// </summary>
public interface IConnectionManager
{
    /// <summary>
    /// Event raised when data is received from any connection
    /// </summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event raised when a connection status changes
    /// </summary>
    event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// Creates a new connection with the specified configuration
    /// </summary>
    /// <param name="config">Connection configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection was created successfully</returns>
    Task<bool> CreateConnectionAsync(ConnectionConfiguration config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an existing connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection was started successfully</returns>
    Task<bool> StartConnectionAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops an existing connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection was stopped successfully</returns>
    Task<bool> StopConnectionAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes and disposes a connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection was removed successfully</returns>
    Task<bool> RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data through a specific connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <param name="dataPoint">Data to send</param>
    /// <param name="outputId">Optional specific output ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if data was sent successfully</returns>
    Task<bool> SendDataAsync(string connectionId, UNSInfra.Models.Data.DataPoint dataPoint, string? outputId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active connection IDs
    /// </summary>
    /// <returns>Collection of active connection IDs</returns>
    IEnumerable<string> GetActiveConnectionIds();

    /// <summary>
    /// Gets the configuration for a specific connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <returns>Connection configuration, or null if not found</returns>
    ConnectionConfiguration? GetConnectionConfiguration(string connectionId);

    /// <summary>
    /// Gets all connection configurations
    /// </summary>
    /// <returns>Collection of all connection configurations</returns>
    IEnumerable<ConnectionConfiguration> GetAllConnectionConfigurations();

    /// <summary>
    /// Gets the current status of a connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <returns>Connection status</returns>
    ConnectionStatus GetConnectionStatus(string connectionId);

    /// <summary>
    /// Updates a connection configuration and reconfigures the connection if it's active
    /// </summary>
    /// <param name="config">Updated connection configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection was updated successfully</returns>
    Task<bool> UpdateConnectionAsync(ConnectionConfiguration config, CancellationToken cancellationToken = default);
}