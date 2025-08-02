using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity for storing data ingestion configurations in SQLite database.
/// </summary>
[Table("DataIngestionConfigurations")]
public class DataIngestionConfigurationEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this configuration.
    /// </summary>
    [Key]
    [Column("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of this configuration.
    /// </summary>
    [Required]
    [Column("Name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this configuration.
    /// </summary>
    [Column("Description")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the service type (MQTT, SocketIO, etc.).
    /// </summary>
    [Required]
    [Column("ServiceType")]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this configuration is enabled.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the JSON-serialized configuration data.
    /// </summary>
    [Required]
    [Column("ConfigurationJson")]
    public string ConfigurationJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this configuration was created.
    /// </summary>
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this configuration was last modified.
    /// </summary>
    [Column("ModifiedAt")]
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets who created this configuration.
    /// </summary>
    [Column("CreatedBy")]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = "System";

    /// <summary>
    /// Gets or sets the JSON-serialized metadata.
    /// </summary>
    [Column("Metadata")]
    public string? Metadata { get; set; }
}