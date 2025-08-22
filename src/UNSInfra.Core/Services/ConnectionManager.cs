using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Abstractions;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Core.Repositories;
using UNSInfra.Models;
using UNSInfra.Models.Data;
using UNSInfra.Services.Events;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Configuration;

namespace UNSInfra.Services;

/// <summary>
/// Manages connection instances and their lifecycle
/// </summary>
public class ConnectionManager : BackgroundService, IConnectionManager
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConnectionManager> _logger;
    private readonly IEventBus _eventBus;
    private readonly Dictionary<string, IDataConnection> _activeConnections = new();
    private readonly Dictionary<string, ConnectionConfiguration> _connectionConfigurations = new();
    private readonly HashSet<string> _knownTopics = new();
    private readonly object _lockObject = new();

    public ConnectionManager(
        IConnectionRegistry connectionRegistry,
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<ConnectionManager> logger,
        IEventBus eventBus)
    {
        _connectionRegistry = connectionRegistry ?? throw new ArgumentNullException(nameof(connectionRegistry));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <summary>
    /// Event raised when data is received from any connection
    /// </summary>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event raised when a connection status changes
    /// </summary>
    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    /// <inheritdoc />
    public async Task<bool> CreateConnectionAsync(ConnectionConfiguration config, CancellationToken cancellationToken = default)
    {
        return await CreateConnectionAsync(config, cancellationToken, saveToRepository: true);
    }

    /// <summary>
    /// Creates a connection instance with option to control database persistence
    /// </summary>
    private async Task<bool> CreateConnectionAsync(ConnectionConfiguration config, CancellationToken cancellationToken, bool saveToRepository)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Inputs/outputs are now part of connection configuration and already loaded
        _logger.LogDebug("Connection {ConnectionId} has {InputCount} inputs and {OutputCount} outputs",
            config.Id, config.Inputs.Count, config.Outputs.Count);

        try
        {
            var descriptor = _connectionRegistry.GetDescriptor(config.ConnectionType);
            if (descriptor == null)
            {
                _logger.LogError("Unknown connection type: {ConnectionType}", config.ConnectionType);
                return false;
            }

            // Convert the generic ConnectionConfig to the strongly-typed configuration
            var typedConfig = ConvertToTypedConfiguration(config.ConnectionConfig, descriptor);
            if (typedConfig == null)
            {
                _logger.LogError("Failed to convert connection configuration for {ConnectionId} to type {ConnectionType}", 
                    config.Id, config.ConnectionType);
                return false;
            }

            // Validate configuration
            var connection = descriptor.CreateConnection(config.Id, config.Name, _serviceProvider);
            var validationResult = connection.ValidateConfiguration(typedConfig);
            if (!validationResult.IsValid)
            {
                _logger.LogError("Invalid connection configuration for {ConnectionId}: {Errors}", 
                    config.Id, string.Join(", ", validationResult.Errors));
                connection.Dispose();
                return false;
            }

            // Initialize connection
            var initialized = await connection.InitializeAsync(typedConfig, cancellationToken);
            if (!initialized)
            {
                _logger.LogError("Failed to initialize connection {ConnectionId}", config.Id);
                connection.Dispose();
                return false;
            }

            // Subscribe to events
            connection.DataReceived += OnConnectionDataReceived;
            connection.StatusChanged += OnConnectionStatusChanged;

            // Configure inputs and outputs
            _logger.LogInformation("Configuring connection {ConnectionId}: {InputCount} inputs, {OutputCount} outputs", 
                config.Id, config.Inputs.Count, config.Outputs.Count);
            
            foreach (var input in config.Inputs)
            {
                _logger.LogInformation("Configuring input for connection {ConnectionId}: {InputType}", 
                    config.Id, input.GetType().Name);
                await connection.ConfigureInputAsync(input, cancellationToken);
            }

            foreach (var output in config.Outputs)
            {
                _logger.LogInformation("Configuring output for connection {ConnectionId}: {OutputType}", 
                    config.Id, output.GetType().Name);
                await connection.ConfigureOutputAsync(output, cancellationToken);
            }

            lock (_lockObject)
            {
                _activeConnections[config.Id] = connection;
                _connectionConfigurations[config.Id] = config;
                _logger.LogInformation("Added connection {ConnectionId} to active connections and configurations. Active count: {ActiveCount}, Config count: {ConfigCount}", 
                    config.Id, _activeConnections.Count, _connectionConfigurations.Count);
            }

            // Save to repository for persistence (only if requested)
            if (saveToRepository)
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IConnectionConfigurationRepository>();
                _logger.LogInformation("Using repository type: {RepositoryType}", repository.GetType().FullName);
                await repository.SaveConnectionAsync(config);
                _logger.LogInformation("Saved connection {ConnectionId} ({Name}) to repository", config.Id, config.Name);
                
                // Verify the save by reading it back
                var savedConnection = await repository.GetConnectionByIdAsync(config.Id);
                if (savedConnection != null)
                {
                    _logger.LogInformation("Verified: Connection {ConnectionId} successfully saved and can be retrieved", config.Id);
                }
                else
                {
                    _logger.LogError("Failed to verify save: Connection {ConnectionId} not found in repository after save", config.Id);
                }
            }
            else
            {
                _logger.LogDebug("Skipping repository save for connection {ConnectionId} (saveToRepository=false)", config.Id);
            }

            _logger.LogInformation("Created connection {ConnectionId} of type {ConnectionType}", 
                config.Id, config.ConnectionType);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connection {ConnectionId}", config.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> StartConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
            throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

        _logger.LogInformation("StartConnectionAsync called for {ConnectionId}", connectionId);

        IDataConnection? connection;
        ConnectionConfiguration? configToCreate = null;
        
        lock (_lockObject)
        {
            _logger.LogDebug("Checking if connection {ConnectionId} is already active. Active count: {ActiveCount}", 
                connectionId, _activeConnections.Count);
            
            _logger.LogDebug("Available active connections: {ActiveConnections}", 
                string.Join(", ", _activeConnections.Keys));
            _logger.LogDebug("Available configurations: {Configurations}", 
                string.Join(", ", _connectionConfigurations.Keys));
            
            // First check if connection is already active
            if (_activeConnections.TryGetValue(connectionId, out connection))
            {
                _logger.LogInformation("Connection {ConnectionId} is already active with status {Status}", 
                    connectionId, connection.Status);
            }
            else
            {
                _logger.LogDebug("Connection {ConnectionId} not active, checking configurations. Config count: {ConfigCount}", 
                    connectionId, _connectionConfigurations.Count);
                
                // Connection not active, check if we have its configuration
                if (_connectionConfigurations.TryGetValue(connectionId, out configToCreate))
                {
                    _logger.LogInformation("Found stored configuration for connection {ConnectionId}, will create it", connectionId);
                }
                else
                {
                    _logger.LogWarning("Connection {ConnectionId} not found in active connections or configurations", connectionId);
                    _logger.LogWarning("Searched for connection ID: '{ConnectionId}' (length: {Length})", 
                        connectionId, connectionId.Length);
                    return false;
                }
            }
        }
        
        // If we need to create the connection, do it outside the lock
        if (connection == null && configToCreate != null)
        {
            _logger.LogInformation("Creating connection {ConnectionId} from stored configuration", connectionId);
            var success = await CreateConnectionAsync(configToCreate, cancellationToken, saveToRepository: false);
            if (!success)
            {
                _logger.LogError("Failed to create connection {ConnectionId} from configuration", connectionId);
                return false;
            }
            
            // Get the newly created connection
            lock (_lockObject)
            {
                if (!_activeConnections.TryGetValue(connectionId, out connection))
                {
                    _logger.LogError("Connection {ConnectionId} was created but not found in active connections", connectionId);
                    return false;
                }
                _logger.LogInformation("Successfully retrieved created connection {ConnectionId} with status {Status}", 
                    connectionId, connection.Status);
            }
        }

        try
        {
            if (connection == null)
            {
                _logger.LogError("Connection {ConnectionId} is null, cannot start", connectionId);
                return false;
            }
            
            _logger.LogInformation("Attempting to start connection {ConnectionId} with current status {Status}", 
                connectionId, connection.Status);
            
            var started = await connection.StartAsync(cancellationToken);
            if (started)
            {
                _logger.LogInformation("Successfully started connection {ConnectionId}, new status: {Status}", 
                    connectionId, connection.Status);
            }
            else
            {
                _logger.LogWarning("Failed to start connection {ConnectionId}, status: {Status}", 
                    connectionId, connection.Status);
            }
            return started;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting connection {ConnectionId}", connectionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> StopConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
            throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

        IDataConnection? connection;
        lock (_lockObject)
        {
            if (!_activeConnections.TryGetValue(connectionId, out connection))
            {
                _logger.LogWarning("Connection {ConnectionId} not found", connectionId);
                return false;
            }
        }

        try
        {
            var stopped = await connection.StopAsync(cancellationToken);
            if (stopped)
            {
                _logger.LogInformation("Stopped connection {ConnectionId}", connectionId);
            }
            else
            {
                _logger.LogWarning("Failed to stop connection {ConnectionId}", connectionId);
            }
            return stopped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping connection {ConnectionId}", connectionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
            throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

        IDataConnection? connection;
        lock (_lockObject)
        {
            if (!_activeConnections.TryGetValue(connectionId, out connection))
            {
                _logger.LogWarning("Connection {ConnectionId} not found", connectionId);
                return false;
            }

            _activeConnections.Remove(connectionId);
            _connectionConfigurations.Remove(connectionId);
        }

        try
        {
            await connection.StopAsync(cancellationToken);
            
            // Unsubscribe from events
            connection.DataReceived -= OnConnectionDataReceived;
            connection.StatusChanged -= OnConnectionStatusChanged;
            
            connection.Dispose();

            // Remove from repository
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConnectionConfigurationRepository>();
            await repository.DeleteConnectionAsync(connectionId);

            _logger.LogInformation("Removed connection {ConnectionId}", connectionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing connection {ConnectionId}", connectionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendDataAsync(string connectionId, UNSInfra.Models.Data.DataPoint dataPoint, string? outputId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
            throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

        if (dataPoint == null)
            throw new ArgumentNullException(nameof(dataPoint));

        IDataConnection? connection;
        lock (_lockObject)
        {
            if (!_activeConnections.TryGetValue(connectionId, out connection))
            {
                _logger.LogWarning("Connection {ConnectionId} not found", connectionId);
                return false;
            }
        }

        try
        {
            // Convert from UNSInfra.Models.Data.DataPoint to UNSInfra.ConnectionSDK.Models.DataPoint
            var connectionDataPoint = new UNSInfra.ConnectionSDK.Models.DataPoint
            {
                Topic = dataPoint.Topic,
                Value = dataPoint.Value,
                Timestamp = dataPoint.Timestamp,
                Quality = "Good" // Default quality
            };
            
            return await connection.SendDataAsync(connectionDataPoint, outputId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending data to connection {ConnectionId}", connectionId);
            return false;
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetActiveConnectionIds()
    {
        lock (_lockObject)
        {
            return _activeConnections.Keys.ToList();
        }
    }

    /// <inheritdoc />
    public ConnectionConfiguration? GetConnectionConfiguration(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
            return null;

        lock (_lockObject)
        {
            return _connectionConfigurations.TryGetValue(connectionId, out var config) ? config : null;
        }
    }

    /// <inheritdoc />
    public IEnumerable<ConnectionConfiguration> GetAllConnectionConfigurations()
    {
        lock (_lockObject)
        {
            return _connectionConfigurations.Values.ToList();
        }
    }

    /// <inheritdoc />
    public ConnectionStatus GetConnectionStatus(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            _logger.LogDebug("GetConnectionStatus called with null/empty connectionId");
            return ConnectionStatus.Unknown;
        }

        IDataConnection? connection;
        lock (_lockObject)
        {
            _logger.LogDebug("GetConnectionStatus for {ConnectionId}: ActiveConnections={ActiveCount}, Configurations={ConfigCount}", 
                connectionId, _activeConnections.Count, _connectionConfigurations.Count);
            
            if (!_activeConnections.TryGetValue(connectionId, out connection))
            {
                _logger.LogDebug("Connection {ConnectionId} not found in active connections", connectionId);
                // Check if the connection exists in stored configurations but is not active
                if (_connectionConfigurations.ContainsKey(connectionId))
                {
                    _logger.LogDebug("Connection {ConnectionId} found in configurations but not active, returning Disconnected", connectionId);
                    return ConnectionStatus.Disconnected;
                }
                _logger.LogDebug("Connection {ConnectionId} not found anywhere, returning Unknown", connectionId);
                return ConnectionStatus.Unknown;
            }
        }

        var status = connection.Status;
        _logger.LogDebug("Connection {ConnectionId} found in active connections with status: {Status}", connectionId, status);
        return status;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateConnectionAsync(ConnectionConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        try
        {
            // Save to repository first
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConnectionConfigurationRepository>();
            await repository.SaveConnectionAsync(config);

            // Update the cached configuration
            lock (_lockObject)
            {
                _connectionConfigurations[config.Id] = config;
                _logger.LogInformation("Updated cached configuration for connection {ConnectionId}", config.Id);
            }

            // Note: For live reconfiguration of inputs/outputs, the connection would need to be restarted
            // This is a complex operation that can be added later if needed
            if (_activeConnections.ContainsKey(config.Id))
            {
                _logger.LogInformation("Connection {ConnectionId} is active. Inputs/outputs will be applied on next restart.", config.Id);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update connection {ConnectionId}", config.Id);
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Connection Manager started");

        try
        {
            // Load and start auto-start connections on startup
            await LoadAndStartAutoStartConnections(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Perform periodic health checks and maintenance
                await PerformHealthChecks(stoppingToken);
                
                // Wait 30 seconds before next check
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in connection manager background service");
        }

        _logger.LogInformation("Connection Manager stopped");
    }

    private async Task LoadAndStartAutoStartConnections(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Loading connection configurations from repository");
            
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConnectionConfigurationRepository>();
            _logger.LogInformation("Loading connections using repository type: {RepositoryType}", repository.GetType().FullName);
            
            // Load all connections from repository
            var allConnections = await repository.GetAllConnectionsAsync();
            
            lock (_lockObject)
            {
                _connectionConfigurations.Clear();
                foreach (var connection in allConnections)
                {
                    _connectionConfigurations[connection.Id] = connection;
                }
            }
            
            _logger.LogInformation("Loaded {Count} connection configurations", allConnections.Count());
            _logger.LogInformation("Loaded connection IDs: {ConnectionIds}", 
                string.Join(", ", allConnections.Select(c => $"'{c.Id}' ({c.Name})")));

            // Start auto-start connections
            var autoStartConnections = await repository.GetAutoStartConnectionsAsync();
            
            foreach (var connection in autoStartConnections)
            {
                try
                {
                    _logger.LogInformation("Auto-starting connection {ConnectionId} ({Name})", 
                        connection.Id, connection.Name);
                    
                    _logger.LogInformation("Populated connection {ConnectionId} with {InputCount} inputs and {OutputCount} outputs",
                        connection.Id, connection.Inputs.Count, connection.Outputs.Count);
                    
                    await CreateConnectionAsync(connection, cancellationToken, saveToRepository: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-start connection {ConnectionId} ({Name})", 
                        connection.Id, connection.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading connection configurations from repository");
        }
    }

    private async Task PerformHealthChecks(CancellationToken cancellationToken)
    {
        List<IDataConnection> connections;
        lock (_lockObject)
        {
            connections = _activeConnections.Values.ToList();
        }

        foreach (var connection in connections)
        {
            try
            {
                // Check if connection is healthy
                if (connection.Status == ConnectionStatus.Error || connection.Status == ConnectionStatus.Disconnected)
                {
                    _logger.LogWarning("Connection {ConnectionId} is in unhealthy state: {Status}", 
                        connection.ConnectionId, connection.Status);
                    
                    // TODO: Implement auto-restart logic if configured
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health of connection {ConnectionId}", connection.ConnectionId);
            }
        }
    }

    private void OnConnectionDataReceived(object? sender, DataReceivedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Data received from connection {ConnectionId}: {Topic}", 
                e.ConnectionId, e.DataPoint.Topic);

            bool isNewTopic = false;
            
            // Check if this is a new topic (thread-safe)
            lock (_lockObject)
            {
                if (!_knownTopics.Contains(e.DataPoint.Topic))
                {
                    _knownTopics.Add(e.DataPoint.Topic);
                    isNewTopic = true;
                }
            }

            // Convert ConnectionSDK DataPoint to Core DataPoint for events
            var coreDataPoint = new UNSInfra.Models.Data.DataPoint
            {
                Topic = e.DataPoint.Topic,
                Value = e.DataPoint.Value,
                Timestamp = e.DataPoint.Timestamp,
                Source = e.DataPoint.SourceSystem ?? "Unknown",
                Metadata = e.DataPoint.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>()
            };

            // Add additional metadata from ConnectionSDK DataPoint
            if (!string.IsNullOrEmpty(e.DataPoint.ConnectionId))
                coreDataPoint.Metadata["ConnectionId"] = e.DataPoint.ConnectionId;
            if (!string.IsNullOrEmpty(e.DataPoint.Quality))
                coreDataPoint.Metadata["Quality"] = e.DataPoint.Quality;

            // Publish events synchronously but fire-and-forget to avoid blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    // Always publish TopicDataUpdatedEvent for data updates
                    await _eventBus.PublishAsync(new TopicDataUpdatedEvent(
                        e.DataPoint.Topic,
                        coreDataPoint,
                        e.DataPoint.SourceSystem ?? "Unknown"));

                    // Only publish TopicAddedEvent for genuinely new topics
                    if (isNewTopic)
                    {
                        var hierarchicalPath = new HierarchicalPath();
                        await _eventBus.PublishAsync(new TopicAddedEvent(
                            e.DataPoint.Topic,
                            hierarchicalPath,
                            e.DataPoint.SourceSystem ?? "Unknown",
                            DateTime.UtcNow));
                        
                        _logger.LogDebug("Published TopicAddedEvent for new topic {Topic}", e.DataPoint.Topic);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error publishing events for topic {Topic} from connection {ConnectionId}", 
                        e.DataPoint.Topic, e.ConnectionId);
                }
            });
            
            DataReceived?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling data received event from connection {ConnectionId}", e.ConnectionId);
        }
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Connection {ConnectionId} status changed: {OldStatus} -> {NewStatus}", 
                e.ConnectionId, e.OldStatus, e.NewStatus);
            
            ConnectionStatusChanged?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling status changed event from connection {ConnectionId}", e.ConnectionId);
        }
    }

    public override void Dispose()
    {
        // Stop and dispose all connections
        List<IDataConnection> connections;
        lock (_lockObject)
        {
            connections = _activeConnections.Values.ToList();
            _activeConnections.Clear();
            _connectionConfigurations.Clear();
        }

        foreach (var connection in connections)
        {
            try
            {
                connection.DataReceived -= OnConnectionDataReceived;
                connection.StatusChanged -= OnConnectionStatusChanged;
                connection.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(10));
                connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing connection {ConnectionId}", connection.ConnectionId);
            }
        }

        base.Dispose();
    }
    

    /// <summary>
    /// Converts a generic configuration object to the strongly-typed configuration expected by the connection descriptor
    /// </summary>
    private object? ConvertToTypedConfiguration(object config, IConnectionDescriptor descriptor)
    {
        try
        {
            // If it's already the correct type, return as-is
            var defaultConfig = descriptor.CreateDefaultConnectionConfiguration();
            var targetType = defaultConfig.GetType();
            
            if (config.GetType() == targetType)
            {
                return config;
            }
            
            // If it's a dictionary (likely from JSON deserialization), convert to typed object
            if (config is Dictionary<string, object> dict)
            {
                return ConvertDictionaryToTypedObject(dict, targetType);
            }
            
            // Try to use JSON serialization as a fallback
            var json = System.Text.Json.JsonSerializer.Serialize(config);
            return System.Text.Json.JsonSerializer.Deserialize(json, targetType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert configuration to type {TargetType}", descriptor.GetType().Name);
            return null;
        }
    }
    
    /// <summary>
    /// Converts a dictionary to a strongly-typed object using reflection
    /// </summary>
    private object? ConvertDictionaryToTypedObject(Dictionary<string, object> dict, Type targetType)
    {
        try
        {
            var instance = Activator.CreateInstance(targetType);
            if (instance == null) return null;
            
            var properties = targetType.GetProperties();
            
            foreach (var property in properties)
            {
                if (property.CanWrite && dict.TryGetValue(property.Name, out var value))
                {
                    // Convert the value to the property type
                    var convertedValue = ConvertValue(value, property.PropertyType);
                    property.SetValue(instance, convertedValue);
                }
            }
            
            return instance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert dictionary to type {TargetType}", targetType.Name);
            return null;
        }
    }
    
    /// <summary>
    /// Converts a value to the target type
    /// </summary>
    private object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;
        
        try
        {
            // Handle nullable types
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = Nullable.GetUnderlyingType(targetType)!;
            }
            
            // If types already match
            if (value.GetType() == targetType)
                return value;
            
            // Handle common conversions
            if (targetType == typeof(string))
                return value.ToString();
            
            if (targetType == typeof(bool) && value is string boolStr)
                return bool.Parse(boolStr);
            
            if (targetType == typeof(int) && value is string intStr)
                return int.Parse(intStr);
            
            if (targetType == typeof(double) && value is string doubleStr)
                return double.Parse(doubleStr);
            
            // Handle lists
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = targetType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = Activator.CreateInstance(listType);
                
                if (value is System.Text.Json.JsonElement jsonArray && jsonArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var addMethod = listType.GetMethod("Add");
                    foreach (var element in jsonArray.EnumerateArray())
                    {
                        var convertedElement = ConvertValue(element.ToString(), elementType);
                        addMethod?.Invoke(list, new[] { convertedElement });
                    }
                }
                
                return list;
            }
            
            // Use Convert.ChangeType as fallback
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // Return default value if conversion fails
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }
}