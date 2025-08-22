using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Base;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Services.V1.Models;

namespace UNSInfra.Services.V1.Connections;

/// <summary>
/// Production MQTT connection descriptor
/// </summary>
public class MqttConnectionDescriptor : BaseConnectionDescriptor
{
    /// <inheritdoc />
    public override string ConnectionType => "mqtt";

    /// <inheritdoc />
    public override string DisplayName => "MQTT Broker";

    /// <inheritdoc />
    public override string Description => "Connect to MQTT brokers for publishing and subscribing to topics using MQTTnet";

    /// <inheritdoc />
    public override string? IconClass => "fas fa-network-wired";

    /// <inheritdoc />
    public override string Category => "Messaging";

    /// <inheritdoc />
    public override ConfigurationSchema GetConnectionConfigurationSchema()
    {
        return new ConfigurationSchema
        {
            Groups = new List<ConfigurationGroup>
            {
                CreateGroup("connection", "Connection Settings", "Basic MQTT broker connection settings", 0),
                CreateGroup("authentication", "Authentication", "Username and password authentication", 1),
                CreateGroup("advanced", "Advanced Settings", "Advanced connection options", 2, true, true)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("Host", "Broker Host", ConfigurationFieldType.Text, true, "localhost", 
                    "MQTT broker hostname or IP address", "connection", 0),
                
                CreateField("Port", "Port", ConfigurationFieldType.Number, true, 1883, 
                    "MQTT broker port (1883 for standard, 8883 for TLS)", "connection", 1),
                
                CreateField("ClientId", "Client ID", ConfigurationFieldType.Text, false, null, 
                    "Unique client identifier (auto-generated if empty)", "connection", 2),
                
                CreateField("Username", "Username", ConfigurationFieldType.Text, false, null, 
                    "Username for broker authentication", "authentication", 0),
                
                CreatePasswordField("Password", "Password", false, null, 
                    "Password for broker authentication", "authentication", 1),
                
                CreateField("UseTls", "Use TLS/SSL", ConfigurationFieldType.Boolean, false, false, 
                    "Enable secure connection with TLS/SSL", "advanced", 0),
                
                CreateField("TimeoutSeconds", "Connection Timeout", ConfigurationFieldType.Number, false, 30, 
                    "Connection timeout in seconds", "advanced", 1),
                
                CreateField("KeepAliveSeconds", "Keep Alive", ConfigurationFieldType.Number, false, 60, 
                    "Keep alive interval in seconds", "advanced", 2),
                
                CreateField("CleanSession", "Clean Session", ConfigurationFieldType.Boolean, false, true, 
                    "Start with a clean session", "advanced", 3)
            }
        };
    }

    /// <inheritdoc />
    public override ConfigurationSchema GetInputConfigurationSchema()
    {
        return new ConfigurationSchema
        {
            Groups = new List<ConfigurationGroup>
            {
                CreateGroup("basic", "Basic Settings", "Essential input configuration", 0),
                CreateGroup("subscription", "Subscription Settings", "MQTT subscription options", 1),
                CreateGroup("processing", "Data Processing", "How to process received data", 2)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("Id", "Input ID", ConfigurationFieldType.Text, true, null, 
                    "Unique identifier for this input", "basic", 0),
                
                CreateField("Name", "Display Name", ConfigurationFieldType.Text, true, null, 
                    "Human-readable name for this input", "basic", 1),
                
                CreateField("TopicPattern", "Topic Pattern", ConfigurationFieldType.Text, true, null, 
                    "MQTT topic pattern to subscribe to (supports wildcards + and #)", "subscription", 0),
                
                CreateSelectField("QualityOfService", "Quality of Service", false, 1, 
                    "MQTT QoS level for subscription", "subscription", 1,
                    (0, "At most once (0)", "Fire and forget"),
                    (1, "At least once (1)", "Acknowledged delivery"),
                    (2, "Exactly once (2)", "Assured delivery")),
                
                CreateField("IsEnabled", "Enabled", ConfigurationFieldType.Boolean, false, true, 
                    "Whether this input is active", "basic", 2),
                
                CreateSelectField("PayloadFormat", "Payload Format", false, PayloadFormat.Auto, 
                    "How to parse incoming message payloads", "processing", 0,
                    (PayloadFormat.Auto, "Auto-detect", "Automatically detect format"),
                    (PayloadFormat.Raw, "Raw", "Treat as raw string/binary"),
                    (PayloadFormat.Json, "JSON", "Parse as JSON"),
                    (PayloadFormat.SparkplugB, "Sparkplug B", "Parse as Sparkplug B protobuf"),
                    (PayloadFormat.Xml, "XML", "Parse as XML"),
                    (PayloadFormat.Csv, "CSV", "Parse as CSV")),
                
                CreateField("TopicMapping", "Topic Mapping", ConfigurationFieldType.TextArea, false, null, 
                    "Expression to map MQTT topic to UNS path (e.g., '{0}/{1}' for first two topic segments)", "processing", 1)
            }
        };
    }

    /// <inheritdoc />
    public override ConfigurationSchema GetOutputConfigurationSchema()
    {
        return new ConfigurationSchema
        {
            Groups = new List<ConfigurationGroup>
            {
                CreateGroup("basic", "Basic Settings", "Essential output configuration", 0),
                CreateGroup("publishing", "Publishing Settings", "MQTT publishing options", 1),
                CreateGroup("formatting", "Data Formatting", "How to format outgoing data", 2),
                CreateGroup("filtering", "Data Filtering", "Which data to publish", 3)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("Id", "Output ID", ConfigurationFieldType.Text, true, null, 
                    "Unique identifier for this output", "basic", 0),
                
                CreateField("Name", "Display Name", ConfigurationFieldType.Text, true, null, 
                    "Human-readable name for this output", "basic", 1),
                
                CreateField("TopicPattern", "Topic Pattern", ConfigurationFieldType.Text, true, null, 
                    "MQTT topic pattern to publish to (supports placeholders)", "publishing", 0),
                
                CreateSelectField("QualityOfService", "Quality of Service", false, 1, 
                    "MQTT QoS level for publishing", "publishing", 1,
                    (0, "At most once (0)", "Fire and forget"),
                    (1, "At least once (1)", "Acknowledged delivery"),
                    (2, "Exactly once (2)", "Assured delivery")),
                
                CreateField("Retain", "Retain Messages", ConfigurationFieldType.Boolean, false, false, 
                    "Whether published messages should be retained by the broker", "publishing", 2),
                
                CreateField("IsEnabled", "Enabled", ConfigurationFieldType.Boolean, false, true, 
                    "Whether this output is active", "basic", 2),
                
                CreateSelectField("PayloadFormat", "Payload Format", false, PayloadFormat.Json, 
                    "How to format outgoing message payloads", "formatting", 0,
                    (PayloadFormat.Raw, "Raw", "Send raw value as string"),
                    (PayloadFormat.Json, "JSON", "Format as JSON object"),
                    (PayloadFormat.SparkplugB, "Sparkplug B", "Format as Sparkplug B protobuf"),
                    (PayloadFormat.Xml, "XML", "Format as XML"),
                    (PayloadFormat.Csv, "CSV", "Format as CSV")),
                
                CreateField("IncludeTimestamp", "Include Timestamp", ConfigurationFieldType.Boolean, false, true, 
                    "Include timestamp in the payload", "formatting", 1),
                
                CreateField("IncludeQuality", "Include Quality", ConfigurationFieldType.Boolean, false, false, 
                    "Include data quality information in the payload", "formatting", 2),
                
                CreateField("TopicFilters", "Topic Filters", ConfigurationFieldType.TextArea, false, null, 
                    "Comma-separated list of topic patterns to include (empty = all topics)", "filtering", 0),
                
                CreateField("PublishOnChange", "Publish on Change", ConfigurationFieldType.Boolean, false, true, 
                    "Only publish when data values change", "filtering", 1),
                
                CreateField("MinPublishIntervalMs", "Min Publish Interval (ms)", ConfigurationFieldType.Number, false, 1000, 
                    "Minimum time between publishes for the same topic", "filtering", 2)
            }
        };
    }

    /// <inheritdoc />
    public override object CreateDefaultConnectionConfiguration()
    {
        return new MqttConnectionConfiguration();
    }

    /// <inheritdoc />
    public override object CreateDefaultInputConfiguration()
    {
        return new MqttInputConfiguration
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = "New MQTT Input",
            TopicPattern = "data/+/+"
        };
    }

    /// <inheritdoc />
    public override object CreateDefaultOutputConfiguration()
    {
        return new MqttOutputConfiguration
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = "New MQTT Output",
            TopicPattern = "output/{unsPath}"
        };
    }

    /// <inheritdoc />
    public override IDataConnection CreateConnection(string connectionId, string name, IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<MqttConnection>>();
        return new MqttConnection(connectionId, name, logger);
    }
}