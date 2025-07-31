using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using UNSInfra.Core.Configuration;

namespace UNSInfra.Services.V1.Configuration;

/// <summary>
/// Configuration for MQTT data ingestion services.
/// Implements the dynamic configuration interface for UI management.
/// </summary>
public class MqttDataIngestionConfiguration : IDataIngestionConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "Default MQTT Connection";

    [StringLength(500)]
    public string? Description { get; set; }

    public bool Enabled { get; set; } = true;

    public string ServiceType => "MQTT";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = "System";

    public Dictionary<string, object> Metadata { get; set; } = new();

    // MQTT-specific configuration properties
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string BrokerHost { get; set; } = "localhost";

    [Range(1, 65535)]
    public int BrokerPort { get; set; } = 1883;

    public bool UseTls { get; set; } = false;

    [StringLength(100)]
    public string ClientId { get; set; } = $"UNSInfra-{Environment.MachineName}";

    [StringLength(100)]
    public string? Username { get; set; }

    [StringLength(100)]
    public string? Password { get; set; }

    [Range(10, 300)]
    public int KeepAliveInterval { get; set; } = 60;

    [Range(5, 120)]
    public int ConnectionTimeout { get; set; } = 30;

    public bool CleanSession { get; set; } = true;

    [Range(0, 100)]
    public int MaxReconnectAttempts { get; set; } = 10;

    [Range(1, 60)]
    public int ReconnectDelay { get; set; } = 5;

    public bool AutoReconnect { get; set; } = true;

    [Range(100, 10000)]
    public int MessageBufferSize { get; set; } = 1000;

    public bool EnableDetailedLogging { get; set; } = false;

    // TLS Configuration
    public string? ClientCertificatePath { get; set; }
    public string? ClientCertificatePassword { get; set; }
    public string? CaCertificatePath { get; set; }
    public bool AllowUntrustedCertificates { get; set; } = false;
    public bool IgnoreCertificateChainErrors { get; set; } = false;
    public bool IgnoreCertificateRevocationErrors { get; set; } = false;
    public string TlsVersion { get; set; } = "1.2";

    // Last Will Configuration
    public string? LastWillTopic { get; set; }
    public string? LastWillPayload { get; set; }
    public int LastWillQualityOfServiceLevel { get; set; } = 1;
    public bool LastWillRetain { get; set; } = true;
    public int LastWillDelayInterval { get; set; } = 0;

    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(BrokerHost))
            errors.Add("Broker host is required");

        if (BrokerPort < 1 || BrokerPort > 65535)
            errors.Add("Broker port must be between 1 and 65535");

        if (KeepAliveInterval < 10 || KeepAliveInterval > 300)
            errors.Add("Keep alive interval must be between 10 and 300 seconds");

        if (ConnectionTimeout < 5 || ConnectionTimeout > 120)
            errors.Add("Connection timeout must be between 5 and 120 seconds");

        if (MaxReconnectAttempts < 0 || MaxReconnectAttempts > 100)
            errors.Add("Max reconnect attempts must be between 0 and 100");

        if (ReconnectDelay < 1 || ReconnectDelay > 60)
            errors.Add("Reconnect delay must be between 1 and 60 seconds");

        if (MessageBufferSize < 100 || MessageBufferSize > 10000)
            errors.Add("Message buffer size must be between 100 and 10000");

        if (UseTls)
        {
            if (!string.IsNullOrEmpty(ClientCertificatePath) && !File.Exists(ClientCertificatePath))
                errors.Add("Client certificate file does not exist");

            if (!string.IsNullOrEmpty(CaCertificatePath) && !File.Exists(CaCertificatePath))
                errors.Add("CA certificate file does not exist");
        }

        return errors;
    }

    public IDataIngestionConfiguration Clone()
    {
        var json = JsonSerializer.Serialize(this);
        var clone = JsonSerializer.Deserialize<MqttDataIngestionConfiguration>(json)!;
        return clone;
    }

    /// <summary>
    /// Converts this configuration to the legacy MqttConfiguration format.
    /// </summary>
    /// <returns>Legacy MQTT configuration</returns>
    public MqttConfiguration ToLegacyConfiguration()
    {
        return new MqttConfiguration
        {
            BrokerHost = BrokerHost,
            BrokerPort = BrokerPort,
            UseTls = UseTls,
            ClientId = ClientId,
            Username = Username ?? string.Empty,
            Password = Password ?? string.Empty,
            KeepAliveInterval = KeepAliveInterval,
            ConnectionTimeout = ConnectionTimeout,
            CleanSession = CleanSession,
            MaxReconnectAttempts = MaxReconnectAttempts,
            ReconnectDelay = ReconnectDelay,
            AutoReconnect = AutoReconnect,
            MessageBufferSize = MessageBufferSize,
            EnableDetailedLogging = EnableDetailedLogging,
            TlsConfiguration = new MqttTlsConfiguration
            {
                ClientCertificatePath = ClientCertificatePath ?? string.Empty,
                ClientCertificatePassword = ClientCertificatePassword ?? string.Empty,
                CaCertificatePath = CaCertificatePath ?? string.Empty,
                AllowUntrustedCertificates = AllowUntrustedCertificates,
                IgnoreCertificateChainErrors = IgnoreCertificateChainErrors,
                IgnoreCertificateRevocationErrors = IgnoreCertificateRevocationErrors,
                TlsVersion = TlsVersion
            },
            LastWillConfiguration = new MqttLastWillConfiguration
            {
                Topic = LastWillTopic ?? string.Empty,
                Payload = LastWillPayload ?? string.Empty,
                QualityOfServiceLevel = LastWillQualityOfServiceLevel,
                Retain = LastWillRetain,
                DelayInterval = LastWillDelayInterval
            }
        };
    }

    /// <summary>
    /// Creates a configuration from the legacy MqttConfiguration format.
    /// </summary>
    /// <param name="legacy">Legacy MQTT configuration</param>
    /// <param name="name">Name for the new configuration</param>
    /// <returns>New MQTT data ingestion configuration</returns>
    public static MqttDataIngestionConfiguration FromLegacyConfiguration(MqttConfiguration legacy, string name = "Migrated MQTT Connection")
    {
        return new MqttDataIngestionConfiguration
        {
            Name = name,
            BrokerHost = legacy.BrokerHost,
            BrokerPort = legacy.BrokerPort,
            UseTls = legacy.UseTls,
            ClientId = legacy.ClientId,
            Username = legacy.Username,
            Password = legacy.Password,
            KeepAliveInterval = legacy.KeepAliveInterval,
            ConnectionTimeout = legacy.ConnectionTimeout,
            CleanSession = legacy.CleanSession,
            MaxReconnectAttempts = legacy.MaxReconnectAttempts,
            ReconnectDelay = legacy.ReconnectDelay,
            AutoReconnect = legacy.AutoReconnect,
            MessageBufferSize = legacy.MessageBufferSize,
            EnableDetailedLogging = legacy.EnableDetailedLogging,
            ClientCertificatePath = legacy.TlsConfiguration?.ClientCertificatePath,
            ClientCertificatePassword = legacy.TlsConfiguration?.ClientCertificatePassword,
            CaCertificatePath = legacy.TlsConfiguration?.CaCertificatePath,
            AllowUntrustedCertificates = legacy.TlsConfiguration?.AllowUntrustedCertificates ?? false,
            IgnoreCertificateChainErrors = legacy.TlsConfiguration?.IgnoreCertificateChainErrors ?? false,
            IgnoreCertificateRevocationErrors = legacy.TlsConfiguration?.IgnoreCertificateRevocationErrors ?? false,
            TlsVersion = legacy.TlsConfiguration?.TlsVersion ?? "1.2",
            LastWillTopic = legacy.LastWillConfiguration?.Topic,
            LastWillPayload = legacy.LastWillConfiguration?.Payload,
            LastWillQualityOfServiceLevel = legacy.LastWillConfiguration?.QualityOfServiceLevel ?? 1,
            LastWillRetain = legacy.LastWillConfiguration?.Retain ?? true,
            LastWillDelayInterval = legacy.LastWillConfiguration?.DelayInterval ?? 0
        };
    }
}