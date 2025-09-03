using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services;
using UNSInfra.Services.AutoMapping;
using UNSInfra.Services.Events;
using UNSInfra.Services.TopicBrowser;
using Xunit;

namespace UNSInfra.Core.Tests.Services.AutoMapping;

public class SimplifiedAutoMappingBackgroundServiceTests : IDisposable
{
    private readonly Mock<SimplifiedAutoMapperService> _autoMapperMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<ILogger<SimplifiedAutoMappingBackgroundService>> _loggerMock;
    private readonly SimplifiedAutoMappingBackgroundService _backgroundService;

    public SimplifiedAutoMappingBackgroundServiceTests()
    {
        // Create mock with public constructor parameters
        _autoMapperMock = new Mock<SimplifiedAutoMapperService>(
            Mock.Of<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(), 
            Mock.Of<ILogger<SimplifiedAutoMapperService>>()
        );
        _eventBusMock = new Mock<IEventBus>();
        _loggerMock = new Mock<ILogger<SimplifiedAutoMappingBackgroundService>>();

        _backgroundService = new SimplifiedAutoMappingBackgroundService(
            _autoMapperMock.Object,
            _eventBusMock.Object,
            Mock.Of<IServiceScopeFactory>(),
            _loggerMock.Object);
    }

    [Fact]
    public async Task StartAsync_InitializesCacheAndSubscribesToEvents()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        await _backgroundService.StartAsync(cancellationToken);

        // Assert
        _autoMapperMock.Verify(m => m.InitializeCacheAsync(), Times.Once);
        _eventBusMock.Verify(e => e.Subscribe<TopicAddedEvent>(It.IsAny<Func<TopicAddedEvent, Task>>()), Times.Once);
        _eventBusMock.Verify(e => e.Subscribe<NamespaceStructureChangedEvent>(It.IsAny<Func<NamespaceStructureChangedEvent, Task>>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromEvents()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        await _backgroundService.StartAsync(cancellationToken);

        // Act
        await _backgroundService.StopAsync(cancellationToken);

        // Assert
        _eventBusMock.Verify(e => e.Unsubscribe<TopicAddedEvent>(It.IsAny<Func<TopicAddedEvent, Task>>()), Times.Once);
        _eventBusMock.Verify(e => e.Unsubscribe<NamespaceStructureChangedEvent>(It.IsAny<Func<NamespaceStructureChangedEvent, Task>>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Assert
        Assert.NotNull(_backgroundService);
    }

    [Fact]
    public async Task OnNamespaceStructureChanged_RefreshesCache()
    {
        // Arrange
        var namespaceEvent = new NamespaceStructureChangedEvent(
            ChangedNamespace: "Enterprise1/Area1",
            ChangeType: "Modified",
            ChangedBy: "TestUser"
        );

        await _backgroundService.StartAsync(CancellationToken.None);

        // Get the registered handler
        Func<NamespaceStructureChangedEvent, Task>? namespaceHandler = null;
        _eventBusMock.Setup(e => e.Subscribe<NamespaceStructureChangedEvent>(It.IsAny<Func<NamespaceStructureChangedEvent, Task>>()))
            .Callback<Func<NamespaceStructureChangedEvent, Task>>(handler => namespaceHandler = handler);

        await _backgroundService.StartAsync(CancellationToken.None);

        // Act
        if (namespaceHandler != null)
        {
            await namespaceHandler(namespaceEvent);
        }

        // Assert
        _autoMapperMock.Verify(m => m.RefreshCacheAsync(), Times.Once);
    }

    [Fact]
    public async Task Service_CanStartAndStopSuccessfully()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act & Assert - Should not throw
        await _backgroundService.StartAsync(cancellationToken);
        await _backgroundService.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task EventHandlers_CanProcessEvents_WithoutExceptions()
    {
        // Arrange
        var topicEvent = new TopicAddedEvent(
            Topic: "socket/test/topic",
            Path: new HierarchicalPath(),
            SourceType: "SocketIO",
            CreatedAt: DateTime.UtcNow
        );

        var namespaceEvent = new NamespaceStructureChangedEvent(
            ChangedNamespace: "Enterprise1",
            ChangeType: "Added",
            ChangedBy: "TestUser"
        );

        await _backgroundService.StartAsync(CancellationToken.None);

        // Get the registered handlers
        Func<TopicAddedEvent, Task>? topicHandler = null;
        Func<NamespaceStructureChangedEvent, Task>? namespaceHandler = null;

        _eventBusMock.Setup(e => e.Subscribe<TopicAddedEvent>(It.IsAny<Func<TopicAddedEvent, Task>>()))
            .Callback<Func<TopicAddedEvent, Task>>(handler => topicHandler = handler);
        
        _eventBusMock.Setup(e => e.Subscribe<NamespaceStructureChangedEvent>(It.IsAny<Func<NamespaceStructureChangedEvent, Task>>()))
            .Callback<Func<NamespaceStructureChangedEvent, Task>>(handler => namespaceHandler = handler);

        await _backgroundService.StartAsync(CancellationToken.None);

        // Act & Assert - Should not throw
        if (topicHandler != null)
        {
            await topicHandler(topicEvent); // Should complete without exception
        }
        
        if (namespaceHandler != null)
        {
            await namespaceHandler(namespaceEvent);
        }

        // Verify cache refresh was called
        _autoMapperMock.Verify(m => m.RefreshCacheAsync(), Times.Once);
    }

    public void Dispose()
    {
        _backgroundService?.Dispose();
    }
}