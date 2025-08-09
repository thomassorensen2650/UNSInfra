using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Repositories;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.UI.Services;
using UNSInfra.Validation;
using UNSInfra.Core;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Services;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Core.Configuration;
using UNSInfra.Core.Services;
using UNSInfra.Core.Repositories;
using Moq;

namespace UNSInfra.UI.Tests;

public class UITestContext : TestContext
{
    public UITestContext()
    {
        SetupMockServices();
    }

    private void SetupMockServices()
    {
        // Mock logging
        var mockLogger = new Mock<ILogger<object>>();
        Services.AddSingleton(typeof(ILogger<>), typeof(MockLogger<>));

        // Mock repositories
        var mockSchemaRepository = new Mock<ISchemaRepository>();
        Services.AddSingleton(mockSchemaRepository.Object);
        
        var mockHierarchyRepository = new Mock<UNSInfra.Repositories.IHierarchyConfigurationRepository>();
        Services.AddSingleton(mockHierarchyRepository.Object);

        // Mock services
        var mockTopicBrowserService = new Mock<ITopicBrowserService>();
        Services.AddSingleton(mockTopicBrowserService.Object);

        var mockSchemaValidator = new Mock<ISchemaValidator>();
        Services.AddSingleton(mockSchemaValidator.Object);

        var mockInMemoryLogService = new Mock<IInMemoryLogService>();
        Services.AddSingleton(mockInMemoryLogService.Object);

        // MCP service removed from UI project

        // Add JSRuntime mock
        Services.AddSingleton(JSInterop.JSRuntime);

        // Mock storage services for NSTreeEditor
        var mockRealtimeStorage = new Mock<IRealtimeStorage>();
        Services.AddSingleton(mockRealtimeStorage.Object);
        
        var mockHistoricalStorage = new Mock<IHistoricalStorage>();
        Services.AddSingleton(mockHistoricalStorage.Object);

        // Mock data ingestion service
        var mockDataIngestionService = new Mock<IDataIngestionService>();
        Services.AddSingleton(mockDataIngestionService.Object);

        // Mock namespace structure service
        var mockNamespaceStructureService = new Mock<INamespaceStructureService>();
        Services.AddSingleton(mockNamespaceStructureService.Object);

        // Mock additional repositories needed for Settings component
        var mockTopicConfigurationRepository = new Mock<UNSInfra.Repositories.ITopicConfigurationRepository>();
        Services.AddSingleton(mockTopicConfigurationRepository.Object);
        
        var mockNamespaceConfigurationRepository = new Mock<UNSInfra.Repositories.INamespaceConfigurationRepository>();
        Services.AddSingleton(mockNamespaceConfigurationRepository.Object);

        // Mock data ingestion configuration repository
        var mockConfigurationRepository = new Mock<IDataIngestionConfigurationRepository>();
        mockConfigurationRepository.Setup(x => x.GetAllConfigurationsAsync())
            .ReturnsAsync(new List<IDataIngestionConfiguration>());
        Services.AddSingleton(mockConfigurationRepository.Object);

        // Mock data ingestion service manager  
        var mockServiceManager = new Mock<IDataIngestionServiceManager>();
        mockServiceManager.Setup(x => x.GetServicesStatus())
            .Returns(new Dictionary<string, ServiceStatus>());
        mockServiceManager.Setup(x => x.GetAvailableServiceTypes())
            .Returns(new List<IDataIngestionServiceDescriptor>());
        Services.AddSingleton(mockServiceManager.Object);
    }
}

public class MockLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

