using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Storage.SQLite.Extensions;
using Xunit;

namespace UNSInfra.Core.Tests.Services;

public class RepositoryDiagnosticTests
{
    [Fact]
    public void ConfigureStorage_WithSQLiteProvider_ShouldRegisterSQLiteRepository()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Create configuration with SQLite provider
        var configData = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "SQLite",
            ["Storage:ConnectionString"] = "",
            ["Storage:EnableWalMode"] = "true",
            ["Storage:CommandTimeoutSeconds"] = "30",
            ["Storage:CacheSize"] = "1000"
        };
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        
        services.AddLogging();
        
        // Act
        services.AddConfigurableStorage(configuration);
        var serviceProvider = services.BuildServiceProvider();
        
        // Assert
        var repository = serviceProvider.GetService<IConnectionConfigurationRepository>();
        Assert.NotNull(repository);
        
        // Check if it's the SQLite repository type
        var repositoryType = repository.GetType();
        Assert.Equal("SQLiteConnectionConfigurationRepository", repositoryType.Name);
    }
    
    [Fact]
    public void ConfigureStorage_WithInMemoryProvider_ShouldRegisterInMemoryRepository()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Create configuration with InMemory provider
        var configData = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "InMemory"
        };
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        
        services.AddLogging();
        
        // Act
        services.AddConfigurableStorage(configuration);
        var serviceProvider = services.BuildServiceProvider();
        
        // Assert
        var repository = serviceProvider.GetService<IConnectionConfigurationRepository>();
        Assert.NotNull(repository);
        
        // Check if it's the InMemory repository type
        var repositoryType = repository.GetType();
        Assert.Equal("InMemoryConnectionConfigurationRepository", repositoryType.Name);
    }
}