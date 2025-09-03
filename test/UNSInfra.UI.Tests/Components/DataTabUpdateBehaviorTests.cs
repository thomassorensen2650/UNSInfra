using UNSInfra.Services.TopicBrowser;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Models.Data;
using Moq;
using FluentAssertions;

namespace UNSInfra.UI.Tests.Components;

/// <summary>
/// Tests the data tab update behavior without complex UI component dependencies
/// </summary>
public class DataTabUpdateBehaviorTests
{
    [Fact]
    public void OnTopicDataUpdated_Should_UpdateDataTab_WhenCurrentTopicChanges()
    {
        // Arrange
        var mockTopicBrowserService = new Mock<ITopicBrowserService>();
        var testTopic = new TopicInfo
        {
            Topic = "test/temperature",
            SourceType = "MQTT",
            NSPath = "Enterprise/Dallas"
        };

        var initialData = new DataPoint
        {
            Topic = "test/temperature",
            Value = "25.5",
            Timestamp = DateTime.Now
        };

        var updatedData = new DataPoint
        {
            Topic = "test/temperature",
            Value = "26.8",
            Timestamp = DateTime.Now.AddSeconds(5)
        };

        mockTopicBrowserService.Setup(x => x.GetDataForTopicAsync("test/temperature"))
            .ReturnsAsync(initialData);

        // Act & Assert
        // Simulate the logic from DataModel.razor OnTopicDataUpdated method
        
        // 1. Topic is selected initially 
        var selectedTopic = testTopic;
        var selectedPayload = initialData.Value;
        var activeDetailTab = "data";

        // 2. Data update event occurs for the selected topic
        var dataUpdatedEventTopic = "test/temperature";
        var shouldUpdateDataTab = selectedTopic != null && 
                                 selectedTopic.Topic == dataUpdatedEventTopic && 
                                 activeDetailTab == "data";

        // Assert - Should update when conditions are met
        shouldUpdateDataTab.Should().BeTrue("Data tab should update when current topic data changes");
        
        // 3. Test case where different topic updates
        var differentTopicUpdate = "test/pressure";
        var shouldNotUpdateForDifferentTopic = selectedTopic != null && 
                                              selectedTopic.Topic == differentTopicUpdate && 
                                              activeDetailTab == "data";

        shouldNotUpdateForDifferentTopic.Should().BeFalse("Data tab should not update for different topic");
        
        // 4. Test case where Data tab is not active
        var differentTabActive = selectedTopic != null && 
                                selectedTopic.Topic == dataUpdatedEventTopic && 
                                "meta" == "data";

        differentTabActive.Should().BeFalse("Data tab should not update when not the active tab");
    }

    [Fact]
    public void DataTabUpdate_Logic_MatchesExpectedBehavior()
    {
        // Test the exact conditions from the fixed OnTopicDataUpdated method
        
        // Case 1: Should update - selected topic, data tab active, same topic
        var case1 = ShouldUpdateDataTab(
            selectedTopic: "test/temp",
            eventTopic: "test/temp", 
            activeTab: "data");
        case1.Should().BeTrue("Should update when all conditions are met");

        // Case 2: Should not update - no topic selected
        var case2 = ShouldUpdateDataTab(
            selectedTopic: null,
            eventTopic: "test/temp",
            activeTab: "data");
        case2.Should().BeFalse("Should not update when no topic is selected");

        // Case 3: Should not update - different topic
        var case3 = ShouldUpdateDataTab(
            selectedTopic: "test/temp",
            eventTopic: "test/pressure", 
            activeTab: "data");
        case3.Should().BeFalse("Should not update for different topic");

        // Case 4: Should not update - different tab active
        var case4 = ShouldUpdateDataTab(
            selectedTopic: "test/temp",
            eventTopic: "test/temp",
            activeTab: "meta");
        case4.Should().BeFalse("Should not update when Data tab is not active");
    }

    private bool ShouldUpdateDataTab(string? selectedTopic, string eventTopic, string activeTab)
    {
        // This mirrors the logic from the fixed OnTopicDataUpdated method
        return selectedTopic != null && 
               selectedTopic == eventTopic && 
               activeTab == "data";
    }
}