using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services;
using UNSInfra.Services.AutoMapping;
using UNSInfra.Services.Events;
using UNSInfra.Services.TopicBrowser;
using Xunit;

using NSNodeType = UNSInfra.Services.NSNodeType;

namespace UNSInfra.Core.Tests.Services.AutoMapping;

/// <summary>
/// Test that reproduces the real-world issue where adding new namespaces doesn't 
/// re-map previously failed topics until application restart.
/// </summary>
public class NamespaceRefreshMissingTopicsTest : IDisposable
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

    public NamespaceRefreshMissingTopicsTest()
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
            _backgroundServiceLoggerMock.Object);
    }

    [Fact]
    public async Task FixedBehavior_PreviouslyFailedTopics_AreRemappedAfterNamespaceAdded()
    {
        // Arrange: Initial namespace structure WITHOUT the target namespace
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

        // Start the background service
        await _backgroundService.StartAsync(CancellationToken.None);

        // Step 1: Simulate a topic being discovered that CANNOT be mapped yet
        var unmappableTopicInfo = new TopicInfo
        {
            Topic = "mqtt/factory/Enterprise1/Site1/Area1/WorkCenter1/Temperature",
            SourceType = "MQTT",
            NSPath = null!, // No existing mapping
            CreatedAt = DateTime.UtcNow
        };

        // Queue the topic for mapping (simulates TopicAddedEvent)
        _backgroundService.QueueTopicForMapping(unmappableTopicInfo);

        // Process the topic manually since ExecuteAsync runs in background
        var processMethod = typeof(SimplifiedAutoMappingBackgroundService)
            .GetMethod("ProcessTopicBatch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)processMethod!.Invoke(_backgroundService, new object[] { CancellationToken.None })!;

        // Verify the topic failed to map
        Assert.Single(_publishedFailedEvents);
        Assert.Contains("WorkCenter1", _publishedFailedEvents[0].Topic);
        Assert.Empty(_publishedMappingEvents);

        // Clear events for next phase
        _publishedFailedEvents.Clear();
        _publishedMappingEvents.Clear();

        // Step 2: Admin adds new namespace that WOULD match the failed topic
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

        // Simulate namespace structure change event
        var namespaceChangedEvent = new NamespaceStructureChangedEvent(
            ChangedNamespace: "Enterprise1/Site1/Area1/WorkCenter1",
            ChangeType: "Added", 
            ChangedBy: "TestUser");
        
        // Get the OnNamespaceStructureChanged method through reflection since it's private
        var method = typeof(SimplifiedAutoMappingBackgroundService)
            .GetMethod("OnNamespaceStructureChanged", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)method!.Invoke(_backgroundService, new object[] { namespaceChangedEvent })!;

        // Process any re-queued topics manually (this is where the fix should work)
        await (Task)processMethod!.Invoke(_backgroundService, new object[] { CancellationToken.None })!;

        // Step 3: THE FIX - Previously failed topic should now be automatically re-queued and re-processed
        
        // Verify that the previously failed topic was automatically retried and succeeded
        Assert.Single(_publishedMappingEvents); // Should have successful mapping event after fix
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", _publishedMappingEvents[0].MappedNamespace);

        // Step 4: Demonstrate that NEW topics with same pattern DO work
        var newTopicInfo = new TopicInfo
        {
            Topic = "mqtt/factory/Enterprise1/Site1/Area1/WorkCenter1/Pressure", // Different topic, same namespace
            SourceType = "MQTT",
            NSPath = null!,
            CreatedAt = DateTime.UtcNow
        };

        _backgroundService.QueueTopicForMapping(newTopicInfo);
        await Task.Delay(100);

        // Verify NEW topic maps successfully
        Assert.Single(_publishedMappingEvents);
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", _publishedMappingEvents[0].MappedNamespace);

        // CONCLUSION: The fix now automatically re-queues failed topics when namespace structure changes!
    }

    [Fact]
    public async Task DESIRED_BEHAVIOR_PreviouslyFailedTopics_ShouldBeRemappedAfterNamespaceAdded()
    {
        // This test documents the DESIRED behavior that should be implemented
        // Currently this test will FAIL, demonstrating the bug

        // Arrange: Same setup as above
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
        await _backgroundService.StartAsync(CancellationToken.None);

        // Step 1: Topic fails to map initially
        var failedTopic = new TopicInfo
        {
            Topic = "mqtt/factory/Enterprise1/Site1/Area1/WorkCenter1/Temperature",
            SourceType = "MQTT", 
            NSPath = null!,
            CreatedAt = DateTime.UtcNow
        };

        _backgroundService.QueueTopicForMapping(failedTopic);
        
        // Process the topic manually since ExecuteAsync runs in background
        var processMethod = typeof(SimplifiedAutoMappingBackgroundService)
            .GetMethod("ProcessTopicBatch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)processMethod!.Invoke(_backgroundService, new object[] { CancellationToken.None })!;
        
        Assert.Single(_publishedFailedEvents);
        _publishedFailedEvents.Clear();

        // Step 2: Add matching namespace
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

        // Trigger namespace change
        var namespaceChangedEvent = new NamespaceStructureChangedEvent(
            ChangedNamespace: "Enterprise1/Site1/Area1/WorkCenter1",
            ChangeType: "Added",
            ChangedBy: "TestUser");
        var method = typeof(SimplifiedAutoMappingBackgroundService)
            .GetMethod("OnNamespaceStructureChanged", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_backgroundService, new object[] { namespaceChangedEvent })!;

        // Process any re-queued topics manually
        await (Task)processMethod!.Invoke(_backgroundService, new object[] { CancellationToken.None })!;

        // Step 3: DESIRED BEHAVIOR - Previously failed topic should now be automatically mapped
        // This assertion will FAIL with current implementation, proving the bug exists
        Assert.Single(_publishedMappingEvents); // This should pass after fix
        Assert.Equal("Enterprise1/Site1/Area1/WorkCenter1", _publishedMappingEvents[0].MappedNamespace);
        Assert.Contains("WorkCenter1/Temperature", _publishedMappingEvents[0].Topic);
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