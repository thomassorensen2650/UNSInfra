using GraphQL;
using UNSInfra.MCP.Server;

namespace UNSInfra.MCP.Server.Tests;

/// <summary>
/// Helper class for creating test data for MCP server tests
/// </summary>
public static class TestDataHelpers
{
    /// <summary>
    /// Creates a sample GraphQL response with topic data
    /// </summary>
    public static GraphQLResponse<dynamic> CreateSampleTopicsResponse(params TopicNode[] topics)
    {
        var topicsData = topics.Select(topic => new
        {
            topic = topic.Topic,
            unsName = topic.UnsName,
            nsPath = topic.NsPath,
            path = topic.Path,
            isActive = topic.IsActive,
            sourceType = topic.SourceType,
            createdAt = topic.CreatedAt,
            modifiedAt = topic.ModifiedAt,
            lastDataTimestamp = topic.LastDataTimestamp,
            description = topic.Description,
            metadata = topic.Metadata
        }).ToArray();

        return new GraphQLResponse<dynamic>
        {
            Data = new
            {
                systemStatus = CreateSampleSystemStatus(),
                namespaces = new[] { "Enterprise", "Factory", "Test" },
                topics = topicsData
            }
        };
    }

    /// <summary>
    /// Creates a sample system status object
    /// </summary>
    public static object CreateSampleSystemStatus()
    {
        return new
        {
            totalTopics = 50,
            assignedTopics = 45,
            activeTopics = 40,
            totalConnections = 3,
            activeConnections = 2,
            namespaces = 3,
            timestamp = "2024-01-01T12:00:00Z",
            connectionStats = new
            {
                totalConnections = 3,
                activeConnections = 2,
                inactiveConnections = 1
            }
        };
    }

    /// <summary>
    /// Creates a sample topic node with default values
    /// </summary>
    public static TopicNode CreateSampleTopic(
        string topic = "temperature_sensor_001",
        string nsPath = "Enterprise/Site1/Area1",
        string sourceType = "MQTT",
        bool isActive = true,
        string? description = "Sample temperature sensor")
    {
        var displayName = topic.Replace("_", " ").Replace("-", " ");
        
        return new TopicNode
        {
            Topic = topic,
            UnsName = $"{nsPath.Replace("/", " ")} {displayName}",
            NsPath = nsPath,
            Path = $"{nsPath}/{topic}",
            IsActive = isActive,
            SourceType = sourceType,
            CreatedAt = "2024-01-01T10:00:00Z",
            ModifiedAt = "2024-01-01T11:00:00Z",
            LastDataTimestamp = "2024-01-01T11:30:00Z",
            Description = description,
            Metadata = new { Unit = "Celsius", Precision = 0.1 }
        };
    }

    /// <summary>
    /// Creates multiple sample topics for testing hierarchy building
    /// </summary>
    public static List<TopicNode> CreateHierarchicalTopics()
    {
        return new List<TopicNode>
        {
            CreateSampleTopic("temp_001", "Enterprise/Site1/Area1", "MQTT", true, "Temperature sensor in Area 1"),
            CreateSampleTopic("pressure_001", "Enterprise/Site1/Area1", "MQTT", true, "Pressure sensor in Area 1"),
            CreateSampleTopic("flow_001", "Enterprise/Site1/Area1", "MQTT", false, "Flow sensor in Area 1"),
            CreateSampleTopic("temp_002", "Enterprise/Site1/Area2", "SocketIO", true, "Temperature sensor in Area 2"),
            CreateSampleTopic("temp_003", "Enterprise/Site2/Area1", "MQTT", true, "Temperature sensor in Site 2"),
            CreateSampleTopic("counter_001", "Factory/Line1/Station1", "MQTT", true, "Production counter"),
            CreateSampleTopic("quality_sensor", "Factory/Line1/Station2", "SocketIO", false, "Quality sensor"),
            CreateSampleTopic("speed_monitor", "Factory/Line2", "MQTT", true, "Line speed monitor"),
            CreateSampleTopic("simple_topic", "", "Mock", true, "Simple unassigned topic"),
            CreateSampleTopic("another_topic", "", "Test", false, "Another unassigned topic")
        };
    }

    /// <summary>
    /// Creates a GraphQL response with errors
    /// </summary>
    public static GraphQLResponse<dynamic> CreateErrorResponse(string errorMessage = "Test error")
    {
        var response = new GraphQLResponse<dynamic>();
        
        // Use reflection to set the Errors property if needed, or create a minimal mock
        // For now, we'll create a response that will trigger the error handling path
        var errorList = new List<GraphQL.GraphQLError>();
        
        // Create error using available constructor - Message property
        try
        {
            // Try to create the error - different versions may have different constructors
            var error = Activator.CreateInstance(typeof(GraphQL.GraphQLError), errorMessage);
            if (error is GraphQL.GraphQLError gqlError)
            {
                errorList.Add(gqlError);
            }
        }
        catch
        {
            // If constructor fails, we'll simulate error condition differently in tests
        }
        
        response.Errors = errorList.ToArray();
        return response;
    }

    /// <summary>
    /// Creates a GraphQL response for a single topic query
    /// </summary>
    public static GraphQLResponse<dynamic> CreateSingleTopicResponse(TopicNode? topic = null)
    {
        if (topic == null)
        {
            return new GraphQLResponse<dynamic>
            {
                Data = new { topic = (object?)null }
            };
        }

        return new GraphQLResponse<dynamic>
        {
            Data = new
            {
                topic = new
                {
                    topic = topic.Topic,
                    unsName = topic.UnsName,
                    nsPath = topic.NsPath,
                    path = topic.Path,
                    isActive = topic.IsActive,
                    sourceType = topic.SourceType,
                    createdAt = topic.CreatedAt,
                    modifiedAt = topic.ModifiedAt,
                    lastDataTimestamp = topic.LastDataTimestamp,
                    description = topic.Description,
                    metadata = topic.Metadata
                }
            }
        };
    }

    /// <summary>
    /// Creates a GraphQL response for topics by namespace query
    /// </summary>
    public static GraphQLResponse<dynamic> CreateTopicsByNamespaceResponse(string namespaceName, params TopicNode[] topics)
    {
        var filteredTopics = topics
            .Where(t => t.NsPath?.StartsWith(namespaceName, StringComparison.OrdinalIgnoreCase) == true)
            .Select(topic => new
            {
                topic = topic.Topic,
                unsName = topic.UnsName,
                nsPath = topic.NsPath,
                path = topic.Path,
                isActive = topic.IsActive,
                sourceType = topic.SourceType,
                createdAt = topic.CreatedAt,
                modifiedAt = topic.ModifiedAt,
                lastDataTimestamp = topic.LastDataTimestamp,
                description = topic.Description,
                metadata = topic.Metadata
            }).ToArray();

        return new GraphQLResponse<dynamic>
        {
            Data = new
            {
                topicsByNamespace = filteredTopics
            }
        };
    }

    /// <summary>
    /// Creates a GraphQL response for search topics query
    /// </summary>
    public static GraphQLResponse<dynamic> CreateSearchTopicsResponse(string searchTerm, params TopicNode[] topics)
    {
        var matchingTopics = topics
            .Where(t => t.Topic.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                       (t.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true))
            .Select(topic => new
            {
                topic = topic.Topic,
                unsName = topic.UnsName,
                nsPath = topic.NsPath,
                path = topic.Path,
                isActive = topic.IsActive,
                sourceType = topic.SourceType,
                createdAt = topic.CreatedAt,
                modifiedAt = topic.ModifiedAt,
                lastDataTimestamp = topic.LastDataTimestamp,
                description = topic.Description,
                metadata = topic.Metadata
            }).ToArray();

        return new GraphQLResponse<dynamic>
        {
            Data = new
            {
                searchTopics = matchingTopics
            }
        };
    }

    /// <summary>
    /// Creates a GraphQL response for system status query
    /// </summary>
    public static GraphQLResponse<dynamic> CreateSystemStatusResponse(
        int totalTopics = 100,
        int activeTopics = 85,
        int totalConnections = 5,
        int activeConnections = 3)
    {
        return new GraphQLResponse<dynamic>
        {
            Data = new
            {
                systemStatus = new
                {
                    totalTopics,
                    assignedTopics = totalTopics - 5,
                    activeTopics,
                    totalConnections,
                    activeConnections,
                    namespaces = 3,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    connectionStats = new
                    {
                        totalConnections,
                        activeConnections,
                        inactiveConnections = totalConnections - activeConnections
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a simple GraphQL response for connectivity testing
    /// </summary>
    public static GraphQLResponse<dynamic> CreateConnectivityTestResponse()
    {
        return new GraphQLResponse<dynamic>
        {
            Data = new
            {
                namespaces = new[] { "Enterprise", "Factory", "Test" }
            }
        };
    }
}