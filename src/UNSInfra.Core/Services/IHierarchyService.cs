using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Services;

/// <summary>
/// Service interface for managing hierarchical structures and path operations.
/// </summary>
public interface IHierarchyService
{
    /// <summary>
    /// Gets the currently active hierarchy configuration.
    /// </summary>
    /// <returns>The active hierarchy configuration</returns>
    Task<HierarchyConfiguration> GetActiveConfigurationAsync();

    /// <summary>
    /// Creates a dynamic hierarchical path from a path string using the active configuration.
    /// </summary>
    /// <param name="pathString">The path string to parse (e.g., "enterprise/site/area/property")</param>
    /// <returns>A dynamic hierarchical path instance</returns>
    Task<DynamicHierarchicalPath> CreateDynamicPathFromStringAsync(string pathString);

    /// <summary>
    /// Creates a hierarchical path from a path string using the active configuration.
    /// </summary>
    /// <param name="pathString">The path string to parse (e.g., "enterprise/site/area/property")</param>
    /// <returns>A hierarchical path instance</returns>
    Task<HierarchicalPath> CreatePathFromStringAsync(string pathString);

    /// <summary>
    /// Validates a dynamic hierarchical path against the active configuration.
    /// </summary>
    /// <param name="path">The dynamic hierarchical path to validate</param>
    /// <returns>Validation result with any errors</returns>
    Task<ValidationResult> ValidatePathAsync(DynamicHierarchicalPath path);

    /// <summary>
    /// Validates a hierarchical path against the active configuration.
    /// </summary>
    /// <param name="path">The hierarchical path to validate</param>
    /// <returns>Validation result with any errors</returns>
    Task<ValidationResult> ValidatePathAsync(HierarchicalPath path);

    /// <summary>
    /// Gets the expected hierarchy levels for path construction.
    /// </summary>
    /// <returns>Ordered list of hierarchy node names</returns>
    Task<List<string>> GetHierarchyLevelsAsync();

    /// <summary>
    /// Gets hierarchy node information by level name.
    /// </summary>
    /// <param name="levelName">The level name (e.g., "Enterprise", "Site")</param>
    /// <returns>The hierarchy node if found, null otherwise</returns>
    Task<HierarchyNode?> GetNodeByNameAsync(string levelName);

    /// <summary>
    /// Validates whether topics can be mapped to a specific hierarchical path.
    /// Checks the AllowTopics setting of the deepest (rightmost) hierarchy node in the path.
    /// </summary>
    /// <param name="path">The hierarchical path to validate for topic mapping</param>
    /// <returns>Validation result indicating whether topics can be mapped to this path</returns>
    Task<ValidationResult> ValidateTopicMappingAsync(HierarchicalPath path);
}

/// <summary>
/// Represents the result of a path validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}