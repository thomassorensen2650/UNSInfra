namespace UNSInfra.ConnectionSDK.Models;

/// <summary>
/// Result of a configuration validation operation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation error messages
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warning messages
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with error messages
    /// </summary>
    /// <param name="errors">Error messages</param>
    public static ValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };

    /// <summary>
    /// Creates a failed validation result with error and warning messages
    /// </summary>
    /// <param name="errors">Error messages</param>
    /// <param name="warnings">Warning messages</param>
    public static ValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null) => new()
    {
        IsValid = false,
        Errors = errors.ToList(),
        Warnings = warnings?.ToList() ?? new()
    };
}