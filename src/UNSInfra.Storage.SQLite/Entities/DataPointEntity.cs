using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity model for data points in SQLite database (for historical storage).
/// </summary>
[Table("DataPoints")]
public class DataPointEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this data point.
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the topic this data point belongs to.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hierarchical path values as JSON.
    /// </summary>
    [Required]
    public string PathValuesJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the data value as JSON.
    /// </summary>
    [Required]
    public string ValueJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source system that provided this data.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quality indicator for this data point.
    /// </summary>
    [MaxLength(50)]
    public string Quality { get; set; } = "Good";

    /// <summary>
    /// Gets or sets when this data point was created/received.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional metadata as JSON.
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    /// Index for efficient querying by topic and timestamp.
    /// </summary>
    [NotMapped]
    public string TopicTimestamp => $"{Topic}_{Timestamp:yyyyMMddHHmmss}";
}