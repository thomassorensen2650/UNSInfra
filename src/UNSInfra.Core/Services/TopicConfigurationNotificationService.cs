namespace UNSInfra.Services;

/// <summary>
/// Implementation of topic configuration notification service
/// </summary>
public class TopicConfigurationNotificationService : ITopicConfigurationNotificationService
{
    public event EventHandler<TopicConfigurationChangedEventArgs>? TopicConfigurationChanged;

    public async Task NotifyTopicConfigurationChangedAsync(string topicName, TopicConfigurationChangeType changeType)
    {
        var args = new TopicConfigurationChangedEventArgs
        {
            TopicName = topicName,
            ChangeType = changeType
        };

        TopicConfigurationChanged?.Invoke(this, args);
        
        // Allow for async processing if needed
        await Task.CompletedTask;
    }
}