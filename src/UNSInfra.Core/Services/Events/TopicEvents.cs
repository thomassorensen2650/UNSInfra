using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.AutoMapping;

namespace UNSInfra.Services.Events;

/// <summary>
/// Event fired when a new topic is discovered and added to the system
/// </summary>
/// <param name="Topic">The topic name</param>
/// <param name="Path">The hierarchical path</param>
/// <param name="SourceType">The source type (MQTT, SocketIO, etc.)</param>
/// <param name="CreatedAt">When the topic was created</param>
public record TopicAddedEvent(
    string Topic,
    HierarchicalPath Path,
    string SourceType,
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
    IReadOnlyList<(string Topic, HierarchicalPath Path, DateTime CreatedAt)> Topics,
    string SourceType
) : BaseEvent;

/// <summary>
/// Event published when a topic is successfully auto-mapped to a UNS path
/// </summary>
/// <param name="Topic">The original topic name</param>
/// <param name="SourceType">The data source type</param>
/// <param name="MappedNamespace">The mapped namespace path</param>
/// <param name="Confidence">Mapping confidence score (0.0 to 1.0)</param>
/// <param name="TopicConfiguration">The resulting topic configuration</param>
public record TopicAutoMappedEvent(
    string Topic,
    string SourceType,
    string MappedNamespace,
    double Confidence,
    TopicConfiguration? TopicConfiguration
) : BaseEvent;

/// <summary>
/// Event published when auto-mapping fails for a topic
/// </summary>
/// <param name="Topic">The topic that failed to map</param>
/// <param name="SourceType">The data source type</param>
/// <param name="Reason">The reason for mapping failure</param>
public record TopicAutoMappingFailedEvent(
    string Topic,
    string SourceType,
    string Reason
) : BaseEvent;

/// <summary>
/// Event published when the UNS namespace structure changes
/// </summary>
/// <param name="ChangedNamespace">The namespace that changed</param>
/// <param name="ChangeType">The type of change (Added, Modified, Deleted)</param>
/// <param name="ChangedBy">Who made the change</param>
public record NamespaceStructureChangedEvent(
    string ChangedNamespace,
    string ChangeType,
    string? ChangedBy
) : BaseEvent;