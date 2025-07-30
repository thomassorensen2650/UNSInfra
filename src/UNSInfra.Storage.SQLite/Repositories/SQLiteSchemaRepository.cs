using Microsoft.EntityFrameworkCore;
using UNSInfra.Models.Schema;
using UNSInfra.Repositories;
using UNSInfra.Storage.SQLite.Mappers;

namespace UNSInfra.Storage.SQLite.Repositories;

/// <summary>
/// SQLite implementation of the schema repository.
/// </summary>
public class SQLiteSchemaRepository : ISchemaRepository
{
    private readonly UNSInfraDbContext _context;

    /// <summary>
    /// Initializes a new instance of the SQLiteSchemaRepository class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public SQLiteSchemaRepository(UNSInfraDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<DataSchema> GetSchemaAsync(string topic)
    {
        var entity = await _context.DataSchemas
            .FirstOrDefaultAsync(ds => ds.Topic == topic);

        if (entity != null)
        {
            return entity.ToModel();
        }

        // Return a default schema if none exists
        return new DataSchema
        {
            SchemaId = Guid.NewGuid().ToString(),
            Topic = topic,
            JsonSchema = "{}",
            PropertyTypes = new Dictionary<string, Type>(),
            ValidationRules = new List<ValidationRule>()
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DataSchema>> GetAllSchemasAsync()
    {
        var entities = await _context.DataSchemas
            .OrderBy(ds => ds.Topic)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    /// <inheritdoc />
    public async Task SaveSchemaAsync(DataSchema schema)
    {
        var existingEntity = await _context.DataSchemas
            .FirstOrDefaultAsync(ds => ds.SchemaId == schema.SchemaId);

        if (existingEntity != null)
        {
            // Update existing schema
            existingEntity.Topic = schema.Topic;
            existingEntity.JsonSchema = schema.JsonSchema;
            existingEntity.PropertyTypesJson = System.Text.Json.JsonSerializer.Serialize(
                schema.PropertyTypes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.FullName));
            existingEntity.ValidationRulesJson = System.Text.Json.JsonSerializer.Serialize(schema.ValidationRules);
            existingEntity.ModifiedAt = DateTime.UtcNow;
        }
        else
        {
            // Add new schema
            var entity = schema.ToEntity();
            _context.DataSchemas.Add(entity);
        }

        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSchemaAsync(string schemaId)
    {
        var entity = await _context.DataSchemas
            .FirstOrDefaultAsync(ds => ds.SchemaId == schemaId);

        if (entity == null)
        {
            return false;
        }

        _context.DataSchemas.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}