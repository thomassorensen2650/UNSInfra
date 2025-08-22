using System.ComponentModel.DataAnnotations;

namespace UNSInfra.Models;

/// <summary>
/// Configuration for a data connection
/// </summary>
public class ConnectionConfiguration
{
    /// <summary>
    /// Unique identifier for the connection
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the connection
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of connection (e.g., "mqtt", "socketio", etc.)
    /// </summary>
    [Required]
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
    /// Connection-specific configuration object
    /// </summary>
    public object ConnectionConfig { get; set; } = new();

    /// <summary>
    /// Input configurations for this connection
    /// </summary>
    public List<object> Inputs { get; set; } = new();

    /// <summary>
    /// Output configurations for this connection
    /// </summary>
    public List<object> Outputs { get; set; } = new();

    /// <summary>
    /// Optional tags for organizing connections
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Optional description
    /// </summary>
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
    /// Additional metadata for the connection
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}