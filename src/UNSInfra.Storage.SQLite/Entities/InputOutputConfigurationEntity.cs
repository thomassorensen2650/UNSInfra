using System.ComponentModel.DataAnnotations;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity for storing input/output configurations in SQLite database.
/// </summary>
public class InputOutputConfigurationEntity
{
    /// <summary>
    /// Unique identifier for this input/output configuration
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this input/output configuration
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of this input/output configuration
    /// </summary>
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this configuration is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Type of input/output (Input/Output)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Service type this configuration belongs to (MQTT, SocketIO)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the specific connection this I/O configuration belongs to
    /// </summary>
    [MaxLength(255)]
    public string? ConnectionId { get; set; }

    /// <summary>
    /// JSON serialized configuration data
    /// </summary>
    [Required]
    public string ConfigurationJson { get; set; } = string.Empty;

    /// <summary>
    /// When this configuration was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this configuration was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}