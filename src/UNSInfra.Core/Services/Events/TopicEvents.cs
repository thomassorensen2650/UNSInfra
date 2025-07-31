using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Services.Events;

/// <summary>
/// Event fired when a new topic is discovered and added to the system
/// </summary>
/// <param name="Topic">The topic name</param>
/// <param name="Path">The hierarchical path</param>
/// <param name="SourceType">The source type (MQTT, SocketIO, etc.)</param>
/// <param name="IsVerified">Whether the topic is verified</param>
/// <param name="CreatedAt">When the topic was created</param>
public record TopicAddedEvent(
    string Topic,
    HierarchicalPath Path,
    string SourceType,
    bool IsVerified,
    DateTime CreatedAt
) : BaseEvent;

/// <summary>
/// Event fired when topic data is updated
/// </summary>
/// <param name="Topic">The topic name</param>
/// <param name="DataPoint">The updated data point</param>
/// <param name="SourceType">The source type</param>
public record TopicDataUpdatedEvent(
    string Topic,
    DataPoint DataPoint,
    string SourceType
) : BaseEvent;

/// <summary>
/// Event fired when a topic is verified by an administrator
/// </summary>
/// <param name="Topic">The topic name</param>
/// <param name="VerifiedBy">Who verified the topic</param>
/// <param name="VerifiedAt">When the topic was verified</param>
public record TopicVerifiedEvent(
    string Topic,
    string VerifiedBy,
    DateTime VerifiedAt
) : BaseEvent;

/// <summary>
/// Event fired when topic configuration is updated
/// </summary>
/// <param name="Topic">The topic name</param>
/// <param name="OldPath">The old hierarchical path</param>
/// <param name="NewPath">The new hierarchical path</param>
/// <param name="UpdatedBy">Who updated the configuration</param>
public record TopicConfigurationUpdatedEvent(
    string Topic,
    HierarchicalPath OldPath,
    HierarchicalPath NewPath,
    string UpdatedBy
) : BaseEvent;

/// <summary>
/// Bulk event for efficient UI updates when many topics are processed
/// </summary>
/// <param name="Topics">List of topics that were added</param>
/// <param name="SourceType">The source type</param>
public record BulkTopicsAddedEvent(
    IReadOnlyList<(string Topic, HierarchicalPath Path, bool IsVerified, DateTime CreatedAt)> Topics,
    string SourceType
) : BaseEvent;