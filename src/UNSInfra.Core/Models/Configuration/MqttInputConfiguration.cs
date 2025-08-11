namespace UNSInfra.Models.Configuration;

/// <summary>
/// MQTT input configuration for topic filter-based data ingestion
/// </summary>
public class MqttInputConfiguration : InputConfiguration
{
    public MqttInputConfiguration()
    {
        ServiceType = "MQTT";
    }

    /// <summary>
    /// MQTT topic filter to subscribe to (e.g., "#", "Enterprise1/+/Line1", "sensors/+/temperature")
    /// </summary>
    public string TopicFilter { get; set; } = string.Empty;

    /// <summary>
    /// Quality of Service level for MQTT subscription
    /// </summary>
    public int QoS { get; set; } = 1;

    /// <summary>
    /// Whether to automatically map topic hierarchy to UNS hierarchy
    /// </summary>
    public bool AutoMapTopicToUNS { get; set; } = true;

    /// <summary>
    /// Topic pattern for extracting hierarchical path
    /// Uses placeholders like {Enterprise}/{Site}/{Area} to map topic segments to hierarchy levels
    /// </summary>
    public string? TopicPattern { get; set; }

    /// <summary>
    /// Whether to use Sparkplug B protocol for decoding
    /// </summary>
    public bool UseSparkplugB { get; set; } = false;

    /// <summary>
    /// Default namespace to assign when auto-mapping
    /// </summary>
    public string? DefaultNamespace { get; set; }

    /// <summary>
    /// Topic prefix to strip when mapping (e.g., "v1.0" in Sparkplug B)
    /// </summary>
    public string? TopicPrefix { get; set; }

    /// <summary>
    /// Whether to retain the last known value for each topic
    /// </summary>
    public bool RetainLastKnownValue { get; set; } = true;
}