using UNSInfra.Models.Namespace;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.Events;
using UNSInfra.Services.TopicBrowser;

namespace UNSInfra.Services;

/// <summary>
/// Service for building and managing the hierarchical namespace structure (NS) tree.
/// </summary>
public class NamespaceStructureService : INamespaceStructureService
{
    private readonly IHierarchyConfigurationRepository _hierarchyRepository;
    private readonly INamespaceConfigurationRepository _namespaceRepository;
    private readonly INSTreeInstanceRepository _nsTreeInstanceRepository;
    private readonly IEventBus _eventBus;
    private readonly CachedTopicBrowserService _topicBrowserService;

    public NamespaceStructureService(
        IHierarchyConfigurationRepository hierarchyRepository,
        INamespaceConfigurationRepository namespaceRepository,
        INSTreeInstanceRepository nsTreeInstanceRepository,
        IEventBus eventBus,
        CachedTopicBrowserService topicBrowserService)
    {
        _hierarchyRepository = hierarchyRepository;
        _namespaceRepository = namespaceRepository;
        _nsTreeInstanceRepository = nsTreeInstanceRepository;
        _eventBus = eventBus;
        _topicBrowserService = topicBrowserService;
    }

    public async Task<IEnumerable<NSTreeNode>> GetNamespaceStructureAsync()
    {
        var hierarchyConfig = await GetActiveHierarchyConfigurationAsync();
        var namespaces = await _namespaceRepository.GetAllNamespaceConfigurationsAsync(activeOnly: true);
        var nsTreeInstances = await _nsTreeInstanceRepository.GetAllInstancesAsync(activeOnly: true);
        
        var rootNodes = new List<NSTreeNode>();
        var instances = nsTreeInstances.ToList();
        var namespacesList = namespaces.ToList();
        
        // Build tree from NS tree instances
        var rootInstances = instances.Where(i => string.IsNullOrEmpty(i.ParentInstanceId));
        
        foreach (var rootInstance in rootInstances)
        {
            var treeNode = await BuildNSTreeNodeAsync(rootInstance, instances, namespacesList, hierarchyConfig);
            if (treeNode != null)
            {
                rootNodes.Add(treeNode);
            }
        }
        return rootNodes;
    }

    private async Task<NSTreeNode?> BuildNSTreeNodeAsync(
        NSTreeInstance instance, 
        List<NSTreeInstance> allInstances, 
        List<NamespaceConfiguration> allNamespaces,
        HierarchyConfiguration? hierarchyConfig)
    {
        if (hierarchyConfig == null) return null;
        
        var hierarchyNode = hierarchyConfig.GetNodeById(instance.HierarchyNodeId);
        if (hierarchyNode == null) return null;

        var node = new NSTreeNode
        {
            Name = instance.Name,
            FullPath = GetInstancePath(instance, allInstances),
            NodeType = NSNodeType.HierarchyNode,
            HierarchyNode = hierarchyNode,
            Instance = instance,
            CanHaveHierarchyChildren = hierarchyNode.AllowedChildNodeIds.Any(),
            CanHaveNamespaceChildren = true
        };

        // Add child instances
        var childInstances = allInstances.Where(i => i.ParentInstanceId == instance.Id);
        foreach (var childInstance in childInstances)
        {
            var childNode = await BuildNSTreeNodeAsync(childInstance, allInstances, allNamespaces, hierarchyConfig);
            if (childNode != null)
            {
                node.Children.Add(childNode);
            }
        }

        // Add namespaces that belong to this instance
        var instancePath = instance.GetHierarchicalPath(allInstances, hierarchyConfig);
        var matchingNamespaces = allNamespaces.Where(ns => 
            PathsMatch(ns.HierarchicalPath, instancePath) && 
            string.IsNullOrEmpty(ns.ParentNamespaceId));

        foreach (var ns in matchingNamespaces)
        {
            var nsNode = new NSTreeNode
            {
                Name = ns.Name,
                FullPath = $"{node.FullPath}/{ns.Name}",
                NodeType = NSNodeType.Namespace,
                Namespace = ns,
                CanHaveHierarchyChildren = false,
                CanHaveNamespaceChildren = true
            };

            // Add nested namespaces
            await AddNestedNamespacesAsync(nsNode, allNamespaces);
            node.Children.Add(nsNode);
        }

        return node;
    }

    private string GetInstancePath(NSTreeInstance instance, List<NSTreeInstance> allInstances)
    {
        var pathParts = new List<string>();
        var current = instance;
        
        while (current != null)
        {
            pathParts.Insert(0, current.Name);
            
            if (!string.IsNullOrEmpty(current.ParentInstanceId))
            {
                current = allInstances.FirstOrDefault(i => i.Id == current.ParentInstanceId);
            }
            else
            {
                break;
            }
        }
        
        return string.Join("/", pathParts);
    }

    private bool PathsMatch(HierarchicalPath path1, HierarchicalPath path2)
    {
        return GetHierarchyPathKey(path1) == GetHierarchyPathKey(path2);
    }

    public async Task<IEnumerable<HierarchyNode>> GetAvailableHierarchyNodesAsync(string? parentNodeId)
    {
        var hierarchyConfig = await GetActiveHierarchyConfigurationAsync();
        if (hierarchyConfig == null) return Enumerable.Empty<HierarchyNode>();

        if (string.IsNullOrEmpty(parentNodeId))
        {
            // Return root nodes
            return hierarchyConfig.GetRootNodes();
        }

        // Return allowed child nodes
        var parentNode = hierarchyConfig.GetNodeById(parentNodeId);
        if (parentNode == null) return Enumerable.Empty<HierarchyNode>();

        return parentNode.AllowedChildNodeIds
            .Select(childId => hierarchyConfig.GetNodeById(childId))
            .Where(node => node != null)
            .Cast<HierarchyNode>();
    }

    public async Task<NSTreeNode> CreateHierarchyNodeInstanceAsync(string hierarchyNodeId, string parentPath, string name)
    {
        var hierarchyConfig = await GetActiveHierarchyConfigurationAsync();
        var hierarchyNode = hierarchyConfig?.GetNodeById(hierarchyNodeId);
        
        if (hierarchyNode == null)
            throw new ArgumentException($"Hierarchy node {hierarchyNodeId} not found");

        var fullPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
        
        return new NSTreeNode
        {
            Name = name,
            FullPath = fullPath,
            NodeType = NSNodeType.HierarchyNode,
            HierarchyNode = hierarchyNode,
            CanHaveHierarchyChildren = hierarchyNode.AllowedChildNodeIds.Any(),
            CanHaveNamespaceChildren = true
        };
    }

    public async Task<NSTreeNode> CreateNamespaceAsync(string parentPath, NamespaceConfiguration namespaceConfig)
    {
        // Validate that no namespace with the same name exists in the same parent path
        await ValidateUniqueNamespaceNameAsync(parentPath, namespaceConfig.Name, namespaceConfig.HierarchicalPath);
        
        await _namespaceRepository.SaveNamespaceConfigurationAsync(namespaceConfig);
        
        var fullPath = string.IsNullOrEmpty(parentPath) ? namespaceConfig.Name : $"{parentPath}/{namespaceConfig.Name}";
        
        // Publish namespace structure changed event for auto-mapper cache refresh
        var namespaceChangedEvent = new NamespaceStructureChangedEvent(
            ChangedNamespace: fullPath,
            ChangeType: "Added",
            ChangedBy: namespaceConfig.CreatedBy
        );
        await _eventBus.PublishAsync(namespaceChangedEvent);
        
        return new NSTreeNode
        {
            Name = namespaceConfig.Name,
            FullPath = fullPath,
            NodeType = NSNodeType.Namespace,
            Namespace = namespaceConfig,
            CanHaveHierarchyChildren = false,
            CanHaveNamespaceChildren = true
        };
    }

    public async Task<HierarchicalPath> GetHierarchicalPathFromNSPathAsync(string nsPath)
    {
        var hierarchyConfig = await GetActiveHierarchyConfigurationAsync();
        if (hierarchyConfig == null) return new HierarchicalPath();

        var path = new HierarchicalPath();
        var pathParts = nsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var hierarchyLevels = hierarchyConfig.Nodes.OrderBy(n => n.Order).ToList();
        
        // Map path parts to hierarchy levels based on the configured hierarchy
        for (int i = 0; i < Math.Min(pathParts.Length, hierarchyLevels.Count); i++)
        {
            path.SetValue(hierarchyLevels[i].Name, pathParts[i]);
        }
        
        return path;
    }

    public async Task<HierarchicalPath> GetHierarchicalPathFromInstanceIdAsync(string instanceId)
    {
        var hierarchyConfig = await GetActiveHierarchyConfigurationAsync();
        if (hierarchyConfig == null) return new HierarchicalPath();

        var allInstances = await _nsTreeInstanceRepository.GetAllInstancesAsync(activeOnly: true);
        var instance = allInstances.FirstOrDefault(i => i.Id == instanceId);
        
        if (instance == null) return new HierarchicalPath();
        
        return instance.GetHierarchicalPath(allInstances.ToList(), hierarchyConfig);
    }

    public async Task<HierarchyConfiguration?> GetActiveHierarchyConfigurationAsync()
    {
        var configs = await _hierarchyRepository.GetAllConfigurationsAsync();
        return configs.FirstOrDefault(c => c.IsActive);
    }

    private async Task<NSTreeNode?> BuildHierarchyNodeTreeAsync(
        HierarchicalPath hierarchyPath, 
        HierarchyConfiguration hierarchyConfig, 
        List<NamespaceConfiguration> allNamespaces)
    {
        var orderedNodes = hierarchyConfig.Nodes.OrderBy(n => n.Order).ToList();
        NSTreeNode? rootNode = null;
        NSTreeNode? currentNode = null;
        var fullPath = new List<string>();

        // Build the hierarchy chain based on the configured hierarchy levels
        foreach (var hierarchyNode in orderedNodes)
        {
            var value = hierarchyPath.GetValue(hierarchyNode.Name);
            if (string.IsNullOrEmpty(value)) break;

            fullPath.Add(value);
            var node = new NSTreeNode
            {
                Name = value,
                FullPath = string.Join("/", fullPath),
                NodeType = NSNodeType.HierarchyNode,
                HierarchyNode = hierarchyNode,
                CanHaveHierarchyChildren = hierarchyNode.AllowedChildNodeIds.Any(),
                CanHaveNamespaceChildren = true
            };

            if (rootNode == null)
            {
                rootNode = node;
                currentNode = node;
            }
            else
            {
                currentNode!.Children.Add(node);
                currentNode = node;
            }
        }

        // Add namespaces at the appropriate level
        if (currentNode != null)
        {
            var matchingNamespaces = allNamespaces.Where(ns => 
                GetHierarchyPathKey(ns.HierarchicalPath) == GetHierarchyPathKey(hierarchyPath) &&
                string.IsNullOrEmpty(ns.ParentNamespaceId));

            foreach (var ns in matchingNamespaces)
            {
                var nsNode = new NSTreeNode
                {
                    Name = ns.Name,
                    FullPath = $"{currentNode.FullPath}/{ns.Name}",
                    NodeType = NSNodeType.Namespace,
                    Namespace = ns,
                    CanHaveHierarchyChildren = false,
                    CanHaveNamespaceChildren = true
                };

                // Add nested namespaces
                await AddNestedNamespacesAsync(nsNode, allNamespaces);
                currentNode.Children.Add(nsNode);
            }
        }

        return rootNode;
    }

    private async Task AddNestedNamespacesAsync(NSTreeNode parentNode, List<NamespaceConfiguration> allNamespaces)
    {
        if (parentNode.Namespace == null) return;

        var childNamespaces = allNamespaces.Where(ns => ns.ParentNamespaceId == parentNode.Namespace.Id);
        
        foreach (var childNs in childNamespaces)
        {
            var childNode = new NSTreeNode
            {
                Name = childNs.Name,
                FullPath = $"{parentNode.FullPath}/{childNs.Name}",
                NodeType = NSNodeType.Namespace,
                Namespace = childNs,
                CanHaveHierarchyChildren = false,
                CanHaveNamespaceChildren = true
            };

            await AddNestedNamespacesAsync(childNode, allNamespaces);
            parentNode.Children.Add(childNode);
        }
    }

    public async Task<NSTreeInstance> AddHierarchyInstanceAsync(string hierarchyNodeId, string name, string? parentInstanceId)
    {
        // Validate that no hierarchy instance with the same name exists under the same parent
        await ValidateUniqueHierarchyInstanceNameAsync(name, parentInstanceId);
        
        var instance = new NSTreeInstance
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            HierarchyNodeId = hierarchyNodeId,
            ParentInstanceId = parentInstanceId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        await _nsTreeInstanceRepository.SaveInstanceAsync(instance);
        
        // Publish namespace structure changed event for auto-mapper cache refresh
        var namespaceChangedEvent = new NamespaceStructureChangedEvent(
            ChangedNamespace: name,
            ChangeType: "Added",
            ChangedBy: "user"
        );
        await _eventBus.PublishAsync(namespaceChangedEvent);
        
        return instance;
    }

    public async Task DeleteInstanceAsync(string instanceId)
    {
        // Get the instance before deleting to publish the event
        var instance = await _nsTreeInstanceRepository.GetInstanceByIdAsync(instanceId);
        
        await _nsTreeInstanceRepository.DeleteInstanceAsync(instanceId);
        
        // Publish namespace structure changed event for auto-mapper cache refresh
        if (instance != null)
        {
            var namespaceChangedEvent = new NamespaceStructureChangedEvent(
                ChangedNamespace: instance.Name,
                ChangeType: "Deleted",
                ChangedBy: "user"
            );
            await _eventBus.PublishAsync(namespaceChangedEvent);
        }
    }

    public async Task<bool> CanDeleteInstanceAsync(string instanceId)
    {
        return await _nsTreeInstanceRepository.CanDeleteInstanceAsync(instanceId);
    }

    private async Task<string> GetHierarchyPathKeyAsync(HierarchicalPath path)
    {
        var hierarchyConfig = await GetActiveHierarchyConfigurationAsync();
        if (hierarchyConfig == null)
        {
            // Fallback to simple join if no hierarchy config
            return string.Join("/", path.Values.Values.Where(v => !string.IsNullOrEmpty(v)));
        }

        // Use the configured hierarchy order
        var orderedNodes = hierarchyConfig.Nodes.OrderBy(n => n.Order).ToList();
        var parts = new List<string>();
        
        foreach (var node in orderedNodes)
        {
            var value = path.GetValue(node.Name);
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add(value);
            }
        }
        
        return string.Join("/", parts);
    }

    private string GetHierarchyPathKey(HierarchicalPath path)
    {
        // Simple version for synchronous use - just return all values joined
        return string.Join("/", path.Values.Values.Where(v => !string.IsNullOrEmpty(v)));
    }

    /// <summary>
    /// Validates that a namespace name is unique within the same parent path and hierarchical location.
    /// </summary>
    /// <param name="parentPath">The parent path where the namespace will be created</param>
    /// <param name="namespaceName">The name of the namespace to validate</param>
    /// <param name="hierarchicalPath">The hierarchical path of the namespace</param>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate namespace name is found</exception>
    private async Task ValidateUniqueNamespaceNameAsync(string parentPath, string namespaceName, HierarchicalPath hierarchicalPath)
    {
        // Only check for duplicates within the same parent path in the NS tree structure
        // This is the most reliable check since it validates against actual configured namespaces
        var nsTreeStructure = await GetNamespaceStructureAsync();
        var parentNode = FindNodeByPath(nsTreeStructure, parentPath);
        
        if (parentNode != null)
        {
            var existingChildNamespaces = parentNode.Children
                .Where(child => child.NodeType == NSNodeType.Namespace && child.Namespace != null)
                .Select(child => child.Name);

            if (existingChildNamespaces.Any(name => 
                string.Equals(name, namespaceName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"A namespace named '{namespaceName}' already exists under '{parentPath}'. " +
                    "Namespace names must be unique within the same parent location.");
            }
        }
        
        // Secondary check: Look for actual namespace configurations with the same name and exact hierarchical context
        var existingNamespaces = await _namespaceRepository.GetAllNamespaceConfigurationsAsync(activeOnly: true);
        var parentNamespaceId = await GetParentNamespaceIdAsync(parentPath);
        
        // Check for conflicts within the same parent namespace OR same hierarchical level
        var conflictingNamespaces = existingNamespaces.Where(ns => 
            string.Equals(ns.Name, namespaceName, StringComparison.OrdinalIgnoreCase) &&
            (ns.ParentNamespaceId == parentNamespaceId ||
             (parentNamespaceId == null && ns.ParentNamespaceId == null && 
              HierarchicalPathsAreSameLevel(ns.HierarchicalPath, hierarchicalPath))));

        if (conflictingNamespaces.Any())
        {
            var conflictInfo = conflictingNamespaces.First();
            var conflictPathStr = await GetHierarchicalPathDisplayAsync(conflictInfo.HierarchicalPath);
            throw new InvalidOperationException(
                $"A namespace named '{namespaceName}' already exists at '{conflictPathStr}'. " +
                "Namespace names must be unique within the same hierarchical level.");
        }
    }

    /// <summary>
    /// Checks if two hierarchical paths are at the same level (same values for all hierarchy levels).
    /// Used to prevent namespace conflicts only within the same hierarchical context.
    /// </summary>
    /// <param name="path1">First hierarchical path</param>
    /// <param name="path2">Second hierarchical path</param>
    /// <returns>True if paths are at exactly the same hierarchical level</returns>
    private bool HierarchicalPathsAreSameLevel(HierarchicalPath path1, HierarchicalPath path2)
    {
        // Two paths are at the same level if all their hierarchy values match exactly
        var keys = new[] { "Enterprise", "Site", "Area", "WorkCenter", "WorkUnit" };
        
        foreach (var key in keys)
        {
            var value1 = path1.GetValue(key) ?? "";
            var value2 = path2.GetValue(key) ?? "";
            
            if (!string.Equals(value1, value2, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        
        return true;
    }

    /// <summary>
    /// Gets a display string for a hierarchical path.
    /// </summary>
    /// <param name="hierarchicalPath">The hierarchical path to display</param>
    /// <returns>A formatted display string</returns>
    private async Task<string> GetHierarchicalPathDisplayAsync(HierarchicalPath hierarchicalPath)
    {
        return await GetHierarchyPathKeyAsync(hierarchicalPath);
    }

    /// <summary>
    /// Gets the parent namespace ID from a parent path by finding the namespace at that path
    /// </summary>
    private async Task<string?> GetParentNamespaceIdAsync(string parentPath)
    {
        if (string.IsNullOrEmpty(parentPath))
            return null;
            
        // Find the namespace that corresponds to this parent path
        var nsTreeStructure = await GetNamespaceStructureAsync();
        var parentNode = FindNodeByPath(nsTreeStructure, parentPath);
        
        // Return the namespace ID if the parent node is a namespace
        return parentNode?.NodeType == NSNodeType.Namespace ? parentNode.Namespace?.Id : null;
    }

    /// <summary>
    /// Finds a node in the NS tree structure by its full path.
    /// </summary>
    /// <param name="nodes">The root nodes to search</param>
    /// <param name="path">The path to find</param>
    /// <returns>The node if found, null otherwise</returns>
    private NSTreeNode? FindNodeByPath(IEnumerable<NSTreeNode> nodes, string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase))
                return node;

            var foundInChildren = FindNodeByPath(node.Children, path);
            if (foundInChildren != null)
                return foundInChildren;
        }

        return null;
    }

    /// <summary>
    /// Validates that a hierarchy instance name is unique within the same parent.
    /// </summary>
    /// <param name="instanceName">The name of the instance to validate</param>
    /// <param name="parentInstanceId">The parent instance ID (null for root level)</param>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate instance name is found</exception>
    private async Task ValidateUniqueHierarchyInstanceNameAsync(string instanceName, string? parentInstanceId)
    {
        var existingInstances = await _nsTreeInstanceRepository.GetAllInstancesAsync(activeOnly: true);
        
        // Check for instances with the same name under the same parent
        var conflictingInstances = existingInstances.Where(instance =>
            string.Equals(instance.Name, instanceName, StringComparison.OrdinalIgnoreCase) &&
            instance.ParentInstanceId == parentInstanceId);

        if (conflictingInstances.Any())
        {
            var locationDescription = string.IsNullOrEmpty(parentInstanceId)
                ? "the root level"
                : $"the same parent location";
                
            throw new InvalidOperationException(
                $"A hierarchy instance named '{instanceName}' already exists at {locationDescription}. " +
                "Instance names must be unique within the same parent location.");
        }
    }

    /// <inheritdoc />
    public async Task<(bool CanDelete, string? Reason)> CanDeleteNamespaceAsync(string namespaceId)
    {
        try
        {
            var namespaceConfig = await _namespaceRepository.GetNamespaceConfigurationAsync(namespaceId);
            if (namespaceConfig == null)
            {
                return (false, "Namespace not found");
            }

            // Check for child namespaces
            var allNamespaces = await _namespaceRepository.GetAllNamespaceConfigurationsAsync(activeOnly: true);
            var childNamespaces = allNamespaces.Where(ns => ns.ParentNamespaceId == namespaceId).ToList();

            // Check for topics mapped to this namespace
            var allTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
            var nsTreeStructure = await GetNamespaceStructureAsync();
            var targetNode = FindNamespaceNodeById(nsTreeStructure, namespaceId);
            
            var mappedTopics = new List<string>();
            if (targetNode != null)
            {
                // Find topics mapped to this namespace path and all child namespace paths
                var namespacePaths = new List<string> { targetNode.FullPath };
                await CollectChildNamespacePaths(targetNode, namespacePaths);

                mappedTopics = allTopics
                    .Where(topic => namespacePaths.Any(path => 
                        !string.IsNullOrEmpty(topic.NSPath) && 
                        topic.NSPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)))
                    .Select(topic => topic.Topic)
                    .ToList();
            }

            // Build warning message
            var warnings = new List<string>();
            if (childNamespaces.Any())
            {
                warnings.Add($"{childNamespaces.Count} child namespace(s)");
            }
            if (mappedTopics.Any())
            {
                warnings.Add($"{mappedTopics.Count} mapped topic(s)");
            }

            var reason = warnings.Any() 
                ? $"This will also delete: {string.Join(", ", warnings)}"
                : null;

            return (true, reason);
        }
        catch (Exception ex)
        {
            return (false, $"Error checking deletion status: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteNamespaceAsync(string namespaceId)
    {
        try
        {
            var namespaceConfig = await _namespaceRepository.GetNamespaceConfigurationAsync(namespaceId);
            if (namespaceConfig == null)
            {
                return false;
            }

            // Get the full namespace tree structure to find the namespace path
            var nsTreeStructure = await GetNamespaceStructureAsync();
            var targetNode = FindNamespaceNodeById(nsTreeStructure, namespaceId);
            if (targetNode == null)
            {
                return false;
            }

            // Collect all namespace paths that will be affected (this namespace + all children)
            var affectedNamespacePaths = new List<string> { targetNode.FullPath };
            await CollectChildNamespacePaths(targetNode, affectedNamespacePaths);

            // 1. Clean up topic mappings for all affected namespace paths
            var allTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
            var topicsToUpdate = allTopics
                .Where(topic => affectedNamespacePaths.Any(path => 
                    !string.IsNullOrEmpty(topic.NSPath) && 
                    topic.NSPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var topic in topicsToUpdate)
            {
                // Clear the namespace mapping
                await _topicBrowserService.UpdateTopicConfigurationAsync(new TopicConfiguration
                {
                    Topic = topic.Topic,
                    NSPath = string.Empty, // Clear namespace mapping
                    UNSName = topic.UNSName,
                    Description = topic.Description,
                    Path = new HierarchicalPath(), // Reset to empty hierarchical path
                    IsActive = topic.IsActive,
                    CreatedAt = topic.CreatedAt,
                    ModifiedAt = DateTime.UtcNow
                });
            }

            // 2. Delete all child namespaces recursively
            var allNamespaces = await _namespaceRepository.GetAllNamespaceConfigurationsAsync(activeOnly: true);
            var childNamespaceIds = new List<string>();
            await CollectChildNamespaceIds(namespaceId, allNamespaces.ToList(), childNamespaceIds);

            foreach (var childId in childNamespaceIds)
            {
                await _namespaceRepository.DeleteNamespaceConfigurationAsync(childId);
            }

            // 3. Delete the target namespace
            await _namespaceRepository.DeleteNamespaceConfigurationAsync(namespaceId);

            // 4. Publish namespace structure changed event
            var namespaceChangedEvent = new NamespaceStructureChangedEvent(
                ChangedNamespace: targetNode.FullPath,
                ChangeType: "Deleted",
                ChangedBy: "user"
            );
            await _eventBus.PublishAsync(namespaceChangedEvent);

            return true;
        }
        catch (Exception ex)
        {
            // Log error but don't expose internal details
            return false;
        }
    }

    /// <summary>
    /// Finds a namespace node by its namespace ID in the tree structure.
    /// </summary>
    private NSTreeNode? FindNamespaceNodeById(IEnumerable<NSTreeNode> nodes, string namespaceId)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == NSNodeType.Namespace && node.Namespace?.Id == namespaceId)
                return node;

            var foundInChildren = FindNamespaceNodeById(node.Children, namespaceId);
            if (foundInChildren != null)
                return foundInChildren;
        }
        return null;
    }

    /// <summary>
    /// Recursively collects all child namespace paths under a given namespace node.
    /// </summary>
    private async Task CollectChildNamespacePaths(NSTreeNode namespaceNode, List<string> collectedPaths)
    {
        foreach (var child in namespaceNode.Children.Where(c => c.NodeType == NSNodeType.Namespace))
        {
            collectedPaths.Add(child.FullPath);
            await CollectChildNamespacePaths(child, collectedPaths);
        }
    }

    /// <summary>
    /// Recursively collects all child namespace IDs under a given namespace.
    /// </summary>
    private async Task CollectChildNamespaceIds(string parentNamespaceId, List<NamespaceConfiguration> allNamespaces, List<string> collectedIds)
    {
        var children = allNamespaces.Where(ns => ns.ParentNamespaceId == parentNamespaceId).ToList();
        
        foreach (var child in children)
        {
            collectedIds.Add(child.Id);
            await CollectChildNamespaceIds(child.Id, allNamespaces, collectedIds);
        }
    }
}

/// <summary>
/// Comparer for HierarchicalPath objects to enable Distinct operations.
/// </summary>
public class HierarchicalPathComparer : IEqualityComparer<HierarchicalPath>
{
    public bool Equals(HierarchicalPath? x, HierarchicalPath? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        
        // Compare all values in both dictionaries
        if (x.Values.Count != y.Values.Count) return false;
        
        foreach (var kvp in x.Values)
        {
            if (!y.Values.TryGetValue(kvp.Key, out var yValue) || 
                !string.Equals(kvp.Value, yValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        
        return true;
    }

    public int GetHashCode(HierarchicalPath obj)
    {
        var hash = new HashCode();
        foreach (var kvp in obj.Values.OrderBy(x => x.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value?.ToLowerInvariant());
        }
        return hash.ToHashCode();
    }
}