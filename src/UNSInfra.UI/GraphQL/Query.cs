using UNSInfra.Services.TopicBrowser;
using UNSInfra.Abstractions;
using UNSInfra.UI.GraphQL.Types;
using UNSInfra.Services;
using UNSInfra.Models.Hierarchy;

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

    /// <summary>
    /// Get the complete namespace structure including empty namespaces
    /// </summary>
    public async Task<IEnumerable<NamespaceStructureNode>> GetNamespaceStructureAsync(
        [Service] INamespaceStructureService namespaceStructureService,
        CancellationToken cancellationToken = default)
    {
        var nsTree = await namespaceStructureService.GetNamespaceStructureAsync();
        
        // Convert NSTreeNode to GraphQL-friendly structure
        return nsTree.Select(ConvertNSTreeNode);
    }

    /// <summary>
    /// Get the complete namespace structure as a flattened list (solves GraphQL infinite recursion issues)
    /// </summary>
    public async Task<IEnumerable<FlatNamespaceStructureNode>> GetFlatNamespaceStructureAsync(
        [Service] INamespaceStructureService namespaceStructureService,
        CancellationToken cancellationToken = default)
    {
        var nsTree = await namespaceStructureService.GetNamespaceStructureAsync();
        var flatNodes = new List<FlatNamespaceStructureNode>();
        
        // Flatten the tree structure
        FlattenNSTreeNodes(nsTree, null, flatNodes);
        
        return flatNodes;
    }

    private static NamespaceStructureNode ConvertNSTreeNode(NSTreeNode node)
    {
        return new NamespaceStructureNode
        {
            Name = node.Name,
            FullPath = node.FullPath,
            NodeType = node.NodeType.ToString(),
            HierarchyNode = node.HierarchyNode != null ? new HierarchyNodeInfo
            {
                Id = node.HierarchyNode.Id,
                Name = node.HierarchyNode.Name,
                Description = node.HierarchyNode.Description
            } : null,
            Namespace = node.Namespace != null ? new Types.NamespaceInfo
            {
                Id = node.Namespace.Id,
                Name = node.Namespace.Name,
                Type = node.Namespace.Type.ToString(),
                Description = node.Namespace.Description
            } : null,
            Children = node.Children.Select(ConvertNSTreeNode).ToArray()
        };
    }

    /// <summary>
    /// Get the complete namespace structure built dynamically from topic NSPaths
    /// This includes all hierarchy levels that have topics assigned, even if not explicitly configured
    /// </summary>
    public async Task<IEnumerable<NamespaceStructureNode>> GetTopicBasedNamespaceStructureAsync(
        [Service] CachedTopicBrowserService topicBrowserService,
        [Service] INamespaceStructureService namespaceStructureService,
        CancellationToken cancellationToken = default)
    {
        var topics = await topicBrowserService.GetLatestTopicStructureAsync();
        var topicList = topics.Where(t => !string.IsNullOrEmpty(t.NSPath)).ToList();
        
        return await BuildNamespaceStructureFromTopicsAsync(topicList, namespaceStructureService);
    }
    
    private static async Task<IEnumerable<NamespaceStructureNode>> BuildNamespaceStructureFromTopicsAsync(List<TopicInfo> topics, INamespaceStructureService namespaceStructureService)
    {
        // Get the active hierarchy configuration from the repository
        var hierarchyConfig = await GetActiveHierarchyConfigurationAsync(namespaceStructureService);
        
        // Group all NSPaths and build hierarchical structure
        var pathGroups = new Dictionary<string, HashSet<string>>();
        
        // Initialize root
        pathGroups[""] = new HashSet<string>();
        
        // Process all topic NSPaths to build path hierarchy
        foreach (var topic in topics)
        {
            if (string.IsNullOrEmpty(topic.NSPath)) continue;
            
            var pathParts = topic.NSPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";
            
            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                var parentPath = currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                
                // Initialize path structures
                if (!pathGroups.ContainsKey(parentPath))
                    pathGroups[parentPath] = new HashSet<string>();
                
                // Add this part as a child of the parent path
                pathGroups[parentPath].Add(part);
            }
        }
        
        // Build the tree structure starting from root
        var rootChildren = pathGroups.ContainsKey("") ? pathGroups[""] : new HashSet<string>();
        
        return rootChildren.Select(childName => 
            BuildNamespaceNodeFromPath(childName, childName, pathGroups, hierarchyConfig)).ToList();
    }
    
    private static async Task<HierarchyConfiguration?> GetActiveHierarchyConfigurationAsync(INamespaceStructureService namespaceStructureService)
    {
        return await namespaceStructureService.GetActiveHierarchyConfigurationAsync();
    }
    
    private static NamespaceStructureNode BuildNamespaceNodeFromPath(
        string name, string fullPath, Dictionary<string, HashSet<string>> pathGroups, HierarchyConfiguration? hierarchyConfig)
    {
        var hasChildren = pathGroups.ContainsKey(fullPath) && pathGroups[fullPath].Any();
        
        var node = new NamespaceStructureNode
        {
            Name = name,
            FullPath = fullPath,
            NodeType = "HierarchyNode", // All dynamically created nodes are hierarchy nodes
            HierarchyNode = new HierarchyNodeInfo
            {
                Id = Guid.NewGuid().ToString(), // Generate temporary ID
                Name = name,
                Description = GetHierarchyDescription(fullPath, hierarchyConfig)
            },
            Namespace = null, // These are hierarchy nodes, not namespaces
            Children = hasChildren 
                ? pathGroups[fullPath].Select(childName =>
                    BuildNamespaceNodeFromPath(childName, $"{fullPath}/{childName}", pathGroups, hierarchyConfig)).ToArray()
                : Array.Empty<NamespaceStructureNode>()
        };
        
        return node;
    }

    private static void FlattenNSTreeNodes(IEnumerable<NSTreeNode> nodes, string? parentId, List<FlatNamespaceStructureNode> result)
    {
        foreach (var node in nodes)
        {
            var flatNode = new FlatNamespaceStructureNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = node.Name,
                FullPath = node.FullPath,
                ParentId = parentId,
                NodeType = node.NodeType.ToString(),
                HierarchyNode = node.HierarchyNode != null ? new HierarchyNodeInfo
                {
                    Id = node.HierarchyNode.Id,
                    Name = node.HierarchyNode.Name,
                    Description = node.HierarchyNode.Description
                } : null,
                Namespace = node.Namespace != null ? new Types.NamespaceInfo
                {
                    Id = node.Namespace.Id,
                    Name = node.Namespace.Name,
                    Type = node.Namespace.Type.ToString(),
                    Description = node.Namespace.Description
                } : null,
                HasChildren = node.Children.Any(),
                Level = node.FullPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length
            };
            
            result.Add(flatNode);
            
            // Recursively flatten children
            if (node.Children.Any())
            {
                FlattenNSTreeNodes(node.Children, flatNode.Id, result);
            }
        }
    }
    
    private static string GetHierarchyDescription(string fullPath, HierarchyConfiguration? hierarchyConfig)
    {
        var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var level = pathParts.Length;
        var name = pathParts[^1]; // Last part
        
        // Try to get description from hierarchy configuration
        if (hierarchyConfig != null)
        {
            var hierarchyNodes = hierarchyConfig.Nodes.OrderBy(n => n.Order).ToList();
            if (level > 0 && level <= hierarchyNodes.Count)
            {
                var hierarchyNode = hierarchyNodes[level - 1]; // level is 1-based, array is 0-based
                var description = hierarchyNode.Description;
                
                if (!string.IsNullOrEmpty(description))
                {
                    return $"{hierarchyNode.Name}: {name} - {description}";
                }
            }
        }
        
        // Fallback to hardcoded ISA-95 descriptions if no repository data is available
        return level switch
        {
            1 => $"Enterprise: {name}",
            2 => $"Site: {name}",
            3 => $"Area: {name}",
            4 => $"Work Center: {name}",
            5 => $"Work Unit: {name}",
            _ => $"Hierarchy Level {level}: {name}"
        };
    }

}