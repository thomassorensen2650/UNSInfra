namespace UNSInfra.Models.Schema;
public class ValidationRule
{
    public string PropertyName { get; set; }
    public string RuleType { get; set; } // Required, Range, Pattern, etc.
    public object RuleValue { get; set; }
}