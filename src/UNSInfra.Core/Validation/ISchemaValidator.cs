namespace UNSInfra.Validation;

using UNSInfra.Models.Data;
using UNSInfra.Models.Schema;
    
public interface ISchemaValidator
{
    Task<bool> ValidateAsync(DataPoint dataPoint, DataSchema schema);
    Task<ValidationResult> ValidateWithDetailsAsync(DataPoint dataPoint, DataSchema schema);
}