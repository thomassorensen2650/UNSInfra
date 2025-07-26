namespace UNSInfra.Repositories;

using UNSInfra.Models.Schema;

public class InMemorySchemaRepository : ISchemaRepository
{
    private readonly Dictionary<string, DataSchema> _schemas = new();

    public Task<DataSchema> GetSchemaAsync(string topic)
    {
        _schemas.TryGetValue(topic, out var schema);
        return Task.FromResult(schema);
    }

    public Task SaveSchemaAsync(DataSchema schema)
    {
        _schemas[schema.Topic] = schema;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DataSchema>> GetAllSchemasAsync()
    {
        return Task.FromResult(_schemas.Values.AsEnumerable());
    }
}
