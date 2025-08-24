using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using GraphQL;
using GraphQL.Client.Abstractions;

namespace UNSInfra.MCP.Server;

/// <summary>
/// GraphQL-powered implementation of UNS MCP tools
/// </summary>
[McpServerToolType]
public static class GraphQLMcpTools
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
        IGraphQLClient graphQLClient,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting UNS hierarchy via GraphQL");

            var query = new GraphQLRequest
            {
                Query = @"
                query {
                    systemStatus {
                        totalTopics
                        assignedTopics
                        activeTopics
                        namespaces
                        timestamp
                    }
                    namespaces
                }"
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                logger.LogError("GraphQL query failed: {Errors}", errorMessages);
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"GraphQL query failed: {errorMessages}",
                    hierarchyData = (object?)null
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "UNS hierarchy retrieved successfully via GraphQL",
                hierarchyData = response.Data
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving UNS hierarchy via GraphQL");
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                error = ex.Message,
                hierarchyData = (object?)null
            }, _jsonOptions);
        }
    }

    [McpServerTool]
    [Description("Get the current information for a specific topic")]
    public static async Task<string> GetTopicAsync(
        IMcpServer? thisServer,
        IGraphQLClient graphQLClient,
        ILogger logger,
        [Description("The name of the topic to get information for")] string topicName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting topic information via GraphQL for: {TopicName}", topicName);

            if (string.IsNullOrWhiteSpace(topicName))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Topic name is required",
                    topicName = topicName,
                    topic = (object?)null
                }, _jsonOptions);
            }

            var query = new GraphQLRequest
            {
                Query = @"
                query($topicName: String!) {
                    topic(topicName: $topicName) {
                        topic
                        unsName
                        nsPath
                        path
                        isActive
                        sourceType
                        createdAt
                        modifiedAt
                        lastDataTimestamp
                        description
                    }
                }",
                Variables = new { topicName }
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                logger.LogError("GraphQL query failed for topic {TopicName}: {Errors}", topicName, errorMessages);
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"GraphQL query failed: {errorMessages}",
                    topicName = topicName,
                    topic = (object?)null
                }, _jsonOptions);
            }

            var topicData = response.Data?.topic;
            if (topicData == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Topic '{topicName}' not found",
                    topicName = topicName,
                    topic = (object?)null
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Topic information retrieved successfully via GraphQL",
                topicName = topicName,
                topic = topicData
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting topic information via GraphQL for: {TopicName}", topicName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                topicName = topicName,
                error = ex.Message,
                topic = (object?)null
            }, _jsonOptions);
        }
    }

    [McpServerTool]
    [Description("Get topics by namespace")]
    public static async Task<string> GetTopicsByNamespaceAsync(
        IMcpServer? thisServer,
        IGraphQLClient graphQLClient,
        ILogger logger,
        [Description("The name of the namespace to get topics for")] string namespaceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting topics by namespace via GraphQL: {NamespaceName}", namespaceName);

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

            var query = new GraphQLRequest
            {
                Query = @"
                query($namespaceName: String!) {
                    topicsByNamespace(namespaceName: $namespaceName) {
                        topic
                        unsName
                        nsPath
                        path
                        isActive
                        sourceType
                        createdAt
                        modifiedAt
                        lastDataTimestamp
                        description
                    }
                }",
                Variables = new { namespaceName }
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                logger.LogError("GraphQL query failed for namespace {NamespaceName}: {Errors}", namespaceName, errorMessages);
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"GraphQL query failed: {errorMessages}",
                    namespaceName = namespaceName,
                    topics = Array.Empty<object>()
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Topics for namespace '{namespaceName}' retrieved successfully via GraphQL",
                namespaceName = namespaceName,
                topics = response.Data?.topicsByNamespace ?? Array.Empty<object>()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving topics for namespace: {NamespaceName}", namespaceName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                namespaceName = namespaceName,
                error = ex.Message,
                topics = Array.Empty<object>()
            }, _jsonOptions);
        }
    }

    [McpServerTool]
    [Description("Search topics by name pattern")]
    public static async Task<string> SearchTopicsAsync(
        IMcpServer? thisServer,
        IGraphQLClient graphQLClient,
        ILogger logger,
        [Description("The search term to find topics")] string searchTerm,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Searching topics via GraphQL with term: {SearchTerm}", searchTerm);

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Search term is required",
                    searchTerm = searchTerm,
                    topics = Array.Empty<object>()
                }, _jsonOptions);
            }

            var query = new GraphQLRequest
            {
                Query = @"
                query($searchTerm: String!) {
                    searchTopics(searchTerm: $searchTerm) {
                        topic
                        unsName
                        nsPath
                        path
                        isActive
                        sourceType
                        createdAt
                        modifiedAt
                        lastDataTimestamp
                        description
                    }
                }",
                Variables = new { searchTerm }
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                logger.LogError("GraphQL search query failed for term {SearchTerm}: {Errors}", searchTerm, errorMessages);
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"GraphQL query failed: {errorMessages}",
                    searchTerm = searchTerm,
                    topics = Array.Empty<object>()
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Search for '{searchTerm}' completed successfully via GraphQL",
                searchTerm = searchTerm,
                topics = response.Data?.searchTopics ?? Array.Empty<object>()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching topics with term: {SearchTerm}", searchTerm);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                searchTerm = searchTerm,
                error = ex.Message,
                topics = Array.Empty<object>()
            }, _jsonOptions);
        }
    }

    [McpServerTool]
    [Description("Get system status and statistics")]
    public static async Task<string> GetSystemStatusAsync(
        IMcpServer? thisServer,
        IGraphQLClient graphQLClient,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting system status via GraphQL");

            var query = new GraphQLRequest
            {
                Query = @"
                query {
                    systemStatus {
                        totalTopics
                        assignedTopics
                        activeTopics
                        totalConnections
                        activeConnections
                        namespaces
                        timestamp
                        connectionStats {
                            totalConnections
                            activeConnections
                            inactiveConnections
                        }
                    }
                }"
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                logger.LogError("GraphQL system status query failed: {Errors}", errorMessages);
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"GraphQL query failed: {errorMessages}",
                    systemStatus = (object?)null
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "System status retrieved successfully via GraphQL",
                systemStatus = response.Data?.systemStatus
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving system status via GraphQL");
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                error = ex.Message,
                systemStatus = (object?)null
            }, _jsonOptions);
        }
    }
}