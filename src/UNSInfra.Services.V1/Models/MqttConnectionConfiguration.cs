using System.ComponentModel.DataAnnotations;

namespace UNSInfra.Services.V1.Models;

/// <summary>
/// Configuration for MQTT connection
/// </summary>
public class MqttConnectionConfiguration
{
    /// <summary>
    /// MQTT broker hostname or IP address
    /// </summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// MQTT broker port
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 1883;

    /// <summary>
    /// Client ID for MQTT connection
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Username for authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Use TLS/SSL for connection
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Keep alive interval in seconds
    /// </summary>
    [Range(10, 3600)]
    public int KeepAliveSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to use clean session
    /// </summary>
    public bool CleanSession { get; set; } = true;
}

/// <summary>
/// Configuration for MQTT input (subscription)
/// </summary>
public class MqttInputConfiguration
{
    /// <summary>
    /// Unique identifier for this input
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this input
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// MQTT topic pattern to subscribe to
    /// </summary>
    [Required]
    public string TopicPattern { get; set; } = string.Empty;

    /// <summary>
    /// Quality of Service level (0, 1, or 2)
    /// </summary>
    [Range(0, 2)]
    public int QualityOfService { get; set; } = 1;

    /// <summary>
    /// Whether this input is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// How to parse the incoming payload
    /// </summary>
    public PayloadFormat PayloadFormat { get; set; } = PayloadFormat.Auto;

    /// <summary>
    /// Topic mapping configuration for UNS path generation
    /// </summary>
    public string? TopicMapping { get; set; }
}

/// <summary>
/// Configuration for MQTT output (publishing)
/// </summary>
public class MqttOutputConfiguration
{
    /// <summary>
    /// Unique identifier for this output
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this output
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// MQTT topic pattern to publish to
    /// </summary>
    [Required]
    public string TopicPattern { get; set; } = string.Empty;

    /// <summary>
    /// Quality of Service level (0, 1, or 2)
    /// </summary>
    [Range(0, 2)]
    public int QualityOfService { get; set; } = 1;

    /// <summary>
    /// Whether to retain published messages
    /// </summary>
    public bool Retain { get; set; } = false;

    /// <summary>
    /// Whether this output is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// How to format the outgoing payload
    /// </summary>
    public PayloadFormat PayloadFormat { get; set; } = PayloadFormat.Json;

    /// <summary>
    /// Whether to include timestamp in the payload
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Whether to include quality information in the payload
    /// </summary>
    public bool IncludeQuality { get; set; } = false;

    /// <summary>
    /// Filters for which data to publish
    /// </summary>
    public List<string> TopicFilters { get; set; } = new();

    /// <summary>
    /// Whether to publish only on data changes
    /// </summary>
    public bool PublishOnChange { get; set; } = true;

    /// <summary>
    /// Minimum interval between publishes in milliseconds
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MinPublishIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Whether to publish model information for UNS tree nodes
    /// </summary>
    public bool PublishModels { get; set; } = false;

    /// <summary>
    /// Interval for model publishing in milliseconds
    /// </summary>
    [Range(5000, int.MaxValue)]
    public int ModelPublishIntervalMs { get; set; } = 30000; // 30 seconds default

    /// <summary>
    /// Topic suffix for model publishing (e.g., "-model" will create topics like "Enterprise-model")
    /// </summary>
    public string ModelTopicSuffix { get; set; } = "-model";
}

/// <summary>
/// Supported payload formats
/// </summary>
public enum PayloadFormat
{
    /// <summary>
    /// Auto-detect format based on content
    /// </summary>
    Auto,

    /// <summary>
    /// Raw string/binary
    /// </summary>
    Raw,

    /// <summary>
    /// JSON format
    /// </summary>
    Json,

    /// <summary>
    /// Sparkplug B format
    /// </summary>
    SparkplugB,

    /// <summary>
    /// XML format
    /// </summary>
    Xml,

    /// <summary>
    /// CSV format
    /// </summary>
    Csv
}