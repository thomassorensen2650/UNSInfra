namespace UNSInfra.Models.Configuration;

/// <summary>
/// Base class for input/output configuration
/// </summary>
public abstract class InputOutputConfiguration
{
    /// <summary>
    /// Unique identifier for this input/output configuration
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this input/output configuration
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of this input/output configuration
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this configuration is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Type of input/output (Input/Output)
    /// </summary>
    public InputOutputType Type { get; set; }

    /// <summary>
    /// Service type this configuration belongs to (MQTT, SocketIO)
    /// </summary>
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the specific connection this I/O configuration belongs to
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// When this configuration was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this configuration was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of input/output configuration
/// </summary>
public enum InputOutputType
{
    Input,
    Output
}

/// <summary>
/// Input configuration for receiving data
/// </summary>
public abstract class InputConfiguration : InputOutputConfiguration
{
    protected InputConfiguration()
    {
        Type = InputOutputType.Input;
    }
}

/// <summary>
/// Output configuration for sending data
/// </summary>
public abstract class OutputConfiguration : InputOutputConfiguration
{
    protected OutputConfiguration()
    {
        Type = InputOutputType.Output;
    }
}