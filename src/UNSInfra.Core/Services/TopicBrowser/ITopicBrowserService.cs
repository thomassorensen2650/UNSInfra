using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Services.TopicBrowser;

/// <summary>
/// Service for browsing and monitoring topics in the UNS infrastructure.
/// Provides real-time access to topic metadata, data, and change notifications.
/// </summary>
public interface ITopicBrowserService
{
    /// <summary>
    /// Gets the latest topic structure with metadata including validation status.
    /// </summary>
    /// <returns>A collection of topic information with metadata</returns>
    Task<IEnumerable<TopicInfo>> GetLatestTopicStructureAsync();

    /// <summary>
    /// Gets new topics that have been added since the last call.
    /// </summary>
    /// <param name="lastCheckTime">The timestamp of the last check</param>
    /// <returns>A collection of newly added topics</returns>
    Task<IEnumerable<TopicInfo>> GetNewTopicsAsync(DateTime lastCheckTime);

    /// <summary>
    /// Gets the latest data payload for a specific topic.
    /// </summary>
    /// <param name="topic">The topic name</param>
    /// <returns>The latest data point for the topic, or null if not found</returns>
    Task<DataPoint?> GetDataForTopicAsync(string topic);

    /// <summary>
    /// Gets the topic configuration for a specific topic.
    /// </summary>
    /// <param name="topic">The topic name</param>
    /// <returns>The topic configuration, or null if not found</returns>
    Task<TopicConfiguration?> GetTopicConfigurationAsync(string topic);

    /// <summary>
    /// Verifies a topic configuration.
    /// </summary>
    /// <param name="topic">The topic name</param>
    /// <param name="verifiedBy">The user who verified the configuration</param>
    /// <returns>A task representing the asynchronous verification operation</returns>
    Task VerifyTopicAsync(string topic, string verifiedBy);

    /// <summary>
    /// Updates a topic configuration.
    /// </summary>
    /// <param name="configuration">The updated configuration</param>
    /// <returns>A task representing the asynchronous update operation</returns>
    Task UpdateTopicConfigurationAsync(TopicConfiguration configuration);

    /// <summary>
    /// Event that fires when a new topic is added to the system.
    /// </summary>
    event EventHandler<TopicAddedEventArgs> TopicAdded;

    /// <summary>
    /// Event that fires when topic data is updated.
    /// </summary>
    event EventHandler<TopicDataUpdatedEventArgs> TopicDataUpdated;
}

/// <summary>
/// Information about a topic including metadata and validation status.
/// </summary>
public class TopicInfo
{
    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hierarchical path for this topic.
    /// </summary>
    public HierarchicalPath Path { get; set; } = new();


    /// <summary>
    /// Gets or sets whether this topic is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the source type (MQTT, Kafka, etc.).
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this topic was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this topic was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the description of this topic.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this topic.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the latest data point for this topic.
    /// </summary>
    public DateTime? LastDataTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the namespace path this topic is assigned to.
    /// </summary>
    public string NSPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for this topic when used in UNS.
    /// </summary>
    public string UNSName { get; set; } = string.Empty;
}

/// <summary>
/// Event arguments for when a new topic is added.
/// </summary>
public class TopicAddedEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the information about the newly added topic.
    /// </summary>
    public TopicInfo TopicInfo { get; set; } = new();
}

/// <summary>
/// Event arguments for when topic data is updated.
/// </summary>
public class TopicDataUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the updated data point.
    /// </summary>
    public DataPoint DataPoint { get; set; } = new();
} 