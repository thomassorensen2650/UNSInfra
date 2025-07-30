using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity model for hierarchy nodes in SQLite database.
/// </summary>
[Table("HierarchyNodes")]
public class HierarchyNodeEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this hierarchy node.
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of this hierarchy level.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this hierarchy level is required when creating a hierarchical path.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the order/position of this node in the hierarchy (0-based, where 0 is root).
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the description of this hierarchy level for documentation and MCP support.
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent node ID. Null for root nodes.
    /// </summary>
    public string? ParentNodeId { get; set; }

    /// <summary>
    /// Gets or sets the hierarchy configuration ID this node belongs to.
    /// </summary>
    [Required]
    public string HierarchyConfigurationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata for this node as JSON.
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the allowed child node IDs as a JSON array.
    /// </summary>
    public string AllowedChildNodeIdsJson { get; set; } = "[]";

    /// <summary>
    /// Navigation property to the hierarchy configuration.
    /// </summary>
    [ForeignKey(nameof(HierarchyConfigurationId))]
    public virtual HierarchyConfigurationEntity HierarchyConfiguration { get; set; } = null!;

    /// <summary>
    /// Navigation property to the parent node.
    /// </summary>
    [ForeignKey(nameof(ParentNodeId))]
    public virtual HierarchyNodeEntity? ParentNode { get; set; }

    /// <summary>
    /// Navigation property to child nodes.
    /// </summary>
    public virtual ICollection<HierarchyNodeEntity> ChildNodes { get; set; } = new List<HierarchyNodeEntity>();
}