using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Core.Configuration;
using UNSInfra.Core.Repositories;
using UNSInfra.Core.Services;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Services.Events;
using UNSInfra.Services.TopicBrowser;
using Xunit;

namespace UNSInfra.Core.Tests.Services;

/// <summary>
/// Simplified test to identify why data connections require application restart
/// to show up in the data browser. This test focuses on the event flow between
/// data services and the topic browser service.
/// </summary>
public class DataConnectionRestartSimplifiedTest
{
    [Fact]
    public async Task EventBus_ShouldConnectDataServiceToTopicBrowser_WithoutRestart()
    {
        // This is the core issue test: verify that when a data service generates data,
        // the TopicBrowserService receives the events and updates its cache
        
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Use a real in-memory event bus to test the actual event flow
        var eventBus = new InMemoryEventBus();
        services.AddSingleton<IEventBus>(eventBus);
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Create the topic browser service that should receive events
        var topicBrowserService = new EventDrivenTopicBrowserService(
            eventBus,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());
        
        // Track if topic was added to the browser service
        var topicAddedEventFired = false;
        topicBrowserService.TopicAdded += (sender, args) => 
        {
            topicAddedEventFired = true;
        };
        
        // Verify no topics initially
        var initialTopics = await topicBrowserService.GetLatestTopicStructureAsync();
        Assert.Empty(initialTopics);
        Assert.False(topicAddedEventFired);
        
        // Act: Simulate a data service publishing a TopicAddedEvent
        // This is what would happen when a connection is enabled and starts receiving data
        var topicAddedEvent = new TopicAddedEvent(
            "test/sensor/temperature",
            new HierarchicalPath(),
            "TestSource",
            DateTime.UtcNow);
        
        await eventBus.PublishAsync(topicAddedEvent);
        
        // Give the event bus time to process (should be immediate for in-memory)
        await Task.Delay(100);
        
        // Assert: The topic browser should now have the topic
        var topicsAfterEvent = await topicBrowserService.GetLatestTopicStructureAsync();
        
        // This is the key assertion - if this fails, it means the event flow is broken
        Assert.NotEmpty(topicsAfterEvent);
        Assert.Contains(topicsAfterEvent, t => t.Topic == "test/sensor/temperature");
        Assert.True(topicAddedEventFired, "TopicAdded event should have been fired by the browser service");
        
        // Additional verification: check that the topic has correct properties
        var addedTopic = topicsAfterEvent.First(t => t.Topic == "test/sensor/temperature");
        Assert.Equal("TestSource", addedTopic.SourceType);
        Assert.True(addedTopic.IsActive);
    }
    
    [Fact]
    public async Task EventBus_ShouldHandleDataUpdates_AfterTopicAdded()
    {
        // This test verifies that data updates work after topic is added
        
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        var eventBus = new InMemoryEventBus();
        services.AddSingleton<IEventBus>(eventBus);
        
        var serviceProvider = services.BuildServiceProvider();
        
        var topicBrowserService = new EventDrivenTopicBrowserService(
            eventBus,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());
        
        var dataUpdatedEventFired = false;
        topicBrowserService.TopicDataUpdated += (sender, args) => 
        {
            dataUpdatedEventFired = true;
        };
        
        // Act 1: Add a topic first
        var topicAddedEvent = new TopicAddedEvent(
            "sensor/pressure",
            new HierarchicalPath(),
            "TestSource",
            DateTime.UtcNow);
        
        await eventBus.PublishAsync(topicAddedEvent);
        await Task.Delay(50);
        
        // Act 2: Update the topic's data
        var dataPoint = new DataPoint
        {
            Topic = "sensor/pressure",
            Value = 42.5,
            Timestamp = DateTime.UtcNow,
            Path = new HierarchicalPath(),
            Source = "TestSource"
        };
        
        var dataUpdatedEvent = new TopicDataUpdatedEvent(
            "sensor/pressure",
            dataPoint,
            "TestSource");
        
        await eventBus.PublishAsync(dataUpdatedEvent);
        await Task.Delay(50);
        
        // Assert: Data should be available in the topic browser
        var topicData = await topicBrowserService.GetDataForTopicAsync("sensor/pressure");
        Assert.NotNull(topicData);
        Assert.Equal(42.5, topicData.Value);
        Assert.True(dataUpdatedEventFired, "TopicDataUpdated event should have been fired");
    }
    
    [Fact]
    public async Task MultipleEventPublishing_ShouldAllBeProcessed()
    {
        // This test verifies that rapid events don't get lost
        
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        var eventBus = new InMemoryEventBus();
        services.AddSingleton<IEventBus>(eventBus);
        
        var serviceProvider = services.BuildServiceProvider();
        
        var topicBrowserService = new EventDrivenTopicBrowserService(
            eventBus,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());
        
        // Act: Publish multiple topic events rapidly (simulating connection startup)
        var topics = new[]
        {
            "device1/temperature",
            "device1/pressure", 
            "device2/temperature",
            "device2/pressure",
            "device3/status"
        };
        
        foreach (var topic in topics)
        {
            var topicEvent = new TopicAddedEvent(
                topic,
                new HierarchicalPath(),
                "RapidSource",
                DateTime.UtcNow);
            
            await eventBus.PublishAsync(topicEvent);
        }
        
        // Give time for all events to process
        await Task.Delay(200);
        
        // Assert: All topics should be present
        var allTopics = await topicBrowserService.GetLatestTopicStructureAsync();
        Assert.Equal(5, allTopics.Count());
        
        foreach (var expectedTopic in topics)
        {
            Assert.Contains(allTopics, t => t.Topic == expectedTopic);
        }
    }
}

/// <summary>
/// Simple in-memory event bus for testing the actual event flow
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly Dictionary<Type, List<Func<object, Task>>> _handlers = new();

    public Task PublishAsync<T>(T eventData) where T : IEvent
    {
        var eventType = typeof(T);
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            var tasks = handlers.Select(handler => handler(eventData));
            return Task.WhenAll(tasks);
        }
        return Task.CompletedTask;
    }

    public void Subscribe<T>(Func<T, Task> handler) where T : IEvent
    {
        var eventType = typeof(T);
        if (!_handlers.ContainsKey(eventType))
            _handlers[eventType] = new List<Func<object, Task>>();
            
        _handlers[eventType].Add(obj => handler((T)obj));
    }

    public void Unsubscribe<T>(Func<T, Task> handler) where T : IEvent
    {
        // For testing purposes, we'll skip unsubscribe implementation
    }
}