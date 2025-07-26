namespace UNSInfra.Models.Schema;

// Schema Definition
public class DataSchema
{
    public string SchemaId { get; set; }
    public string Topic { get; set; }
    public string JsonSchema { get; set; }
    public Dictionary<string, Type> PropertyTypes { get; set; } = new();
    public List<ValidationRule> ValidationRules { get; set; } = new();
}