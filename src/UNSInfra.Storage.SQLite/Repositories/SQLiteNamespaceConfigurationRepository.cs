using Microsoft.EntityFrameworkCore;
using UNSInfra.Models.Namespace;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Storage.SQLite.Mappers;

namespace UNSInfra.Storage.SQLite.Repositories;

/// <summary>
/// SQLite implementation of the namespace configuration repository.
/// </summary>
public class SQLiteNamespaceConfigurationRepository : INamespaceConfigurationRepository
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;

    /// <summary>
    /// Initializes a new instance of the SQLiteNamespaceConfigurationRepository class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    public SQLiteNamespaceConfigurationRepository(IDbContextFactory<UNSInfraDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<NamespaceConfiguration?> GetNamespaceConfigurationAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.NamespaceConfigurations
            .FirstOrDefaultAsync(nc => nc.Id == id);

        return entity?.ToModel();
    }

    /// <inheritdoc />
    public async Task<NamespaceConfiguration?> GetNamespaceConfigurationByNameAndPathAsync(string name, string hierarchicalPath)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.NamespaceConfigurations
            .FirstOrDefaultAsync(nc => nc.Name == name && nc.HierarchicalPathJson.Contains(hierarchicalPath));

        return entity?.ToModel();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NamespaceConfiguration>> GetAllNamespaceConfigurationsAsync(bool activeOnly = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.NamespaceConfigurations.AsQueryable();
        
        if (activeOnly)
        {
            query = query.Where(nc => nc.IsActive);
        }
        
        var entities = await query
            .OrderBy(nc => nc.Name)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NamespaceConfiguration>> GetNamespaceConfigurationsByTypeAsync(NamespaceType type, bool activeOnly = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.NamespaceConfigurations
            .Where(nc => nc.Type == (int)type);
        
        if (activeOnly)
        {
            query = query.Where(nc => nc.IsActive);
        }
        
        var entities = await query
            .OrderBy(nc => nc.Name)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    /// <inheritdoc />
    public async Task<NamespaceConfiguration?> FindMatchingNamespaceAsync(string topicPath)
    {
        // Auto pattern matching has been removed - topics must be manually assigned to namespaces
        return null;
    }

    /// <inheritdoc />
    public async Task SaveNamespaceConfigurationAsync(NamespaceConfiguration configuration)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        configuration.ModifiedAt = DateTime.UtcNow;

        try
        {
            var existingEntity = await context.NamespaceConfigurations
                .FirstOrDefaultAsync(nc => nc.Id == configuration.Id);

            if (existingEntity != null)
            {
                // Update existing configuration
                existingEntity.Name = configuration.Name;
                existingEntity.Type = (int)configuration.Type;
                existingEntity.Description = configuration.Description;
                existingEntity.HierarchicalPathJson = System.Text.Json.JsonSerializer.Serialize(configuration.HierarchicalPath.Values);
                existingEntity.ParentNamespaceId = configuration.ParentNamespaceId;
                existingEntity.AllowedParentHierarchyNodeId = configuration.AllowedParentHierarchyNodeId;
                existingEntity.IsActive = configuration.IsActive;
                existingEntity.ModifiedAt = configuration.ModifiedAt;
                existingEntity.CreatedBy = configuration.CreatedBy;
                existingEntity.MetadataJson = System.Text.Json.JsonSerializer.Serialize(configuration.Metadata);
            }
            else
            {
                // Add new configuration
                if (string.IsNullOrEmpty(configuration.Id))
                {
                    configuration.Id = Guid.NewGuid().ToString();
                }

                var entity = configuration.ToEntity();
                context.NamespaceConfigurations.Add(entity);
            }

            await context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
        {
            // UNIQUE constraint violation - handle race condition for ID collision
            var existingEntity = await context.NamespaceConfigurations
                .FirstOrDefaultAsync(nc => nc.Id == configuration.Id);
                
            if (existingEntity != null)
            {
                // Clear any pending changes and update the existing entity
                context.ChangeTracker.Clear();
                
                using var updateContext = await _contextFactory.CreateDbContextAsync();
                var entityToUpdate = await updateContext.NamespaceConfigurations
                    .FirstOrDefaultAsync(nc => nc.Id == configuration.Id);
                    
                if (entityToUpdate != null)
                {
                    entityToUpdate.Name = configuration.Name;
                    entityToUpdate.Type = (int)configuration.Type;
                    entityToUpdate.Description = configuration.Description;
                    entityToUpdate.HierarchicalPathJson = System.Text.Json.JsonSerializer.Serialize(configuration.HierarchicalPath.Values);
                    entityToUpdate.ParentNamespaceId = configuration.ParentNamespaceId;
                    entityToUpdate.AllowedParentHierarchyNodeId = configuration.AllowedParentHierarchyNodeId;
                    entityToUpdate.IsActive = configuration.IsActive;
                    entityToUpdate.ModifiedAt = configuration.ModifiedAt;
                    entityToUpdate.CreatedBy = configuration.CreatedBy;
                    entityToUpdate.MetadataJson = System.Text.Json.JsonSerializer.Serialize(configuration.Metadata);
                    
                    await updateContext.SaveChangesAsync();
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteNamespaceConfigurationAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.NamespaceConfigurations
            .FirstOrDefaultAsync(nc => nc.Id == id);

        if (entity != null)
        {
            context.NamespaceConfigurations.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasConflictingPatternAsync(string topicPathPattern, string? excludeId = null)
    {
        // Pattern matching has been removed, so no conflicts exist
        return false;
    }

    /// <inheritdoc />
    public async Task EnsureDefaultConfigurationAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Check if any namespaces already exist
        var existingCount = await context.NamespaceConfigurations.CountAsync();
        if (existingCount > 0)
        {
            return; // Default namespaces already exist
        }

        // Create default namespaces for each type
        var defaultNamespaces = new[]
        {
            // Functional namespaces
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "KPIs",
                Description = "Key Performance Indicators and operational metrics",
                Type = NamespaceType.Functional,
                HierarchicalPath = new HierarchicalPath(), // Root level
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "performance" },
                    { "auto_created", true }
                }
            },
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Production",
                Description = "Production data and manufacturing metrics",
                Type = NamespaceType.Functional,
                HierarchicalPath = new HierarchicalPath(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "manufacturing" },
                    { "auto_created", true }
                }
            },
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Quality",
                Description = "Quality control and assurance data",
                Type = NamespaceType.Functional,
                HierarchicalPath = new HierarchicalPath(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "quality" },
                    { "auto_created", true }
                }
            },

            // Informative namespaces
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Documentation",
                Description = "Documentation, manuals, and reference materials",
                Type = NamespaceType.Informative,
                HierarchicalPath = new HierarchicalPath(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "reference" },
                    { "auto_created", true }
                }
            },
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Reports",
                Description = "Generated reports and analytics",
                Type = NamespaceType.Informative,
                HierarchicalPath = new HierarchicalPath(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "analytics" },
                    { "auto_created", true }
                }
            },

            // Definitional namespaces
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Configuration",
                Description = "System and equipment configurations",
                Type = NamespaceType.Definitional,
                HierarchicalPath = new HierarchicalPath(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "system" },
                    { "auto_created", true }
                }
            },
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Standards",
                Description = "Industry standards and compliance definitions",
                Type = NamespaceType.Definitional,
                HierarchicalPath = new HierarchicalPath(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "compliance" },
                    { "auto_created", true }
                }
            },

            // Ad-Hoc namespaces
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Testing",
                Description = "Temporary testing and experimental data",
                Type = NamespaceType.AdHoc,
                HierarchicalPath = new HierarchicalPath(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "experimental" },
                    { "auto_created", true }
                }
            },
            new NamespaceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Sandbox",
                Description = "Sandbox environment for development and prototyping",
                Type = NamespaceType.AdHoc,
                HierarchicalPath = new HierarchicalPath(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "development" },
                    { "auto_created", true }
                }
            }
        };

        // Save all default namespaces
        foreach (var namespaceConfig in defaultNamespaces)
        {
            await SaveNamespaceConfigurationAsync(namespaceConfig);
        }
    }
}