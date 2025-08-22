namespace UNSInfra.ConnectionSDK.Models;

/// <summary>
/// Represents the connection status of a data connection
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Connection is not configured or disabled
    /// </summary>
    Disabled,

    /// <summary>
    /// Connection is attempting to connect
    /// </summary>
    Connecting,

    /// <summary>
    /// Connection is connected and operational
    /// </summary>
    Connected,

    /// <summary>
    /// Connection is disconnected but configured
    /// </summary>
    Disconnected,

    /// <summary>
    /// Connection has encountered an error
    /// </summary>
    Error,

    /// <summary>
    /// Connection is stopping
    /// </summary>
    Stopping,

    /// <summary>
    /// Connection status is unknown
    /// </summary>
    Unknown
}