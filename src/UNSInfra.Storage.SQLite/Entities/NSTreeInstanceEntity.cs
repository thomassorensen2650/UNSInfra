using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity for NS tree instances in SQLite storage.
/// </summary>
[Table("NSTreeInstances")]
public class NSTreeInstanceEntity
{
    /// <summary>
    /// Unique identifier for this NS tree instance.
    /// </summary>
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display name for this instance.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The hierarchy node type this instance is based on.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string HierarchyNodeId { get; set; } = string.Empty;

    /// <summary>
    /// The parent NS tree instance ID, if any.
    /// </summary>
    [MaxLength(50)]
    public string? ParentInstanceId { get; set; }

    /// <summary>
    /// Whether this instance is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this instance was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this instance was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string MetadataJson { get; set; } = "{}";
}