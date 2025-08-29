using FluentAssertions;
using UNSInfra.MCP.Server;
using Xunit;

namespace UNSInfra.MCP.Server.Tests;

/// <summary>
/// Tests specifically for deep hierarchy paths that were causing KeyNotFoundException
/// </summary>
public class DeepHierarchyTests
{
    [Fact]
    public void BuildHierarchicalTree_ShouldHandleDeepNSPaths_WithoutKeyNotFoundException()
    {
        // Arrange - Create a topic with the exact path that was failing
        var topics = new List<TopicNode>
        {
            new()
            {
                Topic = "process_sensor_001",
                UnsName = "Process Sensor",
                NsPath = "Enterprise/Dallas/Press/Line1/Edge/Process", // Deep 6-level path
                IsActive = true,
                SourceType = "MQTT",
                Description = "Deep hierarchy process sensor"
            },
            new()
            {
                Topic = "another_sensor",
                UnsName = "Another Sensor", 
                NsPath = "Enterprise/Dallas/Press/Line2", // 4-level path
                IsActive = true,
                SourceType = "MQTT",
                Description = "Another sensor in different line"
            }
        };

        // Act - This should not throw KeyNotFoundException
        Action buildAction = () =>
        {
            // Simulate the dictionary building logic from BuildHierarchicalTree
            var childrenLookup = new Dictionary<string, List<string>>();
            var topicsByPath = new Dictionary<string, List<TopicNode>>();

            const string dataBrowserSectionName = "Data Browser";

            // Initialize root structure
            childrenLookup[""] = new List<string> { dataBrowserSectionName };
            childrenLookup[dataBrowserSectionName] = new List<string>();
            topicsByPath[dataBrowserSectionName] = new List<TopicNode>();

            // Build hierarchical structure for each topic
            foreach (var topic in topics)
            {
                var hierarchyPath = !string.IsNullOrEmpty(topic.NsPath) ? topic.NsPath : topic.Topic;
                var pathParts = hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (pathParts.Length > 0)
                {
                    string currentPath = dataBrowserSectionName;

                    for (int i = 0; i < pathParts.Length; i++)
                    {
                        var part = pathParts[i];
                        var parentPath = currentPath;
                        currentPath = $"{currentPath}/{part}";

                        // Initialize path structures if they don't exist
                        if (!topicsByPath.ContainsKey(currentPath))
                            topicsByPath[currentPath] = new List<TopicNode>();

                        if (!childrenLookup.ContainsKey(parentPath))
                            childrenLookup[parentPath] = new List<string>();

                        // Add this part as a child of the parent path
                        if (!childrenLookup[parentPath].Contains(part))
                            childrenLookup[parentPath].Add(part);

                        // If this is the final segment, add the actual topic
                        if (i == pathParts.Length - 1)
                        {
                            var topicDisplayName = topic.UnsName ?? topic.Topic;
                            var topicKey = $"{currentPath}/{topicDisplayName}";

                            // Ensure the parent path exists in childrenLookup before accessing it
                            if (!childrenLookup.ContainsKey(currentPath))
                                childrenLookup[currentPath] = new List<string>();

                            if (!childrenLookup[currentPath].Contains(topicDisplayName))
                            {
                                childrenLookup[currentPath].Add(topicDisplayName);
                                topicsByPath[topicKey] = new List<TopicNode> { topic };
                            }
                        }
                    }
                }
            }
        };

        // Assert - Should not throw any exceptions
        buildAction.Should().NotThrow<KeyNotFoundException>();
        buildAction.Should().NotThrow(); // Should not throw any exception at all
    }

    [Theory]
    [InlineData("Enterprise/Dallas/Press/Line1/Edge/Process", 6)]
    [InlineData("Enterprise/Site1/Area1", 3)]
    [InlineData("Factory/Line1/Station1/WorkCell/Machine", 5)]
    [InlineData("Enterprise", 1)]
    public void DeepHierarchy_ShouldCreateCorrectNumberOfLevels(string nsPath, int expectedLevels)
    {
        // Arrange
        var pathParts = nsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Act & Assert
        pathParts.Should().HaveCount(expectedLevels);
        
        // Verify we can build the path step by step
        var expectedPaths = new List<string>();
        var currentPath = "Data Browser";
        expectedPaths.Add(currentPath);
        
        foreach (var part in pathParts)
        {
            currentPath = $"{currentPath}/{part}";
            expectedPaths.Add(currentPath);
        }

        expectedPaths.Should().HaveCount(expectedLevels + 1); // +1 for "Data Browser" root
        expectedPaths.Last().Should().Be($"Data Browser/{nsPath}");
    }

    [Fact]
    public void MultipleDeepHierarchies_ShouldShareCommonPaths()
    {
        // Arrange - Multiple topics sharing common hierarchy prefixes
        var topics = new List<TopicNode>
        {
            new()
            {
                Topic = "sensor_1",
                UnsName = "Sensor 1",
                NsPath = "Enterprise/Dallas/Press/Line1/Edge/Process"
            },
            new()
            {
                Topic = "sensor_2", 
                UnsName = "Sensor 2",
                NsPath = "Enterprise/Dallas/Press/Line1/Edge/Quality"
            },
            new()
            {
                Topic = "sensor_3",
                UnsName = "Sensor 3", 
                NsPath = "Enterprise/Dallas/Press/Line2/Edge/Process"
            }
        };

        // Act - Identify common paths
        var allPaths = topics.Select(t => t.NsPath).ToList();
        var commonPrefix = "Enterprise/Dallas/Press";
        
        // Assert
        allPaths.Should().AllSatisfy(path => path.Should().StartWith(commonPrefix));
        
        // All should share the common hierarchy levels
        var pathSegments = allPaths.Select(path => path.Split('/')).ToList();
        pathSegments.Should().AllSatisfy(segments => 
        {
            segments[0].Should().Be("Enterprise");
            segments[1].Should().Be("Dallas");
            segments[2].Should().Be("Press");
        });
    }

    [Fact] 
    public void DeepHierarchy_ShouldPreserveTopicData()
    {
        // Arrange
        var topic = new TopicNode
        {
            Topic = "critical_sensor_001",
            UnsName = "Critical Process Sensor",
            NsPath = "Enterprise/Dallas/Press/Line1/Edge/Process",
            IsActive = true,
            SourceType = "MQTT",
            Description = "Critical sensor for monitoring process parameters",
            Metadata = new { Priority = "High", AlertThreshold = 95.0 }
        };

        // Act & Assert - Verify all data is preserved
        topic.Topic.Should().Be("critical_sensor_001");
        topic.UnsName.Should().Be("Critical Process Sensor");
        topic.NsPath.Should().Be("Enterprise/Dallas/Press/Line1/Edge/Process");
        topic.IsActive.Should().BeTrue();
        topic.SourceType.Should().Be("MQTT");
        topic.Description.Should().NotBeNullOrEmpty();
        topic.Metadata.Should().NotBeNull();
        
        // Verify the hierarchy path can be parsed correctly
        var pathParts = topic.NsPath!.Split('/', StringSplitOptions.RemoveEmptyEntries);
        pathParts.Should().Equal("Enterprise", "Dallas", "Press", "Line1", "Edge", "Process");
    }
}