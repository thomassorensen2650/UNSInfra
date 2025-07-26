using System.Text.Json;
using UNSInfra.Models.Data;
using UNSInfra.Models.Schema;

namespace UNSInfra.Validation;

public class JsonSchemaValidator : ISchemaValidator
{
    public async Task<bool> ValidateAsync(DataPoint dataPoint, DataSchema schema)
    {
        var result = await ValidateWithDetailsAsync(dataPoint, schema);
        return result.IsValid;
    }

    public async Task<ValidationResult> ValidateWithDetailsAsync(DataPoint dataPoint, DataSchema schema)
    {
        var result = new ValidationResult { IsValid = true };
        
        try
        {
            // Basic type validation
            if (schema.PropertyTypes.Any())
            {
                var jsonElement = (JsonElement)dataPoint.Value;
                foreach (var propType in schema.PropertyTypes)
                {
                    if (jsonElement.TryGetProperty(propType.Key, out var prop))
                    {
                        if (!IsValidType(prop, propType.Value))
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Property {propType.Key} is not of expected type {propType.Value.Name}");
                        }
                    }
                }
            }

            // Custom validation rules
            foreach (var rule in schema.ValidationRules)
            {
                if (!ValidateRule(dataPoint, rule))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Validation rule {rule.RuleType} failed for property {rule.PropertyName}");
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    private bool IsValidType(JsonElement element, Type expectedType)
    {
        return expectedType.Name switch
        {
            "String" => element.ValueKind == JsonValueKind.String,
            "Int32" => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _),
            "Double" => element.ValueKind == JsonValueKind.Number,
            "Boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            _ => true
        };
    }

    private bool ValidateRule(DataPoint dataPoint, ValidationRule rule)
    {
        // Implement specific validation logic based on rule type
        return rule.RuleType switch
        {
            "Required" => dataPoint.Value != null,
            "Range" => ValidateRange(dataPoint, rule),
            _ => true
        };
    }

    private bool ValidateRange(DataPoint dataPoint, ValidationRule rule)
    {
        // Simplified range validation
        if (dataPoint.Value is JsonElement element && element.ValueKind == JsonValueKind.Number)
        {
            var value = element.GetDouble();
            var range = (double[])rule.RuleValue;
            return value >= range[0] && value <= range[1];
        }
        return true;
    }
}
