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

using NSNodeType = UNSInfra.Services.NSNodeType;

namespace UNSInfra.Core.Tests.Services.AutoMapping;

/// <summary>
/// Integration test that verifies the UI updates correctly when an unmappable topic 
/// is republished after adding a namespace mapping.
/// </summary>
public class TopicRemappingUIIntegrationTest : IDisposable
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
    
    // Track published events to simulate UI updates
    private readonly List<TopicAutoMappedEvent> _publishedMappingEvents = new();
    private readonly List<TopicAutoMappingFailedEvent> _publishedFailedEvents = new();

    public TopicRemappingUIIntegrationTest()
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

        // Setup event publishing tracking to simulate UI responses
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
            _backgroundServiceLoggerMock.Object);
    }

    [Fact]
    public async Task WhenTopicCannotBeAutoMapped_ThenNamespaceAdded_ThenTopicRepublished_UIShouldUpdateCorrectly()
    {
        // Arrange: Initial namespace structure WITHOUT WorkCenter2
        var initialNamespaces = new List<NSTreeNode>
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
            .ReturnsAsync(initialNamespaces);

        await _autoMapperService.InitializeCacheAsync();

        // Act 1: Publish a topic that CANNOT be auto-mapped (WorkCenter2 doesn't exist)
        var unmappableTopic = "mqtt/factory/Enterprise1/Site1/Area1/WorkCenter2/Temperature";
        var initialMapping = _autoMapperService.TryMapTopic(unmappableTopic);
        
        // Assert 1: Topic should not map since WorkCenter2 doesn't exist and Area1/WorkCenter2 path doesn't match existing paths
        Assert.Null(initialMapping); // The partial path won't match existing full paths

        // Clear events to track what happens next
        _publishedMappingEvents.Clear();
        _publishedFailedEvents.Clear();

        // Act 2: Add WorkCenter2 namespace to the structure (simulating UI action)
        var updatedNamespaces = new List<NSTreeNode>
        {
            CreateNSTreeNode("Enterprise1", "Enterprise", new[]
            {
                CreateNSTreeNode("Site1", "Site", new[]
                {
                    CreateNSTreeNode("Area1", "Area", new[]
                    {
                        CreateNSTreeNode("WorkCenter1", "WorkCenter", Array.Empty<NSTreeNode>()),
                        CreateNSTreeNode("WorkCenter2", "WorkCenter", Array.Empty<NSTreeNode>())
                    })
                })
            })
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(updatedNamespaces);

        // Simulate namespace structure change event
        await _autoMapperService.RefreshCacheAsync();

        // Act 3: Republish the same topic (simulating data republishing)
        var updatedMapping = _autoMapperService.TryMapTopic(unmappableTopic);
        
        // Assert 3: Topic should now map to the correct WorkCenter2
        Assert.NotNull(updatedMapping);
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter2", updatedMapping);

        // Act 4: Simulate the background service processing a TopicAddedEvent for this newly mappable topic
        var topicAddedEvent = new TopicAddedEvent(
            Topic: unmappableTopic,
            Path: new HierarchicalPath(),  // Empty path since it wasn't mappable initially
            SourceType: "MQTT",
            CreatedAt: DateTime.UtcNow
        );

        // Use reflection to call the private OnTopicAdded method to simulate event processing
        var onTopicAddedMethod = typeof(SimplifiedAutoMappingBackgroundService)
            .GetMethod("OnTopicAdded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(onTopicAddedMethod);
        await (Task)onTopicAddedMethod.Invoke(_backgroundService, new object[] { topicAddedEvent })!;

        // Allow background service to process
        await Task.Delay(100);

        // Assert 4: Verify cache refresh was performed
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
    public async Task WhenMultipleTopicsCannotBeAutoMapped_ThenNamespaceAdded_AllTopicsShouldRemapCorrectly()
    {
        // Arrange: Structure missing multiple namespaces
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

        await _autoMapperService.InitializeCacheAsync();

        // Act 1: Try to map multiple topics that cannot be fully mapped
        var topics = new[]
        {
            "mqtt/Enterprise1/Site1/Area1/WorkCenter1/Temperature",
            "mqtt/Enterprise1/Site1/Area1/WorkCenter2/Pressure", 
            "mqtt/Enterprise1/Site1/Area1/WorkCenter3/Humidity"
        };

        var initialMappings = topics.Select(topic => _autoMapperService.TryMapTopic(topic)).ToList();
        
        // Assert 1: All should fail to map since WorkCenters don't exist
        Assert.All(initialMappings, mapping => Assert.Null(mapping));

        // Act 2: Add missing WorkCenters
        var completeNamespaces = new List<NSTreeNode>
        {
            CreateNSTreeNode("Enterprise1", "Enterprise", new[]
            {
                CreateNSTreeNode("Site1", "Site", new[]
                {
                    CreateNSTreeNode("Area1", "Area", new[]
                    {
                        CreateNSTreeNode("WorkCenter1", "WorkCenter", Array.Empty<NSTreeNode>()),
                        CreateNSTreeNode("WorkCenter2", "WorkCenter", Array.Empty<NSTreeNode>()),
                        CreateNSTreeNode("WorkCenter3", "WorkCenter", Array.Empty<NSTreeNode>())
                    })
                })
            })
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(completeNamespaces);

        await _autoMapperService.RefreshCacheAsync();

        // Act 3: Remap all topics
        var updatedMappings = topics.Select(topic => _autoMapperService.TryMapTopic(topic)).ToList();

        // Assert 3: All should now map to their specific WorkCenters
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", updatedMappings[0]);
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter2", updatedMappings[1]);
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter3", updatedMappings[2]);
    }

    [Fact]
    public async Task WhenNamespaceIsAdded_ExistingTopicsShouldRemainMappedCorrectly()
    {
        // Arrange: Structure with some existing namespaces
        var initialNamespaces = new List<NSTreeNode>
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
            .ReturnsAsync(initialNamespaces);

        await _autoMapperService.InitializeCacheAsync();

        // Act 1: Map topics - one that can be mapped, one that cannot
        var mappableTopic = "mqtt/Enterprise1/Site1/Area1/WorkCenter1/Temperature";
        var unmappableTopic = "mqtt/Enterprise1/Site1/Area1/WorkCenter2/Pressure";

        var mappableResult = _autoMapperService.TryMapTopic(mappableTopic);
        var unmappableResult = _autoMapperService.TryMapTopic(unmappableTopic);

        // Assert 1: Verify initial mappings
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", mappableResult);
        Assert.Null(unmappableResult); // Should fail to map since WorkCenter2 doesn't exist

        // Act 2: Add WorkCenter2
        var updatedNamespaces = new List<NSTreeNode>
        {
            CreateNSTreeNode("Enterprise1", "Enterprise", new[]
            {
                CreateNSTreeNode("Site1", "Site", new[]
                {
                    CreateNSTreeNode("Area1", "Area", new[]
                    {
                        CreateNSTreeNode("WorkCenter1", "WorkCenter", Array.Empty<NSTreeNode>()),
                        CreateNSTreeNode("WorkCenter2", "WorkCenter", Array.Empty<NSTreeNode>())
                    })
                })
            })
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(updatedNamespaces);

        await _autoMapperService.RefreshCacheAsync();

        // Act 3: Remap both topics
        var mappableResultAfter = _autoMapperService.TryMapTopic(mappableTopic);
        var unmappableResultAfter = _autoMapperService.TryMapTopic(unmappableTopic);

        // Assert 3: Previously mapped topic should remain the same, unmappable should now map correctly
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", mappableResultAfter);
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter2", unmappableResultAfter);
    }

    private static NSTreeNode CreateNSTreeNode(string name, string nodeType, NSTreeNode[] children)
    {
        return new NSTreeNode
        {
            Name = name,
            NodeType = nodeType == "Namespace" ? NSNodeType.Namespace : NSNodeType.HierarchyNode,
            Children = children.ToList()
        };
    }

    public void Dispose()
    {
        _autoMapperService?.Dispose();
        _backgroundService?.Dispose();
    }
}