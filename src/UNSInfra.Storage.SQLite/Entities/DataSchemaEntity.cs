using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UNSInfra.Storage.SQLite.Entities;

/// <summary>
/// Entity model for data schemas in SQLite database.
/// </summary>
[Table("DataSchemas")]
public class DataSchemaEntity
{
    /// <summary>
    /// Gets or sets the unique schema identifier.
    /// </summary>
    [Key]
    [MaxLength(100)]
    public string SchemaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the topic this schema applies to.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON schema definition.
    /// </summary>
    [Required]
    public string JsonSchema { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the property types as JSON.
    /// </summary>
    public string PropertyTypesJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the validation rules as JSON.
    /// </summary>
    public string ValidationRulesJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets when this schema was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this schema was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}