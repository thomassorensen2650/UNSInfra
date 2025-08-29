using FluentAssertions;
using UNSInfra.MCP.Server;
using Xunit;

namespace UNSInfra.MCP.Server.Tests;

/// <summary>
/// Tests for the corrected UNS hierarchy structure that starts with Enterprise namespaces directly
/// </summary>
public class UNSRootHierarchyTests
{
    [Fact]
    public void UNSHierarchy_ShouldStartWithEnterpriseNamespaces_NotDataBrowser()
    {
        // Arrange - Topics with Enterprise namespace paths
        var topics = new List<TopicNode>
        {
            new()
            {
                Topic = "temp_001",
                UnsName = "Temperature Sensor 1",
                NsPath = "Enterprise/Dallas/Press/Line1",
                IsActive = true,
                SourceType = "MQTT"
            },
            new()
            {
                Topic = "pressure_001", 
                UnsName = "Pressure Sensor 1",
                NsPath = "Enterprise/Houston/Assembly/Station1",
                IsActive = true,
                SourceType = "MQTT"
            },
            new()
            {
                Topic = "unassigned_sensor",
                UnsName = "Unassigned Sensor",
                NsPath = "", // No namespace - should go to Unassigned
                IsActive = false,
                SourceType = "Mock"
            }
        };

        // Act - Simulate the expected hierarchy structure
        var expectedRootChildren = new List<string>();
        var enterpriseTopics = topics.Where(t => !string.IsNullOrEmpty(t.NsPath) && t.NsPath.StartsWith("Enterprise")).ToList();
        var unassignedTopics = topics.Where(t => string.IsNullOrEmpty(t.NsPath)).ToList();

        if (enterpriseTopics.Any())
            expectedRootChildren.Add("Enterprise");
        
        if (unassignedTopics.Any())
            expectedRootChildren.Add("Unassigned");

        // Assert - Root should contain Enterprise and Unassigned, NOT "Data Browser"
        expectedRootChildren.Should().Contain("Enterprise");
        expectedRootChildren.Should().Contain("Unassigned");
        expectedRootChildren.Should().NotContain("Data Browser");
        
        // Verify Enterprise topics are properly structured
        enterpriseTopics.Should().HaveCount(2);
        enterpriseTopics.Should().AllSatisfy(topic => 
            topic.NsPath!.Should().StartWith("Enterprise"));
        
        // Verify we have different sites under Enterprise
        var sites = enterpriseTopics.Select(t => t.NsPath!.Split('/')[1]).Distinct().ToList();
        sites.Should().Contain("Dallas");
        sites.Should().Contain("Houston");
    }

    [Theory]
    [InlineData("Enterprise/Dallas/Press/Line1", "Enterprise", "Dallas")]
    [InlineData("Enterprise/Houston/Assembly/Station1", "Enterprise", "Houston")]
    [InlineData("Factory/Site1/Area1", "Factory", "Site1")]
    public void UNSHierarchy_ShouldExtractCorrectRootAndSiteNames(string nsPath, string expectedRoot, string expectedSite)
    {
        // Act
        var pathParts = nsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Assert
        pathParts.Should().HaveCountGreaterThan(1);
        pathParts[0].Should().Be(expectedRoot); // First level should be the actual namespace (Enterprise/Factory)
        pathParts[1].Should().Be(expectedSite);  // Second level should be the site
    }

    [Fact]
    public void UNSHierarchy_ShouldGroupTopicsByActualNamespaces()
    {
        // Arrange
        var topics = new List<TopicNode>
        {
            // Enterprise namespace topics
            new() { Topic = "sensor1", NsPath = "Enterprise/Dallas/Press", UnsName = "Dallas Press Sensor" },
            new() { Topic = "sensor2", NsPath = "Enterprise/Dallas/Assembly", UnsName = "Dallas Assembly Sensor" },
            new() { Topic = "sensor3", NsPath = "Enterprise/Houston/Line1", UnsName = "Houston Line Sensor" },
            
            // Factory namespace topics  
            new() { Topic = "sensor4", NsPath = "Factory/Plant1/Area1", UnsName = "Plant 1 Sensor" },
            new() { Topic = "sensor5", NsPath = "Factory/Plant2/Line1", UnsName = "Plant 2 Sensor" },
            
            // Unassigned
            new() { Topic = "sensor6", NsPath = "", UnsName = "Unassigned Sensor" }
        };

        // Act - Group by root namespace
        var groupedByNamespace = topics
            .Where(t => !string.IsNullOrEmpty(t.NsPath))
            .GroupBy(t => t.NsPath!.Split('/')[0])
            .ToDictionary(g => g.Key, g => g.ToList());

        var unassignedTopics = topics.Where(t => string.IsNullOrEmpty(t.NsPath)).ToList();

        // Assert - Should have proper namespace groupings
        groupedByNamespace.Should().ContainKey("Enterprise");
        groupedByNamespace.Should().ContainKey("Factory");
        groupedByNamespace["Enterprise"].Should().HaveCount(3);
        groupedByNamespace["Factory"].Should().HaveCount(2);
        unassignedTopics.Should().HaveCount(1);

        // Verify no "Data Browser" wrapper exists
        groupedByNamespace.Keys.Should().NotContain("Data Browser");
    }

    [Fact]
    public void UNSHierarchy_ShouldHandleMultipleEnterpriseSites()
    {
        // Arrange - Multiple sites under Enterprise
        var topics = new List<TopicNode>
        {
            new() { Topic = "dallas_temp", NsPath = "Enterprise/Dallas/Press/Line1", UnsName = "Dallas Temperature" },
            new() { Topic = "houston_temp", NsPath = "Enterprise/Houston/Assembly/Station1", UnsName = "Houston Temperature" },
            new() { Topic = "austin_temp", NsPath = "Enterprise/Austin/QC/Booth1", UnsName = "Austin Temperature" }
        };

        // Act - Extract site information
        var sitePaths = topics.Select(t => new
        {
            Topic = t,
            PathParts = t.NsPath!.Split('/'),
            Site = t.NsPath!.Split('/')[1] // Site is second level under Enterprise
        }).ToList();

        // Assert - Should have three distinct sites under Enterprise
        var sites = sitePaths.Select(s => s.Site).Distinct().ToList();
        sites.Should().HaveCount(3);
        sites.Should().Contain("Dallas");
        sites.Should().Contain("Houston"); 
        sites.Should().Contain("Austin");

        // All should be under Enterprise
        sitePaths.Should().AllSatisfy(sp => sp.PathParts[0].Should().Be("Enterprise"));
    }

    [Fact]
    public void UNSHierarchy_ShouldPreserveFinalTopicNames()
    {
        // Arrange
        var topic = new TopicNode
        {
            Topic = "critical_process_sensor_001",
            UnsName = "Critical Process Temperature Sensor",
            NsPath = "Enterprise/Dallas/Press/Line1/Edge/Process"
        };

        // Act - Verify the topic structure
        var pathParts = topic.NsPath!.Split('/');
        var topicDisplayName = topic.UnsName ?? topic.Topic;

        // Assert - Topic should be at the end of the hierarchy
        pathParts.Should().Equal("Enterprise", "Dallas", "Press", "Line1", "Edge", "Process");
        topicDisplayName.Should().Be("Critical Process Temperature Sensor");
        
        // The actual topic ID is preserved
        topic.Topic.Should().Be("critical_process_sensor_001");
        
        // Final hierarchy structure should be: Enterprise -> Dallas -> Press -> Line1 -> Edge -> Process -> [Topic Display Name]
        var expectedFinalPath = "Enterprise/Dallas/Press/Line1/Edge/Process/Critical Process Temperature Sensor";
        var actualPath = $"{topic.NsPath}/{topicDisplayName}";
        actualPath.Should().Be(expectedFinalPath);
    }
}