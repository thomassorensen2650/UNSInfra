using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using UNSInfra.Core.Configuration;

namespace UNSInfra.Services.SocketIO.Configuration;

/// <summary>
/// Configuration for SocketIO data ingestion services.
/// Implements the dynamic configuration interface for UI management.
/// </summary>
public class SocketIODataIngestionConfiguration : IDataIngestionConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "Default SocketIO Connection";

    [StringLength(500)]
    public string? Description { get; set; }

    public bool Enabled { get; set; } = true;

    public string ServiceType => "SocketIO";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = "System";

    public Dictionary<string, object> Metadata { get; set; } = new();

    // SocketIO-specific configuration properties
    [Required]
    [Url]
    [StringLength(500, MinimumLength = 1)]
    public string ServerUrl { get; set; } = "https://localhost:3000";

    [Range(1, 300)]
    public int ConnectionTimeoutSeconds { get; set; } = 10;

    public bool EnableReconnection { get; set; } = true;

    [Range(0, 100)]
    public int ReconnectionAttempts { get; set; } = 5;

    [Range(1, 60)]
    public int ReconnectionDelaySeconds { get; set; } = 2;

    public string[] EventNames { get; set; } = Array.Empty<string>();

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string BaseTopicPath { get; set; } = "socketio";

    public bool EnableDetailedLogging { get; set; } = false;

    // Additional SocketIO options
    public Dictionary<string, string> ExtraHeaders { get; set; } = new();

    public Dictionary<string, object> AuthenticationData { get; set; } = new();

    public string? Namespace { get; set; }

    public bool ForceNew { get; set; } = false;

    public bool AutoConnect { get; set; } = true;

    [Range(100, 10000)]
    public int MessageBufferSize { get; set; } = 1000;

    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(ServerUrl))
            errors.Add("Server URL is required");

        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri))
            errors.Add("Server URL must be a valid URL");
        else if (uri.Scheme != "http" && uri.Scheme != "https")
            errors.Add("Server URL must use HTTP or HTTPS protocol");

        if (ConnectionTimeoutSeconds < 1 || ConnectionTimeoutSeconds > 300)
            errors.Add("Connection timeout must be between 1 and 300 seconds");

        if (ReconnectionAttempts < 0 || ReconnectionAttempts > 100)
            errors.Add("Reconnection attempts must be between 0 and 100");

        if (ReconnectionDelaySeconds < 1 || ReconnectionDelaySeconds > 60)
            errors.Add("Reconnection delay must be between 1 and 60 seconds");

        if (string.IsNullOrWhiteSpace(BaseTopicPath))
            errors.Add("Base topic path is required");

        if (MessageBufferSize < 100 || MessageBufferSize > 10000)
            errors.Add("Message buffer size must be between 100 and 10000");

        // Validate event names
        if (EventNames != null)
        {
            foreach (var eventName in EventNames)
            {
                if (string.IsNullOrWhiteSpace(eventName))
                    errors.Add("Event names cannot be empty");
            }
        }

        return errors;
    }

    public IDataIngestionConfiguration Clone()
    {
        var json = JsonSerializer.Serialize(this);
        var clone = JsonSerializer.Deserialize<SocketIODataIngestionConfiguration>(json)!;
        clone.Id = Guid.NewGuid().ToString();
        clone.Name = $"{Name} (Copy)";
        clone.CreatedAt = DateTime.UtcNow;
        clone.ModifiedAt = DateTime.UtcNow;
        return clone;
    }

    /// <summary>
    /// Converts this configuration to the legacy SocketIOConfiguration format.
    /// </summary>
    /// <returns>Legacy SocketIO configuration</returns>
    public SocketIOConfiguration ToLegacyConfiguration()
    {
        return new SocketIOConfiguration
        {
            ServerUrl = ServerUrl,
            ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
            EnableReconnection = EnableReconnection,
            ReconnectionAttempts = ReconnectionAttempts,
            ReconnectionDelaySeconds = ReconnectionDelaySeconds,
            EventNames = EventNames,
            BaseTopicPath = BaseTopicPath,
            EnableDetailedLogging = EnableDetailedLogging
        };
    }

    /// <summary>
    /// Creates a configuration from the legacy SocketIOConfiguration format.
    /// </summary>
    /// <param name="legacy">Legacy SocketIO configuration</param>
    /// <param name="name">Name for the new configuration</param>
    /// <returns>New SocketIO data ingestion configuration</returns>
    public static SocketIODataIngestionConfiguration FromLegacyConfiguration(SocketIOConfiguration legacy, string name = "Migrated SocketIO Connection")
    {
        return new SocketIODataIngestionConfiguration
        {
            Name = name,
            ServerUrl = legacy.ServerUrl,
            ConnectionTimeoutSeconds = legacy.ConnectionTimeoutSeconds,
            EnableReconnection = legacy.EnableReconnection,
            ReconnectionAttempts = legacy.ReconnectionAttempts,
            ReconnectionDelaySeconds = legacy.ReconnectionDelaySeconds,
            EventNames = legacy.EventNames,
            BaseTopicPath = legacy.BaseTopicPath,
            EnableDetailedLogging = legacy.EnableDetailedLogging
        };
    }
}