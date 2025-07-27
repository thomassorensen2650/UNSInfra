namespace UNSInfra.Services.SocketIO.Configuration;

/// <summary>
/// Configuration options for the SocketIO data service.
/// </summary>
public class SocketIOConfiguration
{
    /// <summary>
    /// Gets or sets the Socket.IO server URL for the connection.
    /// Default: https://virtualfactory.online:3000
    /// </summary>
    public string ServerUrl { get; set; } = "https://virtualfactory.online:3000";

    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// Default: 10 seconds
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether automatic reconnection is enabled.
    /// Default: true
    /// </summary>
    public bool EnableReconnection { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of reconnection attempts.
    /// Default: 5
    /// </summary>
    public int ReconnectionAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the delay between reconnection attempts in seconds.
    /// Default: 2 seconds
    /// </summary>
    public int ReconnectionDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Gets or sets the list of Socket.IO event names to listen for.
    /// If empty, will listen for common event names and any event.
    /// </summary>
    public string[] EventNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the base topic path prefix for all SocketIO topics.
    /// Default: socketio
    /// </summary>
    public string BaseTopicPath { get; set; } = "socketio";

    /// <summary>
    /// Gets or sets whether to enable detailed logging for debugging.
    /// Default: false
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}