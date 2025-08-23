using UNSInfra.Models.Data;

namespace UNSInfra.Services.TopicBrowser.Events;

/// <summary>
/// Event args for when topic structure changes occur.
/// </summary>
public class TopicStructureChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change that occurred.
    /// </summary>
    public TopicChangeType ChangeType { get; set; }
    
    /// <summary>
    /// Topics that were affected by this change.
    /// </summary>
    public IEnumerable<string> AffectedTopics { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// The namespace that was affected (if applicable).
    /// </summary>
    public string? AffectedNamespace { get; set; }
    
    /// <summary>
    /// Additional metadata about the change.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// Timestamp when the change occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of topic structure changes.
/// </summary>
public enum TopicChangeType
{
    /// <summary>
    /// One or more topics were added to the system.
    /// </summary>
    TopicsAdded,
    
    /// <summary>
    /// One or more topics were updated (e.g., namespace assignment).
    /// </summary>
    TopicsUpdated,
    
    /// <summary>
    /// One or more topics were removed from the system.
    /// </summary>
    TopicsRemoved,
    
    /// <summary>
    /// A namespace was created or modified.
    /// </summary>
    NamespaceChanged,
    
    /// <summary>
    /// Topics were auto-mapped to namespaces.
    /// </summary>
    TopicsAutoMapped,
    
    /// <summary>
    /// Full structure refresh (fallback case).
    /// </summary>
    FullRefresh
}