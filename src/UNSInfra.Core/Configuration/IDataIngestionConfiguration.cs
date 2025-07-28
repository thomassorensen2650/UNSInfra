using System.ComponentModel.DataAnnotations;

namespace UNSInfra.Core.Configuration;

/// <summary>
/// Base interface for all data ingestion service configurations.
/// Provides common properties and functionality for dynamic service management.
/// </summary>
public interface IDataIngestionConfiguration
{
    /// <summary>
    /// Unique identifier for the configuration.
    /// </summary>
    string Id { get; set; }

    /// <summary>
    /// Human-readable name for the configuration.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    string Name { get; set; }

    /// <summary>
    /// Description of what this configuration is for.
    /// </summary>
    [StringLength(500)]
    string? Description { get; set; }

    /// <summary>
    /// Whether this configuration is enabled and should be started.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Type identifier for the service (e.g., "MQTT", "SocketIO").
    /// </summary>
    string ServiceType { get; }

    /// <summary>
    /// When this configuration was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this configuration was last modified.
    /// </summary>
    DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Who created this configuration.
    /// </summary>
    string CreatedBy { get; set; }

    /// <summary>
    /// Additional metadata for the configuration.
    /// </summary>
    Dictionary<string, object> Metadata { get; set; }

    /// <summary>
    /// Validates the configuration and returns any validation errors.
    /// </summary>
    /// <returns>List of validation error messages, empty if valid</returns>
    List<string> Validate();

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new instance with the same values</returns>
    IDataIngestionConfiguration Clone();
}