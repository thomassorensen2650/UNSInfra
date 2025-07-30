using UNSInfra.Core.Configuration;
using UNSInfra.Services.SocketIO.Configuration;
using UNSInfra.Services.DataIngestion.Mock;
using Microsoft.Extensions.DependencyInjection;

namespace UNSInfra.Services.SocketIO.Descriptors;

/// <summary>
/// Service descriptor for SocketIO data ingestion services.
/// Provides metadata and configuration field definitions for the UI.
/// </summary>
public class SocketIOServiceDescriptor : IDataIngestionServiceDescriptor
{
    public string ServiceType => "SocketIO";
    public string DisplayName => "Socket.IO Server";
    public string Description => "Connect to Socket.IO servers for real-time data streaming and communication.";
    public string? IconClass => "bi bi-broadcast";
    public string? Category => "Real-time Communication";
    public int Order => 2;
    public Type ConfigurationType => typeof(SocketIODataIngestionConfiguration);
    public Type ServiceImplementationType => typeof(SocketIODataService);

    public List<ConfigurationField> GetConfigurationFields()
    {
        return new List<ConfigurationField>
        {
            // Connection Group
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ServerUrl),
                DisplayName = "Server URL",
                Description = "Socket.IO server URL (including protocol and port)",
                FieldType = FieldType.Url,
                Required = true,
                Group = "Connection",
                Order = 1,
                DefaultValue = "https://localhost:3000"
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ConnectionTimeoutSeconds),
                DisplayName = "Connection Timeout (seconds)",
                Description = "Timeout for connection attempts",
                FieldType = FieldType.Number,
                Group = "Connection",
                Order = 2,
                DefaultValue = 10,
                ValidationAttributes = new Dictionary<string, object> { { "min", 1 }, { "max", 300 } }
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.Namespace),
                DisplayName = "Namespace",
                Description = "Socket.IO namespace to connect to (optional)",
                FieldType = FieldType.Text,
                Group = "Connection",
                Order = 3
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.AutoConnect),
                DisplayName = "Auto Connect",
                Description = "Automatically connect when service starts",
                FieldType = FieldType.Boolean,
                Group = "Connection",
                Order = 4,
                DefaultValue = true
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ForceNew),
                DisplayName = "Force New Connection",
                Description = "Force a new connection instead of reusing existing",
                FieldType = FieldType.Boolean,
                Group = "Connection",
                Order = 5,
                DefaultValue = false
            },

            // Reconnection Group
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.EnableReconnection),
                DisplayName = "Enable Reconnection",
                Description = "Automatically reconnect on connection loss",
                FieldType = FieldType.Boolean,
                Group = "Reconnection",
                Order = 1,
                DefaultValue = true
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ReconnectionAttempts),
                DisplayName = "Reconnection Attempts",
                Description = "Maximum number of reconnection attempts",
                FieldType = FieldType.Number,
                Group = "Reconnection",
                Order = 2,
                DefaultValue = 5,
                ValidationAttributes = new Dictionary<string, object> { { "min", 0 }, { "max", 100 } }
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.ReconnectionDelaySeconds),
                DisplayName = "Reconnection Delay (seconds)",
                Description = "Delay between reconnection attempts",
                FieldType = FieldType.Number,
                Group = "Reconnection",
                Order = 3,
                DefaultValue = 2,
                ValidationAttributes = new Dictionary<string, object> { { "min", 1 }, { "max", 60 } }
            },

            // Events Group
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.EventNames),
                DisplayName = "Event Names",
                Description = "Comma-separated list of event names to listen for (leave empty for all events)",
                FieldType = FieldType.TextArea,
                Group = "Events",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.BaseTopicPath),
                DisplayName = "Base Topic Path",
                Description = "Base path prefix for all topics from this Socket.IO connection",
                FieldType = FieldType.Text,
                Required = true,
                Group = "Events",
                Order = 2,
                DefaultValue = "socketio"
            },

            // Performance Group
            new ConfigurationField
            {
                PropertyName = nameof(SocketIODataIngestionConfiguration.MessageBufferSize),
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
                PropertyName = nameof(SocketIODataIngestionConfiguration.EnableDetailedLogging),
                DisplayName = "Enable Detailed Logging",
                Description = "Enable verbose logging for debugging",
                FieldType = FieldType.Boolean,
                Group = "Performance",
                Order = 2,
                DefaultValue = false
            },

            // Authentication Group
            new ConfigurationField
            {
                PropertyName = "AuthToken",
                DisplayName = "Authentication Token",
                Description = "Bearer token for authentication (stored in AuthenticationData)",
                FieldType = FieldType.Password,
                Group = "Authentication",
                Order = 1
            },
            new ConfigurationField
            {
                PropertyName = "AuthUsername",
                DisplayName = "Username",
                Description = "Username for authentication (stored in AuthenticationData)",
                FieldType = FieldType.Text,
                Group = "Authentication",
                Order = 2
            },
            new ConfigurationField
            {
                PropertyName = "AuthPassword",
                DisplayName = "Password",
                Description = "Password for authentication (stored in AuthenticationData)",
                FieldType = FieldType.Password,
                Group = "Authentication",
                Order = 3
            },

            // Headers Group
            new ConfigurationField
            {
                PropertyName = "CustomHeaders",
                DisplayName = "Custom Headers",
                Description = "Custom HTTP headers (JSON format: {\"key\":\"value\"})",
                FieldType = FieldType.TextArea,
                Group = "Headers",
                Order = 1
            }
        };
    }

    public IDataIngestionConfiguration CreateDefaultConfiguration()
    {
        return new SocketIODataIngestionConfiguration
        {
            Name = "New Socket.IO Connection",
            Description = "Socket.IO server connection for real-time data streaming",
            ServerUrl = "https://localhost:3000",
            BaseTopicPath = "socketio",
            EventNames = Array.Empty<string>()
        };
    }

    public bool CanHandle(IDataIngestionConfiguration configuration)
    {
        return configuration is SocketIODataIngestionConfiguration || 
               configuration.ServiceType.Equals("SocketIO", StringComparison.OrdinalIgnoreCase);
    }

    public IDataIngestionService CreateService(IDataIngestionConfiguration configuration, IServiceProvider serviceProvider)
    {
        if (configuration is not SocketIODataIngestionConfiguration socketIOConfig)
            throw new ArgumentException($"Expected {typeof(SocketIODataIngestionConfiguration).Name}, got {configuration.GetType().Name}");

        // Convert to legacy configuration for the existing service
        var legacyConfig = socketIOConfig.ToLegacyConfiguration();
        
        // Create required dependencies
        var configOptions = Microsoft.Extensions.Options.Options.Create(legacyConfig);
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SocketIODataService>>();
        
        // Create SocketIO service with service provider for scoped dependencies
        var service = new SocketIODataService(logger, serviceProvider, configOptions);
        
        return service;
    }

    public List<string> ValidateConfiguration(IDataIngestionConfiguration configuration)
    {
        if (configuration is not SocketIODataIngestionConfiguration socketIOConfig)
            return new List<string> { "Configuration must be of type SocketIODataIngestionConfiguration" };

        return socketIOConfig.Validate();
    }
}