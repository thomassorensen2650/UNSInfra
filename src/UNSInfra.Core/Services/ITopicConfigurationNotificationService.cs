namespace UNSInfra.Services;

/// <summary>
/// Service for notifying components when topic configurations change
/// </summary>
public interface ITopicConfigurationNotificationService
{
    /// <summary>
    /// Event raised when a topic configuration is updated
    /// </summary>
    event EventHandler<TopicConfigurationChangedEventArgs>? TopicConfigurationChanged;
    
    /// <summary>
    /// Notify all subscribers that a topic configuration has been updated
    /// </summary>
    /// <param name="topicName">The topic that was updated</param>
    /// <param name="changeType">The type of change that occurred</param>
    Task NotifyTopicConfigurationChangedAsync(string topicName, TopicConfigurationChangeType changeType);
}

/// <summary>
/// Event arguments for topic configuration changes
/// </summary>
public class TopicConfigurationChangedEventArgs : EventArgs
{
    public string TopicName { get; set; } = string.Empty;
    public TopicConfigurationChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of topic configuration changes
/// </summary>
public enum TopicConfigurationChangeType
{
    Created,
    Updated,
    Deleted,
    NamespaceAssignmentChanged,
    UNSNameChanged
}