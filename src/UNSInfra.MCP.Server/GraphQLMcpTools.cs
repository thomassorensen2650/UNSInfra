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
/*
    [McpServerTool]
    [Description("Get the complete UNS hierarchy structure showing all namespaces, topics, and their detailed information")]
    public static async Task<string> GetUnsHierarchyAsync(
        IGraphQLClient graphQLClient,
        CancellationToken cancellationToken = default)
    {
        try
        {
            //logger.LogDebug("Getting UNS hierarchy via GraphQL");

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
                        connectionStats {
                            totalConnections
                            activeConnections
                            inactiveConnections
                        }
                    }
                    namespaces
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
               // logger.LogError("GraphQL query failed: {Errors}", errorMessages);
                
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
                message = "Complete UNS hierarchy retrieved successfully via GraphQL",
                hierarchyData = response.Data
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
           // logger.LogError(ex, "Error retrieving UNS hierarchy via GraphQL");
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.",
                error = ex.Message,
                errorType = ex.GetType().Name,
                hierarchyData = (object?)null,
                troubleshooting = new
                {
                    suggestion = "Try running the 'test_graphql_connectivity' tool first to diagnose the issue",
                    uiServerCommand = "dotnet run --project src/UNSInfra.UI",
                    expectedEndpoint = "https://localhost:5001/graphql"
                }
            }, _jsonOptions);
        }
    }
*/
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
/*
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

*/

/*
    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"hello {message}";
*/
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
                    systemStatus {
                        totalTopics
                        assignedTopics
                        activeTopics
                        namespaces
                        timestamp
                        connectionStats {
                            totalConnections
                            activeConnections
                            inactiveConnections
                        }
                    }
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
            object? systemStatus = null;
            int topicCount = 0;
            
            try
            {
                // Convert the dynamic response to JSON string then deserialize properly
                var responseJson = JsonSerializer.Serialize(response.Data);
                using var jsonDoc = JsonDocument.Parse(responseJson);

                // Extract system status
                if (jsonDoc.RootElement.TryGetProperty("systemStatus", out JsonElement systemStatusElement))
                {
                    systemStatus = JsonSerializer.Deserialize<object>(systemStatusElement.GetRawText(), _jsonOptions);
                    
                    // Extract topic count from system status
                    if (systemStatusElement.TryGetProperty("totalTopics", out var totalTopicsProp))
                    {
                        topicCount = totalTopicsProp.GetInt32();
                    }
                }

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
                    systemStatus = systemStatus,
                    namespaces = namespaces,
                    hierarchyTree = hierarchyTree,
                    topicCount = topicCount
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

   /*
    private static NSTreeNodeData? ParseNSTreeNode(JsonElement nsElement)
    {
        var nsNode = new NSTreeNodeData();
        
        if (nsElement.TryGetProperty("name", out var nameProp))
            nsNode.Name = nameProp.GetString() ?? "";
            
        if (nsElement.TryGetProperty("fullPath", out var fullPathProp))
            nsNode.FullPath = fullPathProp.GetString() ?? "";
            
        if (nsElement.TryGetProperty("nodeType", out var nodeTypeProp))
            nsNode.NodeType = nodeTypeProp.GetString() ?? "";
            
        // Parse hierarchy node data
        if (nsElement.TryGetProperty("hierarchyNode", out var hierarchyNodeElement) &&
            hierarchyNodeElement.ValueKind != JsonValueKind.Null)
        {
            nsNode.HierarchyNode = new HierarchyNodeData();
            if (hierarchyNodeElement.TryGetProperty("id", out var idProp))
                nsNode.HierarchyNode.Id = idProp.GetString() ?? "";
            if (hierarchyNodeElement.TryGetProperty("name", out var hNameProp))
                nsNode.HierarchyNode.Name = hNameProp.GetString() ?? "";
            if (hierarchyNodeElement.TryGetProperty("description", out var hDescProp))
                nsNode.HierarchyNode.Description = hDescProp.GetString() ?? "";
        }
        
        // Parse namespace data
        if (nsElement.TryGetProperty("namespace", out var namespaceElement) &&
            namespaceElement.ValueKind != JsonValueKind.Null)
        {
            nsNode.Namespace = new NamespaceNodeData();
            if (namespaceElement.TryGetProperty("id", out var nsIdProp))
                nsNode.Namespace.Id = nsIdProp.GetString() ?? "";
            if (namespaceElement.TryGetProperty("name", out var nsNameProp))
                nsNode.Namespace.Name = nsNameProp.GetString() ?? "";
            if (namespaceElement.TryGetProperty("type", out var nsTypeProp))
                nsNode.Namespace.Type = nsTypeProp.GetString() ?? "";
            if (namespaceElement.TryGetProperty("description", out var nsDescProp))
                nsNode.Namespace.Description = nsDescProp.GetString() ?? "";
        }
        
        // Parse children recursively
        if (nsElement.TryGetProperty("children", out var childrenElement) &&
            childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var childElement in childrenElement.EnumerateArray())
            {
                var childNode = ParseNSTreeNode(childElement);
                if (childNode != null)
                {
                    nsNode.Children.Add(childNode);
                }
            }
        }
        
        return nsNode;
    }
*/
   
   /*
    private static HierarchyTreeNode BuildHierarchicalTreeWithInferredNamespaces(List<TopicNode> topics)
    {
        // Build the basic hierarchical tree from topics
        var basicTree = BuildHierarchicalTree(topics);
        
        return basicTree;
    }
    
    */
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
                    NodeType = GetISA95NodeType(level),
                    NodeTypeDescription = GetISA95Description(level, childData.Name),
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
/*
    private static CleanTreeNode BuildCleanHierarchyTree(List<TopicNode> topics, object? namespaceStructure)
    {
        // Create root node - this is the UNS hierarchy root
        var rootNode = new CleanTreeNode
        {
            Name = "UNS Root",
            Type = "HierarchicalPath",
            NodeType = "Enterprise",
            NodeTypeDescription = "Top level of the hierarchy - the entire organization or company",
            Path = "",
            Children = new List<object>()
        };

        // Group topics by their NSPath hierarchy to build the tree structure
        var pathGroups = new Dictionary<string, List<TopicNode>>();
        var pathHierarchy = new Dictionary<string, List<string>>();

        // Initialize root
        pathHierarchy[""] = new List<string>();
        pathGroups[""] = new List<TopicNode>();

        // Process all topics to build path hierarchy
        foreach (var topic in topics.Where(t => !string.IsNullOrEmpty(t.Topic)))
        {
            var nsPath = !string.IsNullOrEmpty(topic.NsPath) ? topic.NsPath : "";
            
            if (string.IsNullOrEmpty(nsPath))
            {
                // Unassigned topic - add directly to root
                pathGroups[""].Add(topic);
                continue;
            }

            // Build hierarchical path structure
            var pathParts = nsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";
            
            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                var parentPath = currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                
                // Initialize path structures
                if (!pathGroups.ContainsKey(currentPath))
                    pathGroups[currentPath] = new List<TopicNode>();
                
                if (!pathHierarchy.ContainsKey(parentPath))
                    pathHierarchy[parentPath] = new List<string>();
                
                // Add this part as a child of the parent path
                if (!pathHierarchy[parentPath].Contains(part))
                    pathHierarchy[parentPath].Add(part);
            }
            
            // Add topic to its final path
            pathGroups[nsPath].Add(topic);
        }

        // Build the tree recursively, using actual namespace structure as the source of truth
        if (namespaceStructure != null)
        {
            BuildTreeFromNamespaceStructure(rootNode, namespaceStructure, topics);
        }
        else
        {
            // Fallback to building from topics only if no namespace structure available
            BuildCleanChildren(rootNode, "", pathHierarchy, pathGroups, null);
        }
        
        return rootNode;
    }

    */
   /*
    private static void BuildTreeFromNamespaceStructure(CleanTreeNode rootNode, object namespaceStructure, List<TopicNode> topics)
    {
        try
        {
            // Convert namespace structure to JSON and parse it
            var json = JsonSerializer.Serialize(namespaceStructure, _jsonOptions);
            using var jsonDoc = JsonDocument.Parse(json);
            
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var nsElement in jsonDoc.RootElement.EnumerateArray())
                {
                    ProcessNamespaceElement(rootNode, nsElement, topics);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to process namespace structure: {ex.Message}");
            // If namespace structure processing fails, don't add anything to avoid inconsistencies
        }
    }
    */
   
   /*
   private static void ProcessNamespaceElement(CleanTreeNode parentNode, JsonElement nsElement, List<TopicNode> topics)
    {
        if (!nsElement.TryGetProperty("fullPath", out var fullPathElement))
            return;
            
        var fullPath = fullPathElement.GetString();
        if (string.IsNullOrEmpty(fullPath))
            return;
            
        // Get node information
        var name = nsElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "";
        var nodeTypeStr = nsElement.TryGetProperty("nodeType", out var nodeTypeElement) ? nodeTypeElement.GetString() : "";
        
        CleanTreeNode node;
        
        if (nodeTypeStr == "Namespace")
        {
            // This is a namespace node
            node = new CleanTreeNode
            {
                Name = name ?? "",
                Type = "Namespace",
                Path = fullPath,
                Children = new List<object>()
            };
            
            // Try to get namespace type from the namespace field
            if (nsElement.TryGetProperty("namespace", out var namespaceInfo) && 
                namespaceInfo.TryGetProperty("type", out var nsTypeElement))
            {
                node.NamespaceType = nsTypeElement.GetString();
            }
            else
            {
                node.NamespaceType = "Functional"; // Default
            }
            
            // Add topics that belong to this namespace path
            AddTopicsToNamespaceNode(node, fullPath, topics);
        }
        else
        {
            // This is a hierarchical path node
            var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var level = pathParts.Length;
            
            node = new CleanTreeNode
            {
                Name = name ?? "",
                Type = "HierarchicalPath",
                NodeType = GetISA95NodeType(level),
                NodeTypeDescription = GetISA95Description(level, name ?? ""),
                Path = fullPath,
                Children = new List<object>()
            };
        }
        
        // Process children recursively
        if (nsElement.TryGetProperty("children", out var childrenElement) &&
            childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var childElement in childrenElement.EnumerateArray())
            {
                ProcessNamespaceElement(node, childElement, topics);
            }
        }
        
        parentNode.Children.Add(node);
    }
    */
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
    private static void BuildCleanChildren(CleanTreeNode parentNode, string parentPath, 
        Dictionary<string, List<string>> pathHierarchy, Dictionary<string, List<TopicNode>> pathGroups, 
        object? namespaceStructure)
    {
        if (!pathHierarchy.ContainsKey(parentPath))
            return;

        var childPaths = pathHierarchy[parentPath];
        
        foreach (var childName in childPaths)
        {
            var fullPath = string.IsNullOrEmpty(parentPath) ? childName : $"{parentPath}/{childName}";
            var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var level = pathParts.Length;
            
            // Create hierarchical path node based on ISA-95 levels
            var hierarchyNode = new CleanTreeNode
            {
                Name = childName,
                Type = "HierarchicalPath",
                NodeType = GetISA95NodeType(level),
                NodeTypeDescription = GetISA95Description(level, childName),
                Path = fullPath,
                Children = new List<object>()
            };
            
            // Build children recursively
            BuildCleanChildren(hierarchyNode, fullPath, pathHierarchy, pathGroups, namespaceStructure);
            
            // Add namespace nodes if they exist at this level
            AddNamespaceNodes(hierarchyNode, fullPath, pathGroups);
            
            parentNode.Children.Add(hierarchyNode);
        }
        
        // Add unassigned topics directly to root if this is the root node
        if (string.IsNullOrEmpty(parentPath) && pathGroups.ContainsKey("") && pathGroups[""].Any())
        {
            var unassignedNamespace = new CleanTreeNode
            {
                Name = "Unassigned",
                Type = "Namespace",
                NamespaceType = "AdHoc",
                Path = "Unassigned",
                Children = new List<object>()
            };
            
            foreach (var topic in pathGroups[""])
            {
                var topicNode = new CleanTreeNode
                {
                    Name = topic.UnsName ?? topic.Topic,
                    Type = "Topic",
                    Description = topic.Description ?? $"Topic: {topic.Topic}",
                    Path = $"Unassigned/{topic.UnsName ?? topic.Topic}"
                };
                unassignedNamespace.Children.Add(topicNode);
            }
            
            parentNode.Children.Add(unassignedNamespace);
        }
    }
    
    */
    
    /*
    private static void AddNamespaceNodes(CleanTreeNode hierarchyNode, string fullPath, Dictionary<string, List<TopicNode>> pathGroups)
    {
        if (!pathGroups.ContainsKey(fullPath) || !pathGroups[fullPath].Any())
            return;
            
        // Instead of grouping topics artificially, add them directly as topics under the hierarchy node
        // The UI's namespace structure should be the source of truth for namespaces
        foreach (var topic in pathGroups[fullPath])
        {
            var topicNode = new CleanTreeNode
            {
                Name = topic.UnsName ?? topic.Topic,
                Type = "Topic", 
                Description = topic.Description ?? $"Topic: {topic.Topic}",
                Path = $"{fullPath}/{topic.UnsName ?? topic.Topic}"
            };
            hierarchyNode.Children.Add(topicNode);
        }
    }
    */
    private static string GetISA95NodeType(int level) => level switch
    {
        1 => "Enterprise",
        2 => "Site", 
        3 => "Area",
        4 => "WorkCenter",
        5 => "WorkUnit",
        _ => "Property"
    };
    
    private static string GetISA95Description(int level, string name) => level switch
    {
        1 => $"Enterprise: {name} - Top level of the hierarchy",
        2 => $"Site: {name} - Physical location or facility",
        3 => $"Area: {name} - Production area or department", 
        4 => $"WorkCenter: {name} - Group of equipment performing similar functions",
        5 => $"WorkUnit: {name} - Individual piece of equipment or machine",
        _ => $"Property: {name} - Specific data point or measurement"
    };
    
    /*
    private static string InferNamespaceFromTopic(TopicNode topic)
    {
        var topicName = (topic.UnsName ?? topic.Topic).ToLower();
        
        // Group by functional categories
        if (topicName.Contains("kpi") || topicName.Contains("metric") || topicName.Contains("performance"))
            return "KPIs";
        if (topicName.Contains("alarm") || topicName.Contains("alert") || topicName.Contains("warning"))
            return "Alarms";
        if (topicName.Contains("config") || topicName.Contains("setting") || topicName.Contains("parameter"))
            return "Configuration";
        if (topicName.Contains("status") || topicName.Contains("state"))
            return "Status";
        if (topicName.Contains("data") || topicName.Contains("value") || topicName.Contains("measurement"))
            return "Data";
        
        return "General";
    }
    */
    /*
    private static string GetNamespaceTypeName(string namespaceType) => namespaceType switch
    {
        "KPIs" => "Functional",
        "Alarms" => "Functional", 
        "Configuration" => "Definitional",
        "Status" => "Informative",
        "Data" => "Functional",
        _ => "Functional"
    };
    */
/*
    private static HierarchyTreeNode BuildHierarchicalTreeWithNamespaceStructure(List<TopicNode> topics, object? namespaceStructure)
    {
        // Build the basic hierarchical tree from topics
        var basicTree = BuildHierarchicalTree(topics);
        
        // If we have namespace structure data, merge it with the topic-based tree
        if (namespaceStructure != null)
        {
            MergeNamespaceStructureIntoTree(basicTree, namespaceStructure);
        }
        
        return basicTree;
    }
    */
    /*
    private static void MergeNamespaceStructureIntoTree(HierarchyTreeNode rootNode, object? namespaceStructure)
    {
        try
        {
            // Convert the namespace structure to JSON and parse it
            var json = JsonSerializer.Serialize(namespaceStructure, _jsonOptions);
            using var jsonDoc = JsonDocument.Parse(json);
            
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var nsElement in jsonDoc.RootElement.EnumerateArray())
                {
                    ProcessNamespaceStructureNode(rootNode, nsElement);
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail - just continue without namespace structure
            Console.WriteLine($"Warning: Failed to merge namespace structure: {ex.Message}");
        }
    }
    */
   /*
    private static void ProcessNamespaceStructureNode(HierarchyTreeNode rootNode, JsonElement nsElement)
    {
        try
        {
            if (!nsElement.TryGetProperty("path", out var pathElement) || 
                string.IsNullOrEmpty(pathElement.GetString()))
                return;
                
            var path = pathElement.GetString()!;
            var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (pathParts.Length == 0) return;
            
            // Navigate/create the path in the tree
            var currentNode = rootNode;
            var currentPath = "";
            
            foreach (var part in pathParts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                
                // Check if child already exists
                var existingChild = currentNode.Children.FirstOrDefault(c => c.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                
                if (existingChild == null)
                {
                    // Create new child node for empty namespace
                    existingChild = new HierarchyTreeNode
                    {
                        Name = part,
                        FullPath = currentPath,
                        Children = new List<HierarchyTreeNode>(),
                        Topics = new List<TopicNode>(),
                        NodeType = "Namespace",
                        NamespaceType = "Functional", // Default, will be enhanced later
                        TopicCount = 0,
                        HasTopics = false,
                        IsEmpty = true // Mark as empty namespace
                    };
                    
                    // Extract namespace information from the structure if available
                    if (nsElement.TryGetProperty("type", out var typeElement) && 
                        !string.IsNullOrEmpty(typeElement.GetString()))
                    {
                        existingChild.NamespaceType = typeElement.GetString();
                    }
                    
                    if (nsElement.TryGetProperty("description", out var descElement) && 
                        !string.IsNullOrEmpty(descElement.GetString()))
                    {
                        existingChild.Description = descElement.GetString();
                    }
                    
                    currentNode.Children.Add(existingChild);
                }
                
                currentNode = existingChild;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to process namespace structure node: {ex.Message}");
        }
    }
    */
/*
    private static HierarchyTreeNode BuildHierarchicalTree(List<TopicNode> topics)
    {
        var childrenLookup = new Dictionary<string, List<string>>();
        var topicsByPath = new Dictionary<string, List<TopicNode>>();
        
        // Initialize root structure - no "Data Browser" wrapper, start directly with namespaces
        if (!childrenLookup.ContainsKey(""))
            childrenLookup[""] = new List<string>();
        if (!topicsByPath.ContainsKey(""))
            topicsByPath[""] = new List<TopicNode>();

        // Build hierarchical structure based on NSPath (UNS hierarchy)
        foreach (var topic in topics.Where(t => !string.IsNullOrEmpty(t.Topic)))
        {
            // Use NSPath for hierarchy structure, fallback to Topic if NSPath is empty
            var hierarchyPath = !string.IsNullOrEmpty(topic.NsPath) ? topic.NsPath : topic.Topic;
            var pathParts = hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // If no hierarchical path (unassigned topic), place in "Unassigned" section
            if (pathParts.Length == 0 || string.IsNullOrEmpty(hierarchyPath))
            {
                const string unassignedSection = "Unassigned";
                
                // Initialize unassigned section if needed
                if (!childrenLookup[""].Contains(unassignedSection))
                {
                    childrenLookup[""].Add(unassignedSection);
                }
                if (!childrenLookup.ContainsKey(unassignedSection))
                    childrenLookup[unassignedSection] = new List<string>();
                if (!topicsByPath.ContainsKey(unassignedSection))
                    topicsByPath[unassignedSection] = new List<TopicNode>();
                
                // Add topic to unassigned section
                var topicDisplayName = topic.UnsName ?? topic.Topic;
                var topicKey = $"{unassignedSection}/{topicDisplayName}";
                if (!childrenLookup[unassignedSection].Contains(topicDisplayName))
                {
                    childrenLookup[unassignedSection].Add(topicDisplayName);
                    topicsByPath[topicKey] = new List<TopicNode> { topic };
                }
            }
            else
            {
                // Build nested folder structure based on NSPath hierarchy (Enterprise/Site/Area/WorkCenter/WorkUnit)
                string currentPath = "";
                
                for (int i = 0; i < pathParts.Length; i++)
                {
                    var part = pathParts[i];
                    var parentPath = currentPath;
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                    
                    // Initialize path structures if they don't exist
                    if (!topicsByPath.ContainsKey(currentPath))
                        topicsByPath[currentPath] = new List<TopicNode>();
                    
                    if (!childrenLookup.ContainsKey(parentPath))
                        childrenLookup[parentPath] = new List<string>();
                    
                    // Add this part as a child of the parent path
                    if (!childrenLookup[parentPath].Contains(part))
                        childrenLookup[parentPath].Add(part);
                    
                    // If this is the final segment, add the actual topic as a child with its display name
                    if (i == pathParts.Length - 1)
                    {
                        var topicDisplayName = topic.UnsName ?? topic.Topic;
                        var topicKey = string.IsNullOrEmpty(currentPath) ? topicDisplayName : $"{currentPath}/{topicDisplayName}";
                        
                        // Ensure the parent path exists in childrenLookup before accessing it
                        if (!childrenLookup.ContainsKey(currentPath))
                            childrenLookup[currentPath] = new List<string>();
                        
                        if (!childrenLookup[currentPath].Contains(topicDisplayName))
                        {
                            childrenLookup[currentPath].Add(topicDisplayName);
                            topicsByPath[topicKey] = new List<TopicNode> { topic };
                        }
                        else
                        {
                            // If display name already exists, add to existing topic list
                            if (topicsByPath.ContainsKey(topicKey))
                            {
                                topicsByPath[topicKey].Add(topic);
                            }
                            else
                            {
                                topicsByPath[topicKey] = new List<TopicNode> { topic };
                            }
                        }
                    }
                }
            }
        }

        // Build the tree starting from UNS root
        var rootNode = new HierarchyTreeNode
        {
            Name = "UNS Root",
            FullPath = "",
            Description = "Unified Namespace root containing all enterprise and factory hierarchies",
            Children = new List<HierarchyTreeNode>(),
            Topics = topicsByPath.ContainsKey("") ? topicsByPath[""] : new List<TopicNode>(),
            HasChildren = childrenLookup.ContainsKey("") && childrenLookup[""].Count > 0
        };

        // Build children recursively
        if (rootNode.HasChildren)
        {
            var childNames = childrenLookup[""];
            foreach (var childName in childNames)
            {
                var childNode = BuildTreeNode(childName, childName, childrenLookup, topicsByPath);
                if (childNode != null)
                {
                    rootNode.Children.Add(childNode);
                }
            }
        }

        return rootNode;
    }
*/
    /*
    private static HierarchyTreeNode? BuildTreeNode(string name, string fullPath, 
        Dictionary<string, List<string>> childrenLookup, 
        Dictionary<string, List<TopicNode>> topicsByPath)
    {
        var hasData = topicsByPath.ContainsKey(fullPath) && topicsByPath[fullPath].Any();
        var hasChildren = childrenLookup.ContainsKey(fullPath) && childrenLookup[fullPath].Count > 0;
        
        var node = new HierarchyTreeNode
        {
            Name = name,
            FullPath = fullPath,
            Description = GetNodeDescriptionFromData(name, fullPath, hasData, topicsByPath),
            Children = new List<HierarchyTreeNode>(),
            Topics = hasData ? topicsByPath[fullPath] : new List<TopicNode>(),
            HasChildren = hasChildren,
            TopicCount = hasData ? topicsByPath[fullPath].Count : 0
        };

        // Build children recursively
        if (hasChildren)
        {
            var childNames = childrenLookup[fullPath];
            foreach (var childName in childNames)
            {
                var childFullPath = $"{fullPath}/{childName}";
                var childNode = BuildTreeNode(childName, childFullPath, childrenLookup, topicsByPath);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }
        }

        return node;
    }
*/
   
    /*
    private static string GetNodeDescriptionFromData(string name, string fullPath, bool hasData, 
        Dictionary<string, List<TopicNode>> topicsByPath)
    {
        // First, try to get description from topics themselves if they have descriptions
        if (hasData && topicsByPath.ContainsKey(fullPath))
        {
            var topics = topicsByPath[fullPath];
            var firstTopicWithDescription = topics.FirstOrDefault(t => !string.IsNullOrEmpty(t.Description));
            if (firstTopicWithDescription != null)
            {
                // If we have a topic description, add topic count info
                var topicCount = topics.Count;
                var activeCount = topics.Count(t => t.IsActive);
                var sources = topics.Select(t => t.SourceType).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                
                var detailInfo = $" ({topicCount} topics, {activeCount} active";
                if (sources.Any())
                {
                    detailInfo += $", sources: {string.Join(", ", sources)}";
                }
                detailInfo += ")";
                
                return $"{firstTopicWithDescription.Description}{detailInfo}";
            }
        }
        
        // Handle special cases
        if (name == "Unassigned")
        {
            var topicCount = hasData && topicsByPath.ContainsKey(fullPath) 
                ? topicsByPath[fullPath].Count 
                : 0;
            return $"Topics not yet assigned to a hierarchical namespace ({topicCount} topics)";
        }

        // Split the path to determine ISA-S95 hierarchy level
        var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var level = pathParts.Length;

        // If this node has topics, include that information
        var topicInfo = "";
        if (hasData && topicsByPath.ContainsKey(fullPath))
        {
            var topics = topicsByPath[fullPath];
            var topicCount = topics.Count;
            var activeCount = topics.Count(t => t.IsActive);
            var sources = topics.Select(t => t.SourceType).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            
            topicInfo = $" ({topicCount} topics, {activeCount} active";
            if (sources.Any())
            {
                topicInfo += $", sources: {string.Join(", ", sources)}";
            }
            topicInfo += ")";
        }

        // Use ISA-S95 standard descriptions based on hierarchy level
        return level switch
        {
            1 => $"Top level of the hierarchy - the entire organization or company{topicInfo}", // Enterprise level
            2 => $"Physical location or facility within the enterprise{topicInfo}", // Site level
            3 => $"Production area or department within a site{topicInfo}", // Area level
            4 => $"Group of equipment or workstations performing similar functions{topicInfo}", // WorkCenter level  
            5 => $"Individual piece of equipment, machine, or process unit{topicInfo}", // WorkUnit level
            6 => $"Specific data point, measurement, or property being monitored{topicInfo}", // Property level
            _ => hasData && topicsByPath.ContainsKey(fullPath) 
                ? $"Data point: {name}{topicInfo}"
                : $"Data container: {name}{topicInfo}"
        };
    }
*/
/*
    [McpServerTool]
    [Description("Test connectivity to the UNS Infrastructure GraphQL API")]
    public static async Task<string> TestGraphQLConnectivityAsync(
        IGraphQLClient graphQLClient,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple test query to check if GraphQL endpoint is accessible
            var query = new GraphQLRequest
            {
                Query = @"
                query {
                    namespaces
                }"
            };

            var response = await graphQLClient.SendQueryAsync<dynamic>(query, cancellationToken);

            if (response.Errors?.Any() == true)
            {
                var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "GraphQL endpoint is accessible but query failed",
                    errors = errorMessages,
                    connectivity = "GraphQL endpoint reachable"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "GraphQL connectivity test successful",
                connectivity = "GraphQL endpoint accessible",
                testResult = response.Data
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "GraphQL connectivity test failed",
                error = ex.Message,
                connectivity = "Cannot reach GraphQL endpoint",
                troubleshooting = new
                {
                    checkUiServerRunning = "Ensure the UI server is running with 'dotnet run --project src/UNSInfra.UI'",
                    checkPort = "Verify the UI server is listening on https://localhost:5001",
                    checkGraphQL = "GraphQL endpoint should be at https://localhost:5001/graphql"
                }
            }, _jsonOptions);
        }
    }
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
/// Represents namespace structure data from GraphQL
/// </summary>
public class NSTreeNodeData
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public HierarchyNodeData? HierarchyNode { get; set; }
    public NamespaceNodeData? Namespace { get; set; }
    public List<NSTreeNodeData> Children { get; set; } = new();
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