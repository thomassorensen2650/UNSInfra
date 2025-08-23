using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.Core.Repositories;
using UNSInfra.Models;
using UNSInfra.Abstractions;
using UNSInfra.Services;
using UNSInfra.Services.V1.Connections;
using UNSInfra.Services.V1.Models;
using UNSInfra.Storage.SQLite;
using UNSInfra.Storage.SQLite.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace UNSInfra.Core.Tests.Services;

/// <summary>
/// Test class specifically for reproducing the MQTT connection configuration retrieval issue
/// where saved MQTT broker settings appear empty when retrieved from the UI
/// </summary>
public class MqttConnectionConfigurationPersistenceTest : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UNSInfraDbContext _context;
    private readonly ITestOutputHelper _output;

    public MqttConnectionConfigurationPersistenceTest(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        
        // Use a shared in-memory connection string that persists for the test
        var connectionString = $"Data Source=:memory:?cache=shared&foreign_keys=true";
        
        // Configure in-memory SQLite database for testing
        services.AddDbContext<UNSInfraDbContext>(options =>
            options.UseSqlite(connectionString));
        
        // Add DbContextFactory for SQLiteConnectionConfigurationRepository
        services.AddDbContextFactory<UNSInfraDbContext>(options =>
            options.UseSqlite(connectionString));
        
        services.AddScoped<IConnectionConfigurationRepository, SQLiteConnectionConfigurationRepository>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<UNSInfraDbContext>();
        
        // Create database schema
        _context.Database.EnsureCreated();
        
        _output.WriteLine("Test setup completed with SQLite in-memory database");
    }

    /// <summary>
    /// This test reproduces the exact issue described:
    /// 1. Create and save an MQTT connection with broker configuration
    /// 2. Retrieve the connection configuration 
    /// 3. Verify that the MQTT broker settings are preserved (they currently appear empty)
    /// </summary>
    [Fact]
    public async Task MqttConnectionConfiguration_ShouldPreserveBrokerSettingsAfterSaveAndRetrieve()
    {
        // Clear any existing data
        await ClearDatabaseAsync();
        
        // Arrange: Create MQTT connection configuration with specific broker settings
        var originalMqttConfig = new MqttConnectionConfiguration
        {
            Host = "test.mosquitto.org",
            Port = 1883,
            ClientId = "test-client-123",
            Username = "testuser",
            Password = "testpassword",
            UseTls = false,
            TimeoutSeconds = 30,
            KeepAliveSeconds = 60,
            CleanSession = true
        };
        
        var connectionConfig = new ConnectionConfiguration
        {
            Id = "mqtt-test-connection",
            Name = "Test MQTT Broker",
            ConnectionType = "mqtt",
            ConnectionConfig = originalMqttConfig, // This should be the strongly-typed config
            IsEnabled = true,
            AutoStart = false,
            CreatedAt = DateTime.UtcNow
        };
        
        _output.WriteLine($"Original MQTT config: Host={originalMqttConfig.Host}, Port={originalMqttConfig.Port}, Username={originalMqttConfig.Username}");
        _output.WriteLine($"Connection config type: {connectionConfig.ConnectionConfig?.GetType().Name}");
        
        // Act: Save the connection
        var repository = _serviceProvider.GetRequiredService<IConnectionConfigurationRepository>();
        await repository.SaveConnectionAsync(connectionConfig);
        
        _output.WriteLine("Connection saved to repository");
        
        // Act: Retrieve the connection (simulating what the UI does)
        var retrievedConnection = await repository.GetConnectionByIdAsync("mqtt-test-connection");
        
        // Assert: Connection should be found
        Assert.NotNull(retrievedConnection);
        Assert.Equal("mqtt-test-connection", retrievedConnection.Id);
        Assert.Equal("Test MQTT Broker", retrievedConnection.Name);
        Assert.Equal("mqtt", retrievedConnection.ConnectionType);
        
        _output.WriteLine($"Retrieved connection: {retrievedConnection.Name} (Type: {retrievedConnection.ConnectionType})");
        _output.WriteLine($"Retrieved ConnectionConfig type: {retrievedConnection.ConnectionConfig?.GetType().Name ?? "null"}");
        
        // Assert: ConnectionConfig should not be null
        Assert.NotNull(retrievedConnection.ConnectionConfig);
        
        // This is where the issue occurs - let's check what type we actually get back
        if (retrievedConnection.ConnectionConfig is MqttConnectionConfiguration retrievedMqttConfig)
        {
            _output.WriteLine($"Retrieved as MqttConnectionConfiguration: Host={retrievedMqttConfig.Host}, Port={retrievedMqttConfig.Port}");
            
            // These assertions should pass if persistence works correctly
            Assert.Equal(originalMqttConfig.Host, retrievedMqttConfig.Host);
            Assert.Equal(originalMqttConfig.Port, retrievedMqttConfig.Port);
            Assert.Equal(originalMqttConfig.ClientId, retrievedMqttConfig.ClientId);
            Assert.Equal(originalMqttConfig.Username, retrievedMqttConfig.Username);
            Assert.Equal(originalMqttConfig.Password, retrievedMqttConfig.Password);
            Assert.Equal(originalMqttConfig.UseTls, retrievedMqttConfig.UseTls);
            Assert.Equal(originalMqttConfig.TimeoutSeconds, retrievedMqttConfig.TimeoutSeconds);
            Assert.Equal(originalMqttConfig.KeepAliveSeconds, retrievedMqttConfig.KeepAliveSeconds);
            Assert.Equal(originalMqttConfig.CleanSession, retrievedMqttConfig.CleanSession);
        }
        else
        {
            // If we get here, the configuration was not properly deserialized
            _output.WriteLine($"ERROR: Retrieved config is not MqttConnectionConfiguration! Type: {retrievedConnection.ConnectionConfig.GetType().Name}");
            _output.WriteLine($"Retrieved config content: {System.Text.Json.JsonSerializer.Serialize(retrievedConnection.ConnectionConfig)}");
            
            // This will fail and show us what we actually got back
            Assert.IsType<MqttConnectionConfiguration>(retrievedConnection.ConnectionConfig);
        }
    }

    /// <summary>
    /// Test to verify what type of object we get back from the database
    /// and if it contains the expected MQTT configuration values
    /// </summary>
    [Fact]
    public async Task Repository_ShouldReturnCorrectTypeAndValues()
    {
        // Clear any existing data
        await ClearDatabaseAsync();
        
        var originalConfig = new MqttConnectionConfiguration
        {
            Host = "broker.example.com",
            Port = 8883,
            ClientId = "test-client-456",
            Username = "mqttuser",
            Password = "mqttpass",
            UseTls = true,
            TimeoutSeconds = 20,
            KeepAliveSeconds = 45,
            CleanSession = false
        };
        
        var connection = new ConnectionConfiguration
        {
            Id = "test-repository-config",
            Name = "Repository Test Connection",
            ConnectionType = "mqtt",
            ConnectionConfig = originalConfig,
            IsEnabled = true
        };
        
        var repository = _serviceProvider.GetRequiredService<IConnectionConfigurationRepository>();
        
        // Save and immediately retrieve to see what happens
        await repository.SaveConnectionAsync(connection);
        var retrieved = await repository.GetConnectionByIdAsync("test-repository-config");
        
        Assert.NotNull(retrieved);
        _output.WriteLine($"Retrieved connection config type: {retrieved.ConnectionConfig?.GetType().FullName ?? "null"}");
        
        if (retrieved.ConnectionConfig != null)
        {
            _output.WriteLine($"Config object content: {System.Text.Json.JsonSerializer.Serialize(retrieved.ConnectionConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");
        }
        
        // The key test: Is it the right type with the right values?
        if (retrieved.ConnectionConfig is MqttConnectionConfiguration mqttConfig)
        {
            _output.WriteLine("SUCCESS: Retrieved as MqttConnectionConfiguration");
            _output.WriteLine($"Host: '{mqttConfig.Host}' (expected: '{originalConfig.Host}')");
            _output.WriteLine($"Port: {mqttConfig.Port} (expected: {originalConfig.Port})");
            _output.WriteLine($"Username: '{mqttConfig.Username}' (expected: '{originalConfig.Username}')");
        }
        else
        {
            _output.WriteLine($"ISSUE: Retrieved as {retrieved.ConnectionConfig?.GetType().Name}, not MqttConnectionConfiguration");
        }
    }

    private async Task ClearDatabaseAsync()
    {
        var connections = _context.ConnectionConfigurations.ToList();
        _context.ConnectionConfigurations.RemoveRange(connections);
        await _context.SaveChangesAsync();
        _output.WriteLine("Database cleared");
    }

    public void Dispose()
    {
        _context?.Dispose();
        if (_serviceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }
    }
}