using System.ComponentModel.DataAnnotations;

namespace UNSInfra.Services.SocketIO.Models;

/// <summary>
/// Configuration for SocketIO connection
/// </summary>
public class SocketIOConnectionConfiguration
{
    /// <summary>
    /// Socket.IO server URL for the connection
    /// </summary>
    [Required]
    [Url]
    public string ServerUrl { get; set; } = "https://virtualfactory.online:3000";

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Range(1, 300)]
    public int ConnectionTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether automatic reconnection is enabled
    /// </summary>
    public bool EnableReconnection { get; set; } = true;

    /// <summary>
    /// Maximum number of reconnection attempts
    /// </summary>
    [Range(0, 20)]
    public int ReconnectionAttempts { get; set; } = 5;

    /// <summary>
    /// Delay between reconnection attempts in seconds
    /// </summary>
    [Range(1, 60)]
    public int ReconnectionDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Whether to enable detailed logging for debugging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Connection authentication token (if required)
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Additional headers to send with the connection (key=value format, one per line)
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    /// Connection query parameters (key=value format, one per line)
    /// </summary>
    public string? QueryParameters { get; set; }
}

/// <summary>
/// Configuration for SocketIO input (event subscription)
/// </summary>
public class SocketIOInputConfiguration
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
    /// List of event names to subscribe to
    /// </summary>
    [Required]
    public List<string> EventNames { get; set; } = new();

    /// <summary>
    /// Whether this input is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether to automatically map event data to UNS hierarchy
    /// </summary>
    public bool AutoMapToUNS { get; set; } = true;

    /// <summary>
    /// Default namespace to use when mapping events to UNS
    /// </summary>
    public string? DefaultNamespace { get; set; }

    /// <summary>
    /// JSON path mappings for extracting hierarchical path from event data
    /// Key: Hierarchy level name (e.g., "Enterprise", "Site", "Area")
    /// Value: JSON path to extract the value (e.g., "$.enterprise", "$.location.site")
    /// </summary>
    public Dictionary<string, string> HierarchyPathMappings { get; set; } = new();

    /// <summary>
    /// JSON path to extract the topic/identifier from event data
    /// </summary>
    public string? TopicPathMapping { get; set; }

    /// <summary>
    /// JSON path to extract the data value from event data
    /// </summary>
    public string? DataValuePathMapping { get; set; } = "$.value";

    /// <summary>
    /// How to parse the incoming event data
    /// </summary>
    public SocketIODataFormat DataFormat { get; set; } = SocketIODataFormat.Auto;

    /// <summary>
    /// Whether to listen for all events (using OnAny) if no specific events are configured
    /// </summary>
    public bool ListenForAllEvents { get; set; } = false;
}

/// <summary>
/// Configuration for SocketIO output (event emission)
/// </summary>
public class SocketIOOutputConfiguration
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
    /// Event name to emit to
    /// </summary>
    [Required]
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this output is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// How to format the outgoing event data
    /// </summary>
    public SocketIODataFormat DataFormat { get; set; } = SocketIODataFormat.Json;

    /// <summary>
    /// Whether to include timestamp in the payload
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Whether to include quality information in the payload
    /// </summary>
    public bool IncludeQuality { get; set; } = false;

    /// <summary>
    /// Filters for which data to emit
    /// </summary>
    public List<string> TopicFilters { get; set; } = new();

    /// <summary>
    /// Whether to emit only on data changes
    /// </summary>
    public bool EmitOnChange { get; set; } = true;

    /// <summary>
    /// Minimum interval between emits in milliseconds
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MinEmitIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Template for constructing the emit payload
    /// </summary>
    public string? PayloadTemplate { get; set; }
}

/// <summary>
/// Supported SocketIO data formats
/// </summary>
public enum SocketIODataFormat
{
    /// <summary>
    /// Auto-detect format based on content
    /// </summary>
    Auto,

    /// <summary>
    /// Raw string/object
    /// </summary>
    Raw,

    /// <summary>
    /// JSON format
    /// </summary>
    Json,

    /// <summary>
    /// XML format
    /// </summary>
    Xml,

    /// <summary>
    /// MessagePack format
    /// </summary>
    MessagePack
}