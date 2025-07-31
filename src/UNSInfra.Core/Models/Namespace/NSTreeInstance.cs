using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Models.Namespace;

/// <summary>
/// Represents an instance of a hierarchy node in the NS tree structure.
/// This is different from the hierarchy configuration - it's an actual instantiated node.
/// </summary>
public class NSTreeInstance
{
    /// <summary>
    /// Unique identifier for this NS tree instance.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The display name for this instance (e.g., "Enterprise1", "Dallas", "Production").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The hierarchy node type this instance is based on.
    /// </summary>
    public string HierarchyNodeId { get; set; } = string.Empty;

    /// <summary>
    /// The parent NS tree instance ID, if any.
    /// </summary>
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
    /// Additional metadata for this instance.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets the hierarchical path for this instance.
    /// </summary>
    /// <param name="allInstances">All instances to resolve parent hierarchy</param>
    /// <param name="hierarchyConfig">The hierarchy configuration</param>
    /// <returns>The hierarchical path</returns>
    public HierarchicalPath GetHierarchicalPath(IEnumerable<NSTreeInstance> allInstances, HierarchyConfiguration hierarchyConfig)
    {
        var path = new HierarchicalPath();
        var current = this;
        var instances = allInstances.ToList();
        
        while (current != null)
        {
            var hierarchyNode = hierarchyConfig.GetNodeById(current.HierarchyNodeId);
            if (hierarchyNode != null)
            {
                path.SetValue(hierarchyNode.Name, current.Name);
            }
            
            if (!string.IsNullOrEmpty(current.ParentInstanceId))
            {
                current = instances.FirstOrDefault(i => i.Id == current.ParentInstanceId);
            }
            else
            {
                break;
            }
        }
        
        return path;
    }
}