using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Models;
using UNSInfra.Services.V1.Models;
using UNSInfra.Storage.SQLite;
using UNSInfra.Storage.SQLite.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace UNSInfra.Core.Tests.Services;

/// <summary>
/// Test to reproduce the UI issue where connectionConfig dictionary is not updated 
/// when editing an existing MQTT connection
/// </summary>
public class MqttConnectionConfigEditingTest : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UNSInfraDbContext _context;
    private readonly ITestOutputHelper _output;

    public MqttConnectionConfigEditingTest(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        
        var connectionString = $"Data Source=:memory:?cache=shared&foreign_keys=true";
        
        services.AddDbContext<UNSInfraDbContext>(options =>
            options.UseSqlite(connectionString));
        
        services.AddDbContextFactory<UNSInfraDbContext>(options =>
            options.UseSqlite(connectionString));
        
        services.AddScoped<IConnectionConfigurationRepository, SQLiteConnectionConfigurationRepository>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<UNSInfraDbContext>();
        
        _context.Database.EnsureCreated();
        _output.WriteLine("Test setup completed");
    }

    /// <summary>
    /// This test simulates the exact UI workflow:
    /// 1. Save an MQTT connection with specific settings
    /// 2. Retrieve it (as the UI edit dialog would)  
    /// 3. Try to populate a dictionary from the ConnectionConfig
    /// 4. Verify the dictionary can be populated correctly
    /// </summary>
    [Fact]
    public async Task EditConnection_ShouldPopulateDictionaryFromRetrievedConfig()
    {
        // Clear any existing data
        await ClearDatabaseAsync();
        
        // Step 1: Save connection with MQTT config (simulates initial creation)
        var originalMqttConfig = new MqttConnectionConfiguration
        {
            Host = "mqtt.broker.com",
            Port = 8883,
            ClientId = "edit-test-client",
            Username = "edituser",
            Password = "editpass",
            UseTls = true,
            TimeoutSeconds = 45,
            KeepAliveSeconds = 120,
            CleanSession = false
        };
        
        var connection = new ConnectionConfiguration
        {
            Id = "edit-test-connection",
            Name = "Edit Test MQTT Connection",
            ConnectionType = "mqtt",
            ConnectionConfig = originalMqttConfig,
            IsEnabled = true
        };
        
        var repository = _serviceProvider.GetRequiredService<IConnectionConfigurationRepository>();
        await repository.SaveConnectionAsync(connection);
        
        _output.WriteLine($"Saved connection with Host={originalMqttConfig.Host}, Port={originalMqttConfig.Port}");
        
        // Step 2: Retrieve the connection (simulates opening edit dialog)
        var retrievedConnection = await repository.GetConnectionByIdAsync("edit-test-connection");
        Assert.NotNull(retrievedConnection);
        
        _output.WriteLine($"Retrieved connection config type: {retrievedConnection.ConnectionConfig?.GetType().Name}");
        
        // Step 3: Try to populate dictionary from ConnectionConfig (simulates UI MapObjectToDictionary)
        var connectionConfig = new Dictionary<string, object>();
        
        if (retrievedConnection.ConnectionConfig != null)
        {
            // This simulates the UI's MapObjectToDictionary method
            var properties = retrievedConnection.ConnectionConfig.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        var value = property.GetValue(retrievedConnection.ConnectionConfig);
                        if (value != null)
                        {
                            connectionConfig[property.Name] = value;
                            _output.WriteLine($"Mapped {property.Name} = {value}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Failed to read property {property.Name}: {ex.Message}");
                    }
                }
            }
        }
        
        // Step 4: Verify the dictionary was populated correctly
        Assert.True(connectionConfig.Count > 0, "connectionConfig dictionary should not be empty");
        
        // Verify specific MQTT configuration values
        Assert.True(connectionConfig.ContainsKey("Host"), "Dictionary should contain Host key");
        Assert.Equal("mqtt.broker.com", connectionConfig["Host"].ToString());
        
        Assert.True(connectionConfig.ContainsKey("Port"), "Dictionary should contain Port key");
        Assert.Equal(8883, connectionConfig["Port"]);
        
        Assert.True(connectionConfig.ContainsKey("Username"), "Dictionary should contain Username key");
        Assert.Equal("edituser", connectionConfig["Username"].ToString());
        
        Assert.True(connectionConfig.ContainsKey("UseTls"), "Dictionary should contain UseTls key");
        Assert.Equal(true, connectionConfig["UseTls"]);
        
        _output.WriteLine($"Successfully populated connectionConfig with {connectionConfig.Count} properties");
        _output.WriteLine($"Keys: {string.Join(", ", connectionConfig.Keys)}");
        
        // Step 5: Test that we can update values in the dictionary (simulates form field changes)
        connectionConfig["Host"] = "updated.broker.com";
        connectionConfig["Port"] = 1883;
        connectionConfig["Username"] = "updateduser";
        
        Assert.Equal("updated.broker.com", connectionConfig["Host"].ToString());
        Assert.Equal(1883, connectionConfig["Port"]);
        Assert.Equal("updateduser", connectionConfig["Username"].ToString());
        
        _output.WriteLine("Dictionary updates work correctly");
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