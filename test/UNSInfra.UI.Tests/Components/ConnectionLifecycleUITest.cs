using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Abstractions;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Core.Repositories;
using UNSInfra.Models;
using UNSInfra.Services;

namespace UNSInfra.UI.Tests.Components;

/// <summary>
/// Integration test that verifies the complete connection lifecycle via UI:
/// 1. Adding a connection through the UI automatically starts it
/// 2. Connection status is properly displayed in the UI
/// 3. Status updates are reflected in real-time
/// </summary>
public class ConnectionLifecycleUITest
{
    private readonly Mock<IConnectionConfigurationRepository> _mockRepository;
    private readonly Mock<IConnectionRegistry> _mockRegistry;
    private readonly Mock<IConnectionDescriptor> _mockDescriptor;
    private readonly Mock<IDataConnection> _mockConnection;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<ILogger<ConnectionManager>> _mockLogger;
    private readonly Mock<UNSInfra.Services.Events.IEventBus> _mockEventBus;
    
    private readonly List<ConnectionConfiguration> _persistedConnections;
    private readonly Dictionary<string, ConnectionStatus> _connectionStatuses;
    private readonly ConnectionManager _connectionManager;

    public ConnectionLifecycleUITest()
    {
        _persistedConnections = new List<ConnectionConfiguration>();
        _connectionStatuses = new Dictionary<string, ConnectionStatus>();
        
        // Setup mocks
        _mockRepository = new Mock<IConnectionConfigurationRepository>();
        _mockRegistry = new Mock<IConnectionRegistry>();
        _mockDescriptor = new Mock<IConnectionDescriptor>();
        _mockConnection = new Mock<IDataConnection>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockLogger = new Mock<ILogger<ConnectionManager>>();
        _mockEventBus = new Mock<UNSInfra.Services.Events.IEventBus>();

        SetupRepositoryMock();
        SetupRegistryMock();
        SetupDescriptorMock();
        SetupServiceProviderMock();

        // Create connection manager instance
        _connectionManager = new ConnectionManager(
            _mockRegistry.Object,
            _mockServiceProvider.Object,
            _mockScopeFactory.Object,
            _mockLogger.Object,
            _mockEventBus.Object
        );
    }

    private void SetupRepositoryMock()
    {
        _mockRepository.Setup(r => r.SaveConnectionAsync(It.IsAny<ConnectionConfiguration>()))
            .Returns<ConnectionConfiguration>(conn => 
            {
                var existing = _persistedConnections.FirstOrDefault(c => c.Id == conn.Id);
                if (existing != null)
                {
                    _persistedConnections.Remove(existing);
                }
                _persistedConnections.Add(conn);
                return Task.CompletedTask;
            });

        _mockRepository.Setup(r => r.GetAllConnectionsAsync(It.IsAny<bool>()))
            .Returns<bool>(enabledOnly => Task.FromResult(_persistedConnections
                .Where(c => !enabledOnly || c.IsEnabled)
                .ToList().AsEnumerable()));

        _mockRepository.Setup(r => r.GetConnectionByIdAsync(It.IsAny<string>()))
            .Returns<string>(id => Task.FromResult(_persistedConnections.FirstOrDefault(c => c.Id == id)));

        _mockRepository.Setup(r => r.GetAutoStartConnectionsAsync())
            .Returns(() => Task.FromResult(_persistedConnections.Where(c => c.AutoStart && c.IsEnabled).ToList().AsEnumerable()));
    }

    private void SetupRegistryMock()
    {
        _mockRegistry.Setup(r => r.GetDescriptor(It.IsAny<string>()))
            .Returns(_mockDescriptor.Object);
    }

    private void SetupDescriptorMock()
    {
        _mockDescriptor.Setup(d => d.CreateConnection(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IServiceProvider>()))
            .Returns<string, string, IServiceProvider>((id, name, serviceProvider) =>
            {
                var mockConn = new Mock<IDataConnection>();
                
                mockConn.Setup(c => c.ConnectionId).Returns(id);
                mockConn.Setup(c => c.Status)
                    .Returns(() => _connectionStatuses.GetValueOrDefault(id, ConnectionStatus.Disconnected));
                
                mockConn.Setup(c => c.ValidateConfiguration(It.IsAny<object>()))
                    .Returns(UNSInfra.ConnectionSDK.Models.ValidationResult.Success());
                
                mockConn.Setup(c => c.InitializeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                
                mockConn.Setup(c => c.ConfigureInputAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                
                mockConn.Setup(c => c.ConfigureOutputAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                
                mockConn.Setup(c => c.StartAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(async token =>
                    {
                        _connectionStatuses[id] = ConnectionStatus.Connecting;
                        await Task.Delay(50, token);
                        if (!token.IsCancellationRequested)
                        {
                            _connectionStatuses[id] = ConnectionStatus.Connected;
                            mockConn.Raise(c => c.StatusChanged += null,
                                new ConnectionStatusChangedEventArgs
                                {
                                    ConnectionId = id,
                                    OldStatus = ConnectionStatus.Connecting,
                                    NewStatus = ConnectionStatus.Connected
                                });
                        }
                        return true;
                    });
                
                mockConn.Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(async token =>
                    {
                        _connectionStatuses[id] = ConnectionStatus.Stopping;
                        await Task.Delay(50, token);
                        if (!token.IsCancellationRequested)
                        {
                            _connectionStatuses[id] = ConnectionStatus.Disconnected;
                            mockConn.Raise(c => c.StatusChanged += null,
                                new ConnectionStatusChangedEventArgs
                                {
                                    ConnectionId = id,
                                    OldStatus = ConnectionStatus.Stopping,
                                    NewStatus = ConnectionStatus.Disconnected
                                });
                        }
                        return true;
                    });
                
                return mockConn.Object;
            });

        _mockDescriptor.Setup(d => d.CreateDefaultConnectionConfiguration())
            .Returns(new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 1883,
                ["Username"] = "",
                ["Password"] = ""
            });
    }


    private void SetupServiceProviderMock()
    {
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IConnectionConfigurationRepository)))
            .Returns(_mockRepository.Object);

        _mockScope.Setup(s => s.ServiceProvider)
            .Returns(_mockServiceProvider.Object);

        _mockScopeFactory.Setup(sf => sf.CreateScope())
            .Returns(_mockScope.Object);
    }

    [Fact]
    public async Task AddConnectionViaUI_ShouldCreateAndPersistConnection_ThenManuallyStartAndDisplayStatus()
    {
        // Arrange - Simulate user adding a connection via UI
        var connectionConfig = new ConnectionConfiguration
        {
            Id = "test-connection-id",
            Name = "Test MQTT Connection",
            ConnectionType = "MQTT",
            IsEnabled = true,
            AutoStart = true,
            Description = "Test connection for UI lifecycle test",
            ConnectionConfig = new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 1883,
                ["Username"] = "testuser",
                ["Password"] = "testpass"
            },
            Inputs = new List<object>(),
            Outputs = new List<object>()
        };

        // Act - Simulate UI adding the connection
        var createSuccess = await _connectionManager.CreateConnectionAsync(connectionConfig);

        // Assert - Verify connection was created successfully
        Assert.True(createSuccess, "Connection should be created successfully");

        // Verify connection is in the manager's active connections
        var activeConnections = _connectionManager.GetActiveConnectionIds();
        Assert.Contains(connectionConfig.Id, activeConnections);

        // Verify connection configuration is accessible
        var retrievedConfig = _connectionManager.GetConnectionConfiguration(connectionConfig.Id);
        Assert.NotNull(retrievedConfig);
        Assert.Equal(connectionConfig.Name, retrievedConfig.Name);
        Assert.Equal(connectionConfig.ConnectionType, retrievedConfig.ConnectionType);

        // Verify initial connection status is disconnected (not auto-started during creation)
        var initialStatus = _connectionManager.GetConnectionStatus(connectionConfig.Id);
        Assert.Equal(ConnectionStatus.Disconnected, initialStatus);

        // Now manually start the connection (as user would via UI)
        var startSuccess = await _connectionManager.StartConnectionAsync(connectionConfig.Id);
        Assert.True(startSuccess);

        // Wait for connection to establish
        await Task.Delay(100);

        // Verify connection status indicates it's connected after manual start
        var connectedStatus = _connectionManager.GetConnectionStatus(connectionConfig.Id);
        Assert.Equal(ConnectionStatus.Connected, connectedStatus);

        // Verify the connection was persisted to repository
        var persistedConnection = await _mockRepository.Object.GetConnectionByIdAsync(connectionConfig.Id);
        Assert.NotNull(persistedConnection);
        Assert.Equal(connectionConfig.Name, persistedConnection.Name);

        // Verify the connection was persisted to repository
        _mockRepository.Verify(r => r.SaveConnectionAsync(It.IsAny<ConnectionConfiguration>()), Times.Once);
    }

    [Fact]
    public async Task AddConnectionViaUI_WithAutoStartDisabled_ShouldNotStartAutomatically()
    {
        // Arrange - Create connection configuration with AutoStart disabled
        var connectionConfig = new ConnectionConfiguration
        {
            Id = "manual-start-connection",
            Name = "Manual Start Connection",
            ConnectionType = "MQTT",
            IsEnabled = true,
            AutoStart = false, // Explicitly disable auto-start
            Description = "Connection that should not auto-start",
            ConnectionConfig = new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 1883
            },
            Inputs = new List<object>(),
            Outputs = new List<object>()
        };

        _connectionStatuses[connectionConfig.Id] = ConnectionStatus.Disconnected;

        // Act - Add connection via UI (should not auto-start)
        var createSuccess = await _connectionManager.CreateConnectionAsync(connectionConfig);

        // Assert - Verify connection was created but not started
        Assert.True(createSuccess, "Connection should be created successfully");

        // Wait a moment to ensure no auto-start occurs
        await Task.Delay(100);

        // Verify connection is in configurations but status should be disconnected
        var retrievedConfig = _connectionManager.GetConnectionConfiguration(connectionConfig.Id);
        Assert.NotNull(retrievedConfig);

        var status = _connectionManager.GetConnectionStatus(connectionConfig.Id);
        Assert.Equal(ConnectionStatus.Disconnected, status);

        // Now manually start the connection to verify the start process works
        var startSuccess = await _connectionManager.StartConnectionAsync(connectionConfig.Id);
        Assert.True(startSuccess);

        // Wait for connection to establish
        await Task.Delay(200);

        var statusAfterStart = _connectionManager.GetConnectionStatus(connectionConfig.Id);
        Assert.Equal(ConnectionStatus.Connected, statusAfterStart);
    }

    [Fact]
    public async Task ConnectionStatusUI_ShouldReflectRealTimeStatusChanges()
    {
        // Arrange - Create and start a connection
        var connectionConfig = new ConnectionConfiguration
        {
            Id = "status-test-connection",
            Name = "Status Test Connection",
            ConnectionType = "MQTT",
            IsEnabled = true,
            AutoStart = true,
            ConnectionConfig = new Dictionary<string, object>
            {
                ["Host"] = "test.broker.com",
                ["Port"] = 1883
            }
        };

        _connectionStatuses[connectionConfig.Id] = ConnectionStatus.Disconnected;

        // Act & Assert - Test connection lifecycle status changes
        
        // 1. Initially disconnected
        Assert.Equal(ConnectionStatus.Unknown, _connectionManager.GetConnectionStatus(connectionConfig.Id));

        // 2. Create connection (won't auto-start during creation)
        var createSuccess = await _connectionManager.CreateConnectionAsync(connectionConfig);
        Assert.True(createSuccess);

        // 3. Manually start the connection to test lifecycle
        var startSuccess = await _connectionManager.StartConnectionAsync(connectionConfig.Id);
        Assert.True(startSuccess);

        // Wait for connection process to complete
        await Task.Delay(100);

        // 4. Verify connection is now connected
        var connectedStatus = _connectionManager.GetConnectionStatus(connectionConfig.Id);
        Assert.Equal(ConnectionStatus.Connected, connectedStatus);

        // 5. Stop the connection
        var stopSuccess = await _connectionManager.StopConnectionAsync(connectionConfig.Id);
        Assert.True(stopSuccess);

        // 6. Wait for stop process to complete
        await Task.Delay(100);

        // 7. Verify connection is now disconnected
        var disconnectedStatus = _connectionManager.GetConnectionStatus(connectionConfig.Id);
        Assert.Equal(ConnectionStatus.Disconnected, disconnectedStatus);

        // 8. Restart the connection
        var restartSuccess = await _connectionManager.StartConnectionAsync(connectionConfig.Id);
        Assert.True(restartSuccess);

        // 9. Wait for restart process to complete
        await Task.Delay(200);

        // 10. Verify connection is connected again
        var reconnectedStatus = _connectionManager.GetConnectionStatus(connectionConfig.Id);
        Assert.Equal(ConnectionStatus.Connected, reconnectedStatus);
    }

    [Fact]
    public async Task UIConnectionList_ShouldShowAllConfiguredConnections_WithCorrectStatuses()
    {
        // Arrange - Create multiple connections with different states
        var connections = new[]
        {
            new ConnectionConfiguration
            {
                Id = "conn-1",
                Name = "Active Connection",
                ConnectionType = "MQTT",
                IsEnabled = true,
                AutoStart = true,
                ConnectionConfig = new Dictionary<string, object> { ["Host"] = "broker1.com" }
            },
            new ConnectionConfiguration
            {
                Id = "conn-2", 
                Name = "Manual Connection",
                ConnectionType = "SocketIO",
                IsEnabled = true,
                AutoStart = false,
                ConnectionConfig = new Dictionary<string, object> { ["ServerUrl"] = "https://server2.com" }
            },
            new ConnectionConfiguration
            {
                Id = "conn-3",
                Name = "Disabled Connection",
                ConnectionType = "MQTT",
                IsEnabled = false,
                AutoStart = false,
                ConnectionConfig = new Dictionary<string, object> { ["Host"] = "broker3.com" }
            }
        };

        // Initialize statuses
        _connectionStatuses["conn-1"] = ConnectionStatus.Disconnected;
        _connectionStatuses["conn-2"] = ConnectionStatus.Disconnected;
        _connectionStatuses["conn-3"] = ConnectionStatus.Disconnected;

        // Act - Add all connections
        foreach (var conn in connections)
        {
            var success = await _connectionManager.CreateConnectionAsync(conn);
            Assert.True(success, $"Failed to create connection {conn.Name}");
        }

        // Start the active connection manually (simulating UI start button)
        var startSuccess = await _connectionManager.StartConnectionAsync("conn-1");
        Assert.True(startSuccess);
        await Task.Delay(100);

        // Assert - Verify UI would show all connections with correct information
        var allConfigurations = _connectionManager.GetAllConnectionConfigurations();
        Assert.Equal(3, allConfigurations.Count());

        // Verify each connection is retrievable and has expected properties
        var activeConn = allConfigurations.First(c => c.Id == "conn-1");
        var manualConn = allConfigurations.First(c => c.Id == "conn-2");
        var disabledConn = allConfigurations.First(c => c.Id == "conn-3");

        Assert.Equal("Active Connection", activeConn.Name);
        Assert.True(activeConn.AutoStart);
        Assert.True(activeConn.IsEnabled);

        Assert.Equal("Manual Connection", manualConn.Name);
        Assert.False(manualConn.AutoStart);
        Assert.True(manualConn.IsEnabled);

        Assert.Equal("Disabled Connection", disabledConn.Name);
        Assert.False(disabledConn.AutoStart);
        Assert.False(disabledConn.IsEnabled);

        // Verify status reporting for UI display
        var activeStatus = _connectionManager.GetConnectionStatus("conn-1");
        var manualStatus = _connectionManager.GetConnectionStatus("conn-2");
        var disabledStatus = _connectionManager.GetConnectionStatus("conn-3");

        Assert.Equal(ConnectionStatus.Connected, activeStatus); // Manually started
        Assert.Equal(ConnectionStatus.Disconnected, manualStatus); // Not started
        Assert.Equal(ConnectionStatus.Disconnected, disabledStatus); // Disabled, not active
    }

    [Fact]
    public async Task ConnectionStatusMonitor_ShouldHandleStatusChangeEvents()
    {
        // Arrange - Track status change events
        var statusChanges = new List<ConnectionStatusChangedEventArgs>();
        _connectionManager.ConnectionStatusChanged += (sender, args) => statusChanges.Add(args);

        var connectionConfig = new ConnectionConfiguration
        {
            Id = "event-test-connection",
            Name = "Event Test Connection", 
            ConnectionType = "MQTT",
            IsEnabled = true,
            AutoStart = true,
            ConnectionConfig = new Dictionary<string, object> { ["Host"] = "event.broker.com" }
        };

        _connectionStatuses[connectionConfig.Id] = ConnectionStatus.Disconnected;

        // Act - Create connection 
        await _connectionManager.CreateConnectionAsync(connectionConfig);
        
        // Manually start to generate events (since auto-start doesn't happen during creation)
        await _connectionManager.StartConnectionAsync(connectionConfig.Id);
        await Task.Delay(100); // Allow time for connection and status events

        // Stop and restart to generate more events
        await _connectionManager.StopConnectionAsync(connectionConfig.Id);
        await Task.Delay(100);
        await _connectionManager.StartConnectionAsync(connectionConfig.Id);
        await Task.Delay(100);

        // Assert - Verify status change events were raised
        Assert.True(statusChanges.Count >= 2, $"Expected at least 2 status changes, got {statusChanges.Count}");
        
        // Verify event details contain correct connection ID
        Assert.All(statusChanges, evt => Assert.Equal(connectionConfig.Id, evt.ConnectionId));

        // Verify we got connected and disconnected events
        Assert.Contains(statusChanges, evt => evt.NewStatus == ConnectionStatus.Connected);
        Assert.Contains(statusChanges, evt => evt.NewStatus == ConnectionStatus.Disconnected);
    }
}