using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Namespace;
using UNSInfra.Core.Repositories;
using UNSInfra.Repositories;
using UNSInfra.Services;

namespace UNSInfra.Services.V1.Mqtt;

/// <summary>
/// Service for exporting UNS model information to MQTT
/// </summary>
public class MqttModelExportService : IDisposable
{
    private readonly ILogger<MqttModelExportService> _logger;
    private readonly IInputOutputConfigurationRepository _configRepository;
    private readonly INamespaceStructureService _namespaceStructureService;
    private readonly INamespaceConfigurationRepository _namespaceConfigurationRepository;
    private IManagedMqttClient? _mqttClient;
    private readonly Dictionary<string, Timer> _republishTimers = new();
    private bool _isRunning;
    private bool _disposed;

    public MqttModelExportService(
        ILogger<MqttModelExportService> logger,
        IInputOutputConfigurationRepository configRepository,
        INamespaceStructureService namespaceStructureService,
        INamespaceConfigurationRepository namespaceConfigurationRepository)
    {
        _logger = logger;
        _configRepository = configRepository;
        _namespaceStructureService = namespaceStructureService;
        _namespaceConfigurationRepository = namespaceConfigurationRepository;
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("MQTT model export service is already running");
            return true;
        }

        try
        {
            _logger.LogInformation("Starting MQTT model export service");

            // Load enabled MQTT output configurations for model export
            var configurations = await _configRepository.GetMqttOutputConfigurationsAsync(enabledOnly: true);
            var modelConfigs = configurations
                .Where(c => c.OutputType == MqttOutputType.Model || c.OutputType == MqttOutputType.Both)
                .ToList();

            if (!modelConfigs.Any())
            {
                _logger.LogWarning("No enabled MQTT model export configurations found");
                return false;
            }

            _logger.LogInformation("Found {Count} enabled MQTT model export configurations", modelConfigs.Count);

            // Create MQTT client
            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("localhost", 1883) // Should be configurable
                .WithClientId($"UNSModelExporter_{Guid.NewGuid():N}")
                .WithCleanSession()
                .Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions)
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            await _mqttClient.StartAsync(managedOptions);

            // Setup timers for each configuration
            foreach (var config in modelConfigs)
            {
                await SetupModelExportConfiguration(config);
            }

            _isRunning = true;
            _logger.LogInformation("MQTT model export service started successfully with {Count} configurations", 
                modelConfigs.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT model export service");
            return false;
        }
    }

    private async Task SetupModelExportConfiguration(MqttOutputConfiguration config)
    {
        if (config.ModelExportConfig == null)
        {
            _logger.LogWarning("Model export configuration is null for config '{ConfigName}'", config.Name);
            return;
        }

        try
        {
            _logger.LogInformation("Setting up model export for configuration '{ConfigName}' with {IntervalMinutes} minute intervals",
                config.Name, config.ModelExportConfig.RepublishIntervalMinutes);

            // Publish immediately on startup
            await PublishModelData(config);

            // Setup timer for periodic republishing
            var interval = TimeSpan.FromMinutes(config.ModelExportConfig.RepublishIntervalMinutes);
            var timer = new Timer(async _ =>
            {
                try
                {
                    await PublishModelData(config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in periodic model publish for configuration '{ConfigName}'", config.Name);
                }
            }, null, interval, interval);

            _republishTimers[config.Id] = timer;

            _logger.LogInformation("Successfully setup model export for configuration '{ConfigName}'", config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup model export for configuration '{ConfigName}'", config.Name);
        }
    }

    private async Task PublishModelData(MqttOutputConfiguration config)
    {
        try
        {
            _logger.LogDebug("Publishing model data for configuration '{ConfigName}'", config.Name);

            var modelConfig = config.ModelExportConfig!;
            var unsTree = await _namespaceStructureService.GetNamespaceStructureAsync();
            var namespaceConfigs = await _namespaceConfigurationRepository.GetAllNamespaceConfigurationsAsync();

            var publishedCount = 0;

            foreach (var node in unsTree)
            {
                // Apply namespace filter if configured
                if (modelConfig.NamespaceFilter.Any() && 
                    !modelConfig.NamespaceFilter.Contains(node.Namespace?.Name ?? "", StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Apply hierarchy level filter if configured  
                if (modelConfig.HierarchyLevelFilter.Any() &&
                    !modelConfig.HierarchyLevelFilter.Contains(node.HierarchyNode?.Name ?? "", StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                await PublishNodeModel(config, node, namespaceConfigs);
                publishedCount++;
            }

            _logger.LogInformation("Published model data for {Count} nodes using configuration '{ConfigName}'",
                publishedCount, config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing model data for configuration '{ConfigName}'", config.Name);
        }
    }

    private async Task PublishNodeModel(MqttOutputConfiguration config, NSTreeNode node, IEnumerable<NamespaceConfiguration> namespaceConfigs)
    {
        try
        {
            var modelConfig = config.ModelExportConfig!;
            
            // Build topic: Enterprise1/Site/_model
            var topicParts = new List<string>();
            
            if (!string.IsNullOrEmpty(config.TopicPrefix))
            {
                topicParts.Add(config.TopicPrefix);
            }

            topicParts.Add(node.FullPath);
            topicParts.Add(modelConfig.ModelAttributeName);
            
            var topic = string.Join("/", topicParts.Where(p => !string.IsNullOrEmpty(p)));

            // Build model payload
            var modelPayload = new Dictionary<string, object>();

            // Add type information
            if (node.HierarchyNode != null)
            {
                modelPayload["Type"] = node.HierarchyNode.Name;
            }
            else if (node.Namespace != null)
            {
                modelPayload["Type"] = node.Namespace.Name;
            }

            // Add description if enabled and available
            if (modelConfig.IncludeDescription)
            {
                var description = GetNodeDescription(node, namespaceConfigs);
                if (!string.IsNullOrEmpty(description))
                {
                    modelPayload["Description"] = description;
                }
            }

            // Add metadata if enabled
            if (modelConfig.IncludeMetadata)
            {
                var metadata = new Dictionary<string, object>
                {
                    ["NodeType"] = node.NodeType.ToString(),
                    ["CanHaveHierarchyChildren"] = node.CanHaveHierarchyChildren,
                    ["CanHaveNamespaceChildren"] = node.CanHaveNamespaceChildren,
                    ["FullPath"] = node.FullPath,
                    ["PublishedAt"] = DateTime.UtcNow
                };

                if (node.HierarchyNode != null)
                {
                    metadata["HierarchyNodeId"] = node.HierarchyNode.Id;
                    metadata["Order"] = node.HierarchyNode.Order;
                }

                if (node.Namespace != null)
                {
                    metadata["NamespaceId"] = node.Namespace.Id;
                }

                modelPayload["Metadata"] = metadata;
            }

            // Add children information if enabled
            if (modelConfig.IncludeChildren && node.Children.Any())
            {
                modelPayload["Children"] = node.Children.Select(child => new
                {
                    Name = child.Name,
                    Type = child.HierarchyNode?.Name ?? child.Namespace?.Name,
                    NodeType = child.NodeType.ToString(),
                    FullPath = child.FullPath
                }).ToArray();
            }

            // Add custom fields
            foreach (var customField in modelConfig.CustomFields)
            {
                modelPayload[customField.Key] = customField.Value;
            }

            // Serialize and publish
            var jsonPayload = JsonSerializer.Serialize(modelPayload, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = null
            });

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(jsonPayload)
                .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)config.QoS)
                .WithRetainFlag(config.Retain)
                .Build();

            if (_mqttClient != null)
            {
                await _mqttClient.EnqueueAsync(message);
                
                _logger.LogDebug("Published model for '{NodePath}' to topic '{Topic}'", 
                    node.FullPath, topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing model for node '{NodePath}'", node.FullPath);
        }
    }

    private string? GetNodeDescription(NSTreeNode node, IEnumerable<NamespaceConfiguration> namespaceConfigs)
    {
        // Try to get description from hierarchy node first
        if (!string.IsNullOrEmpty(node.HierarchyNode?.Description))
        {
            return node.HierarchyNode.Description;
        }

        // Then try namespace configuration
        if (node.Namespace != null)
        {
            var nsConfig = namespaceConfigs.FirstOrDefault(nc => nc.Id == node.Namespace.Id);
            if (!string.IsNullOrEmpty(nsConfig?.Description))
            {
                return nsConfig.Description;
            }
        }

        return null;
    }

    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return true;
        }

        try
        {
            _logger.LogInformation("Stopping MQTT model export service");

            // Stop all timers
            foreach (var timer in _republishTimers.Values)
            {
                timer?.Dispose();
            }
            _republishTimers.Clear();

            // Stop MQTT client
            if (_mqttClient != null)
            {
                await _mqttClient.StopAsync();
                _mqttClient.Dispose();
                _mqttClient = null;
            }

            _isRunning = false;
            _logger.LogInformation("MQTT model export service stopped successfully");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT model export service");
            return false;
        }
    }

    public Task<bool> IsRunningAsync() => Task.FromResult(_isRunning);

    public Task<Dictionary<string, object>> GetStatusAsync()
    {
        var status = new Dictionary<string, object>
        {
            ["IsRunning"] = _isRunning,
            ["MqttConnected"] = _mqttClient?.IsConnected ?? false,
            ["ActiveTimers"] = _republishTimers.Count,
            ["Configurations"] = _republishTimers.Count
        };

        return Task.FromResult(status);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopAsync().Wait(TimeSpan.FromSeconds(10));
            _disposed = true;
        }
    }
}