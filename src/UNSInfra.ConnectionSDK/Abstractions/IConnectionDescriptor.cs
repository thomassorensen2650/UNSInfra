using UNSInfra.ConnectionSDK.Models;

namespace UNSInfra.ConnectionSDK.Abstractions;

/// <summary>
/// Describes a connection type and provides metadata for UI rendering and configuration
/// </summary>
public interface IConnectionDescriptor
{
    /// <summary>
    /// Unique identifier for this connection type (e.g., "mqtt", "opcua", "socketio")
    /// </summary>
    string ConnectionType { get; }

    /// <summary>
    /// Display name for this connection type
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Description of what this connection type does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Icon class or identifier for UI display
    /// </summary>
    string? IconClass { get; }

    /// <summary>
    /// Category for grouping connection types in UI
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Version of this connection implementation
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Whether this connection supports inputs (receiving data)
    /// </summary>
    bool SupportsInputs { get; }

    /// <summary>
    /// Whether this connection supports outputs (sending data)
    /// </summary>
    bool SupportsOutputs { get; }

    /// <summary>
    /// Gets the configuration schema for the connection
    /// </summary>
    /// <returns>Configuration fields metadata</returns>
    ConfigurationSchema GetConnectionConfigurationSchema();

    /// <summary>
    /// Gets the configuration schema for inputs
    /// </summary>
    /// <returns>Input configuration fields metadata</returns>
    ConfigurationSchema GetInputConfigurationSchema();

    /// <summary>
    /// Gets the configuration schema for outputs
    /// </summary>
    /// <returns>Output configuration fields metadata</returns>
    ConfigurationSchema GetOutputConfigurationSchema();

    /// <summary>
    /// Creates a default connection configuration
    /// </summary>
    /// <returns>Default configuration object</returns>
    object CreateDefaultConnectionConfiguration();

    /// <summary>
    /// Creates a default input configuration
    /// </summary>
    /// <returns>Default input configuration object</returns>
    object CreateDefaultInputConfiguration();

    /// <summary>
    /// Creates a default output configuration
    /// </summary>
    /// <returns>Default output configuration object</returns>
    object CreateDefaultOutputConfiguration();

    /// <summary>
    /// Creates an instance of the connection
    /// </summary>
    /// <param name="connectionId">Unique identifier for the connection instance</param>
    /// <param name="name">Display name for the connection instance</param>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <returns>Connection instance</returns>
    IDataConnection CreateConnection(string connectionId, string name, IServiceProvider serviceProvider);
}