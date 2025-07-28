using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UNSInfra.Core.Configuration;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Services.SocketIO.Configuration;

namespace UNSInfra.Services.SocketIO;

/// <summary>
/// Service descriptor for SocketIO data ingestion services.
/// Provides metadata and factory methods for dynamic service management.
/// </summary>
public class SocketIOServiceDescriptor : IDataIngestionServiceDescriptor
{
    public string ServiceType => "SocketIO";

    public string DisplayName => "Socket.IO Server";

    public string Description => "Connect to Socket.IO servers for real-time JSON data ingestion and processing";

    public string? IconClass => "fas fa-plug";

    public Type ConfigurationType => typeof(SocketIODataIngestionConfiguration);

    public Type ServiceImplementationType => typeof(SocketIODataService);

    public IDataIngestionConfiguration CreateDefaultConfiguration()
    {
        return new SocketIODataIngestionConfiguration
        {
            Name = "New Socket.IO Connection",
            Description = "Socket.IO server connection",
            ServerUrl = "https://localhost:3000",
            ConnectionTimeoutSeconds = 10,
            EnableReconnection = true,
            ReconnectionAttempts = 5,
            ReconnectionDelaySeconds = 2,
            EventNames = new[] { "update", "data" },
            BaseTopicPath = "socketio",
            EnableDetailedLogging = false,
            AutoConnect = true,
            MessageBufferSize = 1000
        };
    }

    public IDataIngestionService CreateService(IDataIngestionConfiguration configuration, IServiceProvider serviceProvider)
    {
        if (configuration is not SocketIODataIngestionConfiguration socketConfig)
        {
            throw new ArgumentException($"Configuration must be of type {typeof(SocketIODataIngestionConfiguration).Name}", nameof(configuration));
        }

        // Create the legacy configuration
        var legacyConfig = socketConfig.ToLegacyConfiguration();

        // Get required services
        var logger = serviceProvider.GetRequiredService<ILogger<SocketIODataService>>();
        var topicDiscovery = serviceProvider.GetRequiredService<ITopicDiscoveryService>();

        // Create an options wrapper for the configuration
        var options = Options.Create(legacyConfig);

        // Create the service instance with the correct constructor order
        return new SocketIODataService(logger, topicDiscovery, options);
    }

    public List<ConfigurationField> GetConfigurationFields()
    {
        return new List<ConfigurationField>
        {
            // Connection Settings
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ServerUrl),
                DisplayName = "Server URL",
                Description = "Full URL of the Socket.IO server (e.g., https://example.com:3000)",
                FieldType = FieldType.Url,
                Required = true,
                Group = "Connection",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ConnectionTimeoutSeconds),
                DisplayName = "Connection Timeout (seconds)",
                Description = "Maximum time to wait for initial connection",
                FieldType = FieldType.Number,
                DefaultValue = 10,
                ValidationAttributes = new Dictionary<string, object> { { "min", 1 }, { "max", 300 } },
                Group = "Connection",
                Order = 2
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.Namespace),
                DisplayName = "Namespace",
                Description = "Socket.IO namespace to connect to (optional, e.g., '/admin')",
                FieldType = FieldType.Text,
                Group = "Connection",
                Order = 3
            },

            // Reconnection Settings
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.EnableReconnection),
                DisplayName = "Enable Reconnection",
                Description = "Automatically reconnect when connection is lost",
                FieldType = FieldType.Boolean,
                DefaultValue = true,
                Group = "Reconnection",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ReconnectionAttempts),
                DisplayName = "Reconnection Attempts",
                Description = "Maximum number of reconnection attempts (0 = unlimited)",
                FieldType = FieldType.Number,
                DefaultValue = 5,
                ValidationAttributes = new Dictionary<string, object> { { "min", 0 }, { "max", 100 } },
                Group = "Reconnection",
                Order = 2
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ReconnectionDelaySeconds),
                DisplayName = "Reconnection Delay (seconds)",
                Description = "Delay between reconnection attempts",
                FieldType = FieldType.Number,
                DefaultValue = 2,
                ValidationAttributes = new Dictionary<string, object> { { "min", 1 }, { "max", 60 } },
                Group = "Reconnection",
                Order = 3
            },

            // Data Processing
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.BaseTopicPath),
                DisplayName = "Base Topic Path",
                Description = "Base path for all topics created from this connection",
                FieldType = FieldType.Text,
                Required = true,
                DefaultValue = "socketio",
                Group = "Data Processing",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.EventNames),
                DisplayName = "Event Names",
                Description = "Comma-separated list of Socket.IO events to listen for (empty = listen to all)",
                FieldType = FieldType.Text,
                Group = "Data Processing",
                Order = 2
            },

            // Advanced Options
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.AutoConnect),
                DisplayName = "Auto Connect",
                Description = "Automatically connect when service starts",
                FieldType = FieldType.Boolean,
                DefaultValue = true,
                Group = "Advanced",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ForceNew),
                DisplayName = "Force New Connection",
                Description = "Force a new connection instead of reusing existing ones",
                FieldType = FieldType.Boolean,
                DefaultValue = false,
                Group = "Advanced",
                Order = 2
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.MessageBufferSize),
                DisplayName = "Message Buffer Size",
                Description = "Maximum number of messages to buffer",
                FieldType = FieldType.Number,
                DefaultValue = 1000,
                ValidationAttributes = new Dictionary<string, object> { { "min", 100 }, { "max", 10000 } },
                Group = "Advanced",
                Order = 3
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.EnableDetailedLogging),
                DisplayName = "Enable Detailed Logging",
                Description = "Enable verbose logging for debugging",
                FieldType = FieldType.Boolean,
                DefaultValue = false,
                Group = "Advanced",
                Order = 4
            }
        };
    }

    public List<string> ValidateConfiguration(IDataIngestionConfiguration configuration)
    {
        if (configuration is not SocketIODataIngestionConfiguration socketConfig)
        {
            return new List<string> { "Configuration must be of type SocketIODataIngestionConfiguration" };
        }

        return socketConfig.Validate();
    }
}