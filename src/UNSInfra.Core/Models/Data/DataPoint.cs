using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Models.Data;

/// <summary>
/// Represents a single data point received from MQTT or Kafka with hierarchical context.
/// Contains the actual data value, metadata, and ISA-S95 path information.
/// </summary>
public class DataPoint
{
    /// <summary>
    /// Gets or sets the unique identifier for this data point.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the ISA-S95 hierarchical path where this data point originates.
    /// </summary>
    public HierarchicalPath Path { get; set; } = new();

    /// <summary>
    /// Gets or sets the MQTT/Kafka topic from which this data was received.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actual data payload. Can be any JSON-serializable object.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this data point was created (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the source system that provided this data (e.g., "MQTT", "Kafka").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata associated with this data point.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
