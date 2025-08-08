using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Repositories;
using UNSInfra.Services;
using UNSInfra.Models.Namespace;
using UNSInfra.Models.Hierarchy;

namespace UNSInfra.MCP.Server;

/// <summary>
/// Implementation of UNS MCP tools using ModelContextProtocol attributes
/// </summary>
[McpServerToolType]
public static class UnsMcpTools
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }; 
 
    [McpServerTool]
    [Description("Get the complete UNS hierarchy structure showing all namespaces and topics")]
    public static async Task<string> GetUnsHierarchyAsync(
        IMcpServer? thisServer,
        ITopicConfigurationRepository topicConfigurationRepository,
        INamespaceStructureService namespaceStructureService,
       /* ILogger logger, */
        CancellationToken cancellationToken = default)
    {
        try
        {
            //logger.LogDebug("Getting UNS hierarchy");
            
            // Get the actual UNS tree structure from namespace service
            var unsTree = await namespaceStructureService.GetNamespaceStructureAsync();
            
            // Get all topic configurations for additional metadata
            var allTopicConfigs = await topicConfigurationRepository.GetAllTopicConfigurationsAsync();
            var topicConfigsList = allTopicConfigs.ToList();
            
            // Build hierarchical tree structure
            var result = new
            {
                success = true,
                message = "UNS hierarchy tree retrieved successfully",
                totalTopics = topicConfigsList.Count,
                totalNamespaces = unsTree.Count(),
                unsHierarchy = BuildHierarchyTree(unsTree, topicConfigsList.Cast<UNSInfra.Models.Hierarchy.TopicConfiguration>())
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            //logger.LogError(ex, "Error getting UNS hierarchy");
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = $"Error retrieving UNS hierarchy: {ex.Message}",
                unsHierarchy = new { }
            }, _jsonOptions);
        }
    }
    
    private static object BuildHierarchyTree(IEnumerable<NSTreeNode> namespaceTree, IEnumerable<TopicConfiguration> allTopics)
    {
        return namespaceTree.Select(node => new
        {
            name = node.Name,
            fullPath = node.FullPath,
            nodeType = node.NodeType.ToString(),
            hierarchyNode = node.HierarchyNode?.Name,
            namespaceName = node.Namespace?.Name,
            canHaveHierarchyChildren = node.CanHaveHierarchyChildren,
            canHaveNamespaceChildren = node.CanHaveNamespaceChildren,
            children = node.Children.Any() ? BuildHierarchyTree(node.Children, allTopics) : null,
            topics = allTopics
                .Where(t => t.Path?.ToString().StartsWith(node.FullPath, StringComparison.OrdinalIgnoreCase) == true)
                .Select(t => new
                {
                    topic = t.Topic,
                    unsName = t.UNSName,
                    isVerified = t.IsVerified,
                    lastUpdated = t.ModifiedAt,
                    sourceType = t.SourceType
                }).ToArray()
        }).ToArray();
    }

    [McpServerTool]
    [Description("Get all topics for a specific namespace")]
    public static async Task<string> GetNamespaceTopicsAsync(
        IMcpServer? thisServer,
        ITopicConfigurationRepository topicConfigurationRepository,
        ILogger logger,
        [Description("The name of the namespace to get topics for")] string namespaceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting topics for namespace: {NamespaceName}", namespaceName);
            
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Namespace name is required",
                    namespaceName = namespaceName,
                    topics = Array.Empty<object>()
                }, _jsonOptions);
            }

            var allTopicConfigs = await topicConfigurationRepository.GetAllTopicConfigurationsAsync();
            var namespaceTopics = allTopicConfigs.Where(t => 
                t.Path?.ToString().Contains(namespaceName, StringComparison.OrdinalIgnoreCase) == true ||
                t.NSPath?.Contains(namespaceName, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            var result = new
            {
                success = true,
                message = $"Found {namespaceTopics.Count} topics for namespace '{namespaceName}'",
                namespaceName = namespaceName,
                topics = namespaceTopics.Select(t => new
                {
                    topic = t.Topic,
                    unsName = t.UNSName,
                    hierarchicalPath = t.Path?.ToString(),
                    nsPath = t.NSPath,
                    isVerified = t.IsVerified,
                    lastUpdated = t.ModifiedAt,
                    sourceType = t.SourceType
                }).ToArray()
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting topics for namespace: {NamespaceName}", namespaceName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = $"Error retrieving topics for namespace '{namespaceName}': {ex.Message}",
                namespaceName = namespaceName,
                topics = Array.Empty<object>()
            }, _jsonOptions);
        }
    }

    [McpServerTool]
    [Description("Get the current value of a specific topic")]
    public static async Task<string> GetTopicCurrentValueAsync(
        IMcpServer? thisServer,
        ITopicConfigurationRepository topicConfigurationRepository,
        IRealtimeStorage realtimeStorage,
        ILogger logger,
        [Description("The name of the topic to get the current value for")] string topicName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting current value for topic: {TopicName}", topicName);
            
            if (string.IsNullOrWhiteSpace(topicName))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Topic name is required",
                    topicName = topicName,
                    value = (object?)null
                }, _jsonOptions);
            }

            // First check if topic configuration exists
            var allTopics = await topicConfigurationRepository.GetAllTopicConfigurationsAsync();
            var topicConfig = allTopics.FirstOrDefault(t => t.Topic.Equals(topicName, StringComparison.OrdinalIgnoreCase));
            
            if (topicConfig == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Topic '{topicName}' not found",
                    topicName = topicName,
                    value = (object?)null
                }, _jsonOptions);
            }

            // Get current value from realtime storage
            var currentDataPoint = await realtimeStorage.GetLatestAsync(topicName);

            var result = new
            {
                success = true,
                message = "Topic value retrieved successfully",
                topicName = topicName,
                topic = new
                {
                    topic = topicConfig.Topic,
                    unsName = topicConfig.UNSName,
                    hierarchicalPath = topicConfig.Path?.ToString(),
                    currentValue = currentDataPoint?.Value,
                    lastUpdated = currentDataPoint?.Timestamp ?? topicConfig.ModifiedAt,
                    sourceType = topicConfig.SourceType,
                    isVerified = topicConfig.IsVerified
                }
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current value for topic: {TopicName}", topicName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = $"Error retrieving current value for topic '{topicName}': {ex.Message}",
                topicName = topicName,
                value = (object?)null
            }, _jsonOptions);
        }
    }

    [McpServerTool]
    [Description("Get historical data for a topic within a specified time range")]
    public static async Task<string> GetTopicHistoricalDataAsync(
        IMcpServer? thisServer,
        IHistoricalStorage historicalStorage,
        ILogger logger,
        [Description("The name of the topic to get historical data for")] string topicName,
        [Description("Start time for historical data (ISO 8601 format)")] DateTime startTime,
        [Description("End time for historical data (ISO 8601 format)")] DateTime endTime,
        [Description("Maximum number of data points to return (default: 1000)")] int maxPoints = 1000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting historical data for topic: {TopicName} from {StartTime} to {EndTime}", 
                topicName, startTime, endTime);
            
            if (string.IsNullOrWhiteSpace(topicName))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Topic name is required",
                    topicName = topicName,
                    data = Array.Empty<object>()
                }, _jsonOptions);
            }

            if (startTime >= endTime)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Start time must be before end time",
                    topicName = topicName,
                    startTime = startTime,
                    endTime = endTime,
                    data = Array.Empty<object>()
                }, _jsonOptions);
            }

            var historicalData = await historicalStorage.GetHistoryAsync(topicName, startTime, endTime);
            var dataList = historicalData.Take(maxPoints).ToList();
            
            var result = new
            {
                success = true,
                message = $"Retrieved {dataList.Count} historical data points for topic '{topicName}'",
                topicName = topicName,
                startTime = startTime,
                endTime = endTime,
                dataPointCount = dataList.Count,
                data = dataList.Select(d => new
                {
                    timestamp = d.Timestamp,
                    value = d.Value,
                    topic = d.Topic
                }).ToArray()
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting historical data for topic: {TopicName}", topicName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = $"Error retrieving historical data for topic '{topicName}': {ex.Message}",
                topicName = topicName,
                startTime = startTime,
                endTime = endTime,
                data = Array.Empty<object>()
            }, _jsonOptions);
        }
    }

    [McpServerTool]
    [Description("Search for topics by pattern with optional namespace filtering")]
    public static async Task<string> SearchTopicsAsync(
        IMcpServer? thisServer,
        ITopicConfigurationRepository topicConfigurationRepository,
        ILogger logger,
        [Description("Search pattern to match against topic names")] string pattern,
        [Description("Optional namespace name to filter results (can be null)")] string? namespaceName = null,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Searching topics with pattern: {Pattern}, namespace: {Namespace}", 
                pattern, namespaceName ?? "all");
            
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Search pattern is required",
                    pattern = pattern,
                    topics = Array.Empty<object>()
                }, _jsonOptions);
            }

            var allTopicConfigs = await topicConfigurationRepository.GetAllTopicConfigurationsAsync();
            var searchResults = allTopicConfigs.Where(t =>
            {
                // Check if topic name or UNS name matches pattern
                var topicMatches = t.Topic?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true;
                var unsNameMatches = t.UNSName?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true;
                var nameMatches = topicMatches || unsNameMatches;
                
                // If namespace filter is specified, apply it
                var namespaceMatches = string.IsNullOrWhiteSpace(namespaceName) || 
                    t.Path?.ToString().Contains(namespaceName, StringComparison.OrdinalIgnoreCase) == true ||
                    t.NSPath?.Contains(namespaceName, StringComparison.OrdinalIgnoreCase) == true;
                
                return nameMatches && namespaceMatches;
            })
            .Take(maxResults)
            .ToList();

            var result = new
            {
                success = true,
                message = $"Found {searchResults.Count} topics matching pattern '{pattern}'",
                pattern = pattern,
                namespaceName = namespaceName,
                resultCount = searchResults.Count,
                maxResults = maxResults,
                topics = searchResults.Select(t => new
                {
                    topic = t.Topic,
                    unsName = t.UNSName,
                    hierarchicalPath = t.Path?.ToString(),
                    nsPath = t.NSPath,
                    isVerified = t.IsVerified,
                    lastUpdated = t.ModifiedAt,
                    sourceType = t.SourceType
                }).ToArray()
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching topics with pattern: {Pattern}", pattern);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = $"Error searching topics with pattern '{pattern}': {ex.Message}",
                pattern = pattern,
                namespaceName = namespaceName,
                topics = Array.Empty<object>()
            }, _jsonOptions);
        }
    }
}