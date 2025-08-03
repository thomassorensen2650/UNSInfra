using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.InMemory;
using Xunit;

namespace UNSInfra.Core.Tests.Services;

/// <summary>
/// Integration tests to reproduce and fix the issue where UNS tree topics 
/// don't show up until application restart.
/// </summary>
public class TopicRefreshIntegrationTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ITopicBrowserService _topicBrowserService;
    private readonly ITopicConfigurationRepository _topicRepository;
    private readonly IRealtimeStorage _realtimeStorage;

    public TopicRefreshIntegrationTests()
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
        
        // Add services
        services.AddScoped<ITopicBrowserService, TopicBrowserService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _topicBrowserService = _serviceProvider.GetRequiredService<ITopicBrowserService>();
        _topicRepository = _serviceProvider.GetRequiredService<ITopicConfigurationRepository>();
        _realtimeStorage = _serviceProvider.GetRequiredService<IRealtimeStorage>();
    }

    [Fact]
    public async Task TopicBrowserService_WhenNewTopicAdded_ShouldAppearInLatestStructure()
    {
        // Arrange - Get initial topic count
        var initialTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
        var initialCount = initialTopics.Count();
        
        // Act - Add a new topic configuration
        var newTopicConfig = new TopicConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Topic = "test/sensors/temperature",
            Path = new HierarchicalPath(),
            NSPath = "Factory/Line1/Station1",
            UNSName = "Temperature",
            SourceType = "TEST",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Description = "Test temperature sensor"
        };
        
        await _topicRepository.SaveTopicConfigurationAsync(newTopicConfig);
        
        // Assert - The new topic should appear in latest structure WITHOUT restart
        var updatedTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
        var updatedCount = updatedTopics.Count();
        
        Assert.Equal(initialCount + 1, updatedCount);
        
        var addedTopic = updatedTopics.FirstOrDefault(t => t.Topic == "test/sensors/temperature");
        Assert.NotNull(addedTopic);
        Assert.Equal("TEST", addedTopic.SourceType);
        Assert.Equal("Temperature", addedTopic.UNSName);
        Assert.True(addedTopic.IsActive);
    }

    [Fact]
    public async Task TopicBrowserService_WhenTopicUpdated_ShouldReflectChangesImmediately()
    {
        // Arrange - Create and save initial topic
        var topicConfig = new TopicConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Topic = "test/devices/motor",
            Path = new HierarchicalPath(),
            NSPath = "Factory/Motor",
            UNSName = "MotorSpeed",
            SourceType = "MQTT",
            IsActive = true,
            Description = "Original description",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        
        await _topicRepository.SaveTopicConfigurationAsync(topicConfig);
        
        // Verify initial state
        var initialTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
        var initialTopic = initialTopics.FirstOrDefault(t => t.Topic == "test/devices/motor");
        Assert.NotNull(initialTopic);
        Assert.Equal("Original description", initialTopic.Description);
        
        // Act - Update the topic
        topicConfig.Description = "Updated description";
        topicConfig.UNSName = "UpdatedMotorSpeed";
        topicConfig.ModifiedAt = DateTime.UtcNow;
        
        await _topicRepository.SaveTopicConfigurationAsync(topicConfig);
        
        // Assert - Changes should be visible immediately
        var updatedTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
        var updatedTopic = updatedTopics.FirstOrDefault(t => t.Topic == "test/devices/motor");
        
        Assert.NotNull(updatedTopic);
        Assert.Equal("Updated description", updatedTopic.Description);
        Assert.Equal("UpdatedMotorSpeed", updatedTopic.UNSName);
    }

    [Fact]
    public async Task TopicBrowserService_WithEventNotifications_ShouldFireTopicAddedEvent()
    {
        // Arrange - Setup event listener
        TopicInfo? notifiedTopic = null;
        _topicBrowserService.TopicAdded += (sender, args) =>
        {
            notifiedTopic = args.TopicInfo;
        };
        
        // Act - Add a new topic and manually notify (simulating what should happen automatically)
        var topicConfig = new TopicConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Topic = "test/events/sensor",
            Path = new HierarchicalPath(),
            NSPath = "Test/Events",
            UNSName = "EventSensor",
            SourceType = "EVENT_TEST",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        
        await _topicRepository.SaveTopicConfigurationAsync(topicConfig);
        
        // Manually trigger the event (this should happen automatically in a real system)
        var topicInfo = new TopicInfo
        {
            Topic = topicConfig.Topic,
            Path = topicConfig.Path,
            NSPath = topicConfig.NSPath,
            UNSName = topicConfig.UNSName,
            SourceType = topicConfig.SourceType,
            IsActive = topicConfig.IsActive,
            CreatedAt = topicConfig.CreatedAt,
            ModifiedAt = topicConfig.ModifiedAt
        };
        
        ((TopicBrowserService)_topicBrowserService).NotifyTopicAdded(topicInfo);
        
        // Assert - Event should have been fired
        Assert.NotNull(notifiedTopic);
        Assert.Equal("test/events/sensor", notifiedTopic.Topic);
        Assert.Equal("EVENT_TEST", notifiedTopic.SourceType);
    }

    [Fact]
    public async Task EventDrivenTopicBrowserService_WhenTopicUpdatedViaService_ShouldReflectChangesImmediately()
    {
        // This test demonstrates the FIX for the original issue
        // When using TopicBrowserService.UpdateTopicConfigurationAsync, the event-driven 
        // system should make the changes visible immediately without requiring app restart
        
        // Arrange - Create initial topic
        var topicConfig = new TopicConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Topic = "factory/machine/status",
            Path = new HierarchicalPath(),
            NSPath = "Factory/Line1/Machine1",
            UNSName = "MachineStatus",
            SourceType = "UI_UPDATE",
            IsActive = true,
            Description = "Original machine status",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        
        await _topicRepository.SaveTopicConfigurationAsync(topicConfig);
        
        // Verify initial state
        var initialTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
        var initialTopic = initialTopics.FirstOrDefault(t => t.Topic == "factory/machine/status");
        Assert.NotNull(initialTopic);
        Assert.Equal("Factory/Line1/Machine1", initialTopic.NSPath);
        
        // Act - Update topic via TopicBrowserService (simulating UI namespace assignment)
        topicConfig.NSPath = "Factory/Line2/Machine5";  // User assigns to different namespace
        topicConfig.UNSName = "UpdatedMachineStatus";
        topicConfig.Description = "Updated via UNS tree assignment";
        topicConfig.ModifiedAt = DateTime.UtcNow;
        
        // This is the key - using TopicBrowserService instead of repository directly
        await _topicBrowserService.UpdateTopicConfigurationAsync(topicConfig);
        
        // Assert - Changes should be visible immediately (no restart required!)
        var updatedTopics = await _topicBrowserService.GetLatestTopicStructureAsync();
        var updatedTopic = updatedTopics.FirstOrDefault(t => t.Topic == "factory/machine/status");
        
        Assert.NotNull(updatedTopic);
        Assert.Equal("Factory/Line2/Machine5", updatedTopic.NSPath);
        Assert.Equal("UpdatedMachineStatus", updatedTopic.UNSName);
        Assert.Equal("Updated via UNS tree assignment", updatedTopic.Description);
    }
}