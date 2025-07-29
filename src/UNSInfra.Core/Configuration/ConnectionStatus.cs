namespace UNSInfra.Core.Configuration;

/// <summary>
/// Represents the connection status of a data ingestion service.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Service is not configured or disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// Service is starting up or attempting to connect.
    /// </summary>
    Connecting,

    /// <summary>
    /// Service is connected and operational.
    /// </summary>
    Connected,

    /// <summary>
    /// Service is disconnected but will attempt to reconnect.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Service has encountered an error and cannot connect.
    /// </summary>
    Error,

    /// <summary>
    /// Service is stopping.
    /// </summary>
    Stopping
}

/// <summary>
/// Detailed status information for a data ingestion service.
/// </summary>
public class ServiceStatus
{
    /// <summary>
    /// Configuration ID this status relates to.
    /// </summary>
    public string ConfigurationId { get; set; } = string.Empty;

    /// <summary>
    /// Current connection status.
    /// </summary>
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Disabled;

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error details if status is Error.
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// When this status was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional status metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Number of connection attempts made.
    /// </summary>
    public int ConnectionAttempts { get; set; }

    /// <summary>
    /// When the service was last successfully connected.
    /// </summary>
    public DateTime? LastConnected { get; set; }

    /// <summary>
    /// Statistics about data throughput.
    /// </summary>
    public DataThroughputStats? ThroughputStats { get; set; }

    /// <summary>
    /// Total data points received since service started.
    /// </summary>
    public long DataPointsReceived { get; set; }

    /// <summary>
    /// Total bytes received since service started.
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// Current message rate (messages per second).
    /// </summary>
    public double MessageRate { get; set; }

    /// <summary>
    /// Current throughput in bytes per second.
    /// </summary>
    public double ThroughputBytesPerSecond { get; set; }
}

/// <summary>
/// Statistics about data throughput for a service.
/// </summary>
public class DataThroughputStats
{
    /// <summary>
    /// Total messages received since service started.
    /// </summary>
    public long TotalMessages { get; set; }

    /// <summary>
    /// Messages received in the last minute.
    /// </summary>
    public long MessagesPerMinute { get; set; }

    /// <summary>
    /// Total bytes received since service started.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Average bytes per message.
    /// </summary>
    public double AverageBytesPerMessage { get; set; }

    /// <summary>
    /// When these statistics were last calculated.
    /// </summary>
    public DateTime LastCalculated { get; set; } = DateTime.UtcNow;
}