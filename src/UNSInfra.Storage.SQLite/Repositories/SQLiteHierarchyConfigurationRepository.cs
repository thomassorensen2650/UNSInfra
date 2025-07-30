using Microsoft.EntityFrameworkCore;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Storage.SQLite.Mappers;

namespace UNSInfra.Storage.SQLite.Repositories;

/// <summary>
/// SQLite implementation of the hierarchy configuration repository.
/// </summary>
public class SQLiteHierarchyConfigurationRepository : IHierarchyConfigurationRepository
{
    private readonly UNSInfraDbContext _context;

    /// <summary>
    /// Initializes a new instance of the SQLiteHierarchyConfigurationRepository class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public SQLiteHierarchyConfigurationRepository(UNSInfraDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<HierarchyConfiguration?> GetActiveConfigurationAsync()
    {
        var entity = await _context.HierarchyConfigurations
            .Include(c => c.Nodes)
            .FirstOrDefaultAsync(c => c.IsActive);

        return entity?.ToModel();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<HierarchyConfiguration>> GetAllConfigurationsAsync()
    {
        var entities = await _context.HierarchyConfigurations
            .Include(c => c.Nodes)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    /// <inheritdoc />
    public async Task<HierarchyConfiguration?> GetConfigurationByIdAsync(string id)
    {
        var entity = await _context.HierarchyConfigurations
            .Include(c => c.Nodes)
            .FirstOrDefaultAsync(c => c.Id == id);

        return entity?.ToModel();
    }

    /// <inheritdoc />
    public async Task SaveConfigurationAsync(HierarchyConfiguration configuration)
    {
        configuration.ModifiedAt = DateTime.UtcNow;

        var existingEntity = await _context.HierarchyConfigurations
            .Include(c => c.Nodes)
            .FirstOrDefaultAsync(c => c.Id == configuration.Id);

        if (existingEntity != null)
        {
            // Update existing configuration
            existingEntity.Name = configuration.Name;
            existingEntity.Description = configuration.Description;
            existingEntity.IsActive = configuration.IsActive;
            existingEntity.IsSystemDefined = configuration.IsSystemDefined;
            existingEntity.ModifiedAt = configuration.ModifiedAt;

            // Remove existing nodes
            _context.HierarchyNodes.RemoveRange(existingEntity.Nodes);

            // Add updated nodes
            existingEntity.Nodes = configuration.Nodes.Select(n => n.ToEntity(configuration.Id)).ToList();
        }
        else
        {
            // Add new configuration
            if (string.IsNullOrEmpty(configuration.Id))
            {
                configuration.Id = Guid.NewGuid().ToString();
            }
            
            var entity = configuration.ToEntity();
            _context.HierarchyConfigurations.Add(entity);
        }

        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConfigurationAsync(string id)
    {
        var entity = await _context.HierarchyConfigurations
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null || entity.IsSystemDefined)
        {
            return false;
        }

        _context.HierarchyConfigurations.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetActiveConfigurationAsync(string id)
    {
        var targetEntity = await _context.HierarchyConfigurations
            .FirstOrDefaultAsync(c => c.Id == id);

        if (targetEntity == null)
        {
            return false;
        }

        // Set all configurations to inactive
        await _context.HierarchyConfigurations
            .ExecuteUpdateAsync(c => c.SetProperty(e => e.IsActive, false));

        // Set target configuration to active
        targetEntity.IsActive = true;
        await _context.SaveChangesAsync();

        return true;
    }

    /// <inheritdoc />
    public async Task EnsureDefaultConfigurationAsync()
    {
        const string defaultConfigId = "isa-s95-standard";

        var existingConfig = await _context.HierarchyConfigurations
            .FirstOrDefaultAsync(c => c.Id == defaultConfigId);

        if (existingConfig == null)
        {
            var defaultConfig = CreateISAS95Configuration();
            await SaveConfigurationAsync(defaultConfig);
            await SetActiveConfigurationAsync(defaultConfigId);
        }
        else
        {
            var hasActiveConfig = await _context.HierarchyConfigurations
                .AnyAsync(c => c.IsActive);

            if (!hasActiveConfig)
            {
                await SetActiveConfigurationAsync(defaultConfigId);
            }
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