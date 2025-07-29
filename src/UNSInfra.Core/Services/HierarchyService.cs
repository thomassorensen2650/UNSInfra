using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;

namespace UNSInfra.Services;

/// <summary>
/// Service for managing hierarchical structures and path operations.
/// </summary>
public class HierarchyService : IHierarchyService
{
    private readonly IHierarchyConfigurationRepository _repository;

    public HierarchyService(IHierarchyConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<HierarchyConfiguration> GetActiveConfigurationAsync()
    {
        var config = await _repository.GetActiveConfigurationAsync();
        if (config == null)
        {
            await _repository.EnsureDefaultConfigurationAsync();
            config = await _repository.GetActiveConfigurationAsync();
        }
        
        return config ?? throw new InvalidOperationException("No active hierarchy configuration found");
    }

    public async Task<DynamicHierarchicalPath> CreateDynamicPathFromStringAsync(string pathString)
    {
        var config = await GetActiveConfigurationAsync();
        return DynamicHierarchicalPath.FromPath(pathString, config);
    }

    public async Task<HierarchicalPath> CreatePathFromStringAsync(string pathString)
    {
        var config = await GetActiveConfigurationAsync();
        var parts = pathString.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var nodes = config.Nodes.OrderBy(n => n.Order).ToList();
        
        var path = new HierarchicalPath();
        
        // Map parts to hierarchy levels dynamically
        for (int i = 0; i < Math.Min(parts.Length, nodes.Count); i++)
        {
            path.SetValue(nodes[i].Name, parts[i]);
        }

        return path;
    }

    public async Task<ValidationResult> ValidatePathAsync(DynamicHierarchicalPath path)
    {
        var result = new ValidationResult { IsValid = true };
        var config = await GetActiveConfigurationAsync();
        var nodes = config.Nodes.OrderBy(n => n.Order).ToList();

        // Check required levels
        foreach (var node in nodes.Where(n => n.IsRequired))
        {
            var hasValue = !string.IsNullOrEmpty(path.GetValue(node.Name));
            if (!hasValue)
            {
                result.IsValid = false;
                result.Errors.Add($"Required hierarchy level '{node.Name}' is missing");
            }
        }

        // Check for gaps in hierarchy - ensure parent levels are specified when child levels are
        foreach (var node in nodes.Where(n => !string.IsNullOrEmpty(n.ParentNodeId)))
        {
            var hasValue = !string.IsNullOrEmpty(path.GetValue(node.Name));
            var parentNode = config.GetNodeById(node.ParentNodeId!);
            
            if (hasValue && parentNode != null)
            {
                var hasParentValue = !string.IsNullOrEmpty(path.GetValue(parentNode.Name));
                if (!hasParentValue && parentNode.IsRequired)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Cannot specify '{node.Name}' without specifying required parent '{parentNode.Name}'");
                }
            }
        }

        return result;
    }

    public async Task<ValidationResult> ValidatePathAsync(HierarchicalPath path)
    {
        var result = new ValidationResult { IsValid = true };
        var config = await GetActiveConfigurationAsync();
        var nodes = config.Nodes.OrderBy(n => n.Order).ToList();

        // Check required levels using dynamic values first, then fallback to legacy properties
        foreach (var node in nodes.Where(n => n.IsRequired))
        {
            var hasValue = !string.IsNullOrEmpty(path.GetValue(node.Name));
            if (!hasValue)
            {
                result.IsValid = false;
                result.Errors.Add($"Required hierarchy level '{node.Name}' is missing");
            }
        }

        return result;
    }

    public async Task<List<string>> GetHierarchyLevelsAsync()
    {
        var config = await GetActiveConfigurationAsync();
        return config.Nodes.OrderBy(n => n.Order).Select(n => n.Name).ToList();
    }

    public async Task<HierarchyNode?> GetNodeByNameAsync(string levelName)
    {
        var config = await GetActiveConfigurationAsync();
        return config.Nodes.FirstOrDefault(n => 
            string.Equals(n.Name, levelName, StringComparison.OrdinalIgnoreCase));
    }

}