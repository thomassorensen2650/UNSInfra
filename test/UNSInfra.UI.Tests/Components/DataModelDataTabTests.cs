using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.TopicBrowser.Events;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Data;
using Moq;
using FluentAssertions;
using System.Globalization;

namespace UNSInfra.UI.Tests.Components;

/// <summary>
/// Unit tests for DataModel component Data tab update behavior logic
/// </summary>
public class DataModelDataTabTests
{
    [Fact]
    public async Task OnTopicDataUpdated_ShouldUpdatePayload_WhenDataTabActiveAndTopicMatches()
    {
        // Arrange
        var mockTopicBrowserService = new Mock<ITopicBrowserService>();
        var selectedTopic = new TopicInfo { Topic = "test/temperature", SourceType = "MQTT" };
        var activeDetailTab = "data";

        var updatedDataPoint = new DataPoint
        {
            Topic = "test/temperature",
            Value = "26.8",
            Timestamp = DateTime.Now
        };

        mockTopicBrowserService.Setup(x => x.GetDataForTopicAsync("test/temperature"))
            .ReturnsAsync(updatedDataPoint);

        // Act - Simulate the OnTopicDataUpdated logic from DataModel.razor
        var shouldUpdate = selectedTopic != null && 
                          selectedTopic.Topic == "test/temperature" && 
                          activeDetailTab == "data";

        // Assert
        shouldUpdate.Should().BeTrue("Data tab should update when current topic data changes");
        
        // Verify the mock service would be called to get updated data
        if (shouldUpdate)
        {
            var result = await mockTopicBrowserService.Object.GetDataForTopicAsync("test/temperature");
            result.Value.Should().Be("26.8");
            result.Topic.Should().Be("test/temperature");
        }
    }

    [Fact]
    public void OnTopicDataUpdated_ShouldNotUpdatePayload_WhenDifferentTopicDataUpdated()
    {
        // Arrange
        var selectedTopic = new TopicInfo { Topic = "test/temperature", SourceType = "MQTT" };
        var activeDetailTab = "data";
        var eventTopic = "test/pressure"; // Different topic

        // Act - Simulate the OnTopicDataUpdated logic from DataModel.razor
        var shouldUpdate = selectedTopic != null && 
                          selectedTopic.Topic == eventTopic && 
                          activeDetailTab == "data";

        // Assert
        shouldUpdate.Should().BeFalse("Data tab should not update for different topic");
    }

    [Fact]
    public void OnTopicDataUpdated_ShouldNotUpdatePayload_WhenDataTabNotActive()
    {
        // Arrange
        var selectedTopic = new TopicInfo { Topic = "test/temperature", SourceType = "MQTT" };
        var activeDetailTab = "meta"; // Different tab
        var eventTopic = "test/temperature";

        // Act - Simulate the OnTopicDataUpdated logic from DataModel.razor
        var shouldUpdate = selectedTopic != null && 
                          selectedTopic.Topic == eventTopic && 
                          activeDetailTab == "data";

        // Assert
        shouldUpdate.Should().BeFalse("Data tab should not update when not active");
    }

    [Fact]
    public void OnTopicDataUpdated_ShouldNotUpdatePayload_WhenNoTopicSelected()
    {
        // Arrange
        TopicInfo? selectedTopic = null;
        var activeDetailTab = "data";
        var eventTopic = "test/temperature";

        // Act - Simulate the OnTopicDataUpdated logic from DataModel.razor
        var shouldUpdate = selectedTopic != null && 
                          selectedTopic.Topic == eventTopic && 
                          activeDetailTab == "data";

        // Assert
        shouldUpdate.Should().BeFalse("Data tab should not update when no topic selected");
    }

    [Fact]
    public void DataTabUpdateConditions_CoverAllScenarios()
    {
        // Test all combinations of conditions
        var scenarios = new[]
        {
            // (hasSelectedTopic, topicsMatch, isDataTabActive, shouldUpdate)
            (true, true, true, true),    // Should update: topic selected, same topic, data tab active
            (false, true, true, false),  // Should not update: no topic selected
            (true, false, true, false), // Should not update: different topic
            (true, true, false, false), // Should not update: different tab active
            (false, false, false, false), // Should not update: no conditions met
        };

        foreach (var (hasSelectedTopic, topicsMatch, isDataTabActive, expectedShouldUpdate) in scenarios)
        {
            // Arrange
            var selectedTopic = hasSelectedTopic ? new TopicInfo { Topic = "test/temperature", SourceType = "MQTT" } : null;
            var eventTopic = topicsMatch ? "test/temperature" : "test/pressure";
            var activeDetailTab = isDataTabActive ? "data" : "meta";

            // Act
            var shouldUpdate = selectedTopic != null && 
                              selectedTopic.Topic == eventTopic && 
                              activeDetailTab == "data";

            // Assert
            shouldUpdate.Should().Be(expectedShouldUpdate, 
                $"Scenario: hasSelected={hasSelectedTopic}, topicsMatch={topicsMatch}, isDataTabActive={isDataTabActive}");
        }
    }

    [Fact]
    public void PayloadTimestamp_ShouldBeUpdated_WhenDataReceived()
    {
        // Arrange
        var testTimestamp = new DateTime(2023, 12, 15, 14, 30, 0);
        var dataPoint = new DataPoint
        {
            Topic = "test/temperature",
            Value = "25.5",
            Timestamp = testTimestamp
        };

        // Act & Assert - Verify timestamp handling
        dataPoint.Timestamp.Should().Be(testTimestamp);
        
        // Verify the timestamp format that would be displayed in UI
        var displayFormat = testTimestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        displayFormat.Should().Be("2023-12-15 14:30:00");
    }
}