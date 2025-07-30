using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity model for hierarchy configurations in SQLite database.
/// </summary>
[Table("HierarchyConfigurations")]
public class HierarchyConfigurationEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this hierarchy configuration.
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of this hierarchy configuration.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this hierarchy configuration.
    /// </summary>
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is the currently active hierarchy configuration.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets whether this is a system-defined configuration that cannot be deleted.
    /// </summary>
    public bool IsSystemDefined { get; set; }

    /// <summary>
    /// Gets or sets when this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this configuration was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the collection of hierarchy nodes that make up this configuration.
    /// </summary>
    public virtual ICollection<HierarchyNodeEntity> Nodes { get; set; } = new List<HierarchyNodeEntity>();
}