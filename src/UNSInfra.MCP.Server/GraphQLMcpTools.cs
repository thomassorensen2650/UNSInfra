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
                        metadata
                        currentValue
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
    [Description("Get topics by namespace path")]
    public static async Task<string> GetTopicsByNamespaceAsync(
        IMcpServer? thisServer,
        IGraphQLClient graphQLClient,
        [Description("The path of the namespace to get topics for")] string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            //logger.LogDebug("Getting topics by namespace path via GraphQL: {Path}", path);

            if (string.IsNullOrWhiteSpace(path))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Namespace path is required",
                    path = path,
                    topics = Array.Empty<object>()
                }, _jsonOptions);
            }

            var query = new GraphQLRequest
            {
                Query = @"
                query {
                    topics {
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
                        metadata
                        currentValue
                    }
                }"
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                //logger.LogError("GraphQL query failed for namespace path {Path}: {Errors}", path, errorMessages);
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"GraphQL query failed: {errorMessages}",
                    path = path,
                    topics = Array.Empty<object>()
                }, _jsonOptions);
            }

            // Filter topics to find those in the specified namespace path
            var matchingTopics = new List<object>();
            
            try
            {
                // Convert the dynamic response to JSON string then deserialize properly
                var responseJson = JsonSerializer.Serialize(response.Data);
                using var jsonDoc = JsonDocument.Parse(responseJson);
                
                // Extract topics
                if (jsonDoc.RootElement.TryGetProperty("topics", out JsonElement topicsElement) && 
                    topicsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var topicElement in topicsElement.EnumerateArray())
                    {
                        string? nsPath = topicElement.TryGetProperty("nsPath", out JsonElement nsPathProp) ? nsPathProp.GetString() : null;
                        
                        // Check if the topic's UNS namespace path exactly matches the requested path
                        if (!string.IsNullOrEmpty(nsPath) && nsPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                        {
                            var topic = JsonSerializer.Deserialize<object>(topicElement.GetRawText(), _jsonOptions);
                            if (topic != null)
                            {
                                matchingTopics.Add(topic);
                            }
                        }
                    }
                }
            }
            catch (Exception parseEx)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Failed to parse topics response: {parseEx.Message}",
                    path = path,
                    topics = Array.Empty<object>()
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Found {matchingTopics.Count} topics for namespace path '{path}'",
                path = path,
                topics = matchingTopics.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            //logger.LogError(ex, "Error retrieving topics for namespace path: {Path}", path);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                path = path,
                error = ex.Message,
                topics = Array.Empty<object>()
            }, _jsonOptions);
        }
    }

    [McpServerTool]
    [Description("Get current value and metadata for a topic by its path")]
    public static async Task<string> GetTopicCurrentValueByPathAsync(
        IMcpServer? thisServer,
        IGraphQLClient graphQLClient,
        [Description("The path of the topic to get current value and metadata for")] string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            //logger.LogDebug("Getting current value for topic path via GraphQL: {Path}", path);

            if (string.IsNullOrWhiteSpace(path))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Topic path is required",
                    path = path,
                    topic = (object?)null
                }, _jsonOptions);
            }

            // Use the same query structure as the working GetUnsHierarchyTreeAsync method
            var query = new GraphQLRequest
            {
                Query = @"
                query {
                    topics {
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
                        metadata
                        currentValue
                    }
                }"
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                //logger.LogError("GraphQL query failed for topic path {Path}: {Errors}", path, errorMessages);
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"GraphQL query failed: {errorMessages}",
                    path = path,
                    topic = (object?)null
                }, _jsonOptions);
            }

            // Extract topics using the same pattern as GetUnsHierarchyTreeAsync
            object? matchingTopic = null;
            
            try
            {
                // Convert the dynamic response to JSON string then deserialize properly
                var responseJson = JsonSerializer.Serialize(response.Data);
                using var jsonDoc = JsonDocument.Parse(responseJson);
                
                // Extract topics
                if (jsonDoc.RootElement.TryGetProperty("topics", out JsonElement topicsElement) && 
                    topicsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var topicElement in topicsElement.EnumerateArray())
                    {
                        // Parse topic data
                        string? topicName = topicElement.TryGetProperty("topic", out JsonElement topicProp) ? topicProp.GetString() : null;
                        string? unsName = topicElement.TryGetProperty("unsName", out JsonElement unsNameProp) ? unsNameProp.GetString() : null;
                        string? nsPath = topicElement.TryGetProperty("nsPath", out JsonElement nsPathProp) ? nsPathProp.GetString() : null;
                        string? hierarchicalPath = topicElement.TryGetProperty("path", out JsonElement pathProp) ? pathProp.GetString() : null;

                        // ONLY match the combined UNS path: unsNamespacePath + "/" + unsName
                        var combinedUnsPath = !string.IsNullOrEmpty(nsPath) && !string.IsNullOrEmpty(unsName) 
                            ? $"{nsPath}/{unsName}" 
                            : null;
                            
                        if (combinedUnsPath != null && combinedUnsPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingTopic = JsonSerializer.Deserialize<object>(topicElement.GetRawText(), _jsonOptions);
                            break;
                        }
                    }
                }
            }
            catch (Exception parseEx)
            {
                //logger.LogError(parseEx, "Failed to parse topics response for path: {Path}", path);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Failed to parse topics response: {parseEx.Message}",
                    path = path,
                    topic = (object?)null
                }, _jsonOptions);
            }

            if (matchingTopic == null)
            {
                // Debug: Show available topics to help with troubleshooting
                var availableTopics = new List<object>();
                try
                {
                    var responseJson = JsonSerializer.Serialize(response.Data);
                    using var jsonDoc = JsonDocument.Parse(responseJson);
                    
                    if (jsonDoc.RootElement.TryGetProperty("topics", out JsonElement topicsElement) && 
                        topicsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var topicElement in topicsElement.EnumerateArray())
                        {
                            string? topicName = topicElement.TryGetProperty("topic", out JsonElement topicProp) ? topicProp.GetString() : null;
                            string? unsName = topicElement.TryGetProperty("unsName", out JsonElement unsNameProp) ? unsNameProp.GetString() : null;
                            string? nsPath = topicElement.TryGetProperty("nsPath", out JsonElement nsPathProp) ? nsPathProp.GetString() : null;
                            string? hierarchicalPath = topicElement.TryGetProperty("path", out JsonElement pathProp) ? pathProp.GetString() : null;
                            
                            var debugCombinedPath = !string.IsNullOrEmpty(nsPath) && !string.IsNullOrEmpty(unsName) 
                                ? $"{nsPath}/{unsName}" 
                                : null;
                                
                            availableTopics.Add(new
                            {
                                combinedUnsPath = debugCombinedPath,
                                unsNamespacePath = nsPath,
                                unsName = unsName,
                                originalTopic = topicName
                            });
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors for debug info
                }

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Topic with path '{path}' not found",
                    path = path,
                    topic = (object?)null,
                    debug = new
                    {
                        searchedFor = path,
                        totalTopicsFound = availableTopics.Count,
                        availableTopics = availableTopics.Take(10).ToArray() // Show first 10 for debugging
                    }
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Topic current value and metadata retrieved successfully via GraphQL",
                path = path,
                topic = matchingTopic
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            //logger.LogError(ex, "Error getting current value for topic path: {Path}", path);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                path = path,
                error = ex.Message,
                topic = (object?)null
            }, _jsonOptions);
        }
    }
    
    [McpServerTool]
    [Description("Get the complete UNS hierarchy as a tree structure based on NSPath (Enterprise/Site/Area/WorkCenter/WorkUnit)")]
    public static async Task<string> GetUnsHierarchyTreeAsync(
        IGraphQLClient graphQLClient,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GraphQLRequest
            {
                Query = @"
                query {
                    namespaces
                    flatNamespaceStructure {
                        id
                        name
                        fullPath
                        parentId
                        nodeType
                        hierarchyNode {
                            id
                            name
                            description
                        }
                        namespace {
                            id
                            name
                            type
                            description
                        }
                        hasChildren
                        level
                    }
                    topics {
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
                        metadata
                    }
                }"
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"GraphQL query failed: {errorMessages}",
                    hierarchyTree = (object?)null
                }, _jsonOptions);
            }

            // Extract all data from response by converting to JSON and back
            var topics = new List<TopicNode>();
            object? namespaces = null;
            object? namespaceStructure = null;
            
            try
            {
                // Convert the dynamic response to JSON string then deserialize properly
                var responseJson = JsonSerializer.Serialize(response.Data);
                using var jsonDoc = JsonDocument.Parse(responseJson);

                // Extract namespaces
                if (jsonDoc.RootElement.TryGetProperty("namespaces", out JsonElement namespacesElement))
                {
                    namespaces = JsonSerializer.Deserialize<object>(namespacesElement.GetRawText(), _jsonOptions);
                }

                // Extract namespace structure (from flattened service-based structure)
                if (jsonDoc.RootElement.TryGetProperty("flatNamespaceStructure", out JsonElement namespaceStructureElement))
                {
                    namespaceStructure = JsonSerializer.Deserialize<object>(namespaceStructureElement.GetRawText(), _jsonOptions);
                }
                
                // Extract topics
                if (jsonDoc.RootElement.TryGetProperty("topics", out JsonElement topicsElement) && 
                    topicsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var topicElement in topicsElement.EnumerateArray())
                    {
                        var topic = new TopicNode();
                        
                        if (topicElement.TryGetProperty("topic", out var topicProp))
                            topic.Topic = topicProp.GetString() ?? "";
                        
                        if (topicElement.TryGetProperty("unsName", out var unsNameProp))
                            topic.UnsName = unsNameProp.GetString();
                        
                        if (topicElement.TryGetProperty("nsPath", out var nsPathProp))
                            topic.NsPath = nsPathProp.GetString();
                        
                        if (topicElement.TryGetProperty("path", out var pathProp))
                            topic.Path = pathProp.GetString();
                        
                        if (topicElement.TryGetProperty("isActive", out var isActiveProp))
                            topic.IsActive = isActiveProp.GetBoolean();
                        
                        if (topicElement.TryGetProperty("sourceType", out var sourceTypeProp))
                            topic.SourceType = sourceTypeProp.GetString() ?? "";
                        
                        if (topicElement.TryGetProperty("createdAt", out var createdAtProp))
                            topic.CreatedAt = createdAtProp.GetString();
                        
                        if (topicElement.TryGetProperty("modifiedAt", out var modifiedAtProp))
                            topic.ModifiedAt = modifiedAtProp.GetString();
                        
                        if (topicElement.TryGetProperty("lastDataTimestamp", out var lastDataTimestampProp))
                            topic.LastDataTimestamp = lastDataTimestampProp.GetString();
                        
                        if (topicElement.TryGetProperty("description", out var descriptionProp))
                            topic.Description = descriptionProp.GetString();
                        
                        if (topicElement.TryGetProperty("metadata", out var metadataProp))
                            topic.Metadata = JsonSerializer.Deserialize<object>(metadataProp.GetRawText(), _jsonOptions);
                        
                        topics.Add(topic);
                    }
                }
                
                // Build clean hierarchical tree structure from flattened namespace structure
                var hierarchyTree = BuildCleanHierarchyTreeFromFlat(topics, namespaceStructure);
                
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "UNS hierarchy tree retrieved successfully via GraphQL",
                    namespaces = namespaces,
                    hierarchyTree = hierarchyTree
                }, _jsonOptions);
            }
            catch (Exception parseEx)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Failed to parse GraphQL response: {parseEx.Message}",
                    hierarchyTree = (object?)null
                }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                error = ex.Message,
                errorType = ex.GetType().Name,
                hierarchyTree = (object?)null,
                troubleshooting = new
                {
                    suggestion = "Try running the 'test_graphql_connectivity' tool first to diagnose the issue",
                    uiServerCommand = "dotnet run --project src/UNSInfra.UI",
                    expectedEndpoint = "https://localhost:5001/graphql"
                }
            }, _jsonOptions);
        }
    }

    private static CleanTreeNode BuildCleanHierarchyTreeFromFlat(List<TopicNode> topics, object? namespaceStructure)
    {
        // Create root node
        var rootNode = new CleanTreeNode
        {
            Name = "UNS Root",
            Type = "HierarchicalPath",
            NodeType = "Enterprise",
            NodeTypeDescription = "Top level of the hierarchy - the entire organization or company",
            Path = "",
            Children = new List<object>()
        };

        if (namespaceStructure == null) return rootNode;

        try
        {
            // Parse the flattened structure
            var json = JsonSerializer.Serialize(namespaceStructure, _jsonOptions);
            using var jsonDoc = JsonDocument.Parse(json);
            
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var flatNodes = new List<FlatNodeData>();
                
                // Parse all flat nodes
                foreach (var element in jsonDoc.RootElement.EnumerateArray())
                {
                    var flatNode = ParseFlatNode(element);
                    if (flatNode != null)
                    {
                        flatNodes.Add(flatNode);
                    }
                }
                
                // Build hierarchy from flat structure
                BuildHierarchyFromFlatNodes(rootNode, flatNodes, null, topics);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to process flattened namespace structure: {ex.Message}");
        }
        
        return rootNode;
    }

    private static FlatNodeData? ParseFlatNode(JsonElement element)
    {
        try
        {
            var node = new FlatNodeData();
            
            if (element.TryGetProperty("id", out var idProp))
                node.Id = idProp.GetString() ?? "";
                
            if (element.TryGetProperty("name", out var nameProp))
                node.Name = nameProp.GetString() ?? "";
                
            if (element.TryGetProperty("fullPath", out var fullPathProp))
                node.FullPath = fullPathProp.GetString() ?? "";
                
            if (element.TryGetProperty("parentId", out var parentIdProp) && parentIdProp.ValueKind != JsonValueKind.Null)
                node.ParentId = parentIdProp.GetString();
                
            if (element.TryGetProperty("nodeType", out var nodeTypeProp))
                node.NodeType = nodeTypeProp.GetString() ?? "";
                
            if (element.TryGetProperty("hasChildren", out var hasChildrenProp))
                node.HasChildren = hasChildrenProp.GetBoolean();
                
            if (element.TryGetProperty("level", out var levelProp))
                node.Level = levelProp.GetInt32();
                
            // Parse hierarchy node data
            if (element.TryGetProperty("hierarchyNode", out var hierarchyNodeElement) &&
                hierarchyNodeElement.ValueKind != JsonValueKind.Null)
            {
                node.HierarchyNode = new HierarchyNodeData();
                if (hierarchyNodeElement.TryGetProperty("id", out var hIdProp))
                    node.HierarchyNode.Id = hIdProp.GetString() ?? "";
                if (hierarchyNodeElement.TryGetProperty("name", out var hNameProp))
                    node.HierarchyNode.Name = hNameProp.GetString() ?? "";
                if (hierarchyNodeElement.TryGetProperty("description", out var hDescProp))
                    node.HierarchyNode.Description = hDescProp.GetString() ?? "";
            }
            
            // Parse namespace data
            if (element.TryGetProperty("namespace", out var namespaceElement) &&
                namespaceElement.ValueKind != JsonValueKind.Null)
            {
                node.Namespace = new NamespaceNodeData();
                if (namespaceElement.TryGetProperty("id", out var nsIdProp))
                    node.Namespace.Id = nsIdProp.GetString() ?? "";
                if (namespaceElement.TryGetProperty("name", out var nsNameProp))
                    node.Namespace.Name = nsNameProp.GetString() ?? "";
                if (namespaceElement.TryGetProperty("type", out var nsTypeProp))
                    node.Namespace.Type = nsTypeProp.GetString() ?? "";
                if (namespaceElement.TryGetProperty("description", out var nsDescProp))
                    node.Namespace.Description = nsDescProp.GetString() ?? "";
            }
            
            return node;
        }
        catch
        {
            return null;
        }
    }

    private static void BuildHierarchyFromFlatNodes(CleanTreeNode parentNode, List<FlatNodeData> allNodes, string? parentId, List<TopicNode> topics)
    {
        // Find children of this parent
        var childNodes = allNodes.Where(n => n.ParentId == parentId).OrderBy(n => n.Level).ThenBy(n => n.Name).ToList();
        
        foreach (var childData in childNodes)
        {
            CleanTreeNode childNode;
            
            if (childData.NodeType == "Namespace")
            {
                // This is a namespace node
                childNode = new CleanTreeNode
                {
                    Name = childData.Name,
                    Type = "Namespace",
                    Path = childData.FullPath,
                    Children = new List<object>()
                };
                
                if (childData.Namespace != null)
                {
                    childNode.NamespaceType = childData.Namespace.Type;
                    childNode.Description = childData.Namespace.Description; // Add namespace description
                }
                else
                {
                    childNode.NamespaceType = "Functional"; // Default
                }
                
                // Add topics that belong to this namespace path
                AddTopicsToNamespaceNode(childNode, childData.FullPath, topics);
            }
            else
            {
                // This is a hierarchical path node
                var pathParts = childData.FullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var level = pathParts.Length;
                
                childNode = new CleanTreeNode
                {
                    Name = childData.Name,
                    Type = "HierarchicalPath",
                    
                    NodeType = childData.NodeType, // GetISA95NodeType(level),
                    NodeTypeDescription = "ADD ME", //GetISA95Description(level, childData.Name),
                    Path = childData.FullPath,
                    Children = new List<object>()
                };
            }
            
            // Recursively build children
            if (childData.HasChildren)
            {
                BuildHierarchyFromFlatNodes(childNode, allNodes, childData.Id, topics);
            }
            
            parentNode.Children.Add(childNode);
        }
    }

    private static void AddTopicsToNamespaceNode(CleanTreeNode namespaceNode, string fullPath, List<TopicNode> topics)
    {
        // Find topics that belong to this namespace
        var matchingTopics = topics.Where(t => !string.IsNullOrEmpty(t.NsPath) && 
                                               t.NsPath.StartsWith(fullPath, StringComparison.OrdinalIgnoreCase))
                                   .ToList();
        
        foreach (var topic in matchingTopics)
        {
            var topicNode = new CleanTreeNode
            {
                Name = topic.UnsName ?? topic.Topic,
                Type = "Topic",
                Description = topic.Description ?? $"Topic: {topic.Topic}",
                Path = $"{fullPath}/{topic.UnsName ?? topic.Topic}"
            };
            namespaceNode.Children.Add(topicNode);
        }
    }
    
    /*
    private static string GetISA95NodeType(int level) => level switch
    {
        1 => "Enterprise",
        2 => "Site", 
        3 => "Area",
        4 => "WorkCenter",
        5 => "WorkUnit",
        _ => "Property"
    };
    */
    
    /*
    private static string GetISA95Description(int level, string name) => level switch
    {
        1 => $"Enterprise: {name} - Top level of the hierarchy",
        2 => $"Site: {name} - Physical location or facility",
        3 => $"Area: {name} - Production area or department", 
        4 => $"WorkCenter: {name} - Group of equipment performing similar functions",
        5 => $"WorkUnit: {name} - Individual piece of equipment or machine",
        _ => $"Property: {name} - Specific data point or measurement"
    };
    */
    
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

/// <summary>
/// Represents a topic node with all its data
/// </summary>
public class TopicNode
{
    public string Topic { get; set; } = string.Empty;
    public string? UnsName { get; set; }
    public string? NsPath { get; set; }
    public string? Path { get; set; }
    public bool IsActive { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }
    public string? ModifiedAt { get; set; }
    public string? LastDataTimestamp { get; set; }
    public string? Description { get; set; }
    public object? Metadata { get; set; }
}

/// <summary>
/// Represents hierarchy node data from GraphQL
/// </summary>
public class HierarchyNodeData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents namespace node data from GraphQL
/// </summary>
public class NamespaceNodeData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents a node in the hierarchical tree structure
/// </summary>
public class HierarchyTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<HierarchyTreeNode> Children { get; set; } = new();
    public List<TopicNode> Topics { get; set; } = new();
    public bool HasChildren { get; set; }
    public int TopicCount { get; set; }
    
    /// <summary>
    /// The type of node (HierarchyNode or Namespace)
    /// </summary>
    public string? NodeType { get; set; }
    
    /// <summary>
    /// The namespace type if this is a namespace node (Functional, Informative, Definitional, AdHoc)
    /// </summary>
    public string? NamespaceType { get; set; }
    
    /// <summary>
    /// Display name for the namespace type
    /// </summary>
    public string? NamespaceTypeDisplayName { get; set; }
    
    /// <summary>
    /// Icon class for the namespace type
    /// </summary>
    public string? NamespaceTypeIcon { get; set; }
    
    /// <summary>
    /// Color class for the namespace type
    /// </summary>
    public string? NamespaceTypeColor { get; set; }
    
    /// <summary>
    /// Whether this node has topics assigned to it
    /// </summary>
    public bool HasTopics { get; set; }
    
    /// <summary>
    /// Whether this is an empty namespace (no topics assigned)
    /// </summary>
    public bool IsEmpty { get; set; }
}

/// <summary>
/// Represents a flattened node from GraphQL
/// </summary>
public class FlatNodeData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public HierarchyNodeData? HierarchyNode { get; set; }
    public NamespaceNodeData? Namespace { get; set; }
    public bool HasChildren { get; set; }
    public int Level { get; set; }
}

/// <summary>
/// Clean tree node structure matching the specified requirements
/// </summary>
public class CleanTreeNode
{
    /// <summary>
    /// The name of the node
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of node: HierarchicalPath, Namespace, or Topic
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// For HierarchicalPath: Enterprise, Site, Area, WorkCenter, WorkUnit, Property
    /// </summary>
    public string? NodeType { get; set; }
    
    /// <summary>
    /// Description of the node type for HierarchicalPath nodes
    /// </summary>
    public string? NodeTypeDescription { get; set; }
    
    /// <summary>
    /// For Namespace: Functional, Informative, Definitional, AdHoc
    /// </summary>
    public string? NamespaceType { get; set; }
    
    /// <summary>
    /// For Topic: Description of what the topic represents
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The full path to this node
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Children nodes - can be HierarchicalPath, Namespace, or Topic nodes
    /// </summary>
    public List<object> Children { get; set; } = new();
}