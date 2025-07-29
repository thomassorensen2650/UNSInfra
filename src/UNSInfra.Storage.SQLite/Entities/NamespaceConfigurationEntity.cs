using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity class for namespace configurations in SQLite storage.
/// </summary>
[Table("NamespaceConfigurations")]
public class NamespaceConfigurationEntity
{
    /// <summary>
    /// Unique identifier for the namespace configuration.
    /// </summary>
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the namespace.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type of namespace (stored as integer).
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Description of the namespace.
    /// </summary>
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The hierarchical path values as JSON.
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string HierarchicalPathJson { get; set; } = string.Empty;

    /// <summary>
    /// The topic path pattern that this namespace matches.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TopicPathPattern { get; set; } = string.Empty;

    /// <summary>
    /// Whether topics should be automatically verified.
    /// </summary>
    public bool AutoVerifyTopics { get; set; }

    /// <summary>
    /// Whether this configuration is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Who created this configuration.
    /// </summary>
    [MaxLength(200)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string MetadataJson { get; set; } = string.Empty;
}