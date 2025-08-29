using UNSInfra.Services.TopicBrowser;

namespace UNSInfra.UI.GraphQL.Types;

/// <summary>
/// GraphQL type for namespace structure nodes
/// </summary>
public class NamespaceStructureNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Path => FullPath; // Alias for compatibility with MCP server
    public string NodeType { get; set; } = string.Empty;
    public HierarchyNodeInfo? HierarchyNode { get; set; }
    public NamespaceInfo? Namespace { get; set; }
    public IEnumerable<NamespaceStructureNode> Children { get; set; } = Enumerable.Empty<NamespaceStructureNode>();
}

/// <summary>
/// Information about a hierarchy node
/// </summary>
public class HierarchyNodeInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Information about a namespace node
/// </summary>
public class NamespaceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Flattened namespace structure node - solves GraphQL infinite recursion issues
/// </summary>
public class FlatNamespaceStructureNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Path => FullPath; // Alias for compatibility
    public string? ParentId { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public HierarchyNodeInfo? HierarchyNode { get; set; }
    public NamespaceInfo? Namespace { get; set; }
    public bool HasChildren { get; set; }
    public int Level { get; set; }
}