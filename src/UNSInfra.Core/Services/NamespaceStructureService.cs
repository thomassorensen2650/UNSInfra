using UNSInfra.Models.Namespace;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;

namespace UNSInfra.Services;

/// <summary>
/// Service for building and managing the hierarchical namespace structure (NS) tree.
/// </summary>
public class NamespaceStructureService : INamespaceStructureService
{
    private readonly IHierarchyConfigurationRepository _hierarchyRepository;
    private readonly INamespaceConfigurationRepository _namespaceRepository;
    private readonly INSTreeInstanceRepository _nsTreeInstanceRepository;

    public NamespaceStructureService(
        IHierarchyConfigurationRepository hierarchyRepository,
        INamespaceConfigurationRepository namespaceRepository,
        INSTreeInstanceRepository nsTreeInstanceRepository)
    {
        _hierarchyRepository = hierarchyRepository;
        _namespaceRepository = namespaceRepository;
        _nsTreeInstanceRepository = nsTreeInstanceRepository;
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
        
        // If there are no hierarchy instances but there are namespaces, show them as root-level nodes
        if (!rootNodes.Any() && namespacesList.Any())
        {
            var rootNamespaces = namespacesList.Where(ns => string.IsNullOrEmpty(ns.ParentNamespaceId));
            
            foreach (var ns in rootNamespaces)
            {
                var nsNode = new NSTreeNode
                {
                    Name = ns.Name,
                    FullPath = ns.Name,
                    NodeType = NSNodeType.Namespace,
                    Namespace = ns,
                    CanHaveHierarchyChildren = false,
                    CanHaveNamespaceChildren = true
                };

                // Add nested namespaces
                await AddNestedNamespacesAsync(nsNode, namespacesList);
                rootNodes.Add(nsNode);
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
        
        // Map path parts to hierarchy levels
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

    private async Task<HierarchyConfiguration?> GetActiveHierarchyConfigurationAsync()
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

        // Build the hierarchy chain
        foreach (var hierarchyNode in orderedNodes)
        {
            var value = hierarchyPath.GetValue(hierarchyNode.Name);
            if (string.IsNullOrEmpty(value)) break;

            var node = new NSTreeNode
            {
                Name = value,
                FullPath = rootNode == null ? value : $"{rootNode.FullPath}/{value}",
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
                GetHierarchyPathKey(ns.HierarchicalPath) == GetHierarchyPathKey(hierarchyPath));

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
        return instance;
    }

    public async Task DeleteInstanceAsync(string instanceId)
    {
        await _nsTreeInstanceRepository.DeleteInstanceAsync(instanceId);
    }

    public async Task<bool> CanDeleteInstanceAsync(string instanceId)
    {
        return await _nsTreeInstanceRepository.CanDeleteInstanceAsync(instanceId);
    }

    private string GetHierarchyPathKey(HierarchicalPath path)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(path.Enterprise)) parts.Add(path.Enterprise);
        if (!string.IsNullOrEmpty(path.Site)) parts.Add(path.Site);
        if (!string.IsNullOrEmpty(path.Area)) parts.Add(path.Area);
        if (!string.IsNullOrEmpty(path.WorkCenter)) parts.Add(path.WorkCenter);
        if (!string.IsNullOrEmpty(path.WorkUnit)) parts.Add(path.WorkUnit);
        return string.Join("/", parts);
    }
}