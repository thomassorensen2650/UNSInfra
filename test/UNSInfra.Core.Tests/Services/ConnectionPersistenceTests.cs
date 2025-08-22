using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Models;
using UNSInfra.Storage.SQLite;
using UNSInfra.Storage.SQLite.Repositories;
using Xunit;

namespace UNSInfra.Core.Tests.Services;

public class ConnectionPersistenceTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UNSInfraDbContext _context;

    public ConnectionPersistenceTests()
    {
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
        services.AddLogging(builder => builder.AddConsole());

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<UNSInfraDbContext>();
        
        // Create database schema
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task SaveConnectionAsync_ShouldPersistConnection()
    {
        // Clean database before test
        await ClearDatabaseAsync();
        // Arrange
        var repository = _serviceProvider.GetRequiredService<IConnectionConfigurationRepository>();
        var connection = new ConnectionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Connection",
            ConnectionType = "SocketIO",
            IsEnabled = true,
            AutoStart = false,
            Description = "Test connection for persistence",
            ConnectionConfig = new Dictionary<string, object>
            {
                ["ServerUrl"] = "https://test.example.com",
                ["EnableReconnection"] = true
            },
            Inputs = new List<object>(),
            Outputs = new List<object>(),
            Tags = new List<string> { "test", "socketio" },
            Metadata = new Dictionary<string, object>
            {
                ["CreatedBy"] = "UnitTest"
            }
        };

        // Act
        await repository.SaveConnectionAsync(connection);

        // Assert
        var savedConnection = await repository.GetConnectionByIdAsync(connection.Id);
        Assert.NotNull(savedConnection);
        Assert.Equal(connection.Name, savedConnection.Name);
        Assert.Equal(connection.ConnectionType, savedConnection.ConnectionType);
        Assert.Equal(connection.IsEnabled, savedConnection.IsEnabled);
        Assert.Equal(connection.AutoStart, savedConnection.AutoStart);
        Assert.Equal(connection.Description, savedConnection.Description);
    }

    [Fact]
    public async Task GetAllConnectionsAsync_ShouldReturnPersistedConnections()
    {
        // Clean database before test
        await ClearDatabaseAsync();
        // Arrange
        var repository = _serviceProvider.GetRequiredService<IConnectionConfigurationRepository>();
        var connection1 = new ConnectionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Connection 1",
            ConnectionType = "SocketIO",
            IsEnabled = true,
            AutoStart = false,
            ConnectionConfig = new Dictionary<string, object>(),
            Inputs = new List<object>(),
            Outputs = new List<object>(),
            Tags = new List<string>(),
            Metadata = new Dictionary<string, object>()
        };
        
        var connection2 = new ConnectionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Connection 2",
            ConnectionType = "MQTT",
            IsEnabled = false,
            AutoStart = true,
            ConnectionConfig = new Dictionary<string, object>(),
            Inputs = new List<object>(),
            Outputs = new List<object>(),
            Tags = new List<string>(),
            Metadata = new Dictionary<string, object>()
        };

        // Act
        await repository.SaveConnectionAsync(connection1);
        await repository.SaveConnectionAsync(connection2);

        var allConnections = await repository.GetAllConnectionsAsync();

        // Assert
        Assert.Equal(2, allConnections.Count());
        Assert.Contains(allConnections, c => c.Id == connection1.Id);
        Assert.Contains(allConnections, c => c.Id == connection2.Id);
    }

    [Fact]
    public async Task GetAutoStartConnectionsAsync_ShouldReturnOnlyAutoStartConnections()
    {
        // Clean database before test
        await ClearDatabaseAsync();
        // Arrange
        var repository = _serviceProvider.GetRequiredService<IConnectionConfigurationRepository>();
        var autoStartConnection = new ConnectionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Auto Start Connection",
            ConnectionType = "SocketIO",
            IsEnabled = true,
            AutoStart = true,
            ConnectionConfig = new Dictionary<string, object>(),
            Inputs = new List<object>(),
            Outputs = new List<object>(),
            Tags = new List<string>(),
            Metadata = new Dictionary<string, object>()
        };
        
        var manualConnection = new ConnectionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Manual Connection",
            ConnectionType = "MQTT",
            IsEnabled = true,
            AutoStart = false,
            ConnectionConfig = new Dictionary<string, object>(),
            Inputs = new List<object>(),
            Outputs = new List<object>(),
            Tags = new List<string>(),
            Metadata = new Dictionary<string, object>()
        };

        // Act
        await repository.SaveConnectionAsync(autoStartConnection);
        await repository.SaveConnectionAsync(manualConnection);

        var autoStartConnections = await repository.GetAutoStartConnectionsAsync();

        // Assert
        Assert.Single(autoStartConnections);
        Assert.Equal(autoStartConnection.Id, autoStartConnections.First().Id);
    }

    [Fact]
    public async Task DeleteConnectionAsync_ShouldRemovePersistedConnection()
    {
        // Clean database before test
        await ClearDatabaseAsync();
        // Arrange
        var repository = _serviceProvider.GetRequiredService<IConnectionConfigurationRepository>();
        var connection = new ConnectionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Connection to Delete",
            ConnectionType = "SocketIO",
            IsEnabled = true,
            AutoStart = false,
            ConnectionConfig = new Dictionary<string, object>(),
            Inputs = new List<object>(),
            Outputs = new List<object>(),
            Tags = new List<string>(),
            Metadata = new Dictionary<string, object>()
        };

        await repository.SaveConnectionAsync(connection);

        // Act
        await repository.DeleteConnectionAsync(connection.Id);

        // Assert
        var deletedConnection = await repository.GetConnectionByIdAsync(connection.Id);
        Assert.Null(deletedConnection);
    }

    private async Task ClearDatabaseAsync()
    {
        using var context = await _serviceProvider.GetRequiredService<IDbContextFactory<UNSInfraDbContext>>().CreateDbContextAsync();
        await context.ConnectionConfigurations.ExecuteDeleteAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.GetService<IServiceScope>()?.Dispose();
    }
}