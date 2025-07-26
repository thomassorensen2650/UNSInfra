using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;

namespace UNSInfra.Services.TopicBrowser;

/// <summary>
/// Implementation of the topic browser service that provides real-time access to topic metadata and data.
/// </summary>
public class TopicBrowserService : ITopicBrowserService
{
    private readonly ITopicConfigurationRepository _topicRepository;
    private readonly IRealtimeStorage _realtimeStorage;
    private readonly IHistoricalStorage _historicalStorage;
    private readonly Dictionary<string, DateTime> _lastTopicCheck = new();

    public TopicBrowserService(
        ITopicConfigurationRepository topicRepository,
        IRealtimeStorage realtimeStorage,
        IHistoricalStorage historicalStorage)
    {
        _topicRepository = topicRepository;
        _realtimeStorage = realtimeStorage;
        _historicalStorage = historicalStorage;
    }

    /// <summary>
    /// Gets the latest topic structure with metadata including validation status.
    /// </summary>
    /// <returns>A collection of topic information with metadata</returns>
    public async Task<IEnumerable<TopicInfo>> GetLatestTopicStructureAsync()
    {
        var configurations = await _topicRepository.GetAllTopicConfigurationsAsync();
        
        // Fast conversion without individual storage calls for better performance
        var topicInfos = configurations.Select(config => new TopicInfo
        {
            Topic = config.Topic,
            Path = config.Path,
            IsVerified = config.IsVerified,
            IsActive = config.IsActive,
            SourceType = config.SourceType,
            CreatedAt = config.CreatedAt,
            ModifiedAt = config.ModifiedAt,
            Description = config.Description,
            Metadata = config.Metadata
            // LastDataTimestamp will be updated by data update events for performance
        }).ToList();

        return topicInfos;
    }

    /// <summary>
    /// Gets new topics that have been added since the last call.
    /// </summary>
    /// <param name="lastCheckTime">The timestamp of the last check</param>
    /// <returns>A collection of newly added topics</returns>
    public async Task<IEnumerable<TopicInfo>> GetNewTopicsAsync(DateTime lastCheckTime)
    {
        var allConfigurations = await _topicRepository.GetAllTopicConfigurationsAsync();
        var newTopics = allConfigurations.Where(c => c.CreatedAt > lastCheckTime);

        var topicInfos = new List<TopicInfo>();
        foreach (var config in newTopics)
        {
            var topicInfo = new TopicInfo
            {
                Topic = config.Topic,
                Path = config.Path,
                IsVerified = config.IsVerified,
                IsActive = config.IsActive,
                SourceType = config.SourceType,
                CreatedAt = config.CreatedAt,
                ModifiedAt = config.ModifiedAt,
                Description = config.Description,
                Metadata = config.Metadata
            };

            topicInfos.Add(topicInfo);
        }

        return topicInfos;
    }

    /// <summary>
    /// Gets the latest data payload for a specific topic.
    /// </summary>
    /// <param name="topic">The topic name</param>
    /// <returns>The latest data point for the topic, or null if not found</returns>
    public async Task<DataPoint?> GetDataForTopicAsync(string topic)
    {
        try
        {
            return await _realtimeStorage.GetLatestAsync(topic);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Event that fires when a new topic is added to the system.
    /// </summary>
    public event EventHandler<TopicAddedEventArgs>? TopicAdded;

    /// <summary>
    /// Event that fires when topic data is updated.
    /// </summary>
    public event EventHandler<TopicDataUpdatedEventArgs>? TopicDataUpdated;

    /// <summary>
    /// Raises the TopicAdded event.
    /// </summary>
    /// <param name="topicInfo">The topic information</param>
    protected virtual void OnTopicAdded(TopicInfo topicInfo)
    {
        TopicAdded?.Invoke(this, new TopicAddedEventArgs { TopicInfo = topicInfo });
    }

    /// <summary>
    /// Raises the TopicDataUpdated event.
    /// </summary>
    /// <param name="topic">The topic name</param>
    /// <param name="dataPoint">The updated data point</param>
    protected virtual void OnTopicDataUpdated(string topic, DataPoint dataPoint)
    {
        TopicDataUpdated?.Invoke(this, new TopicDataUpdatedEventArgs 
        { 
            Topic = topic, 
            DataPoint = dataPoint 
        });
    }

    /// <summary>
    /// Notifies the service that a new topic has been added.
    /// This method should be called by the topic discovery service.
    /// </summary>
    /// <param name="topicInfo">The topic information</param>
    public void NotifyTopicAdded(TopicInfo topicInfo)
    {
        OnTopicAdded(topicInfo);
    }

    /// <summary>
    /// Notifies the service that topic data has been updated.
    /// This method should be called by the data processing service.
    /// </summary>
    /// <param name="topic">The topic name</param>
    /// <param name="dataPoint">The updated data point</param>
    public void NotifyTopicDataUpdated(string topic, DataPoint dataPoint)
    {
        OnTopicDataUpdated(topic, dataPoint);
    }
} 