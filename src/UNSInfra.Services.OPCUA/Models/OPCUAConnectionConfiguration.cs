using System.ComponentModel.DataAnnotations;

namespace UNSInfra.Services.OPCUA.Models;

/// <summary>
/// Configuration for OPC UA connection
/// </summary>
public class OPCUAConnectionConfiguration
{
    /// <summary>
    /// OPC UA server endpoint URL
    /// </summary>
    [Required]
    [Url]
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4840";

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Range(1, 300)]
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Session timeout in seconds
    /// </summary>
    [Range(30, 3600)]
    public int SessionTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to enable automatic reconnection
    /// </summary>
    public bool EnableReconnection { get; set; } = true;

    /// <summary>
    /// Reconnection interval in seconds
    /// </summary>
    [Range(1, 60)]
    public int ReconnectionIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Security policy for the connection
    /// </summary>
    public OPCUASecurityPolicy SecurityPolicy { get; set; } = OPCUASecurityPolicy.None;

    /// <summary>
    /// Message security mode
    /// </summary>
    public OPCUAMessageSecurityMode MessageSecurityMode { get; set; } = OPCUAMessageSecurityMode.None;

    /// <summary>
    /// Username for authentication (if required)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication (if required)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Client certificate file path (for certificate-based authentication)
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Client certificate password
    /// </summary>
    public string? ClientCertificatePassword { get; set; }

    /// <summary>
    /// Whether to enable detailed logging for debugging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Application name for the OPC UA client
    /// </summary>
    public string ApplicationName { get; set; } = "UNSInfra OPC UA Client";

    /// <summary>
    /// Application URI for the OPC UA client
    /// </summary>
    public string ApplicationUri { get; set; } = "urn:UNSInfra:OPCUAClient";
}

/// <summary>
/// Configuration for OPC UA input (subscription)
/// </summary>
public class OPCUAInputConfiguration
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
    /// Whether this input is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// List of node IDs to subscribe to
    /// </summary>
    [Required]
    public List<string> NodeIds { get; set; } = new();

    /// <summary>
    /// Subscription publishing interval in milliseconds
    /// </summary>
    [Range(100, 60000)]
    public int PublishingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Sampling interval in milliseconds
    /// </summary>
    [Range(100, 60000)]
    public int SamplingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Whether to automatically map node data to UNS hierarchy
    /// </summary>
    public bool AutoMapToUNS { get; set; } = true;

    /// <summary>
    /// Default namespace to use when mapping nodes to UNS
    /// </summary>
    public string? DefaultNamespace { get; set; }

    /// <summary>
    /// Mapping rules for extracting hierarchy from node identifiers
    /// Key: Hierarchy level name, Value: Regex pattern to extract from node ID
    /// </summary>
    public Dictionary<string, string> HierarchyMappings { get; set; } = new();

    /// <summary>
    /// Whether to include quality information in data points
    /// </summary>
    public bool IncludeQuality { get; set; } = true;

    /// <summary>
    /// Whether to include server timestamp
    /// </summary>
    public bool IncludeServerTimestamp { get; set; } = true;

    /// <summary>
    /// Whether to include source timestamp
    /// </summary>
    public bool IncludeSourceTimestamp { get; set; } = false;
}

/// <summary>
/// Configuration for OPC UA output (writing values)
/// </summary>
public class OPCUAOutputConfiguration
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
    /// Whether this output is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Target node ID for writing values
    /// </summary>
    [Required]
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Filters for which topics to write to this node
    /// </summary>
    public List<string> TopicFilters { get; set; } = new();

    /// <summary>
    /// Whether to validate data types before writing
    /// </summary>
    public bool ValidateDataTypes { get; set; } = true;

    /// <summary>
    /// Write timeout in seconds
    /// </summary>
    [Range(1, 60)]
    public int WriteTimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// OPC UA security policies
/// </summary>
public enum OPCUASecurityPolicy
{
    None,
    Basic128Rsa15,
    Basic256,
    Basic256Sha256,
    Aes128Sha256RsaOaep,
    Aes256Sha256RsaPss
}

/// <summary>
/// OPC UA message security modes
/// </summary>
public enum OPCUAMessageSecurityMode
{
    None,
    Sign,
    SignAndEncrypt
}