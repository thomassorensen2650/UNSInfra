using System.ComponentModel.DataAnnotations;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity for storing connection configurations in SQLite database.
/// </summary>
public class ConnectionConfigurationEntity
{
    /// <summary>
    /// Unique identifier for this connection
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this connection
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of connection (e.g., "mqtt", "socketio", etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ConnectionType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the connection is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether to automatically start the connection on application startup
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// JSON serialized connection-specific configuration object
    /// </summary>
    [Required]
    public string ConnectionConfigJson { get; set; } = string.Empty;

    /// <summary>
    /// JSON serialized input configurations list
    /// </summary>
    public string InputsJson { get; set; } = string.Empty;

    /// <summary>
    /// JSON serialized output configurations list
    /// </summary>
    public string OutputsJson { get; set; } = string.Empty;

    /// <summary>
    /// JSON serialized tags list
    /// </summary>
    public string TagsJson { get; set; } = string.Empty;

    /// <summary>
    /// Optional description
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON serialized additional metadata dictionary
    /// </summary>
    public string MetadataJson { get; set; } = string.Empty;
}