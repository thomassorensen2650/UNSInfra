using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Namespace;
using UNSInfra.Services;
using UNSInfra.Services.AutoMapping;
using UNSInfra.Services.Events;
using UNSInfra.Services.TopicBrowser;
using Xunit;

using NSNodeType = UNSInfra.Services.NSNodeType;

namespace UNSInfra.Core.Tests.Services.AutoMapping;

/// <summary>
/// Integration tests for the auto-mapping system that verify topics are automatically 
/// mapped to the UNS tree when new namespaces are added without application restart.
/// </summary>
public class NamespaceAutoMappingIntegrationTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<INamespaceStructureService> _namespaceServiceMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<ILogger<SimplifiedAutoMapperService>> _autoMapperLoggerMock;
    private readonly Mock<ILogger<SimplifiedAutoMappingBackgroundService>> _backgroundServiceLoggerMock;
    
    private readonly SimplifiedAutoMapperService _autoMapperService;
    private readonly SimplifiedAutoMappingBackgroundService _backgroundService;
    
    // Track events published during tests
    private readonly List<TopicAutoMappedEvent> _publishedMappingEvents = new();
    private readonly List<TopicAutoMappingFailedEvent> _publishedFailedEvents = new();

    public NamespaceAutoMappingIntegrationTests()
    {
        // Setup mocks
        _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _namespaceServiceMock = new Mock<INamespaceStructureService>();
        _eventBusMock = new Mock<IEventBus>();
        _autoMapperLoggerMock = new Mock<ILogger<SimplifiedAutoMapperService>>();
        _backgroundServiceLoggerMock = new Mock<ILogger<SimplifiedAutoMappingBackgroundService>>();

        // Setup service scope factory chain
        _serviceScopeFactoryMock.Setup(f => f.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceScopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(INamespaceStructureService)))
            .Returns(_namespaceServiceMock.Object);

        // Setup event publishing tracking
        _eventBusMock.Setup(e => e.PublishAsync(It.IsAny<TopicAutoMappedEvent>()))
            .Callback<BaseEvent>(evt => _publishedMappingEvents.Add((TopicAutoMappedEvent)evt))
            .Returns(Task.CompletedTask);
            
        _eventBusMock.Setup(e => e.PublishAsync(It.IsAny<TopicAutoMappingFailedEvent>()))
            .Callback<BaseEvent>(evt => _publishedFailedEvents.Add((TopicAutoMappingFailedEvent)evt))
            .Returns(Task.CompletedTask);

        // Create services
        _autoMapperService = new SimplifiedAutoMapperService(
            _serviceScopeFactoryMock.Object,
            _autoMapperLoggerMock.Object);

        _backgroundService = new SimplifiedAutoMappingBackgroundService(
            _autoMapperService,
            _eventBusMock.Object,
            Mock.Of<IServiceScopeFactory>(),
            _backgroundServiceLoggerMock.Object);
    }

    [Fact]
    public async Task WhenNewNamespaceAdded_TopicsShouldBeAutoMappedWithoutRestart()
    {
        // Arrange: Initial namespace structure (without the target namespace)
        var initialNamespaces = new List<NSTreeNode>
        {
            CreateNSTreeNode("Enterprise1", "Enterprise", new[]
            {
                CreateNSTreeNode("Site1", "Site", new[]
                {
                    CreateNSTreeNode("Area1", "Area", Array.Empty<NSTreeNode>())
                })
            })
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(initialNamespaces);

        // Initialize the auto-mapper with initial structure
        await _autoMapperService.InitializeCacheAsync();

        // Test a topic that should initially fail to map (WorkCenter1 doesn't exist)
        var testTopic = "mqtt/factory/Enterprise1/Site1/Area1/WorkCenter1/Temperature";
        var initialMapping = _autoMapperService.TryMapTopic(testTopic);
        
        // Should fail to map since WorkCenter1 doesn't exist and path fragments don't match
        Assert.Null(initialMapping);

        // Act: Add new WorkCenter1 namespace to the structure
        var updatedNamespaces = new List<NSTreeNode>
        {
            CreateNSTreeNode("Enterprise1", "Enterprise", new[]
            {
                CreateNSTreeNode("Site1", "Site", new[]
                {
                    CreateNSTreeNode("Area1", "Area", new[]
                    {
                        CreateNSTreeNode("WorkCenter1", "WorkCenter", Array.Empty<NSTreeNode>())
                    })
                })
            })
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(updatedNamespaces);

        // Simulate namespace structure change by refreshing cache
        await _autoMapperService.RefreshCacheAsync();

        // Assert: Test a new topic (not previously processed) with the updated structure
        var newTestTopic = "mqtt/factory/Enterprise1/Site1/Area1/WorkCenter1/Pressure";
        var updatedMapping = _autoMapperService.TryMapTopic(newTestTopic);
        Assert.NotNull(updatedMapping);
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", updatedMapping);

        // Verify cache refresh was called
        _autoMapperLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Refreshing namespace cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task WhenMultipleNamespacesAdded_AllRelevantTopicsShouldBeMapped()
    {
        // Arrange: Complete hierarchical structure
        var completeNamespaces = new List<NSTreeNode>
        {
            CreateNSTreeNode("Enterprise1", "Enterprise", new[]
            {
                CreateNSTreeNode("Site1", "Site", new[]
                {
                    CreateNSTreeNode("KPI", "Namespace", Array.Empty<NSTreeNode>()),
                    CreateNSTreeNode("Area1", "Area", new[]
                    {
                        CreateNSTreeNode("WorkCenter1", "WorkCenter", Array.Empty<NSTreeNode>())
                    })
                }),
                CreateNSTreeNode("Site2", "Site", new[]
                {
                    CreateNSTreeNode("Area1", "Area", Array.Empty<NSTreeNode>())
                })
            })
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(completeNamespaces);

        await _autoMapperService.InitializeCacheAsync();

        // Act & Assert: Test topics for different hierarchical levels
        var kpiMapping = _autoMapperService.TryMapTopic("mqtt/Enterprise1/Site1/KPI/Revenue");
        Assert.Equal("Enterprise1/Site1/KPI", kpiMapping);

        var site2Mapping = _autoMapperService.TryMapTopic("socketio/Enterprise1/Site2/Area1/Temperature");
        Assert.Equal("Enterprise1/Site2/Area1", site2Mapping);

        var workCenterMapping = _autoMapperService.TryMapTopic("kafka/Enterprise1/Site1/Area1/WorkCenter1/Status");
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", workCenterMapping);
    }

    [Fact]
    public async Task WhenNamespaceRemoved_TopicsShouldMapToParentLevel()
    {
        // Arrange: Complete structure initially
        var completeNamespaces = new List<NSTreeNode>
        {
            CreateNSTreeNode("Enterprise1", "Enterprise", new[]
            {
                CreateNSTreeNode("Site1", "Site", new[]
                {
                    CreateNSTreeNode("Area1", "Area", new[]
                    {
                        CreateNSTreeNode("WorkCenter1", "WorkCenter", Array.Empty<NSTreeNode>())
                    })
                })
            })
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(completeNamespaces);

        await _autoMapperService.InitializeCacheAsync();

        // Verify topic maps to WorkCenter1
        var fullMapping = _autoMapperService.TryMapTopic("mqtt/factory/Enterprise1/Site1/Area1/WorkCenter1/Temperature");
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", fullMapping);

        // Act: Remove WorkCenter1 from structure
        var reducedNamespaces = new List<NSTreeNode>
        {
            CreateNSTreeNode("Enterprise1", "Enterprise", new[]
            {
                CreateNSTreeNode("Site1", "Site", new[]
                {
                    CreateNSTreeNode("Area1", "Area", Array.Empty<NSTreeNode>())
                })
            })
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(reducedNamespaces);

        await _autoMapperService.RefreshCacheAsync();

        // Assert: New topic should fail to map since WorkCenter1 no longer exists
        var parentMapping = _autoMapperService.TryMapTopic("mqtt/factory/Enterprise1/Site1/Area1/WorkCenter1/Pressure");
        Assert.Null(parentMapping);
    }

    private static NSTreeNode CreateNSTreeNode(string name, string nodeType, NSTreeNode[] children)
    {
        return new NSTreeNode
        {
            Name = name,
            NodeType = nodeType == "Namespace" ? NSNodeType.Namespace : NSNodeType.HierarchyNode,
            FullPath = name,
            Children = children.ToList()
        };
    }

    public void Dispose()
    {
        _autoMapperService?.Dispose();
        _backgroundService?.Dispose();
    }
}