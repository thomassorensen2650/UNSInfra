using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Models.Namespace;

/// <summary>
/// Represents a namespace configuration that defines how topics are organized and processed.
/// </summary>
public class NamespaceConfiguration
{
    /// <summary>
    /// Unique identifier for the namespace configuration.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the namespace (e.g., "KPIs", "Production", "Quality").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type of namespace (Functional, Informative, Definitional, Ad-Hoc).
    /// </summary>
    public NamespaceType Type { get; set; }

    /// <summary>
    /// Detailed description of the namespace and its purpose.
    /// This can be used for MCP (Model Context Protocol) documentation.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The hierarchical path where this namespace applies.
    /// </summary>
    public HierarchicalPath HierarchicalPath { get; set; } = new();

    /// <summary>
    /// The parent namespace ID if this namespace is nested within another namespace.
    /// Null for root-level namespaces.
    /// </summary>
    public string? ParentNamespaceId { get; set; }

    /// <summary>
    /// The hierarchy node ID that this namespace can be placed under.
    /// This determines where in the NS tree structure this namespace can exist.
    /// </summary>
    public string? AllowedParentHierarchyNodeId { get; set; }

    /// <summary>
    /// Whether this namespace configuration is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this namespace configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this namespace configuration was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created this namespace configuration.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata for the namespace in JSON format.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets the full namespace path combining hierarchical path and namespace name.
    /// For example: "Enterprise 1/KPIs" where "Enterprise 1" is from hierarchical path.
    /// </summary>
    public string GetFullNamespacePath()
    {
        var hierarchicalPart = HierarchicalPath.GetFullPath();
        return string.IsNullOrEmpty(hierarchicalPart) 
            ? Name 
            : $"{hierarchicalPart}/{Name}";
    }

    /// <summary>
    /// Gets the full hierarchical namespace path for display in the NS tree.
    /// This includes parent namespaces if nested.
    /// </summary>
    /// <param name="allNamespaces">All available namespaces to resolve parent references</param>
    /// <returns>The full NS tree path</returns>
    public string GetFullNSTreePath(IEnumerable<NamespaceConfiguration> allNamespaces)
    {
        var pathParts = new List<string>();
        var current = this;
        
        while (current != null)
        {
            pathParts.Insert(0, current.Name);
            
            if (!string.IsNullOrEmpty(current.ParentNamespaceId))
            {
                current = allNamespaces.FirstOrDefault(ns => ns.Id == current.ParentNamespaceId);
            }
            else
            {
                break;
            }
        }
        
        return string.Join("/", pathParts);
    }
}