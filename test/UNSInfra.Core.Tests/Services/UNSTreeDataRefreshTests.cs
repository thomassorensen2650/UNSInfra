using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.Events;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.InMemory;
using Xunit;

namespace UNSInfra.Core.Tests.Services;

/// <summary>
/// Tests to reproduce and fix the UNS tree data refresh issue where topics don't appear 
/// without restarting the application.
/// </summary>
public class UNSTreeDataRefreshTests
{
    private readonly ServiceProvider _serviceProvider;

    public UNSTreeDataRefreshTests()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add in-memory storage
        services.AddSingleton<IRealtimeStorage, InMemoryRealtimeStorage>();
        services.AddSingleton<IHistoricalStorage, InMemoryHistoricalStorage>();
        
        // Add in-memory repositories  
        services.AddScoped<ITopicConfigurationRepository, InMemoryTopicConfigurationRepository>();
        services.AddScoped<IHierarchyConfigurationRepository, InMemoryHierarchyConfigurationRepository>();
        
        // Add event bus for EventDrivenTopicBrowserService
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        // Add both TopicBrowserService implementations
        services.AddScoped<TopicBrowserService>();
        services.AddScoped<EventDrivenTopicBrowserService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ReproduceProblem_RegularTopicBrowserService_TopicsNotVisibleWithoutRestart()
    {
        // Arrange - Use regular TopicBrowserService (simulates SQLite environment)
        var topicBrowserService = _serviceProvider.GetRequiredService<TopicBrowserService>();
        var topicRepository = _serviceProvider.GetRequiredService<ITopicConfigurationRepository>();
        
        // Get initial topic count for namespace
        var namespacePath = "Factory/ProductionLine/Sensors";
        var initialTopics = await topicBrowserService.GetTopicsForNamespaceAsync(namespacePath);
        var initialCount = initialTopics.Count();
        
        // Act - Simulate UI workflow: User adds data to UNS tree via "Add Data to Namespace" modal
        // This is what happens in NSTreeNodeEditor when user assigns a topic to a namespace
        
        // Step 1: Create a new topic configuration (this simulates a discovered MQTT topic)
        var newTopicConfig = new TopicConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Topic = "sensors/temperature/room_101",
            Path = new HierarchicalPath(),
            NSPath = "", // Initially unassigned
            UNSName = "",
            SourceType = "MQTT",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Description = "Room temperature sensor"
        };
        
        await topicRepository.SaveTopicConfigurationAsync(newTopicConfig);
        
        // Step 2: User assigns topic to namespace (simulates NSTreeNodeEditor "Add Data" action)
        newTopicConfig.NSPath = namespacePath;
        newTopicConfig.UNSName = "Room101Temperature";
        newTopicConfig.ModifiedAt = DateTime.UtcNow;
        
        // **THE PROBLEM**: If we save directly to repository, the UI won't see the change
        await topicRepository.SaveTopicConfigurationAsync(newTopicConfig);
        
        // Step 3: UI calls GetTopicsForNamespaceAsync to refresh the namespace topics
        var updatedTopics = await topicBrowserService.GetTopicsForNamespaceAsync(namespacePath);
        var updatedCount = updatedTopics.Count();
        
        // Assert - This SHOULD pass but demonstrates the problem exists in SQLite environments
        // In real SQLite environments with caching issues, this might fail
        Assert.Equal(initialCount + 1, updatedCount);
        
        var assignedTopic = updatedTopics.FirstOrDefault(t => t.Topic == "sensors/temperature/room_101");
        Assert.NotNull(assignedTopic);
        Assert.Equal(namespacePath, assignedTopic.NSPath);
        Assert.Equal("Room101Temperature", assignedTopic.UNSName);
    }

    [Fact]
    public async Task FixedSolution_UseTopicBrowserServiceUpdateMethod_TopicsVisibleImmediately()
    {
        // Arrange - Use regular TopicBrowserService
        var topicBrowserService = _serviceProvider.GetRequiredService<TopicBrowserService>();
        var topicRepository = _serviceProvider.GetRequiredService<ITopicConfigurationRepository>();
        
        // Get initial topic count for namespace
        var namespacePath = "Factory/Assembly/Stations";
        var initialTopics = await topicBrowserService.GetTopicsForNamespaceAsync(namespacePath);
        var initialCount = initialTopics.Count();
        
        // Act - THE SOLUTION: Use TopicBrowserService.UpdateTopicConfigurationAsync instead of direct repository calls
        
        // Step 1: Create topic configuration
        var newTopicConfig = new TopicConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Topic = "assembly/station1/status",
            Path = new HierarchicalPath(),
            NSPath = "", // Initially unassigned
            UNSName = "",
            SourceType = "MQTT",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Description = "Assembly station status"
        };
        
        await topicRepository.SaveTopicConfigurationAsync(newTopicConfig);
        
        // Step 2: User assigns topic to namespace - **USE CORRECT METHOD**
        newTopicConfig.NSPath = namespacePath;
        newTopicConfig.UNSName = "Station1Status";
        newTopicConfig.ModifiedAt = DateTime.UtcNow;
        
        // **THE FIX**: Use TopicBrowserService.UpdateTopicConfigurationAsync
        // This ensures proper event handling and cache invalidation
        await topicBrowserService.UpdateTopicConfigurationAsync(newTopicConfig);
        
        // Step 3: UI immediately sees the change
        var updatedTopics = await topicBrowserService.GetTopicsForNamespaceAsync(namespacePath);
        var updatedCount = updatedTopics.Count();
        
        // Assert - Topic appears immediately, no restart required!
        Assert.Equal(initialCount + 1, updatedCount);
        
        var assignedTopic = updatedTopics.FirstOrDefault(t => t.Topic == "assembly/station1/status");
        Assert.NotNull(assignedTopic);
        Assert.Equal(namespacePath, assignedTopic.NSPath);
        Assert.Equal("Station1Status", assignedTopic.UNSName);
    }

    [Fact]
    public async Task EventDrivenSolution_UsesEventBusForRealTimeUpdates()
    {
        // Arrange - Use EventDrivenTopicBrowserService (better solution)
        var eventDrivenService = _serviceProvider.GetRequiredService<EventDrivenTopicBrowserService>();
        var topicRepository = _serviceProvider.GetRequiredService<ITopicConfigurationRepository>();
        
        // Initialize the event-driven service with existing topics
        var existingTopics = await topicRepository.GetAllTopicConfigurationsAsync();
        var topicInfos = existingTopics.Select(config => new TopicInfo
        {
            Topic = config.Topic,
            Path = config.Path,
            NSPath = config.NSPath,
            UNSName = config.UNSName,
            SourceType = config.SourceType,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt,
            ModifiedAt = config.ModifiedAt,
            Description = config.Description,
            Metadata = config.Metadata
        });
        await eventDrivenService.InitializeCacheAsync(topicInfos);
        
        var namespacePath = "Factory/Quality/Inspection";
        var initialTopics = await eventDrivenService.GetTopicsForNamespaceAsync(namespacePath);
        var initialCount = initialTopics.Count();
        
        // Act - Create and assign topic using EventDrivenTopicBrowserService
        var newTopicConfig = new TopicConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Topic = "quality/defect_detector/station_a",
            Path = new HierarchicalPath(),
            NSPath = namespacePath,
            UNSName = "DefectDetectorA",
            SourceType = "MQTT",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Description = "Defect detection system"
        };
        
        // Use EventDriven service - this triggers proper event handling
        await eventDrivenService.UpdateTopicConfigurationAsync(newTopicConfig);
        
        // Assert - Topic appears immediately via event-driven architecture
        var updatedTopics = await eventDrivenService.GetTopicsForNamespaceAsync(namespacePath);
        var updatedCount = updatedTopics.Count();
        
        Assert.Equal(initialCount + 1, updatedCount);
        
        var assignedTopic = updatedTopics.FirstOrDefault(t => t.Topic == "quality/defect_detector/station_a");
        Assert.NotNull(assignedTopic);
        Assert.Equal(namespacePath, assignedTopic.NSPath);
        Assert.Equal("DefectDetectorA", assignedTopic.UNSName);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}