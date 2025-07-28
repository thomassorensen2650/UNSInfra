using UNSInfra.Core.Configuration;
using UNSInfra.Services.V1.Configuration;
using UNSInfra.Services.V1.Mqtt;
using UNSInfra.Services.DataIngestion.Mock;
using Microsoft.Extensions.DependencyInjection;

namespace UNSInfra.Services.V1.Descriptors;

/// <summary>
/// Service descriptor for MQTT data ingestion services.
/// Provides metadata and configuration field definitions for the UI.
/// </summary>
public class MqttServiceDescriptor : IDataIngestionServiceDescriptor
{
    public string ServiceType => "MQTT";
    public string DisplayName => "MQTT Broker";
    public string Description => "Connect to MQTT brokers for real-time data ingestion using the MQTT protocol.";
    public string? IconClass => "bi bi-router";
    public string? Category => "Message Brokers";
    public int Order => 1;
    public Type ConfigurationType => typeof(MqttDataIngestionConfiguration);
    public Type ServiceImplementationType => typeof(MqttDataService);

    public List<ConfigurationField> GetConfigurationFields()
    {
        return new List<ConfigurationField>
        {
            // Connection Group
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.BrokerHost),
                DisplayName = "Broker Host",
                Description = "MQTT broker hostname or IP address",
                FieldType = FieldType.Text,
                Required = true,
                Group = "Connection",
                Order = 1,
                DefaultValue = "localhost"
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.BrokerPort),
                DisplayName = "Broker Port",
                Description = "MQTT broker port (1883 for non-TLS, 8883 for TLS)",
                FieldType = FieldType.Number,
                Required = true,
                Group = "Connection",
                Order = 2,
                DefaultValue = 1883,
                ValidationAttributes = new Dictionary<string, object> { { "min", 1 }, { "max", 65535 } }
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.UseTls),
                DisplayName = "Use TLS/SSL",
                Description = "Enable secure connection using TLS/SSL",
                FieldType = FieldType.Boolean,
                Group = "Connection",
                Order = 3,
                DefaultValue = false
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.ClientId),
                DisplayName = "Client ID",
                Description = "Unique identifier for this MQTT client",
                FieldType = FieldType.Text,
                Required = true,
                Group = "Connection",
                Order = 4,
                DefaultValue = $"UNSInfra-{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}"
            },

            // Authentication Group
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.Username),
                DisplayName = "Username",
                Description = "Username for MQTT broker authentication (optional)",
                FieldType = FieldType.Text,
                Group = "Authentication",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.Password),
                DisplayName = "Password",
                Description = "Password for MQTT broker authentication (optional)",
                FieldType = FieldType.Password,
                Group = "Authentication",
                Order = 2
            },

            // Connection Behavior Group
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.KeepAliveInterval),
                DisplayName = "Keep Alive Interval (seconds)",
                Description = "Interval for keep-alive messages",
                FieldType = FieldType.Number,
                Group = "Connection Behavior",
                Order = 1,
                DefaultValue = 60,
                ValidationAttributes = new Dictionary<string, object> { { "min", 10 }, { "max", 300 } }
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.ConnectionTimeout),
                DisplayName = "Connection Timeout (seconds)",
                Description = "Timeout for connection attempts",
                FieldType = FieldType.Number,
                Group = "Connection Behavior",
                Order = 2,
                DefaultValue = 30,
                ValidationAttributes = new Dictionary<string, object> { { "min", 5 }, { "max", 120 } }
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.CleanSession),
                DisplayName = "Clean Session",
                Description = "Start with a clean session (no persistent state)",
                FieldType = FieldType.Boolean,
                Group = "Connection Behavior",
                Order = 3,
                DefaultValue = true
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.AutoReconnect),
                DisplayName = "Auto Reconnect",
                Description = "Automatically reconnect on connection loss",
                FieldType = FieldType.Boolean,
                Group = "Connection Behavior",
                Order = 4,
                DefaultValue = true
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.MaxReconnectAttempts),
                DisplayName = "Max Reconnect Attempts",
                Description = "Maximum number of reconnection attempts (0 = unlimited)",
                FieldType = FieldType.Number,
                Group = "Connection Behavior",
                Order = 5,
                DefaultValue = 10,
                ValidationAttributes = new Dictionary<string, object> { { "min", 0 }, { "max", 100 } }
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.ReconnectDelay),
                DisplayName = "Reconnect Delay (seconds)",
                Description = "Delay between reconnection attempts",
                FieldType = FieldType.Number,
                Group = "Connection Behavior",
                Order = 6,
                DefaultValue = 5,
                ValidationAttributes = new Dictionary<string, object> { { "min", 1 }, { "max", 60 } }
            },

            // Performance Group
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.MessageBufferSize),
                DisplayName = "Message Buffer Size",
                Description = "Buffer size for incoming messages",
                FieldType = FieldType.Number,
                Group = "Performance",
                Order = 1,
                DefaultValue = 1000,
                ValidationAttributes = new Dictionary<string, object> { { "min", 100 }, { "max", 10000 } }
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.EnableDetailedLogging),
                DisplayName = "Enable Detailed Logging",
                Description = "Enable verbose logging for debugging",
                FieldType = FieldType.Boolean,
                Group = "Performance",
                Order = 2,
                DefaultValue = false
            },

            // TLS/SSL Group
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.ClientCertificatePath),
                DisplayName = "Client Certificate Path",
                Description = "Path to client certificate file (.pfx or .p12)",
                FieldType = FieldType.Text,
                Group = "TLS/SSL",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.ClientCertificatePassword),
                DisplayName = "Client Certificate Password",
                Description = "Password for the client certificate",
                FieldType = FieldType.Password,
                Group = "TLS/SSL",
                Order = 2
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.CaCertificatePath),
                DisplayName = "CA Certificate Path",
                Description = "Path to CA certificate file for server verification",
                FieldType = FieldType.Text,
                Group = "TLS/SSL",
                Order = 3
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.AllowUntrustedCertificates),
                DisplayName = "Allow Untrusted Certificates",
                Description = "Allow untrusted certificates (development only)",
                FieldType = FieldType.Boolean,
                Group = "TLS/SSL",
                Order = 4,
                DefaultValue = false
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.IgnoreCertificateChainErrors),
                DisplayName = "Ignore Certificate Chain Errors",
                Description = "Ignore certificate chain validation errors",
                FieldType = FieldType.Boolean,
                Group = "TLS/SSL",
                Order = 5,
                DefaultValue = false
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.IgnoreCertificateRevocationErrors),
                DisplayName = "Ignore Certificate Revocation Errors",
                Description = "Ignore certificate revocation validation errors",
                FieldType = FieldType.Boolean,
                Group = "TLS/SSL",
                Order = 6,
                DefaultValue = false
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.TlsVersion),
                DisplayName = "TLS Version",
                Description = "TLS protocol version to use",
                FieldType = FieldType.Select,
                Group = "TLS/SSL",
                Order = 7,
                DefaultValue = "1.2",
                Options = new List<SelectOption>
                {
                    new SelectOption { Value = "1.0", Text = "TLS 1.0" },
                    new SelectOption { Value = "1.1", Text = "TLS 1.1" },
                    new SelectOption { Value = "1.2", Text = "TLS 1.2 (Recommended)" },
                    new SelectOption { Value = "1.3", Text = "TLS 1.3" }
                }
            },

            // Last Will Group
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.LastWillTopic),
                DisplayName = "Last Will Topic",
                Description = "Topic for Last Will and Testament message",
                FieldType = FieldType.Text,
                Group = "Last Will",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.LastWillPayload),
                DisplayName = "Last Will Payload",
                Description = "Payload for Last Will and Testament message",
                FieldType = FieldType.TextArea,
                Group = "Last Will",
                Order = 2
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.LastWillQualityOfServiceLevel),
                DisplayName = "Last Will QoS Level",
                Description = "Quality of Service level for Last Will message",
                FieldType = FieldType.Select,
                Group = "Last Will",
                Order = 3,
                DefaultValue = 1,
                Options = new List<SelectOption>
                {
                    new SelectOption { Value = "0", Text = "QoS 0 - At most once" },
                    new SelectOption { Value = "1", Text = "QoS 1 - At least once" },
                    new SelectOption { Value = "2", Text = "QoS 2 - Exactly once" }
                }
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.LastWillRetain),
                DisplayName = "Last Will Retain",
                Description = "Retain the Last Will message on the broker",
                FieldType = FieldType.Boolean,
                Group = "Last Will",
                Order = 4,
                DefaultValue = true
            },
            new ConfigurationField
            {
                PropertyName = nameof(MqttDataIngestionConfiguration.LastWillDelayInterval),
                DisplayName = "Last Will Delay (seconds)",
                Description = "Delay before sending Last Will message",
                FieldType = FieldType.Number,
                Group = "Last Will",
                Order = 5,
                DefaultValue = 0,
                ValidationAttributes = new Dictionary<string, object> { { "min", 0 }, { "max", 3600 } }
            }
        };
    }

    public IDataIngestionConfiguration CreateDefaultConfiguration()
    {
        return new MqttDataIngestionConfiguration
        {
            Name = "New MQTT Connection",
            Description = "MQTT broker connection for data ingestion",
            BrokerHost = "localhost",
            BrokerPort = 1883,
            ClientId = $"UNSInfra-{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}"
        };
    }

    public bool CanHandle(IDataIngestionConfiguration configuration)
    {
        return configuration is MqttDataIngestionConfiguration || 
               configuration.ServiceType.Equals("MQTT", StringComparison.OrdinalIgnoreCase);
    }

    public IDataIngestionService CreateService(IDataIngestionConfiguration configuration, IServiceProvider serviceProvider)
    {
        if (configuration is not MqttDataIngestionConfiguration mqttConfig)
            throw new ArgumentException($"Expected {typeof(MqttDataIngestionConfiguration).Name}, got {configuration.GetType().Name}");

        // Convert to legacy configuration for the existing service
        var legacyConfig = mqttConfig.ToLegacyConfiguration();
        
        // Create required dependencies
        var configOptions = Microsoft.Extensions.Options.Options.Create(legacyConfig);
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MqttDataService>>();
        var topicDiscoveryService = serviceProvider.GetRequiredService<UNSInfra.Services.TopicDiscovery.ITopicDiscoveryService>();
        var sparkplugBDecoder = serviceProvider.GetRequiredService<UNSInfra.Services.V1.SparkplugB.SparkplugBDecoder>();
        
        // Create MQTT service with correct constructor signature
        var service = new MqttDataService(configOptions, topicDiscoveryService, sparkplugBDecoder, logger);
        
        return service;
    }

    public List<string> ValidateConfiguration(IDataIngestionConfiguration configuration)
    {
        if (configuration is not MqttDataIngestionConfiguration mqttConfig)
            return new List<string> { "Configuration must be of type MqttDataIngestionConfiguration" };

        return mqttConfig.Validate();
    }
}