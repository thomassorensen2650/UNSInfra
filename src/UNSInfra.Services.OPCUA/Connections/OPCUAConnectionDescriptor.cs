using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Base;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Services.OPCUA.Models;

namespace UNSInfra.Services.OPCUA.Connections;

/// <summary>
/// Production OPC UA connection descriptor
/// </summary>
public class OPCUAConnectionDescriptor : BaseConnectionDescriptor
{
    /// <inheritdoc />
    public override string ConnectionType => "opcua";

    /// <inheritdoc />
    public override string DisplayName => "OPC UA Server";

    /// <inheritdoc />
    public override string Description => "Connect to OPC UA servers for industrial automation data";

    /// <inheritdoc />
    public override string? IconClass => "fas fa-industry";

    /// <inheritdoc />
    public override string Category => "Industrial";

    /// <inheritdoc />
    public override ConfigurationSchema GetConnectionConfigurationSchema()
    {
        return new ConfigurationSchema
        {
            Groups = new List<ConfigurationGroup>
            {
                CreateGroup("connection", "Connection Settings", "Basic OPC UA server connection settings", 0),
                CreateGroup("security", "Security", "Security and authentication settings", 1),
                CreateGroup("advanced", "Advanced Settings", "Advanced connection options", 2, true, true)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("EndpointUrl", "Endpoint URL", ConfigurationFieldType.Text, true, "opc.tcp://localhost:4840", 
                    "OPC UA server endpoint URL", "connection", 0),
                
                CreateField("ConnectionTimeoutSeconds", "Connection Timeout", ConfigurationFieldType.Number, false, 30, 
                    "Connection timeout in seconds", "connection", 1),
                
                CreateField("SessionTimeoutSeconds", "Session Timeout", ConfigurationFieldType.Number, false, 300, 
                    "Session timeout in seconds", "connection", 2),
                
                CreateField("EnableReconnection", "Enable Reconnection", ConfigurationFieldType.Boolean, false, true, 
                    "Enable automatic reconnection to the server", "connection", 3),
                
                CreateField("ReconnectionIntervalSeconds", "Reconnection Interval", ConfigurationFieldType.Number, false, 5, 
                    "Interval between reconnection attempts in seconds", "connection", 4),
                
                CreateSelectField("SecurityPolicy", "Security Policy", false, OPCUASecurityPolicy.None, 
                    "OPC UA security policy to use", "security", 0,
                    (OPCUASecurityPolicy.None, "None", "No security"),
                    (OPCUASecurityPolicy.Basic128Rsa15, "Basic128Rsa15", "Basic 128-bit encryption with RSA15"),
                    (OPCUASecurityPolicy.Basic256, "Basic256", "Basic 256-bit encryption"),
                    (OPCUASecurityPolicy.Basic256Sha256, "Basic256Sha256", "Basic 256-bit with SHA256"),
                    (OPCUASecurityPolicy.Aes128Sha256RsaOaep, "Aes128Sha256RsaOaep", "AES 128-bit with SHA256 and RSA OAEP"),
                    (OPCUASecurityPolicy.Aes256Sha256RsaPss, "Aes256Sha256RsaPss", "AES 256-bit with SHA256 and RSA PSS")),
                
                CreateSelectField("MessageSecurityMode", "Message Security Mode", false, OPCUAMessageSecurityMode.None, 
                    "Message security mode", "security", 1,
                    (OPCUAMessageSecurityMode.None, "None", "No message security"),
                    (OPCUAMessageSecurityMode.Sign, "Sign", "Sign messages"),
                    (OPCUAMessageSecurityMode.SignAndEncrypt, "SignAndEncrypt", "Sign and encrypt messages")),
                
                CreateField("Username", "Username", ConfigurationFieldType.Text, false, null, 
                    "Username for authentication", "security", 2),
                
                CreateField("Password", "Password", ConfigurationFieldType.Password, false, null, 
                    "Password for authentication", "security", 3),
                
                CreateField("ClientCertificatePath", "Client Certificate", ConfigurationFieldType.Text, false, null, 
                    "Path to client certificate file (.pfx)", "security", 4),
                
                CreateField("ClientCertificatePassword", "Certificate Password", ConfigurationFieldType.Password, false, null, 
                    "Password for client certificate", "security", 5),
                
                CreateField("ApplicationName", "Application Name", ConfigurationFieldType.Text, false, "UNSInfra OPC UA Client", 
                    "OPC UA application name", "advanced", 0),
                
                CreateField("ApplicationUri", "Application URI", ConfigurationFieldType.Text, false, "urn:UNSInfra:OPCUAClient", 
                    "OPC UA application URI", "advanced", 1),
                
                CreateField("EnableDetailedLogging", "Detailed Logging", ConfigurationFieldType.Boolean, false, false, 
                    "Enable detailed logging for debugging", "advanced", 2)
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
                CreateGroup("nodes", "Node Settings", "OPC UA node subscription options", 1),
                CreateGroup("subscription", "Subscription Settings", "OPC UA subscription parameters", 2),
                CreateGroup("processing", "Data Processing", "How to process received data", 3)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("Id", "Input ID", ConfigurationFieldType.Text, true, null, 
                    "Unique identifier for this input", "basic", 0),
                
                CreateField("Name", "Display Name", ConfigurationFieldType.Text, true, null, 
                    "Human-readable name for this input", "basic", 1),
                
                CreateField("IsEnabled", "Enabled", ConfigurationFieldType.Boolean, false, true, 
                    "Whether this input is active", "basic", 2),
                
                CreateField("NodeIds", "Node IDs", ConfigurationFieldType.TextArea, true, "ns=2;s=Temperature\nns=2;s=Pressure", 
                    "List of OPC UA node IDs to subscribe to (one per line)", "nodes", 0),
                
                CreateField("PublishingIntervalMs", "Publishing Interval (ms)", ConfigurationFieldType.Number, false, 1000, 
                    "Subscription publishing interval in milliseconds", "subscription", 0),
                
                CreateField("SamplingIntervalMs", "Sampling Interval (ms)", ConfigurationFieldType.Number, false, 1000, 
                    "Node sampling interval in milliseconds", "subscription", 1),
                
                CreateField("AutoMapToUNS", "Auto Map to UNS", ConfigurationFieldType.Boolean, false, true, 
                    "Automatically map node data to UNS hierarchy", "processing", 0),
                
                CreateField("DefaultNamespace", "Default Namespace", ConfigurationFieldType.Text, false, null, 
                    "Default namespace for UNS mapping", "processing", 1),
                
                CreateField("HierarchyMappings", "Hierarchy Mappings", ConfigurationFieldType.TextArea, false, null, 
                    "Regex patterns for extracting hierarchy from node IDs (key=value, one per line)", "processing", 2),
                
                CreateField("IncludeQuality", "Include Quality", ConfigurationFieldType.Boolean, false, true, 
                    "Include OPC UA quality information in data points", "processing", 3),
                
                CreateField("IncludeServerTimestamp", "Include Server Timestamp", ConfigurationFieldType.Boolean, false, true, 
                    "Use server timestamp for data points", "processing", 4),
                
                CreateField("IncludeSourceTimestamp", "Include Source Timestamp", ConfigurationFieldType.Boolean, false, false, 
                    "Use source timestamp for data points", "processing", 5)
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
                CreateGroup("target", "Target Settings", "OPC UA write target options", 1),
                CreateGroup("filtering", "Data Filtering", "Which data to write", 2),
                CreateGroup("validation", "Validation", "Data validation options", 3)
            },
            Fields = new List<ConfigurationField>
            {
                CreateField("Id", "Output ID", ConfigurationFieldType.Text, true, null, 
                    "Unique identifier for this output", "basic", 0),
                
                CreateField("Name", "Display Name", ConfigurationFieldType.Text, true, null, 
                    "Human-readable name for this output", "basic", 1),
                
                CreateField("IsEnabled", "Enabled", ConfigurationFieldType.Boolean, false, true, 
                    "Whether this output is active", "basic", 2),
                
                CreateField("NodeId", "Target Node ID", ConfigurationFieldType.Text, true, "ns=2;s=Setpoint", 
                    "OPC UA node ID to write values to", "target", 0),
                
                CreateField("TopicFilters", "Topic Filters", ConfigurationFieldType.TextArea, false, null, 
                    "List of topic patterns to include (one per line, empty = all topics)", "filtering", 0),
                
                CreateField("ValidateDataTypes", "Validate Data Types", ConfigurationFieldType.Boolean, false, true, 
                    "Validate data types before writing", "validation", 0),
                
                CreateField("WriteTimeoutSeconds", "Write Timeout", ConfigurationFieldType.Number, false, 10, 
                    "Timeout for write operations in seconds", "validation", 1)
            }
        };
    }

    /// <inheritdoc />
    public override object CreateDefaultConnectionConfiguration()
    {
        return new OPCUAConnectionConfiguration();
    }

    /// <inheritdoc />
    public override object CreateDefaultInputConfiguration()
    {
        return new OPCUAInputConfiguration
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = "New OPC UA Input",
            NodeIds = new List<string> { "ns=2;s=Temperature", "ns=2;s=Pressure" }
        };
    }

    /// <inheritdoc />
    public override object CreateDefaultOutputConfiguration()
    {
        return new OPCUAOutputConfiguration
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = "New OPC UA Output",
            NodeId = "ns=2;s=Setpoint"
        };
    }

    /// <inheritdoc />
    public override IDataConnection CreateConnection(string connectionId, string name, IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<OPCUAConnection>>();
        return new OPCUAConnection(connectionId, name, logger);
    }
}