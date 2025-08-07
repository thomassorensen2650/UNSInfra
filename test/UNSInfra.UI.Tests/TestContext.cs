using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Repositories;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.UI.Services;
using UNSInfra.Validation;
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

        // Mock MCP service - create a simple mock implementation for testing
        var mockMcpService = new TestMcpServerBackgroundService();
        Services.AddSingleton<UNSInfra.UI.Services.McpServerBackgroundService>(mockMcpService);

        // Add JSRuntime mock
        Services.AddSingleton(JSInterop.JSRuntime);
    }
}

public class MockLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

public class TestMcpServerBackgroundService : UNSInfra.UI.Services.McpServerBackgroundService
{
    public TestMcpServerBackgroundService() 
        : base(Mock.Of<IServiceProvider>(), Mock.Of<ILogger<UNSInfra.UI.Services.McpServerBackgroundService>>()) 
    {
        // Override properties for testing
    }

    // Override the background service execution to do nothing in tests
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}