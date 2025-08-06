using System.Text.Json;
using UNSInfra.MCP.Server.Models;
using UNSInfra.Services;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Models.Data;
using UNSInfra.Models.Namespace;

namespace UNSInfra.MCP.Server.Services;

/// <summary>
/// MCP service for UNS hierarchy and data querying
/// </summary>
public class UnsMcpService
{
    private readonly INamespaceStructureService _namespaceService;
    private readonly ITopicBrowserService _topicBrowserService;
    private readonly IRealtimeStorage _realtimeStorage;
    private readonly IHistoricalStorage _historicalStorage;
    private readonly ILogger<UnsMcpService> _logger;

    public UnsMcpService(
        INamespaceStructureService namespaceService,
        ITopicBrowserService topicBrowserService,
        IRealtimeStorage realtimeStorage,
        IHistoricalStorage historicalStorage,
        ILogger<UnsMcpService> logger)
    {
        _namespaceService = namespaceService;
        _topicBrowserService = topicBrowserService;
        _realtimeStorage = realtimeStorage;
        _historicalStorage = historicalStorage;
        _logger = logger;
    }

    /// <summary>
    /// Get available MCP tools
    /// </summary>
    public List<McpTool> GetAvailableTools()
    {
        return new List<McpTool>
        {
            new McpTool
            {
                Name = "get_uns_hierarchy",
                Description = "Get the complete UNS (Unified Namespace) hierarchy including hierarchy nodes and namespaces",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        includeTopic = new
                        {
                            type = "boolean",
                            description = "Whether to include topics assigned to namespaces",
                            @default = false
                        },
                        includeCurrentValues = new
                        {
                            type = "boolean", 
                            description = "Whether to include current values for topics",
                            @default = false
                        }
                    }
                }
            },
            new McpTool
            {
                Name = "get_namespace_topics",
                Description = "Get all topics assigned to a specific namespace",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        namespacePath = new
                        {
                            type = "string",
                            description = "The full path to the namespace (e.g., 'Enterprise1/Site1/Area1/MyNamespace')"
                        },
                        includeCurrentValues = new
                        {
                            type = "boolean",
                            description = "Whether to include current values for topics",
                            @default = false
                        }
                    },
                    required = new[] { "namespacePath" }
                }
            },
            new McpTool
            {
                Name = "get_topic_current_value",
                Description = "Get the current value of a specific topic",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        topic = new
                        {
                            type = "string",
                            description = "The topic name to query"
                        }
                    },
                    required = new[] { "topic" }
                }
            },
            new McpTool
            {
                Name = "get_topic_historical_data",
                Description = "Get historical data for a topic within a time range",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        topic = new
                        {
                            type = "string",
                            description = "The topic name to query"
                        },
                        startTime = new
                        {
                            type = "string",
                            format = "date-time",
                            description = "Start time for the query (ISO 8601 format)"
                        },
                        endTime = new
                        {
                            type = "string",
                            format = "date-time", 
                            description = "End time for the query (ISO 8601 format)"
                        },
                        maxPoints = new
                        {
                            type = "integer",
                            description = "Maximum number of data points to return",
                            @default = 1000
                        },
                        aggregation = new
                        {
                            type = "string",
                            description = "Aggregation method (avg, min, max, first, last)",
                            @enum = new[] { "avg", "min", "max", "first", "last" }
                        }
                    },
                    required = new[] { "topic", "startTime", "endTime" }
                }
            },
            new McpTool
            {
                Name = "search_topics",
                Description = "Search for topics by name or path pattern",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        searchPattern = new
                        {
                            type = "string",
                            description = "Search pattern for topic names (supports wildcards)"
                        },
                        sourceType = new
                        {
                            type = "string",
                            description = "Filter by data source type (e.g., 'MQTT', 'SocketIO')"
                        },
                        includeCurrentValues = new
                        {
                            type = "boolean",
                            description = "Whether to include current values for topics",
                            @default = false
                        }
                    },
                    required = new[] { "searchPattern" }
                }
            }
        };
    }

    /// <summary>
    /// Execute an MCP tool call
    /// </summary>
    public async Task<McpToolResult> ExecuteToolAsync(McpToolCall toolCall)
    {
        try
        {
            return toolCall.Name switch
            {
                "get_uns_hierarchy" => await GetUnsHierarchyAsync(toolCall.Arguments),
                "get_namespace_topics" => await GetNamespaceTopicsAsync(toolCall.Arguments),
                "get_topic_current_value" => await GetTopicCurrentValueAsync(toolCall.Arguments),
                "get_topic_historical_data" => await GetTopicHistoricalDataAsync(toolCall.Arguments),
                "search_topics" => await SearchTopicsAsync(toolCall.Arguments),
                _ => new McpToolResult
                {
                    IsError = true,
                    Content = new List<McpContent>
                    {
                        new McpContent
                        {
                            Type = "text",
                            Text = $"Unknown tool: {toolCall.Name}"
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Error executing {toolCall.Name}: {ex.Message}"
                    }
                }
            };
        }
    }

    private async Task<McpToolResult> GetUnsHierarchyAsync(Dictionary<string, object> arguments)
    {
        var includeTopics = GetBooleanArgument(arguments, "includeTopics", false);
        var includeCurrentValues = GetBooleanArgument(arguments, "includeCurrentValues", false);

        var nsStructure = await _namespaceService.GetNamespaceStructureAsync();
        var hierarchyNodes = new List<UnsHierarchyNode>();

        foreach (var node in nsStructure)
        {
            var hierarchyNode = await ConvertToUnsHierarchyNodeAsync(node, includeTopics, includeCurrentValues);
            hierarchyNodes.Add(hierarchyNode);
        }

        var jsonResult = JsonSerializer.Serialize(hierarchyNodes, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = jsonResult
                }
            }
        };
    }

    private async Task<McpToolResult> GetNamespaceTopicsAsync(Dictionary<string, object> arguments)
    {
        var namespacePath = GetStringArgument(arguments, "namespacePath");
        var includeCurrentValues = GetBooleanArgument(arguments, "includeCurrentValues", false);

        if (string.IsNullOrEmpty(namespacePath))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = "namespacePath is required"
                    }
                }
            };
        }

        var topics = await _topicBrowserService.GetTopicsForNamespaceAsync(namespacePath);
        var unsTopics = new List<UnsTopic>();

        foreach (var topic in topics)
        {
            var unsTopic = await ConvertToUnsTopicAsync(topic, includeCurrentValues);
            unsTopics.Add(unsTopic);
        }

        var jsonResult = JsonSerializer.Serialize(unsTopics, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = jsonResult
                }
            }
        };
    }

    private async Task<McpToolResult> GetTopicCurrentValueAsync(Dictionary<string, object> arguments)
    {
        var topicName = GetStringArgument(arguments, "topic");

        if (string.IsNullOrEmpty(topicName))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = "topic is required"
                    }
                }
            };
        }

        var currentValue = await _realtimeStorage.GetLatestAsync(topicName);
        var dataPoint = currentValue != null ? ConvertToUnsDataPoint(currentValue) : null;

        var result = new
        {
            topic = topicName,
            currentValue = dataPoint,
            timestamp = DateTime.UtcNow
        };

        var jsonResult = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = jsonResult
                }
            }
        };
    }

    private async Task<McpToolResult> GetTopicHistoricalDataAsync(Dictionary<string, object> arguments)
    {
        var topicName = GetStringArgument(arguments, "topic");
        var startTimeStr = GetStringArgument(arguments, "startTime");
        var endTimeStr = GetStringArgument(arguments, "endTime");
        var maxPoints = GetIntegerArgument(arguments, "maxPoints", 1000);
        var aggregation = GetStringArgument(arguments, "aggregation");

        if (string.IsNullOrEmpty(topicName) || string.IsNullOrEmpty(startTimeStr) || string.IsNullOrEmpty(endTimeStr))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = "topic, startTime, and endTime are required"
                    }
                }
            };
        }

        if (!DateTime.TryParse(startTimeStr, out var startTime) || !DateTime.TryParse(endTimeStr, out var endTime))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = "Invalid date format. Use ISO 8601 format (e.g., '2024-01-01T00:00:00Z')"
                    }
                }
            };
        }

        var historicalData = await _historicalStorage.GetHistoryAsync(topicName, startTime, endTime);
        var dataPoints = historicalData.Take(maxPoints).Select(ConvertToUnsDataPoint).ToList();

        var result = new HistoricalDataResponse
        {
            Topic = topicName,
            StartTime = startTime,
            EndTime = endTime,
            DataPoints = dataPoints,
            TotalPoints = dataPoints.Count()
        };

        var jsonResult = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = jsonResult
                }
            }
        };
    }

    private async Task<McpToolResult> SearchTopicsAsync(Dictionary<string, object> arguments)
    {
        var searchPattern = GetStringArgument(arguments, "searchPattern");
        var sourceType = GetStringArgument(arguments, "sourceType");
        var includeCurrentValues = GetBooleanArgument(arguments, "includeCurrentValues", false);

        if (string.IsNullOrEmpty(searchPattern))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = "searchPattern is required"
                    }
                }
            };
        }

        var allTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
        var filteredTopics = allTopics.Where(t => 
            (string.IsNullOrEmpty(sourceType) || t.SourceType == sourceType) &&
            (t.Topic.Contains(searchPattern, StringComparison.OrdinalIgnoreCase) ||
             (t.UNSName?.Contains(searchPattern, StringComparison.OrdinalIgnoreCase) ?? false))
        ).ToList();

        var unsTopics = new List<UnsTopic>();
        foreach (var topic in filteredTopics)
        {
            var unsTopic = await ConvertToUnsTopicAsync(topic, includeCurrentValues);
            unsTopics.Add(unsTopic);
        }

        var result = new
        {
            searchPattern,
            sourceType,
            totalResults = unsTopics.Count,
            topics = unsTopics
        };

        var jsonResult = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = jsonResult
                }
            }
        };
    }

    // Helper methods for converting domain models to MCP models
    private async Task<UnsHierarchyNode> ConvertToUnsHierarchyNodeAsync(NSTreeNode node, bool includeTopics, bool includeCurrentValues)
    {
        var hierarchyNode = new UnsHierarchyNode
        {
            Name = node.Name,
            FullPath = node.FullPath,
            NodeType = node.NodeType.ToString(),
            CanHaveHierarchyChildren = node.CanHaveHierarchyChildren,
            CanHaveNamespaceChildren = node.CanHaveNamespaceChildren
        };

        // Add child hierarchy nodes
        foreach (var child in node.Children)
        {
            var childNode = await ConvertToUnsHierarchyNodeAsync(child, includeTopics, includeCurrentValues);
            hierarchyNode.Children.Add(childNode);
        }

        // Add namespaces if this is a namespace node
        if (node.NodeType == NSNodeType.Namespace && node.Namespace != null)
        {
            var unsNamespace = new UnsNamespace
            {
                Id = node.Namespace.Id,
                Name = node.Namespace.Name,
                Description = node.Namespace.Description ?? string.Empty,
                Type = node.Namespace.Type.ToString(),
                HierarchicalPath = node.Namespace.HierarchicalPath?.GetFullPath() ?? string.Empty,
                IsActive = node.Namespace.IsActive,
                CreatedAt = node.Namespace.CreatedAt
            };

            if (includeTopics)
            {
                var topics = await _topicBrowserService.GetTopicsForNamespaceAsync(node.FullPath);
                foreach (var topic in topics)
                {
                    var unsTopic = await ConvertToUnsTopicAsync(topic, includeCurrentValues);
                    unsNamespace.Topics.Add(unsTopic);
                }
            }

            hierarchyNode.Namespaces.Add(unsNamespace);
        }

        return hierarchyNode;
    }

    private async Task<UnsTopic> ConvertToUnsTopicAsync(TopicInfo topic, bool includeCurrentValue)
    {
        var unsTopic = new UnsTopic
        {
            Topic = topic.Topic,
            UnsName = topic.UNSName ?? string.Empty,
            NsPath = topic.NSPath ?? string.Empty,
            SourceType = topic.SourceType,
            Description = topic.Description ?? string.Empty,
            CreatedAt = topic.CreatedAt
        };

        if (includeCurrentValue)
        {
            var currentValue = await _realtimeStorage.GetLatestAsync(topic.Topic);
            if (currentValue != null)
            {
                unsTopic = unsTopic with { CurrentValue = ConvertToUnsDataPoint(currentValue) };
            }
        }

        return unsTopic;
    }

    private static UnsDataPoint ConvertToUnsDataPoint(DataPoint dataPoint)
    {
        return new UnsDataPoint
        {
            Value = dataPoint.Value,
            Timestamp = dataPoint.Timestamp,
            Quality = "Good", // DataPoint doesn't have Quality property in this version
            SourceTimestamp = null // DataPoint doesn't have SourceTimestamp property in this version
        };
    }

    // Helper methods for extracting arguments
    private static string GetStringArgument(Dictionary<string, object> arguments, string key, string defaultValue = "")
    {
        if (arguments.TryGetValue(key, out var value))
        {
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    private static bool GetBooleanArgument(Dictionary<string, object> arguments, string key, bool defaultValue = false)
    {
        if (arguments.TryGetValue(key, out var value))
        {
            if (value is bool boolValue) return boolValue;
            if (bool.TryParse(value?.ToString(), out var parsedBool)) return parsedBool;
        }
        return defaultValue;
    }

    private static int GetIntegerArgument(Dictionary<string, object> arguments, string key, int defaultValue = 0)
    {
        if (arguments.TryGetValue(key, out var value))
        {
            if (value is int intValue) return intValue;
            if (int.TryParse(value?.ToString(), out var parsedInt)) return parsedInt;
        }
        return defaultValue;
    }
}