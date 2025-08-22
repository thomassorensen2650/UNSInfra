using UNSInfra.ConnectionSDK.Models;

namespace UNSInfra.ConnectionSDK.Abstractions;

/// <summary>
/// Core interface for all data connections in the UNS Infrastructure system.
/// A connection handles both inputs (receiving data) and outputs (sending data) for a specific system or protocol.
/// </summary>
public interface IDataConnection : IDisposable
{
    /// <summary>
    /// Unique identifier for this connection instance
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// Display name for this connection
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Event raised when data is received from an input source
    /// </summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event raised when the connection status changes
    /// </summary>
    event EventHandler<ConnectionStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Initializes the connection with the provided configuration
    /// </summary>
    /// <param name="configuration">Connection-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if initialization was successful</returns>
    Task<bool> InitializeAsync(object configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the connection and begins processing inputs/outputs
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if start was successful</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the connection and ceases all processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if stop was successful</returns>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the connection
    /// </summary>
    ConnectionStatus Status { get; }

    /// <summary>
    /// Configures an input to receive data from the external system
    /// </summary>
    /// <param name="inputConfig">Input-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if input was configured successfully</returns>
    Task<bool> ConfigureInputAsync(object inputConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an input configuration
    /// </summary>
    /// <param name="inputId">ID of the input to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if input was removed successfully</returns>
    Task<bool> RemoveInputAsync(string inputId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures an output to send data to the external system
    /// </summary>
    /// <param name="outputConfig">Output-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if output was configured successfully</returns>
    Task<bool> ConfigureOutputAsync(object outputConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an output configuration
    /// </summary>
    /// <param name="outputId">ID of the output to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if output was removed successfully</returns>
    Task<bool> RemoveOutputAsync(string outputId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a data point through a configured output
    /// </summary>
    /// <param name="dataPoint">Data point to send</param>
    /// <param name="outputId">ID of the output to use (optional - may use default output)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if data was sent successfully</returns>
    Task<bool> SendDataAsync(DataPoint dataPoint, string? outputId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the provided configuration is valid for this connection type
    /// </summary>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>Validation result with any error messages</returns>
    ValidationResult ValidateConfiguration(object configuration);
}