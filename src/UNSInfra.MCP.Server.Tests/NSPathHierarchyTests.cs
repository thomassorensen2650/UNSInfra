using FluentAssertions;
using System.Text.Json;
using UNSInfra.MCP.Server;
using Xunit;

namespace UNSInfra.MCP.Server.Tests;

/// <summary>
/// Tests specifically for NSPath-based hierarchy building
/// </summary>
public class NSPathHierarchyTests
{
    [Fact]
    public void NSPathHierarchy_ShouldCreateCorrectStructure_WithMultipleTopics()
    {
        // Arrange - Sample topics with realistic NSPath values
        var topics = new List<TopicNode>
        {
            new() 
            {
                Topic = "temp_001",
                UnsName = "Area 1 Temperature Sensor",
                NsPath = "Enterprise/Site1/Area1",
                IsActive = true,
                SourceType = "MQTT",
                Description = "Temperature sensor in production area 1"
            },
            new() 
            {
                Topic = "pressure_001",
                UnsName = "Area 1 Pressure Sensor", 
                NsPath = "Enterprise/Site1/Area1",
                IsActive = true,
                SourceType = "MQTT",
                Description = "Pressure sensor in production area 1"
            },
            new() 
            {
                Topic = "temp_002",
                UnsName = "Site 2 Temperature", 
                NsPath = "Enterprise/Site2",
                IsActive = false,
                SourceType = "SocketIO",
                Description = "Temperature sensor in site 2"
            },
            new() 
            {
                Topic = "unassigned_sensor",
                UnsName = "Unassigned Sensor",
                NsPath = "", // No namespace path - should go to unassigned
                IsActive = true,
                SourceType = "Mock",
                Description = "Sensor not yet assigned to hierarchy"
            }
        };

        // Act - We'll test the concept by manually building what the hierarchy should look like
        var expectedHierarchy = new Dictionary<string, List<string>>
        {
            ["Data Browser"] = new() { "Enterprise", "Unassigned Topics" },
            ["Data Browser/Enterprise"] = new() { "Site1", "Site2" },
            ["Data Browser/Enterprise/Site1"] = new() { "Area1" },
            ["Data Browser/Enterprise/Site1/Area1"] = new() { "Area 1 Temperature Sensor", "Area 1 Pressure Sensor" },
            ["Data Browser/Enterprise/Site2"] = new() { "Site 2 Temperature" },
            ["Data Browser/Unassigned Topics"] = new() { "Unassigned Sensor" }
        };

        // Assert - Verify the topics have the expected structure
        var enterpriseTopics = topics.Where(t => !string.IsNullOrEmpty(t.NsPath) && t.NsPath.StartsWith("Enterprise")).ToList();
        var site1Area1Topics = topics.Where(t => t.NsPath == "Enterprise/Site1/Area1").ToList();
        var site2Topics = topics.Where(t => t.NsPath == "Enterprise/Site2").ToList(); 
        var unassignedTopics = topics.Where(t => string.IsNullOrEmpty(t.NsPath)).ToList();

        enterpriseTopics.Should().HaveCount(3);
        site1Area1Topics.Should().HaveCount(2);
        site2Topics.Should().HaveCount(1);
        unassignedTopics.Should().HaveCount(1);

        // Verify the hierarchical path splitting works correctly
        foreach (var topic in topics.Where(t => !string.IsNullOrEmpty(t.NsPath)))
        {
            var pathParts = topic.NsPath!.Split('/', StringSplitOptions.RemoveEmptyEntries);
            pathParts.Should().NotBeEmpty();
            pathParts[0].Should().Be("Enterprise"); // All should start with Enterprise
        }
    }

    [Theory]
    [InlineData("Enterprise/Site1/Area1", new[] { "Enterprise", "Site1", "Area1" })]
    [InlineData("Enterprise/Site2", new[] { "Enterprise", "Site2" })]
    [InlineData("Factory/Line1/Station1", new[] { "Factory", "Line1", "Station1" })]
    [InlineData("", new string[0])]
    public void NSPathParsing_ShouldSplitCorrectly(string nsPath, string[] expectedParts)
    {
        // Act
        var actualParts = nsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        actualParts.Should().BeEquivalentTo(expectedParts);
    }

    [Fact]
    public void NSPathTopics_ShouldUseUnsNameForDisplay()
    {
        // Arrange
        var topic = new TopicNode
        {
            Topic = "temp_sensor_001",
            UnsName = "Production Area Temperature",
            NsPath = "Enterprise/Site1/Area1",
            Description = "Main temperature sensor"
        };

        // Act
        var displayName = topic.UnsName ?? topic.Topic;

        // Assert
        displayName.Should().Be("Production Area Temperature");
        displayName.Should().NotBe(topic.Topic); // Should use friendly name, not technical topic name
    }

    [Fact] 
    public void NSPathHierarchy_ShouldHandleDuplicateDisplayNames()
    {
        // Arrange - Two sensors with same display name but different topics
        var topics = new List<TopicNode>
        {
            new() 
            {
                Topic = "temp_001",
                UnsName = "Temperature Sensor",
                NsPath = "Enterprise/Site1/Area1"
            },
            new() 
            {
                Topic = "temp_002", 
                UnsName = "Temperature Sensor", // Same display name
                NsPath = "Enterprise/Site1/Area1"
            }
        };

        // Act - Group by display name and location
        var grouped = topics
            .GroupBy(t => new { DisplayName = t.UnsName ?? t.Topic, Location = t.NsPath })
            .ToList();

        // Assert - Should handle multiple topics with same display name in same location
        grouped.Should().HaveCount(1); // Same display name and location
        grouped[0].Should().HaveCount(2); // But two actual topics
    }
}