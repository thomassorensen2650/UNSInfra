using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Models;
using UNSInfra.Storage.SQLite;
using UNSInfra.Storage.SQLite.Repositories;
using Xunit;

namespace UNSInfra.Core.Tests.Services;

/// <summary>
/// Simple test to validate that connection persistence works correctly
/// </summary>
public class ConnectionPersistenceValidationTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _testDbPath;

    public ConnectionPersistenceValidationTest()
    {
        // Create a unique test database path
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_unsinfra_{Guid.NewGuid()}.db");
        
        var services = new ServiceCollection();
        
        // Configure SQLite database for testing with a real file
        services.AddDbContextFactory<UNSInfraDbContext>(options =>
            options.UseSqlite($"Data Source={_testDbPath};Cache=Shared;Pooling=True;"));
        
        services.AddScoped<IConnectionConfigurationRepository, SQLiteConnectionConfigurationRepository>();
        services.AddLogging(builder => builder.AddConsole());

        _serviceProvider = services.BuildServiceProvider();
        
        // Create database schema
        using var scope = _serviceProvider.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<UNSInfraDbContext>>();
        using var context = contextFactory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task ConnectionRepository_SaveAndRetrieve_ShouldWork()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConnectionConfigurationRepository>();
        
        var testConnection = new ConnectionConfiguration
        {
            Id = "test-connection-123",
            Name = "Test SocketIO Connection",
            ConnectionType = "SocketIO",
            IsEnabled = true,
            AutoStart = false,
            Description = "Test connection for validation",
            ConnectionConfig = new Dictionary<string, object>
            {
                ["ServerUrl"] = "https://test.example.com",
                ["EnableReconnection"] = true
            },
            Inputs = new List<object>(),
            Outputs = new List<object>(),
            Tags = new List<string> { "test" },
            Metadata = new Dictionary<string, object>()
        };

        // Act - Save connection
        await repository.SaveConnectionAsync(testConnection);

        // Assert - Retrieve and verify
        var retrievedConnection = await repository.GetConnectionByIdAsync(testConnection.Id);
        Assert.NotNull(retrievedConnection);
        Assert.Equal(testConnection.Name, retrievedConnection.Name);
        Assert.Equal(testConnection.ConnectionType, retrievedConnection.ConnectionType);
        Assert.Equal(testConnection.Description, retrievedConnection.Description);
        
        // Verify that the connection config was properly serialized/deserialized
        Assert.NotNull(retrievedConnection.ConnectionConfig);
        if (retrievedConnection.ConnectionConfig is Dictionary<string, object> configDict)
        {
            Assert.Equal("https://test.example.com", configDict["ServerUrl"].ToString());
        }
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        
        // Clean up test database file
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }
}