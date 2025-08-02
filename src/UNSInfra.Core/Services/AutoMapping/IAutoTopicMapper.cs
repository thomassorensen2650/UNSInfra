using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Services.AutoMapping;

/// <summary>
/// Interface for automatic topic mapping functionality.
/// Maps incoming topics to existing UNS tree structures based on configurable rules.
/// </summary>
public interface IAutoTopicMapper
{
    /// <summary>
    /// Attempts to automatically map a topic to an existing UNS path.
    /// </summary>
    /// <param name="topic">The topic to map</param>
    /// <param name="sourceType">The source type (MQTT, SocketIO, etc.)</param>
    /// <param name="autoMapperConfig">Configuration for auto mapping behavior</param>
    /// <returns>The mapped topic configuration, or null if no mapping was found</returns>
    Task<TopicConfiguration?> TryMapTopicAsync(string topic, string sourceType, AutoTopicMapperConfiguration autoMapperConfig);

    /// <summary>
    /// Validates whether a topic can be auto-mapped based on the UNS tree structure.
    /// </summary>
    /// <param name="topic">The topic to validate</param>
    /// <param name="autoMapperConfig">Configuration for auto mapping behavior</param>
    /// <returns>Auto mapping result with success status and details</returns>
    Task<AutoMappingResult> ValidateAutoMappingAsync(string topic, AutoTopicMapperConfiguration autoMapperConfig);

    /// <summary>
    /// Gets potential auto mapping suggestions for a topic.
    /// </summary>
    /// <param name="topic">The topic to get suggestions for</param>
    /// <param name="autoMapperConfig">Configuration for auto mapping behavior</param>
    /// <returns>List of possible auto mapping suggestions</returns>
    Task<List<AutoMappingSuggestion>> GetAutoMappingSuggestionsAsync(string topic, AutoTopicMapperConfiguration autoMapperConfig);
}

/// <summary>
/// Configuration for auto topic mapper behavior.
/// </summary>
public class AutoTopicMapperConfiguration
{
    /// <summary>
    /// Whether auto mapping is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Minimum confidence level required for auto mapping (0.0 to 1.0).
    /// </summary>
    public double MinimumConfidence { get; set; } = 0.8;


    /// <summary>
    /// Maximum depth to search in the UNS tree for matches.
    /// </summary>
    public int MaxSearchDepth { get; set; } = 10;

    /// <summary>
    /// Prefixes to strip from topics before mapping (e.g., "socketio/update/").
    /// </summary>
    public List<string> StripPrefixes { get; set; } = new();


    /// <summary>
    /// Whether to create missing intermediate nodes in the UNS tree.
    /// </summary>
    public bool CreateMissingNodes { get; set; } = false;

    /// <summary>
    /// Case sensitivity for topic matching.
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Custom mapping rules specific to this configuration.
    /// </summary>
    public List<AutoMappingRule> CustomRules { get; set; } = new();
}

/// <summary>
/// Custom rule for auto mapping topics.
/// </summary>
public class AutoMappingRule
{
    /// <summary>
    /// Regular expression pattern to match topics.
    /// </summary>
    public string TopicPattern { get; set; } = string.Empty;

    /// <summary>
    /// UNS path template to map to (can include placeholders like {0}, {1}).
    /// </summary>
    public string UNSPathTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level for this rule (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>
    /// Whether this rule is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Description of what this rule does.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Result of an auto mapping operation.
/// </summary>
public class AutoMappingResult
{
    /// <summary>
    /// Whether the mapping was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Confidence level of the mapping (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The mapped UNS path.
    /// </summary>
    public string? MappedPath { get; set; }

    /// <summary>
    /// The hierarchical path that was matched.
    /// </summary>
    public HierarchicalPath? HierarchicalPath { get; set; }

    /// <summary>
    /// The rule that was used for mapping (if any).
    /// </summary>
    public AutoMappingRule? UsedRule { get; set; }

    /// <summary>
    /// Error message if mapping failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional details about the mapping process.
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Suggestion for auto mapping a topic.
/// </summary>
public class AutoMappingSuggestion
{
    /// <summary>
    /// The suggested UNS path.
    /// </summary>
    public string UNSPath { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level for this suggestion (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reason for this suggestion.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// The hierarchical path for this suggestion.
    /// </summary>
    public HierarchicalPath HierarchicalPath { get; set; } = new();

    /// <summary>
    /// Whether this suggestion requires creating new nodes.
    /// </summary>
    public bool RequiresNewNodes { get; set; }
}