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
    /// The topic path pattern that this namespace matches.
    /// For example: "Enterprise1/KPI" would match "Enterprise1/KPI/OEE/Performance".
    /// </summary>
    public string TopicPathPattern { get; set; } = string.Empty;

    /// <summary>
    /// Whether topics matching this namespace should be automatically verified.
    /// </summary>
    public bool AutoVerifyTopics { get; set; } = false;

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
    /// Checks if a topic path matches this namespace pattern.
    /// </summary>
    /// <param name="topicPath">The topic path to check</param>
    /// <returns>True if the topic matches this namespace</returns>
    public bool MatchesTopicPath(string topicPath)
    {
        if (string.IsNullOrEmpty(TopicPathPattern) || string.IsNullOrEmpty(topicPath))
            return false;

        // Simple prefix matching for now - can be enhanced with regex or glob patterns
        return topicPath.StartsWith(TopicPathPattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the namespace-relative path from a full topic path.
    /// For example: "Enterprise1/KPI/OEE/Performance" with pattern "Enterprise1/KPI" 
    /// returns "OEE/Performance".
    /// </summary>
    /// <param name="fullTopicPath">The full topic path</param>
    /// <returns>The namespace-relative path</returns>
    public string GetNamespaceRelativePath(string fullTopicPath)
    {
        if (!MatchesTopicPath(fullTopicPath))
            return string.Empty;

        var relativePath = fullTopicPath.Substring(TopicPathPattern.Length);
        return relativePath.StartsWith("/") ? relativePath.Substring(1) : relativePath;
    }
}