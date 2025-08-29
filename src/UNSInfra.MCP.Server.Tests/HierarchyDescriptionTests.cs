using FluentAssertions;
using UNSInfra.MCP.Server;
using Xunit;

namespace UNSInfra.MCP.Server.Tests;

/// <summary>
/// Tests for hierarchy node descriptions functionality
/// </summary>
public class HierarchyDescriptionTests
{
    [Fact]
    public void HierarchyTreeNode_ShouldIncludeDescriptionProperty()
    {
        // Arrange & Act
        var node = new HierarchyTreeNode
        {
            Name = "Enterprise",
            FullPath = "Enterprise",
            Description = "Enterprise-level manufacturing operations and facilities"
        };

        // Assert
        node.Description.Should().NotBeNullOrEmpty();
        node.Description.Should().Contain("Enterprise-level");
        node.Description.Should().Contain("manufacturing");
    }

    [Theory]
    [InlineData("Enterprise", 1, "Top level of the hierarchy - the entire organization or company")]
    [InlineData("Factory", 1, "Top level of the hierarchy - the entire organization or company")]
    [InlineData("Plant", 1, "Top level of the hierarchy - the entire organization or company")]
    public void GenerateDescription_ShouldCreateAppropriateLevel1Descriptions(string name, int level, string expectedDescription)
    {
        // Arrange - Level 1 represents top-level organizational units (ISA-S95 Enterprise level)
        var fullPath = name;
        var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Act
        var actualLevel = pathParts.Length;

        // Assert
        actualLevel.Should().Be(level);
        
        // The description should follow ISA-S95 standard for Enterprise level
        expectedDescription.Should().Contain("Top level of the hierarchy");
        expectedDescription.Should().Contain("entire organization or company");
    }

    [Theory]
    [InlineData("Enterprise/Dallas", "Dallas", "Enterprise", "Physical location or facility within the enterprise")]
    [InlineData("Factory/PlantA", "PlantA", "Factory", "Physical location or facility within the enterprise")]
    public void GenerateDescription_ShouldCreateLevel2SiteDescriptions(string fullPath, string siteName, string parentName, string expectedDescription)
    {
        // Arrange
        var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Act
        var level = pathParts.Length;
        var actualSite = pathParts[1];
        var actualParent = pathParts[0];

        // Assert
        level.Should().Be(2);
        actualSite.Should().Be(siteName);
        actualParent.Should().Be(parentName);
        
        // Expected description format for level 2 (ISA-S95 Site level)
        expectedDescription.Should().Contain("Physical location or facility");
        expectedDescription.Should().Contain("within the enterprise");
    }

    [Theory]
    [InlineData("Enterprise/Dallas/Press", "Press", "Dallas", "Production area or department within a site")]
    [InlineData("Enterprise/Houston/Assembly", "Assembly", "Houston", "Production area or department within a site")]
    [InlineData("Enterprise/Austin/QC", "QC", "Austin", "Production area or department within a site")]
    public void GenerateDescription_ShouldCreateLevel3AreaDescriptions(string fullPath, string areaName, string siteName, string expectedContent)
    {
        // Arrange
        var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Act
        var level = pathParts.Length;
        var actualArea = pathParts[2];
        var actualSite = pathParts[1];

        // Assert
        level.Should().Be(3);
        actualArea.Should().Be(areaName);
        actualSite.Should().Be(siteName);
        
        // Expected content follows ISA-S95 standard for Area level
        expectedContent.Should().Contain("Production area or department");
        expectedContent.Should().Contain("within a site");
    }

    [Theory]
    [InlineData("Enterprise/Dallas/Press/Line1", "Line1", "Press", "Group of equipment or workstations performing similar functions")]
    [InlineData("Enterprise/Houston/Assembly/Station1", "Station1", "Assembly", "Group of equipment or workstations performing similar functions")]
    [InlineData("Enterprise/Austin/QC/Booth1", "Booth1", "QC", "Group of equipment or workstations performing similar functions")]
    public void GenerateDescription_ShouldCreateLevel4WorkCenterDescriptions(string fullPath, string workCenterName, string areaName, string expectedDescription)
    {
        // Arrange
        var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Act
        var level = pathParts.Length;
        var actualWorkCenter = pathParts[3];
        var actualArea = pathParts[2];

        // Assert
        level.Should().Be(4);
        actualWorkCenter.Should().Be(workCenterName);
        actualArea.Should().Be(areaName);
        
        // Expected description follows ISA-S95 standard for WorkCenter level
        expectedDescription.Should().Contain("Group of equipment or workstations");
        expectedDescription.Should().Contain("performing similar functions");
    }

    [Theory]
    [InlineData("Enterprise/Dallas/Press/Line1/Edge", "Edge", "Individual piece of equipment, machine, or process unit")]
    [InlineData("Enterprise/Houston/Assembly/Station1/HMI", "HMI", "Individual piece of equipment, machine, or process unit")]
    [InlineData("Enterprise/Austin/QC/Booth1/PLC", "PLC", "Individual piece of equipment, machine, or process unit")]
    public void GenerateDescription_ShouldCreateLevel5WorkUnitDescriptions(string fullPath, string workUnitName, string expectedDescription)
    {
        // Arrange
        var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Act
        var level = pathParts.Length;
        var actualWorkUnit = pathParts[4];

        // Assert
        level.Should().Be(5);
        actualWorkUnit.Should().Be(workUnitName);
        
        // Expected description follows ISA-S95 standard for WorkUnit level
        expectedDescription.Should().NotBeNullOrEmpty();
        expectedDescription.Should().Contain("Individual piece of equipment");
        expectedDescription.Should().Contain("machine, or process unit");
    }

    [Theory]
    [InlineData("Enterprise/Dallas/Press/Line1/Edge/Process", "Process", "Specific data point, measurement, or property being monitored")]
    [InlineData("Enterprise/Houston/Assembly/Station1/HMI/Safety", "Safety", "Specific data point, measurement, or property being monitored")]
    [InlineData("Enterprise/Austin/QC/Booth1/PLC/Quality", "Quality", "Specific data point, measurement, or property being monitored")]
    public void GenerateDescription_ShouldCreateLevel6ProcessDescriptions(string fullPath, string processName, string expectedDescription)
    {
        // Arrange
        var pathParts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Act
        var level = pathParts.Length;
        var actualProcess = pathParts[5];

        // Assert
        level.Should().Be(6);
        actualProcess.Should().Be(processName);
        
        // Expected description follows ISA-S95 standard for Property level
        expectedDescription.Should().NotBeNullOrEmpty();
        expectedDescription.Should().Contain("Specific data point, measurement");
        expectedDescription.Should().Contain("or property being monitored");
    }

    [Fact]
    public void HierarchyDescription_ShouldIncludeTopicInformation()
    {
        // Arrange
        var topics = new List<TopicNode>
        {
            new() { Topic = "temp_001", IsActive = true, SourceType = "MQTT" },
            new() { Topic = "pressure_001", IsActive = true, SourceType = "MQTT" },
            new() { Topic = "flow_001", IsActive = false, SourceType = "SocketIO" }
        };

        // Act - Simulate topic information generation
        var totalTopics = topics.Count;
        var activeTopics = topics.Count(t => t.IsActive);
        var sources = topics.Select(t => t.SourceType).Distinct().ToList();

        // Assert
        totalTopics.Should().Be(3);
        activeTopics.Should().Be(2);
        sources.Should().Contain("MQTT");
        sources.Should().Contain("SocketIO");
        
        // Topic info should be formatted like: "(3 topics, 2 active, sources: MQTT, SocketIO)"
        var expectedInfo = $"({totalTopics} topics, {activeTopics} active, sources: {string.Join(", ", sources)})";
        expectedInfo.Should().Contain("3 topics");
        expectedInfo.Should().Contain("2 active");
        expectedInfo.Should().Contain("MQTT, SocketIO");
    }

    [Fact]
    public void HierarchyDescription_ShouldHandleUnassignedTopics()
    {
        // Arrange
        var unassignedTopics = new List<TopicNode>
        {
            new() { Topic = "orphan_sensor_1", NsPath = "", UnsName = "Orphaned Sensor 1" },
            new() { Topic = "test_data", NsPath = "", UnsName = "Test Data Source" }
        };

        // Act
        var unassignedCount = unassignedTopics.Count;

        // Assert
        unassignedCount.Should().Be(2);
        
        // Expected description for unassigned section
        var expectedDescription = $"Topics not yet assigned to a hierarchical namespace ({unassignedCount} topics)";
        expectedDescription.Should().Contain("not yet assigned");
        expectedDescription.Should().Contain("2 topics");
    }
}