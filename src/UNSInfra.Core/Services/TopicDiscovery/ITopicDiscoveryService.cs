using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Services.TopicDiscovery;

/// <summary>
/// Interface for discovering and mapping unknown topics to hierarchical paths.
/// Handles automatic topic configuration creation and pattern matching.
/// </summary>
public interface ITopicDiscoveryService
{
    /// <summary>
    /// Attempts to resolve a topic to a hierarchical path using existing configuration or discovery rules.
    /// </summary>
    /// <param name="topic">The topic to resolve</param>
    /// <param name="sourceType">The source system type (MQTT, Kafka, etc.)</param>
    /// <returns>A topic configuration with the resolved path, or null if no mapping could be determined</returns>
    Task<TopicConfiguration?> ResolveTopicAsync(string topic, string sourceType);

    /// <summary>
    /// Creates a new unverified topic configuration for an unknown topic.
    /// </summary>
    /// <param name="topic">The unknown topic</param>
    /// <param name="sourceType">The source system type</param>
    /// <param name="suggestedPath">Optional suggested hierarchical path</param>
    /// <returns>The newly created topic configuration</returns>
    Task<TopicConfiguration> CreateUnverifiedTopicAsync(string topic, string sourceType, HierarchicalPath? suggestedPath = null);

    /// <summary>
    /// Attempts to generate a hierarchical path from a topic using mapping rules.
    /// </summary>
    /// <param name="topic">The topic to generate a path for</param>
    /// <returns>A generated hierarchical path, or null if no rules match</returns>
    Task<HierarchicalPath?> GeneratePathFromTopicAsync(string topic);
}
