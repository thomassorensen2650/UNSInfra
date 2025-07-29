using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Repositories;

/// <summary>
/// In-memory implementation of the hierarchy configuration repository.
/// </summary>
public class InMemoryHierarchyConfigurationRepository : IHierarchyConfigurationRepository
{
    private readonly Dictionary<string, HierarchyConfiguration> _configurations = new();
    private string? _activeConfigurationId;

    public Task<HierarchyConfiguration?> GetActiveConfigurationAsync()
    {
        if (_activeConfigurationId != null && _configurations.TryGetValue(_activeConfigurationId, out var config))
        {
            return Task.FromResult<HierarchyConfiguration?>(config);
        }
        return Task.FromResult<HierarchyConfiguration?>(null);
    }

    public Task<IEnumerable<HierarchyConfiguration>> GetAllConfigurationsAsync()
    {
        return Task.FromResult(_configurations.Values.AsEnumerable());
    }

    public Task<HierarchyConfiguration?> GetConfigurationByIdAsync(string id)
    {
        _configurations.TryGetValue(id, out var config);
        return Task.FromResult(config);
    }

    public Task SaveConfigurationAsync(HierarchyConfiguration configuration)
    {
        configuration.ModifiedAt = DateTime.UtcNow;
        _configurations[configuration.Id] = configuration;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteConfigurationAsync(string id)
    {
        if (_configurations.TryGetValue(id, out var config))
        {
            // Cannot delete system-defined configurations
            if (config.IsSystemDefined)
            {
                return Task.FromResult(false);
            }

            // If this is the active configuration, clear the active ID
            if (_activeConfigurationId == id)
            {
                _activeConfigurationId = null;
            }

            _configurations.Remove(id);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> SetActiveConfigurationAsync(string id)
    {
        if (_configurations.ContainsKey(id))
        {
            // Update IsActive flags
            foreach (var config in _configurations.Values)
            {
                config.IsActive = config.Id == id;
            }
            _activeConfigurationId = id;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public async Task EnsureDefaultConfigurationAsync()
    {
        const string defaultConfigId = "isa-s95-standard";
        
        if (!_configurations.ContainsKey(defaultConfigId))
        {
            var defaultConfig = CreateISAS95Configuration();
            await SaveConfigurationAsync(defaultConfig);
            await SetActiveConfigurationAsync(defaultConfigId);
        }
        else if (_activeConfigurationId == null)
        {
            await SetActiveConfigurationAsync(defaultConfigId);
        }
    }

    private static HierarchyConfiguration CreateISAS95Configuration()
    {
        return new HierarchyConfiguration
        {
            Id = "isa-s95-standard",
            Name = "ISA-S95 Standard",
            Description = "Standard ISA-S95 hierarchical structure for manufacturing systems",
            IsActive = true,
            IsSystemDefined = true,
            Nodes = new List<HierarchyNode>
            {
                new()
                {
                    Id = "enterprise",
                    Name = "Enterprise",
                    IsRequired = true,
                    Order = 0,
                    Description = "Top level of the hierarchy - the entire organization or company",
                    AllowedChildNodeIds = new List<string> { "site" },
                    ParentNodeId = null,
                    Metadata = new Dictionary<string, object>
                    {
                        { "level", 0 },
                        { "example", "AcmeCorp" }
                    }
                },
                new()
                {
                    Id = "site",
                    Name = "Site",
                    IsRequired = true,
                    Order = 1,
                    Description = "Physical location or facility within the enterprise",
                    AllowedChildNodeIds = new List<string> { "area" },
                    ParentNodeId = "enterprise",
                    Metadata = new Dictionary<string, object>
                    {
                        { "level", 1 },
                        { "example", "Factory_A" }
                    }
                },
                new()
                {
                    Id = "area",
                    Name = "Area",
                    IsRequired = true,
                    Order = 2,
                    Description = "Production area or department within a site",
                    AllowedChildNodeIds = new List<string> { "workcenter" },
                    ParentNodeId = "site",
                    Metadata = new Dictionary<string, object>
                    {
                        { "level", 2 },
                        { "example", "Assembly_Line_1" }
                    }
                },
                new()
                {
                    Id = "workcenter",
                    Name = "WorkCenter",
                    IsRequired = false,
                    Order = 3,
                    Description = "Group of equipment or workstations performing similar functions",
                    AllowedChildNodeIds = new List<string> { "workunit" },
                    ParentNodeId = "area",
                    Metadata = new Dictionary<string, object>
                    {
                        { "level", 3 },
                        { "example", "Welding_Station" }
                    }
                },
                new()
                {
                    Id = "workunit",
                    Name = "WorkUnit",
                    IsRequired = false,
                    Order = 4,
                    Description = "Individual piece of equipment, machine, or process unit",
                    AllowedChildNodeIds = new List<string> { "property" },
                    ParentNodeId = "workcenter",
                    Metadata = new Dictionary<string, object>
                    {
                        { "level", 4 },
                        { "example", "Robot_001" }
                    }
                },
                new()
                {
                    Id = "property",
                    Name = "Property",
                    IsRequired = true,
                    Order = 5,
                    Description = "Specific data point, measurement, or property being monitored",
                    AllowedChildNodeIds = new List<string>(),
                    ParentNodeId = "workunit",
                    Metadata = new Dictionary<string, object>
                    {
                        { "level", 5 },
                        { "example", "temperature" }
                    }
                }
            }
        };
    }
}