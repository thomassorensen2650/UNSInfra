namespace UNSInfra.Models.Hierarchy;

/// <summary>
/// Represents a complete hierarchical structure configuration.
/// Contains all hierarchy nodes and their relationships.
/// </summary>
public class HierarchyConfiguration
{
    /// <summary>
    /// Gets or sets the unique identifier for this hierarchy configuration.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of this hierarchy configuration (e.g., "ISA-S95 Standard", "Custom Manufacturing").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this hierarchy configuration.
    /// </summary>
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
    /// Gets or sets the collection of hierarchy nodes that make up this configuration.
    /// </summary>
    public List<HierarchyNode> Nodes { get; set; } = new();

    /// <summary>
    /// Gets or sets when this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this configuration was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the root nodes (nodes without parents) in this hierarchy.
    /// </summary>
    public IEnumerable<HierarchyNode> GetRootNodes()
    {
        return Nodes.Where(n => string.IsNullOrEmpty(n.ParentNodeId)).OrderBy(n => n.Order);
    }

    /// <summary>
    /// Gets the child nodes for a given parent node ID.
    /// </summary>
    /// <param name="parentNodeId">The parent node ID</param>
    /// <returns>Collection of child nodes ordered by their Order property</returns>
    public IEnumerable<HierarchyNode> GetChildNodes(string parentNodeId)
    {
        return Nodes.Where(n => n.ParentNodeId == parentNodeId).OrderBy(n => n.Order);
    }

    /// <summary>
    /// Gets a hierarchy node by its ID.
    /// </summary>
    /// <param name="nodeId">The node ID to find</param>
    /// <returns>The hierarchy node if found, null otherwise</returns>
    public HierarchyNode? GetNodeById(string nodeId)
    {
        return Nodes.FirstOrDefault(n => n.Id == nodeId);
    }

    /// <summary>
    /// Validates the hierarchy configuration for consistency.
    /// </summary>
    /// <returns>List of validation errors, empty if valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Check for duplicate node IDs
        var duplicateIds = Nodes.GroupBy(n => n.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var duplicateId in duplicateIds)
        {
            errors.Add($"Duplicate node ID found: {duplicateId}");
        }

        // Check for invalid parent references
        foreach (var node in Nodes.Where(n => !string.IsNullOrEmpty(n.ParentNodeId)))
        {
            if (!Nodes.Any(n => n.Id == node.ParentNodeId))
            {
                errors.Add($"Node '{node.Name}' references non-existent parent ID: {node.ParentNodeId}");
            }
        }

        // Check for circular references
        foreach (var node in Nodes)
        {
            if (HasCircularReference(node.Id, new HashSet<string>()))
            {
                errors.Add($"Circular reference detected in hierarchy starting from node: {node.Name}");
            }
        }

        // Check for invalid AllowedChildNodeIds references
        foreach (var node in Nodes)
        {
            foreach (var childId in node.AllowedChildNodeIds)
            {
                if (!Nodes.Any(n => n.Id == childId))
                {
                    errors.Add($"Node '{node.Name}' references non-existent allowed child ID: {childId}");
                }
            }
        }

        return errors;
    }

    private bool HasCircularReference(string nodeId, HashSet<string> visitedNodes)
    {
        if (visitedNodes.Contains(nodeId))
        {
            return true;
        }

        visitedNodes.Add(nodeId);

        var node = GetNodeById(nodeId);
        if (node?.ParentNodeId != null)
        {
            return HasCircularReference(node.ParentNodeId, new HashSet<string>(visitedNodes));
        }

        return false;
    }
}