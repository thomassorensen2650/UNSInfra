namespace UNSInfra.ConnectionSDK.Models;

/// <summary>
/// Represents a data point with a value and metadata
/// </summary>
public class DataPoint
{
    /// <summary>
    /// The topic or identifier for this data point
    /// </summary>
    public required string Topic { get; set; }

    /// <summary>
    /// The value of the data point
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Timestamp when the data was captured
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Quality indicator for the data (Good, Bad, Uncertain, etc.)
    /// </summary>
    public string Quality { get; set; } = "Good";

    /// <summary>
    /// Additional metadata about the data point
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Source system that provided this data
    /// </summary>
    public string? SourceSystem { get; set; }

    /// <summary>
    /// Connection ID that produced this data
    /// </summary>
    public string? ConnectionId { get; set; }
}