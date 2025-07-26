namespace UNSInfra.Services.V1.Configuration;

/// <summary>
/// Configuration settings for MQTT broker connection and client behavior.
/// </summary>
public class MqttConfiguration
{
    /// <summary>
    /// Gets or sets the MQTT broker host address.
    /// </summary>
    public string BrokerHost { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the MQTT broker port. Default is 1883 for non-TLS, 8883 for TLS.
    /// </summary>
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// Gets or sets whether to use TLS/SSL connection.
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Gets or sets the client ID for MQTT connection.
    /// </summary>
    public string ClientId { get; set; } = $"UNSInfra-{Environment.MachineName}-{Guid.NewGuid():N}";

    /// <summary>
    /// Gets or sets the username for MQTT authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for MQTT authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the keep alive interval in seconds.
    /// </summary>
    public int KeepAliveInterval { get; set; } = 60;

    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to clean the session on connect.
    /// </summary>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of reconnection attempts.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 10;

    /// <summary>
    /// Gets or sets the reconnection delay in seconds.
    /// </summary>
    public int ReconnectDelay { get; set; } = 5;

    /// <summary>
    /// Gets or sets the TLS/SSL configuration.
    /// </summary>
    public MqttTlsConfiguration? TlsConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the Last Will and Testament configuration.
    /// </summary>
    public MqttLastWillConfiguration? LastWillConfiguration { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically reconnect on connection loss.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the message buffer size for incoming messages.
    /// </summary>
    public int MessageBufferSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to enable detailed logging for debugging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}

/// <summary>
/// TLS/SSL configuration for MQTT connections.
/// </summary>
public class MqttTlsConfiguration
{
    /// <summary>
    /// Gets or sets the path to the client certificate file (.pfx or .p12).
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the password for the client certificate.
    /// </summary>
    public string? ClientCertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets the path to the CA certificate file for server verification.
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets whether to allow untrusted certificates (for development only).
    /// </summary>
    public bool AllowUntrustedCertificates { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to ignore certificate chain errors.
    /// </summary>
    public bool IgnoreCertificateChainErrors { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to ignore certificate revocation errors.
    /// </summary>
    public bool IgnoreCertificateRevocationErrors { get; set; } = false;

    /// <summary>
    /// Gets or sets the TLS protocol version to use.
    /// </summary>
    public string TlsVersion { get; set; } = "1.2";
}

/// <summary>
/// Last Will and Testament configuration for MQTT connections.
/// </summary>
public class MqttLastWillConfiguration
{
    /// <summary>
    /// Gets or sets the topic for the Last Will message.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payload for the Last Will message.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Quality of Service level for the Last Will message.
    /// </summary>
    public int QualityOfServiceLevel { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the Last Will message should be retained.
    /// </summary>
    public bool Retain { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay in seconds before sending the Last Will message.
    /// </summary>
    public int DelayInterval { get; set; } = 0;
}