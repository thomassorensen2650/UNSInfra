using UNSInfra.Models.Namespace;
using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Services;

/// <summary>
/// Service for building and managing the hierarchical namespace structure (NS) tree.
/// </summary>
public interface INamespaceStructureService
{
    /// <summary>
    /// Gets the complete namespace structure tree, including hierarchy nodes and namespaces.
    /// </summary>
    /// <returns>A collection of root NS tree nodes</returns>
    Task<IEnumerable<NSTreeNode>> GetNamespaceStructureAsync();

    /// <summary>
    /// Gets the possible hierarchy nodes that can be added under a given parent.
    /// </summary>
    /// <param name="parentNodeId">The parent hierarchy node ID, or null for root nodes</param>
    /// <returns>Available hierarchy nodes that can be added</returns>
    Task<IEnumerable<HierarchyNode>> GetAvailableHierarchyNodesAsync(string? parentNodeId);

    /// <summary>
    /// Creates a new hierarchy node instance in the NS tree.
    /// </summary>
    /// <param name="hierarchyNodeId">The hierarchy node type to create</param>
    /// <param name="parentPath">The parent path in the NS tree</param>
    /// <param name="name">The instance name (e.g., "Enterprise1", "Site2")</param>
    /// <returns>The created NS tree node</returns>
    Task<NSTreeNode> CreateHierarchyNodeInstanceAsync(string hierarchyNodeId, string parentPath, string name);

    /// <summary>
    /// Creates a new namespace in the NS tree.
    /// </summary>
    /// <param name="parentPath">The parent path in the NS tree</param>
    /// <param name="namespaceConfig">The namespace configuration to create</param>
    /// <returns>The created NS tree node</returns>
    Task<NSTreeNode> CreateNamespaceAsync(string parentPath, NamespaceConfiguration namespaceConfig);

    /// <summary>
    /// Gets the full hierarchical path for a given NS tree path.
    /// </summary>
    /// <param name="nsPath">The NS tree path</param>
    /// <returns>The corresponding hierarchical path</returns>
    Task<HierarchicalPath> GetHierarchicalPathFromNSPathAsync(string nsPath);

    /// <summary>
    /// Gets the hierarchical path for a given instance ID.
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <returns>The hierarchical path for this instance</returns>
    Task<HierarchicalPath> GetHierarchicalPathFromInstanceIdAsync(string instanceId);

    /// <summary>
    /// Adds a new hierarchy node instance to the NS tree.
    /// </summary>
    /// <param name="hierarchyNodeId">The hierarchy node type ID</param>
    /// <param name="name">The instance name</param>
    /// <param name="parentInstanceId">The parent instance ID, or null for root</param>
    /// <param name="description">Optional description for the instance</param>
    /// <returns>The created NS tree instance</returns>
    Task<NSTreeInstance> AddHierarchyInstanceAsync(string hierarchyNodeId, string name, string? parentInstanceId, string? description = null);

    /// <summary>
    /// Updates an existing hierarchy node instance.
    /// </summary>
    /// <param name="instanceId">The instance ID to update</param>
    /// <param name="name">The new instance name</param>
    /// <param name="description">The new description (stored in metadata)</param>
    /// <returns>The updated NS tree instance</returns>
    Task<NSTreeInstance> UpdateInstanceAsync(string instanceId, string name, string? description = null);

    /// <summary>
    /// Deletes an NS tree instance and all its children.
    /// </summary>
    /// <param name="instanceId">The instance ID to delete</param>
    Task DeleteInstanceAsync(string instanceId);

    /// <summary>
    /// Checks if an instance can be deleted safely.
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <returns>True if it can be deleted</returns>
    Task<bool> CanDeleteInstanceAsync(string instanceId);

    /// <summary>
    /// Gets the active hierarchy configuration.
    /// </summary>
    /// <returns>The active hierarchy configuration, or null if none is active</returns>
    Task<HierarchyConfiguration?> GetActiveHierarchyConfigurationAsync();

    /// <summary>
    /// Deletes a namespace and all its child namespaces, along with cleaning up topic mappings.
    /// </summary>
    /// <param name="namespaceId">The ID of the namespace to delete</param>
    /// <returns>True if deletion was successful, false otherwise</returns>
    Task<bool> DeleteNamespaceAsync(string namespaceId);

    /// <summary>
    /// Checks if a namespace can be deleted safely.
    /// </summary>
    /// <param name="namespaceId">The namespace ID to check</param>
    /// <returns>True if it can be deleted, along with any warnings or blocking reasons</returns>
    Task<(bool CanDelete, string? Reason)> CanDeleteNamespaceAsync(string namespaceId);
}

/// <summary>
/// Represents a node in the NS (Namespace Structure) tree.
/// </summary>
public class NSTreeNode
{
    /// <summary>
    /// The display name of this node.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The full path to this node in the NS tree.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// The type of this node (HierarchyNode or Namespace).
    /// </summary>
    public NSNodeType NodeType { get; set; }

    /// <summary>
    /// The hierarchy node definition if this is a hierarchy node.
    /// </summary>
    public HierarchyNode? HierarchyNode { get; set; }

    /// <summary>
    /// The namespace configuration if this is a namespace.
    /// </summary>
    public NamespaceConfiguration? Namespace { get; set; }

    /// <summary>
    /// The NS tree instance if this is a hierarchy node instance.
    /// </summary>
    public NSTreeInstance? Instance { get; set; }

    /// <summary>
    /// Child nodes under this node.
    /// </summary>
    public List<NSTreeNode> Children { get; set; } = new();

    /// <summary>
    /// Whether this node can have hierarchy children.
    /// </summary>
    public bool CanHaveHierarchyChildren { get; set; }

    /// <summary>
    /// Whether this node can have namespace children.
    /// </summary>
    public bool CanHaveNamespaceChildren { get; set; } = true;
}

/// <summary>
/// Types of nodes in the NS tree.
/// </summary>
public enum NSNodeType
{
    /// <summary>
    /// A hierarchy node instance (e.g., "Enterprise1", "Site2").
    /// </summary>
    HierarchyNode,

    /// <summary>
    /// A namespace (e.g., "KPIs", "Production").
    /// </summary>
    Namespace
}