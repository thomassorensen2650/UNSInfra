using System.ComponentModel.DataAnnotations;

namespace UNSInfra.ConnectionSDK.Models;

/// <summary>
/// Describes the configuration schema for a connection, input, or output
/// </summary>
public class ConfigurationSchema
{
    /// <summary>
    /// List of configuration fields
    /// </summary>
    public List<ConfigurationField> Fields { get; set; } = new();

    /// <summary>
    /// Groups of related fields for UI organization
    /// </summary>
    public List<ConfigurationGroup> Groups { get; set; } = new();

    /// <summary>
    /// JSON schema for advanced validation (optional)
    /// </summary>
    public string? JsonSchema { get; set; }
}

/// <summary>
/// Describes a single configuration field
/// </summary>
public class ConfigurationField
{
    /// <summary>
    /// Property name in the configuration object
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Display name for the field
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Description or help text for the field
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Data type of the field
    /// </summary>
    public ConfigurationFieldType Type { get; set; }

    /// <summary>
    /// Whether this field is required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Default value for the field
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Placeholder text for the field
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Group this field belongs to (for UI organization)
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Display order within the group
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether this field should be masked (for passwords)
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// Validation attributes for the field
    /// </summary>
    public List<ValidationAttribute> Validations { get; set; } = new();

    /// <summary>
    /// Options for select/dropdown fields
    /// </summary>
    public List<ConfigurationOption>? Options { get; set; }

    /// <summary>
    /// Additional metadata for the field
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Group of related configuration fields
/// </summary>
public class ConfigurationGroup
{
    /// <summary>
    /// Group identifier
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Display name for the group
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Description of the group
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Display order of the group
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether the group is collapsible in the UI
    /// </summary>
    public bool IsCollapsible { get; set; }

    /// <summary>
    /// Whether the group starts collapsed
    /// </summary>
    public bool IsCollapsed { get; set; }
}

/// <summary>
/// Option for select/dropdown fields
/// </summary>
public class ConfigurationOption
{
    /// <summary>
    /// Option value
    /// </summary>
    public required object Value { get; set; }

    /// <summary>
    /// Display text for the option
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Description or tooltip for the option
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Types of configuration fields
/// </summary>
public enum ConfigurationFieldType
{
    /// <summary>
    /// Single line text input
    /// </summary>
    Text,

    /// <summary>
    /// Multi-line text area
    /// </summary>
    TextArea,

    /// <summary>
    /// Password input (masked)
    /// </summary>
    Password,

    /// <summary>
    /// Number input
    /// </summary>
    Number,

    /// <summary>
    /// Boolean checkbox
    /// </summary>
    Boolean,

    /// <summary>
    /// Dropdown select
    /// </summary>
    Select,

    /// <summary>
    /// Multi-select with checkboxes
    /// </summary>
    MultiSelect,

    /// <summary>
    /// Date/time picker
    /// </summary>
    DateTime,

    /// <summary>
    /// JSON editor
    /// </summary>
    Json,

    /// <summary>
    /// File upload
    /// </summary>
    File,

    /// <summary>
    /// URL input with validation
    /// </summary>
    Url,

    /// <summary>
    /// Email input with validation
    /// </summary>
    Email
}