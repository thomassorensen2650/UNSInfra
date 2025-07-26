namespace UNSInfra.Validation;

using UNSInfra.Models.Data;
using UNSInfra.Models.Schema;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}