namespace UNSInfra.Models.Hierarchy;

/// <summary>
/// Represents a rule for automatically mapping topics to hierarchical paths.
/// Allows for pattern-based topic discovery and path generation.
/// </summary>
public class TopicMappingRule
{
    /// <summary>
    /// Gets or sets the unique identifier for this mapping rule.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the regex pattern or wildcard pattern to match topics.
    /// </summary>
    public string TopicPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template for generating hierarchical paths from topic matches.
    /// Can include placeholders like {0}, {1} for regex groups or named groups.
    /// </summary>
    public string PathTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the priority of this rule. Higher priority rules are evaluated first.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether topics matched by this rule should be automatically verified.
    /// </summary>
    public bool AutoVerify { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this rule is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets additional metadata for this mapping rule.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}