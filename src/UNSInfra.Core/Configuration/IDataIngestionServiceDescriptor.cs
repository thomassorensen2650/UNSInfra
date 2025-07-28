using UNSInfra.Services.DataIngestion.Mock;

namespace UNSInfra.Core.Configuration;

/// <summary>
/// Describes a type of data ingestion service and how to configure it.
/// Used for dynamic service registration and UI generation.
/// </summary>
public interface IDataIngestionServiceDescriptor
{
    /// <summary>
    /// Unique identifier for this service type (e.g., "MQTT", "SocketIO").
    /// </summary>
    string ServiceType { get; }

    /// <summary>
    /// Human-readable display name for this service type.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Description of what this service does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Icon or image to display for this service type.
    /// </summary>
    string? IconClass { get; }

    /// <summary>
    /// Type of the configuration class for this service.
    /// </summary>
    Type ConfigurationType { get; }

    /// <summary>
    /// Type of the service implementation.
    /// </summary>
    Type ServiceImplementationType { get; }

    /// <summary>
    /// Creates a new default configuration for this service type.
    /// </summary>
    /// <returns>A new configuration instance with default values</returns>
    IDataIngestionConfiguration CreateDefaultConfiguration();

    /// <summary>
    /// Creates a service instance from the given configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use</param>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <returns>A configured service instance</returns>
    IDataIngestionService CreateService(IDataIngestionConfiguration configuration, IServiceProvider serviceProvider);

    /// <summary>
    /// Gets configuration form fields for the UI.
    /// </summary>
    /// <returns>List of form field definitions</returns>
    List<ConfigurationField> GetConfigurationFields();

    /// <summary>
    /// Validates a configuration specific to this service type.
    /// </summary>
    /// <param name="configuration">The configuration to validate</param>
    /// <returns>List of validation error messages</returns>
    List<string> ValidateConfiguration(IDataIngestionConfiguration configuration);
}

/// <summary>
/// Defines a configuration field for dynamic UI generation.
/// </summary>
public class ConfigurationField
{
    /// <summary>
    /// Property name in the configuration object.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the field.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Help text or description for the field.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of input control to render.
    /// </summary>
    public FieldType FieldType { get; set; } = FieldType.Text;

    /// <summary>
    /// Whether this field is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Default value for the field.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Validation attributes for the field.
    /// </summary>
    public Dictionary<string, object> ValidationAttributes { get; set; } = new();

    /// <summary>
    /// Options for select/dropdown fields.
    /// </summary>
    public List<SelectOption>? Options { get; set; }

    /// <summary>
    /// Group this field belongs to for organization.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Display order within the group.
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Types of form fields that can be rendered.
/// </summary>
public enum FieldType
{
    Text,
    Number,
    Password,
    Email,
    Url,
    Boolean,
    Select,
    MultiSelect,
    TextArea,
    Range
}

/// <summary>
/// Option for select/dropdown fields.
/// </summary>
public class SelectOption
{
    public string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool Disabled { get; set; }
}