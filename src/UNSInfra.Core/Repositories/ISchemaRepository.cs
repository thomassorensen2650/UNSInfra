namespace UNSInfra.Repositories;

using UNSInfra.Models.Schema;
    
public interface ISchemaRepository
{
    Task<DataSchema> GetSchemaAsync(string topic);
    Task SaveSchemaAsync(DataSchema schema);
    Task<IEnumerable<DataSchema>> GetAllSchemasAsync();
}