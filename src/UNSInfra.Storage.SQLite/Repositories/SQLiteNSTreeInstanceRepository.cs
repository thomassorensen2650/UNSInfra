using Microsoft.EntityFrameworkCore;
using UNSInfra.Models.Namespace;
using UNSInfra.Repositories;
using UNSInfra.Storage.SQLite.Entities;
using UNSInfra.Storage.SQLite.Mappers;

namespace UNSInfra.Storage.SQLite.Repositories;

/// <summary>
/// SQLite implementation of NS tree instance repository.
/// </summary>
public class SQLiteNSTreeInstanceRepository : INSTreeInstanceRepository
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;

    public SQLiteNSTreeInstanceRepository(IDbContextFactory<UNSInfraDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<NSTreeInstance>> GetAllInstancesAsync(bool activeOnly = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.NSTreeInstances.AsQueryable();
        if (activeOnly)
        {
            query = query.Where(i => i.IsActive);
        }
        
        var entities = await query.OrderBy(i => i.Name).ToListAsync();
        return entities.Select(e => e.ToModel());
    }

    public async Task<NSTreeInstance?> GetInstanceByIdAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.NSTreeInstances
            .FirstOrDefaultAsync(i => i.Id == id);
            
        return entity?.ToModel();
    }

    public async Task<IEnumerable<NSTreeInstance>> GetChildInstancesAsync(string? parentInstanceId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entities = await context.NSTreeInstances
            .Where(i => i.ParentInstanceId == parentInstanceId && i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync();
            
        return entities.Select(e => e.ToModel());
    }

    public async Task SaveInstanceAsync(NSTreeInstance instance)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        instance.ModifiedAt = DateTime.UtcNow;

        var existingEntity = await context.NSTreeInstances
            .FirstOrDefaultAsync(i => i.Id == instance.Id);

        if (existingEntity != null)
        {
            // Update existing
            var updatedEntity = instance.ToEntity();
            updatedEntity.CreatedAt = existingEntity.CreatedAt; // Preserve creation time
            context.Entry(existingEntity).CurrentValues.SetValues(updatedEntity);
        }
        else
        {
            // Add new
            context.NSTreeInstances.Add(instance.ToEntity());
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteInstanceAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Get all descendant instances recursively
        var allInstances = await context.NSTreeInstances.ToListAsync();
        var toDelete = GetAllDescendants(allInstances, id).ToList();
        toDelete.Add(allInstances.First(i => i.Id == id)); // Add the instance itself
        
        context.NSTreeInstances.RemoveRange(toDelete);
        await context.SaveChangesAsync();
    }

    public async Task<bool> CanDeleteInstanceAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Check if instance has children
        var hasChildren = await context.NSTreeInstances
            .AnyAsync(i => i.ParentInstanceId == id);
            
        if (hasChildren) return false;
        
        // Check if any namespaces reference this instance's hierarchical path
        // For now, we'll allow deletion but this could be enhanced with more checks
        return true;
    }

    private IEnumerable<NSTreeInstanceEntity> GetAllDescendants(List<NSTreeInstanceEntity> allInstances, string parentId)
    {
        var children = allInstances.Where(i => i.ParentInstanceId == parentId).ToList();
        var result = new List<NSTreeInstanceEntity>(children);
        
        foreach (var child in children)
        {
            result.AddRange(GetAllDescendants(allInstances, child.Id));
        }
        
        return result;
    }
}