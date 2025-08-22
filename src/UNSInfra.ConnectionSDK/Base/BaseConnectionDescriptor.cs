using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Models;

namespace UNSInfra.ConnectionSDK.Base;

/// <summary>
/// Base implementation of IConnectionDescriptor that provides common functionality
/// </summary>
public abstract class BaseConnectionDescriptor : IConnectionDescriptor
{
    /// <inheritdoc />
    public abstract string ConnectionType { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public virtual string? IconClass => null;

    /// <inheritdoc />
    public virtual string Category => "General";

    /// <inheritdoc />
    public virtual string Version => "1.0.0";

    /// <inheritdoc />
    public virtual bool SupportsInputs => true;

    /// <inheritdoc />
    public virtual bool SupportsOutputs => true;

    /// <inheritdoc />
    public abstract ConfigurationSchema GetConnectionConfigurationSchema();

    /// <inheritdoc />
    public abstract ConfigurationSchema GetInputConfigurationSchema();

    /// <inheritdoc />
    public abstract ConfigurationSchema GetOutputConfigurationSchema();

    /// <inheritdoc />
    public abstract object CreateDefaultConnectionConfiguration();

    /// <inheritdoc />
    public abstract object CreateDefaultInputConfiguration();

    /// <inheritdoc />
    public abstract object CreateDefaultOutputConfiguration();

    /// <inheritdoc />
    public abstract IDataConnection CreateConnection(string connectionId, string name, IServiceProvider serviceProvider);

    /// <summary>
    /// Helper method to create a configuration field
    /// </summary>
    protected static ConfigurationField CreateField(
        string name,
        string displayName,
        ConfigurationFieldType type,
        bool isRequired = false,
        object? defaultValue = null,
        string? description = null,
        string? group = null,
        int order = 0)
    {
        return new ConfigurationField
        {
            Name = name,
            DisplayName = displayName,
            Type = type,
            IsRequired = isRequired,
            DefaultValue = defaultValue,
            Description = description,
            Group = group,
            Order = order
        };
    }

    /// <summary>
    /// Helper method to create a configuration group
    /// </summary>
    protected static ConfigurationGroup CreateGroup(
        string name,
        string displayName,
        string? description = null,
        int order = 0,
        bool isCollapsible = false,
        bool isCollapsed = false)
    {
        return new ConfigurationGroup
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            Order = order,
            IsCollapsible = isCollapsible,
            IsCollapsed = isCollapsed
        };
    }

    /// <summary>
    /// Helper method to create password configuration fields
    /// </summary>
    protected static ConfigurationField CreatePasswordField(string name, string displayName, bool isRequired, 
        object? defaultValue, string? description, string? groupName = null, int order = 0)
    {
        var field = CreateField(name, displayName, ConfigurationFieldType.Password, isRequired, 
            defaultValue, description, groupName, order);
        field.IsSecret = true;
        return field;
    }

    /// <summary>
    /// Helper method to create select configuration fields with options
    /// </summary>
    protected static ConfigurationField CreateSelectField(string name, string displayName, bool isRequired, 
        object? defaultValue, string? description, string? groupName, int order,
        params (object value, string text, string? description)[] options)
    {
        var field = CreateField(name, displayName, ConfigurationFieldType.Select, isRequired, 
            defaultValue, description, groupName, order);
        field.Options = CreateOptions(options);
        return field;
    }

    /// <summary>
    /// Helper method to create configuration options for select fields
    /// </summary>
    protected static List<ConfigurationOption> CreateOptions(params (object value, string text, string? description)[] options)
    {
        return options.Select(o => new ConfigurationOption
        {
            Value = o.value,
            Text = o.text,
            Description = o.description
        }).ToList();
    }
}