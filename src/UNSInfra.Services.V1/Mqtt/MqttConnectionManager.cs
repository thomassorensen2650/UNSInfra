using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Services.V1.Configuration;

namespace UNSInfra.Services.V1.Mqtt;

/// <summary>
/// Manages MQTT data ingestion services by connection configuration.
/// Ensures that each MQTT broker connection has only one active service instance
/// that can be shared by multiple input and output configurations.
/// </summary>
public class MqttConnectionManager : IDisposable
{
    private readonly ILogger<MqttConnectionManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataIngestionConfigurationRepository _configRepository;
    
    private readonly Dictionary<string, MqttDataIngestionService> _activeConnections = new();
    private readonly Dictionary<string, HashSet<string>> _connectionUsage = new();
    private readonly object _lockObject = new();
    private bool _disposed;

    public MqttConnectionManager(
        ILogger<MqttConnectionManager> logger,
        IServiceProvider serviceProvider,
        IDataIngestionConfigurationRepository configRepository)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configRepository = configRepository;
    }

    /// <summary>
    /// Gets or creates an MQTT data ingestion service for the specified connection configuration.
    /// Multiple services can share the same connection by using the same connection ID.
    /// </summary>
    /// <param name="connectionId">The ID of the MQTT connection configuration</param>
    /// <param name="consumerId">Unique ID of the consumer (input/output service) requesting the connection</param>
    /// <returns>The MQTT data ingestion service, or null if connection configuration not found</returns>
    public async Task<IMqttDataIngestionService?> GetOrCreateConnectionAsync(string connectionId, string consumerId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            _logger.LogWarning("Cannot get MQTT connection for empty connection ID");
            return null;
        }

        lock (_lockObject)
        {
            // Check if we already have an active connection
            if (_activeConnections.TryGetValue(connectionId, out var existingService))
            {
                // Add consumer to usage tracking
                if (!_connectionUsage.TryGetValue(connectionId, out var consumers))
                {
                    consumers = new HashSet<string>();
                    _connectionUsage[connectionId] = consumers;
                }
                consumers.Add(consumerId);

                _logger.LogInformation("Reusing existing MQTT connection '{ConnectionId}' for consumer '{ConsumerId}'",
                    connectionId, consumerId);
                return existingService;
            }
        }

        try
        {
            // Get the connection configuration
            var connectionConfig = await _configRepository.GetConfigurationAsync(connectionId);
            if (connectionConfig is not MqttDataIngestionConfiguration mqttConfig)
            {
                _logger.LogError("MQTT connection configuration '{ConnectionId}' not found or invalid", connectionId);
                return null;
            }

            _logger.LogInformation("Creating new MQTT connection '{ConnectionId}' for consumer '{ConsumerId}'",
                connectionId, consumerId);

            // Create the service with scoped dependencies
            using var scope = _serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<MqttDataIngestionService>>();
            var topicDiscovery = scope.ServiceProvider.GetService<ITopicDiscoveryService>();

            var service = new MqttDataIngestionService(logger, mqttConfig, topicDiscovery);

            // Start the service
            await service.StartAsync();

            lock (_lockObject)
            {
                // Double-check that another thread didn't create the service while we were waiting
                if (_activeConnections.TryGetValue(connectionId, out var existingService2))
                {
                    // Dispose our service and return the existing one
                    service.Dispose();
                    
                    if (!_connectionUsage.TryGetValue(connectionId, out var consumers))
                    {
                        consumers = new HashSet<string>();
                        _connectionUsage[connectionId] = consumers;
                    }
                    consumers.Add(consumerId);
                    
                    return existingService2;
                }

                // Store the new service
                _activeConnections[connectionId] = service;
                _connectionUsage[connectionId] = new HashSet<string> { consumerId };
            }

            _logger.LogInformation("Successfully created and started MQTT connection '{ConnectionId}' for consumer '{ConsumerId}'",
                connectionId, consumerId);
            
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MQTT connection '{ConnectionId}' for consumer '{ConsumerId}'",
                connectionId, consumerId);
            return null;
        }
    }

    /// <summary>
    /// Releases the MQTT connection for the specified consumer.
    /// If no other consumers are using the connection, it will be stopped and disposed.
    /// </summary>
    /// <param name="connectionId">The ID of the MQTT connection configuration</param>
    /// <param name="consumerId">Unique ID of the consumer releasing the connection</param>
    /// <returns>True if released successfully</returns>
    public async Task<bool> ReleaseConnectionAsync(string connectionId, string consumerId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return true; // Nothing to release
        }

        try
        {
            MqttDataIngestionService? serviceToDispose = null;

            lock (_lockObject)
            {
                // Remove consumer from usage tracking
                if (_connectionUsage.TryGetValue(connectionId, out var consumers))
                {
                    consumers.Remove(consumerId);

                    // If no more consumers, prepare to dispose the service
                    if (consumers.Count == 0)
                    {
                        _connectionUsage.Remove(connectionId);
                        if (_activeConnections.TryGetValue(connectionId, out var service))
                        {
                            _activeConnections.Remove(connectionId);
                            serviceToDispose = service;
                        }
                    }
                }
            }

            // Dispose service outside of lock to avoid deadlocks
            if (serviceToDispose != null)
            {
                _logger.LogInformation("Stopping MQTT connection '{ConnectionId}' - no more consumers", connectionId);
                
                await serviceToDispose.StopAsync();
                serviceToDispose.Dispose();
                
                _logger.LogInformation("Successfully stopped MQTT connection '{ConnectionId}'", connectionId);
            }
            else
            {
                _logger.LogDebug("Released MQTT connection '{ConnectionId}' for consumer '{ConsumerId}' - {RemainingConsumers} consumers remaining",
                    connectionId, consumerId, _connectionUsage.GetValueOrDefault(connectionId)?.Count ?? 0);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release MQTT connection '{ConnectionId}' for consumer '{ConsumerId}'",
                connectionId, consumerId);
            return false;
        }
    }

    /// <summary>
    /// Gets information about all active MQTT connections.
    /// </summary>
    /// <returns>Dictionary of connection ID to connection info</returns>
    public Dictionary<string, object> GetConnectionStatus()
    {
        lock (_lockObject)
        {
            var status = new Dictionary<string, object>();

            foreach (var (connectionId, service) in _activeConnections)
            {
                var consumers = _connectionUsage.GetValueOrDefault(connectionId)?.ToList() ?? new List<string>();
                
                status[connectionId] = new
                {
                    ConnectionId = connectionId,
                    IsConnected = service.IsConnectedAsync().Result,
                    ConsumerCount = consumers.Count,
                    Consumers = consumers
                };
            }

            return status;
        }
    }

    /// <summary>
    /// Stops and disposes all active MQTT connections.
    /// </summary>
    public async Task StopAllConnectionsAsync()
    {
        List<MqttDataIngestionService> servicesToDispose;

        lock (_lockObject)
        {
            servicesToDispose = _activeConnections.Values.ToList();
            _activeConnections.Clear();
            _connectionUsage.Clear();
        }

        _logger.LogInformation("Stopping {Count} MQTT connections", servicesToDispose.Count);

        var stopTasks = servicesToDispose.Select(async service =>
        {
            try
            {
                await service.StopAsync();
                service.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping MQTT connection");
            }
        });

        await Task.WhenAll(stopTasks);
        
        _logger.LogInformation("All MQTT connections stopped");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                StopAllConnectionsAsync().Wait(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MQTT connection manager");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}