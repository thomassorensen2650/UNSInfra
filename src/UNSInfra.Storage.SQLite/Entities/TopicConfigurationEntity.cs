using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity model for topic configurations in SQLite database.
/// </summary>
[Table("TopicConfigurations")]
public class TopicConfigurationEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this topic configuration.
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the topic name/path.
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
    /// Gets or sets whether this topic configuration is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the source system type (MQTT, Kafka, etc.).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this topic configuration.
    /// </summary>
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this configuration was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets who created this configuration.
    /// </summary>
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata as JSON.
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the namespace configuration ID this topic belongs to.
    /// </summary>
    [MaxLength(50)]
    public string? NamespaceConfigurationId { get; set; }

    /// <summary>
    /// Gets or sets the path within the assigned namespace.
    /// </summary>
    [MaxLength(500)]
    public string NSPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for this topic when used in UNS.
    /// </summary>
    [MaxLength(200)]
    public string UNSName { get; set; } = string.Empty;
}