using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Configuration;
using UNSInfra.Services.SocketIO.Configuration;
using UNSInfra.Services.V1.Configuration;
using UNSInfra.Storage.SQLite;
using UNSInfra.Storage.SQLite.Repositories;
using Xunit;

namespace UNSInfra.Core.Tests.Repositories;

/// <summary>
/// Tests for verifying that data ingestion configurations are properly persisted to the database.
/// </summary>
public class DataIngestionConfigurationPersistenceTests : IDisposable
{
    private readonly UNSInfraDbContext _context;
    private readonly SQLiteDataIngestionConfigurationRepository _repository;
    private readonly ILogger<SQLiteDataIngestionConfigurationRepository> _logger;

    public DataIngestionConfigurationPersistenceTests()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<UNSInfraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new UNSInfraDbContext(options);
        _context.Database.EnsureCreated();

        // Create logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SQLiteDataIngestionConfigurationRepository>();

        // Create repository with in-memory context factory
        var contextFactory = new TestDbContextFactory(options);
        _repository = new SQLiteDataIngestionConfigurationRepository(contextFactory, _logger);
    }

    [Fact]
    public async Task SaveConfigurationAsync_SocketIOConfiguration_ShouldPersistToDatabase()
    {
        // Arrange
        var config = new SocketIODataIngestionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test SocketIO",
            Description = "Test configuration",
            Enabled = true,
            CreatedBy = "test-user",
            ServerUrl = "https://test.example.com:3000",
            ConnectionTimeoutSeconds = 10,
            EnableReconnection = true,
            ReconnectionAttempts = 5,
            ReconnectionDelaySeconds = 2,
            EventNames = new[] { "update", "data" },
            BaseTopicPath = "test",
            EnableDetailedLogging = true
        };

        // Act
        var savedConfig = await _repository.SaveConfigurationAsync(config);

        // Assert
        Assert.NotNull(savedConfig);
        Assert.Equal(config.Id, savedConfig.Id);
        Assert.Equal(config.Name, savedConfig.Name);
        Assert.Equal(config.ServiceType, savedConfig.ServiceType);

        // Verify it was actually saved to database
        var retrievedConfig = await _repository.GetConfigurationAsync(config.Id);
        Assert.NotNull(retrievedConfig);
        Assert.Equal(config.Id, retrievedConfig.Id);
        Assert.Equal(config.Name, retrievedConfig.Name);
        Assert.IsType<SocketIODataIngestionConfiguration>(retrievedConfig);
    }

    [Fact]
    public async Task SaveConfigurationAsync_MqttConfiguration_ShouldPersistToDatabase()
    {
        // Arrange
        var config = new MqttDataIngestionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test MQTT",
            Description = "Test MQTT configuration",
            Enabled = false,
            CreatedBy = "test-user",
            BrokerHost = "test.mosquitto.org",
            BrokerPort = 1883,
            UseTls = false,
            ClientId = "test-client",
            Username = "",
            Password = "",
            KeepAliveInterval = 60,
            ConnectionTimeout = 30,
            CleanSession = true
        };

        // Act
        var savedConfig = await _repository.SaveConfigurationAsync(config);

        // Assert
        Assert.NotNull(savedConfig);
        Assert.Equal(config.Id, savedConfig.Id);
        Assert.Equal(config.Name, savedConfig.Name);
        Assert.Equal(config.ServiceType, savedConfig.ServiceType);

        // Verify it was actually saved to database
        var retrievedConfig = await _repository.GetConfigurationAsync(config.Id);
        Assert.NotNull(retrievedConfig);
        Assert.Equal(config.Id, retrievedConfig.Id);
        Assert.Equal(config.Name, retrievedConfig.Name);
        Assert.IsType<MqttDataIngestionConfiguration>(retrievedConfig);
    }

    [Fact]
    public async Task GetAllConfigurationsAsync_WithMultipleConfigurations_ShouldReturnAll()
    {
        // Arrange
        var socketIOConfig = new SocketIODataIngestionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "SocketIO Config",
            Enabled = true,
            CreatedBy = "test",
            ServerUrl = "https://test.com",
            EventNames = new[] { "update" },
            BaseTopicPath = "test"
        };

        var mqttConfig = new MqttDataIngestionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "MQTT Config",
            Enabled = false,
            CreatedBy = "test",
            BrokerHost = "localhost",
            BrokerPort = 1883
        };

        await _repository.SaveConfigurationAsync(socketIOConfig);
        await _repository.SaveConfigurationAsync(mqttConfig);

        // Act
        var allConfigs = await _repository.GetAllConfigurationsAsync();

        // Assert
        Assert.Equal(2, allConfigs.Count);
        Assert.Contains(allConfigs, c => c.Id == socketIOConfig.Id);
        Assert.Contains(allConfigs, c => c.Id == mqttConfig.Id);
    }

    [Fact]
    public async Task GetEnabledConfigurationsAsync_ShouldReturnOnlyEnabledConfigurations()
    {
        // Arrange
        var enabledConfig = new SocketIODataIngestionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Enabled Config",
            Enabled = true,
            CreatedBy = "test",
            ServerUrl = "https://test.com",
            EventNames = new[] { "update" },
            BaseTopicPath = "test"
        };

        var disabledConfig = new MqttDataIngestionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Disabled Config",
            Enabled = false,
            CreatedBy = "test",
            BrokerHost = "localhost",
            BrokerPort = 1883
        };

        await _repository.SaveConfigurationAsync(enabledConfig);
        await _repository.SaveConfigurationAsync(disabledConfig);

        // Act
        var enabledConfigs = await _repository.GetEnabledConfigurationsAsync();

        // Assert
        Assert.Single(enabledConfigs);
        Assert.Equal(enabledConfig.Id, enabledConfigs[0].Id);
        Assert.True(enabledConfigs[0].Enabled);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldRemoveFromDatabase()
    {
        // Arrange
        var config = new SocketIODataIngestionConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "To Delete",
            Enabled = true,
            CreatedBy = "test",
            ServerUrl = "https://test.com",
            EventNames = new[] { "update" },
            BaseTopicPath = "test"
        };

        await _repository.SaveConfigurationAsync(config);

        // Verify it exists
        var retrievedConfig = await _repository.GetConfigurationAsync(config.Id);
        Assert.NotNull(retrievedConfig);

        // Act
        var deleted = await _repository.DeleteConfigurationAsync(config.Id);

        // Assert
        Assert.True(deleted);
        var deletedConfig = await _repository.GetConfigurationAsync(config.Id);
        Assert.Null(deletedConfig);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

/// <summary>
/// Test implementation of IDbContextFactory for unit tests.
/// </summary>
internal class TestDbContextFactory : IDbContextFactory<UNSInfraDbContext>
{
    private readonly DbContextOptions<UNSInfraDbContext> _options;

    public TestDbContextFactory(DbContextOptions<UNSInfraDbContext> options)
    {
        _options = options;
    }

    public UNSInfraDbContext CreateDbContext()
    {
        return new UNSInfraDbContext(_options);
    }

    public Task<UNSInfraDbContext> CreateDbContextAsync()
    {
        return Task.FromResult(new UNSInfraDbContext(_options));
    }
}