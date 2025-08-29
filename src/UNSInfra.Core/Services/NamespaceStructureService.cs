using UNSInfra.Models.Namespace;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.Events;

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

    public NamespaceStructureService(
        IHierarchyConfigurationRepository hierarchyRepository,
        INamespaceConfigurationRepository namespaceRepository,
        INSTreeInstanceRepository nsTreeInstanceRepository,
        IEventBus eventBus)
    {
        _hierarchyRepository = hierarchyRepository;
        _namespaceRepository = namespaceRepository;
        _nsTreeInstanceRepository = nsTreeInstanceRepository;
        _eventBus = eventBus;
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