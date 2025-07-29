using Microsoft.EntityFrameworkCore;
using UNSInfra.Models.Namespace;
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
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Get all active namespace configurations and find the best match
        var entities = await context.NamespaceConfigurations
            .Where(nc => nc.IsActive)
            .OrderByDescending(nc => nc.TopicPathPattern.Length) // Prioritize more specific patterns
            .ToListAsync();

        // Find the first namespace that matches the topic path
        foreach (var entity in entities)
        {
            var namespaceConfig = entity.ToModel();
            if (namespaceConfig.MatchesTopicPath(topicPath))
            {
                return namespaceConfig;
            }
        }

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
                existingEntity.TopicPathPattern = configuration.TopicPathPattern;
                existingEntity.AutoVerifyTopics = configuration.AutoVerifyTopics;
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
            // UNIQUE constraint violation - handle race condition for topic path pattern
            var existingEntity = await context.NamespaceConfigurations
                .FirstOrDefaultAsync(nc => nc.TopicPathPattern == configuration.TopicPathPattern);
                
            if (existingEntity != null)
            {
                // Clear any pending changes and update the existing entity
                context.ChangeTracker.Clear();
                
                using var updateContext = await _contextFactory.CreateDbContextAsync();
                var entityToUpdate = await updateContext.NamespaceConfigurations
                    .FirstOrDefaultAsync(nc => nc.TopicPathPattern == configuration.TopicPathPattern);
                    
                if (entityToUpdate != null)
                {
                    entityToUpdate.Name = configuration.Name;
                    entityToUpdate.Type = (int)configuration.Type;
                    entityToUpdate.Description = configuration.Description;
                    entityToUpdate.HierarchicalPathJson = System.Text.Json.JsonSerializer.Serialize(configuration.HierarchicalPath.Values);
                    entityToUpdate.AutoVerifyTopics = configuration.AutoVerifyTopics;
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
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.NamespaceConfigurations
            .Where(nc => nc.TopicPathPattern == topicPathPattern && nc.IsActive);

        if (!string.IsNullOrEmpty(excludeId))
        {
            query = query.Where(nc => nc.Id != excludeId);
        }

        return await query.AnyAsync();
    }
}