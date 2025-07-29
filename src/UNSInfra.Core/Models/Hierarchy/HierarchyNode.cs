namespace UNSInfra.Models.Hierarchy;

/// <summary>
/// Represents a single node in the hierarchical structure configuration.
/// Defines the properties and constraints for each level in the hierarchy.
/// </summary>
public class HierarchyNode
{
    /// <summary>
    /// Gets or sets the unique identifier for this hierarchy node.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of this hierarchy level (e.g., "Enterprise", "Site", "Area").
    /// </summary>
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
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of hierarchy node IDs that are allowed as children of this node.
    /// If empty, this node can be a leaf node.
    /// </summary>
    public List<string> AllowedChildNodeIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the parent node ID. Null for root nodes.
    /// </summary>
    public string? ParentNodeId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this node that can be used by MCP or other integrations.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}