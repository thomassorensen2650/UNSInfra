using UNSInfra.Services.TopicBrowser;
using UNSInfra.Abstractions;
using UNSInfra.UI.GraphQL.Types;

namespace UNSInfra.UI.GraphQL;

/// <summary>
/// GraphQL query root for UNS Infrastructure
/// </summary>
public class Query
{
    /// <summary>
    /// Get all topics in the system
    /// </summary>
    public async Task<IEnumerable<TopicInfo>> GetTopicsAsync(
        [Service] CachedTopicBrowserService topicBrowserService,
        CancellationToken cancellationToken = default)
    {
        return await topicBrowserService.GetLatestTopicStructureAsync();
    }

    /// <summary>
    /// Get a specific topic by name
    /// </summary>
    public async Task<TopicInfo?> GetTopicAsync(
        string topicName,
        [Service] CachedTopicBrowserService topicBrowserService,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return null;

        var topics = await topicBrowserService.GetLatestTopicStructureAsync();
        return topics.FirstOrDefault(t => t.Topic.Equals(topicName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get topics by namespace
    /// </summary>
    public async Task<IEnumerable<TopicInfo>> GetTopicsByNamespaceAsync(
        string namespaceName,
        [Service] CachedTopicBrowserService topicBrowserService,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            return Enumerable.Empty<TopicInfo>();

        var topics = await topicBrowserService.GetLatestTopicStructureAsync();
        return topics.Where(t => !string.IsNullOrEmpty(t.NSPath) && 
                                t.NSPath.StartsWith(namespaceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all unique namespaces
    /// </summary>
    public async Task<IEnumerable<string>> GetNamespacesAsync(
        [Service] CachedTopicBrowserService topicBrowserService,
        CancellationToken cancellationToken = default)
    {
        var topics = await topicBrowserService.GetLatestTopicStructureAsync();
        return topics
            .Where(t => !string.IsNullOrEmpty(t.NSPath))
            .Select(t => t.NSPath!.Split('/')[0])
            .Distinct()
            .OrderBy(ns => ns);
    }

    /// <summary>
    /// Get system status and statistics
    /// </summary>
    public async Task<SystemStatus> GetSystemStatusAsync(
        [Service] CachedTopicBrowserService topicBrowserService,
        [Service] IConnectionManager connectionManager,
        CancellationToken cancellationToken = default)
    {
        var topics = await topicBrowserService.GetLatestTopicStructureAsync();
        var topicList = topics.ToList();
        
        var connectionConfigurations = connectionManager.GetAllConnectionConfigurations().ToList();
        var enabledConfigurations = connectionConfigurations.Where(c => c.IsEnabled).ToList();

        var connectionStats = new ConnectionStats
        {
            TotalConnections = connectionConfigurations.Count,
            ActiveConnections = enabledConfigurations.Count,
            InactiveConnections = connectionConfigurations.Count - enabledConfigurations.Count
        };

        return new SystemStatus
        {
            TotalTopics = topicList.Count,
            AssignedTopics = topicList.Count(t => !string.IsNullOrEmpty(t.NSPath)),
            ActiveTopics = topicList.Count(t => t.IsActive),
            TotalConnections = connectionStats.TotalConnections,
            ActiveConnections = connectionStats.ActiveConnections,
            Namespaces = topicList
                .Where(t => !string.IsNullOrEmpty(t.NSPath))
                .Select(t => t.NSPath!.Split('/')[0])
                .Distinct()
                .Count(),
            Timestamp = DateTime.UtcNow,
            ConnectionStats = connectionStats
        };
    }

    /// <summary>
    /// Search topics by name pattern
    /// </summary>
    public async Task<IEnumerable<TopicInfo>> SearchTopicsAsync(
        string searchTerm,
        [Service] CachedTopicBrowserService topicBrowserService,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Enumerable.Empty<TopicInfo>();

        var topics = await topicBrowserService.GetLatestTopicStructureAsync();
        return topics.Where(t => 
            t.Topic.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(t.UNSName) && t.UNSName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(t.NSPath) && t.NSPath.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
        );
    }

    /// <summary>
    /// Get topics that are currently active
    /// </summary>
    public async Task<IEnumerable<TopicInfo>> GetActiveTopicsAsync(
        [Service] CachedTopicBrowserService topicBrowserService,
        CancellationToken cancellationToken = default)
    {
        var topics = await topicBrowserService.GetLatestTopicStructureAsync();
        return topics.Where(t => t.IsActive);
    }

    /// <summary>
    /// Get topics by source type
    /// </summary>
    public async Task<IEnumerable<TopicInfo>> GetTopicsBySourceTypeAsync(
        string sourceType,
        [Service] CachedTopicBrowserService topicBrowserService,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return Enumerable.Empty<TopicInfo>();

        var topics = await topicBrowserService.GetLatestTopicStructureAsync();
        return topics.Where(t => t.SourceType.Equals(sourceType, StringComparison.OrdinalIgnoreCase));
    }
}