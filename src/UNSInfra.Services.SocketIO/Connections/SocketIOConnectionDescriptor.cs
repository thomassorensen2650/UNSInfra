using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Base;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Services.SocketIO.Models;

namespace UNSInfra.Services.SocketIO.Connections;

/// <summary>
/// Production SocketIO connection descriptor
/// </summary>
public class SocketIOConnectionDescriptor : BaseConnectionDescriptor
{
    /// <inheritdoc />
    public override string ConnectionType => "socketio";

    /// <inheritdoc />
    public override string DisplayName => "Socket.IO Server";

    /// <inheritdoc />
    public override string Description => "Connect to Socket.IO servers for real-time event-based communication";

    /// <inheritdoc />
    public override string? IconClass => "fas fa-plug";

    /// <inheritdoc />
    public override string Category => "Real-time";

    /// <inheritdoc />
    public override ConfigurationSchema GetConnectionConfigurationSchema()
    {
        return new ConfigurationSchema
        {
            Groups = new List<ConfigurationGroup>
            {
                CreateGroup("connection", "Connection Settings", "Basic Socket.IO server connection settings", 0),
                CreateGroup("authentication", "Authentication", "Authentication and authorization settings", 1),
                CreateGroup("advanced", "Advanced Settings", "Advanced connection options", 2, true, true)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("ServerUrl", "Server URL", ConfigurationFieldType.Text, true, "https://virtualfactory.online:3000", 
                    "Socket.IO server URL (must include protocol)", "connection", 0),
                
                CreateField("ConnectionTimeoutSeconds", "Connection Timeout", ConfigurationFieldType.Number, false, 10, 
                    "Connection timeout in seconds", "connection", 1),
                
                CreateField("EnableReconnection", "Enable Reconnection", ConfigurationFieldType.Boolean, false, true, 
                    "Enable automatic reconnection to the server", "connection", 2),
                
                CreateField("ReconnectionAttempts", "Reconnection Attempts", ConfigurationFieldType.Number, false, 5, 
                    "Maximum number of reconnection attempts", "connection", 3),
                
                CreateField("ReconnectionDelaySeconds", "Reconnection Delay", ConfigurationFieldType.Number, false, 2, 
                    "Delay between reconnection attempts in seconds", "connection", 4),
                
                CreateField("AuthToken", "Auth Token", ConfigurationFieldType.Text, false, null, 
                    "Authentication token for the connection", "authentication", 0),
                
                CreateField("Headers", "Headers", ConfigurationFieldType.TextArea, false, null, 
                    "Additional HTTP headers to send (key=value, one per line)", "authentication", 1),
                
                CreateField("QueryParameters", "Query Parameters", ConfigurationFieldType.TextArea, false, null, 
                    "Query parameters to include in the connection (key=value, one per line)", "authentication", 2),
                
                CreateField("EnableDetailedLogging", "Detailed Logging", ConfigurationFieldType.Boolean, false, false, 
                    "Enable detailed logging for debugging", "advanced", 0)
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
                CreateGroup("events", "Event Settings", "Socket.IO event subscription options", 1),
                CreateGroup("processing", "Data Processing", "How to process received events", 2)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("Id", "Input ID", ConfigurationFieldType.Text, true, null, 
                    "Unique identifier for this input", "basic", 0),
                
                CreateField("Name", "Display Name", ConfigurationFieldType.Text, true, null, 
                    "Human-readable name for this input", "basic", 1),
                
                CreateField("IsEnabled", "Enabled", ConfigurationFieldType.Boolean, false, true, 
                    "Whether this input is active", "basic", 2),
                
                CreateField("EventNames", "Event Names", ConfigurationFieldType.TextArea, true, "update\ndata", 
                    "List of Socket.IO event names to subscribe to (one per line)", "events", 0),
                
                CreateField("ListenForAllEvents", "Listen for All Events", ConfigurationFieldType.Boolean, false, false, 
                    "Listen for all events using OnAny (overrides specific event names)", "events", 1),
                
                CreateSelectField("DataFormat", "Data Format", false, SocketIODataFormat.Auto, 
                    "How to parse incoming event data", "processing", 0,
                    (SocketIODataFormat.Auto, "Auto-detect", "Automatically detect format"),
                    (SocketIODataFormat.Raw, "Raw", "Treat as raw object"),
                    (SocketIODataFormat.Json, "JSON", "Parse as JSON"),
                    (SocketIODataFormat.Xml, "XML", "Parse as XML"),
                    (SocketIODataFormat.MessagePack, "MessagePack", "Parse as MessagePack")),
                
                CreateField("AutoMapToUNS", "Auto Map to UNS", ConfigurationFieldType.Boolean, false, true, 
                    "Automatically map event data to UNS hierarchy", "processing", 1),
                
                CreateField("DefaultNamespace", "Default Namespace", ConfigurationFieldType.Text, false, null, 
                    "Default namespace for UNS mapping", "processing", 2),
                
                CreateField("HierarchyPathMappings", "Path Mappings", ConfigurationFieldType.TextArea, false, null, 
                    "JSON path mappings for hierarchy extraction (key=value, one per line)", "processing", 3),
                
                CreateField("TopicPathMapping", "Topic Path", ConfigurationFieldType.Text, false, null, 
                    "JSON path to extract topic identifier", "processing", 4),
                
                CreateField("DataValuePathMapping", "Value Path", ConfigurationFieldType.Text, false, "$.value", 
                    "JSON path to extract data value", "processing", 5)
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
                CreateGroup("emission", "Emission Settings", "Socket.IO event emission options", 1),
                CreateGroup("formatting", "Data Formatting", "How to format outgoing data", 2),
                CreateGroup("filtering", "Data Filtering", "Which data to emit", 3)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("Id", "Output ID", ConfigurationFieldType.Text, true, null, 
                    "Unique identifier for this output", "basic", 0),
                
                CreateField("Name", "Display Name", ConfigurationFieldType.Text, true, null, 
                    "Human-readable name for this output", "basic", 1),
                
                CreateField("IsEnabled", "Enabled", ConfigurationFieldType.Boolean, false, true, 
                    "Whether this output is active", "basic", 2),
                
                CreateField("EventName", "Event Name", ConfigurationFieldType.Text, true, "data", 
                    "Socket.IO event name to emit to", "emission", 0),
                
                CreateSelectField("DataFormat", "Data Format", false, SocketIODataFormat.Json, 
                    "How to format outgoing event data", "formatting", 0,
                    (SocketIODataFormat.Raw, "Raw", "Send raw value"),
                    (SocketIODataFormat.Json, "JSON", "Format as JSON object"),
                    (SocketIODataFormat.Xml, "XML", "Format as XML"),
                    (SocketIODataFormat.MessagePack, "MessagePack", "Format as MessagePack")),
                
                CreateField("IncludeTimestamp", "Include Timestamp", ConfigurationFieldType.Boolean, false, true, 
                    "Include timestamp in the payload", "formatting", 1),
                
                CreateField("IncludeQuality", "Include Quality", ConfigurationFieldType.Boolean, false, false, 
                    "Include data quality information in the payload", "formatting", 2),
                
                CreateField("PayloadTemplate", "Payload Template", ConfigurationFieldType.TextArea, false, null, 
                    "Custom template for payload construction", "formatting", 3),
                
                CreateField("TopicFilters", "Topic Filters", ConfigurationFieldType.TextArea, false, null, 
                    "List of topic patterns to include (one per line, empty = all topics)", "filtering", 0),
                
                CreateField("EmitOnChange", "Emit on Change", ConfigurationFieldType.Boolean, false, true, 
                    "Only emit when data values change", "filtering", 1),
                
                CreateField("MinEmitIntervalMs", "Min Emit Interval (ms)", ConfigurationFieldType.Number, false, 1000, 
                    "Minimum time between emits for the same topic", "filtering", 2)
            }
        };
    }

    /// <inheritdoc />
    public override object CreateDefaultConnectionConfiguration()
    {
        return new SocketIOConnectionConfiguration();
    }

    /// <inheritdoc />
    public override object CreateDefaultInputConfiguration()
    {
        return new SocketIOInputConfiguration
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = "New Socket.IO Input",
            EventNames = new List<string> { "update", "data" }
        };
    }

    /// <inheritdoc />
    public override object CreateDefaultOutputConfiguration()
    {
        return new SocketIOOutputConfiguration
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = "New Socket.IO Output",
            EventName = "data"
        };
    }

    /// <inheritdoc />
    public override IDataConnection CreateConnection(string connectionId, string name, IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<SocketIOConnection>>();
        return new SocketIOConnection(connectionId, name, logger);
    }
}