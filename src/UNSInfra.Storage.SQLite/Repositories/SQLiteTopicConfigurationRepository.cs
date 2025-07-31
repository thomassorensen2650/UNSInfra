using Microsoft.EntityFrameworkCore;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Storage.SQLite.Mappers;

namespace UNSInfra.Storage.SQLite.Repositories;

/// <summary>
/// SQLite implementation of the topic configuration repository.
/// </summary>
public class SQLiteTopicConfigurationRepository : ITopicConfigurationRepository
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;

    /// <summary>
    /// Initializes a new instance of the SQLiteTopicConfigurationRepository class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    public SQLiteTopicConfigurationRepository(IDbContextFactory<UNSInfraDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<TopicConfiguration?> GetTopicConfigurationAsync(string topic)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.TopicConfigurations
            .FirstOrDefaultAsync(tc => tc.Topic == topic);

        return entity?.ToModel();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TopicConfiguration>> GetAllTopicConfigurationsAsync(bool verifiedOnly = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.TopicConfigurations.AsQueryable();
        
        if (verifiedOnly)
        {
            query = query.Where(tc => tc.IsVerified);
        }
        
        var entities = await query
            .OrderBy(tc => tc.Topic)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }


    /// <inheritdoc />
    public async Task<IEnumerable<TopicConfiguration>> GetUnverifiedTopicConfigurationsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entities = await context.TopicConfigurations
            .Where(tc => !tc.IsVerified)
            .OrderBy(tc => tc.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    /// <inheritdoc />
    public async Task SaveTopicConfigurationAsync(TopicConfiguration configuration)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        configuration.ModifiedAt = DateTime.UtcNow;

        try
        {
            var existingEntity = await context.TopicConfigurations
                .FirstOrDefaultAsync(tc => tc.Topic == configuration.Topic);

            if (existingEntity != null)
            {
                // Update existing configuration
                existingEntity.PathValuesJson = System.Text.Json.JsonSerializer.Serialize(configuration.Path.Values);
                existingEntity.IsVerified = configuration.IsVerified;
                existingEntity.IsActive = configuration.IsActive;
                existingEntity.SourceType = configuration.SourceType;
                existingEntity.Description = configuration.Description;
                existingEntity.ModifiedAt = configuration.ModifiedAt;
                existingEntity.CreatedBy = configuration.CreatedBy;
                existingEntity.MetadataJson = System.Text.Json.JsonSerializer.Serialize(configuration.Metadata);
                existingEntity.NSPath = configuration.NSPath;
                existingEntity.NamespaceConfigurationId = configuration.NamespaceConfigurationId;
            }
            else
            {
                // Add new configuration
                if (string.IsNullOrEmpty(configuration.Id))
                {
                    configuration.Id = Guid.NewGuid().ToString();
                }

                var entity = configuration.ToEntity();
                context.TopicConfigurations.Add(entity);
            }

            await context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
        {
            // UNIQUE constraint violation (Error 19) - topic already exists, try to update instead
            // This can happen due to race conditions
            var existingEntity = await context.TopicConfigurations
                .FirstOrDefaultAsync(tc => tc.Topic == configuration.Topic);
                
            if (existingEntity != null)
            {
                // Clear any pending changes and update the existing entity
                context.ChangeTracker.Clear();
                
                using var updateContext = await _contextFactory.CreateDbContextAsync();
                var entityToUpdate = await updateContext.TopicConfigurations
                    .FirstOrDefaultAsync(tc => tc.Topic == configuration.Topic);
                    
                if (entityToUpdate != null)
                {
                    entityToUpdate.PathValuesJson = System.Text.Json.JsonSerializer.Serialize(configuration.Path.Values);
                    entityToUpdate.IsVerified = configuration.IsVerified;
                    entityToUpdate.IsActive = configuration.IsActive;
                    entityToUpdate.SourceType = configuration.SourceType;
                    entityToUpdate.Description = configuration.Description;
                    entityToUpdate.ModifiedAt = configuration.ModifiedAt;
                    entityToUpdate.CreatedBy = configuration.CreatedBy;
                    entityToUpdate.MetadataJson = System.Text.Json.JsonSerializer.Serialize(configuration.Metadata);
                    entityToUpdate.NSPath = configuration.NSPath;
                    entityToUpdate.NamespaceConfigurationId = configuration.NamespaceConfigurationId;
                    
                    await updateContext.SaveChangesAsync();
                }
            }
            // If we can't find the existing entity, just ignore this save (another thread completed it)
        }
    }

    /// <inheritdoc />
    public async Task DeleteTopicConfigurationAsync(string configurationId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.TopicConfigurations
            .FirstOrDefaultAsync(tc => tc.Id == configurationId);

        if (entity != null)
        {
            context.TopicConfigurations.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<IOrderedEnumerable<TopicMappingRule>> GetTopicMappingRulesAsync()
    {
        // For now, return empty collection as topic mapping rules are not yet implemented in SQLite
        // This would require a separate TopicMappingRuleEntity and table
        return (await Task.FromResult(Enumerable.Empty<TopicMappingRule>())).OrderByDescending(r => 0);
    }

    /// <inheritdoc />
    public async Task SaveTopicMappingRuleAsync(TopicMappingRule rule)
    {
        // For now, this is a no-op as topic mapping rules are not yet implemented in SQLite
        // This would require a separate TopicMappingRuleEntity and table
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task VerifyTopicConfigurationAsync(string configurationId, string verifiedBy)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.TopicConfigurations
            .FirstOrDefaultAsync(tc => tc.Id == configurationId);
            
        if (entity != null)
        {
            entity.IsVerified = true;
            entity.ModifiedAt = DateTime.UtcNow;
            // Note: CreatedBy field could be repurposed to store verifiedBy, 
            // or we could add a separate VerifiedBy field to the entity
            await context.SaveChangesAsync();
        }
    }
}