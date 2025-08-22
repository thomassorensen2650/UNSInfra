namespace UNSInfra.ConnectionSDK.Models;

/// <summary>
/// Event arguments for when data is received from an input
/// </summary>
public class DataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// The data point that was received
    /// </summary>
    public required DataPoint DataPoint { get; set; }

    /// <summary>
    /// ID of the input that received the data
    /// </summary>
    public required string InputId { get; set; }

    /// <summary>
    /// Connection that received the data
    /// </summary>
    public required string ConnectionId { get; set; }

    /// <summary>
    /// When the data was received
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for when connection status changes
/// </summary>
public class ConnectionStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// ID of the connection whose status changed
    /// </summary>
    public required string ConnectionId { get; set; }

    /// <summary>
    /// Previous status
    /// </summary>
    public ConnectionStatus OldStatus { get; set; }

    /// <summary>
    /// Current status
    /// </summary>
    public ConnectionStatus NewStatus { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When the status changed
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}