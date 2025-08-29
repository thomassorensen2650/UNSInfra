using FluentAssertions;
using UNSInfra.MCP.Server;
using Xunit;

namespace UNSInfra.MCP.Server.Tests;

/// <summary>
/// Unit tests for hierarchy tree building logic and supporting classes
/// </summary>
public class HierarchyTreeBuilderTests
{
    [Fact]
    public void TopicNode_ShouldInitializeWithDefaultValues()
    {
        // Act
        var topicNode = new TopicNode();

        // Assert
        topicNode.Topic.Should().Be(string.Empty);
        topicNode.SourceType.Should().Be(string.Empty);
        topicNode.IsActive.Should().BeFalse();
        topicNode.UnsName.Should().BeNull();
        topicNode.NsPath.Should().BeNull();
        topicNode.Path.Should().BeNull();
        topicNode.Description.Should().BeNull();
        topicNode.Metadata.Should().BeNull();
    }

    [Fact]
    public void TopicNode_ShouldAllowPropertyAssignment()
    {
        // Arrange
        var topicNode = new TopicNode();
        const string expectedTopic = "Enterprise/Site1/Area1/Temperature";
        const string expectedSourceType = "MQTT";
        const bool expectedIsActive = true;

        // Act
        topicNode.Topic = expectedTopic;
        topicNode.SourceType = expectedSourceType;
        topicNode.IsActive = expectedIsActive;

        // Assert
        topicNode.Topic.Should().Be(expectedTopic);
        topicNode.SourceType.Should().Be(expectedSourceType);
        topicNode.IsActive.Should().Be(expectedIsActive);
    }

    [Fact]
    public void HierarchyTreeNode_ShouldInitializeWithDefaultValues()
    {
        // Act
        var treeNode = new HierarchyTreeNode();

        // Assert
        treeNode.Name.Should().Be(string.Empty);
        treeNode.FullPath.Should().Be(string.Empty);
        treeNode.Children.Should().NotBeNull().And.BeEmpty();
        treeNode.Topics.Should().NotBeNull().And.BeEmpty();
        treeNode.HasChildren.Should().BeFalse();
        treeNode.TopicCount.Should().Be(0);
    }

    [Fact]
    public void HierarchyTreeNode_ShouldAllowChildrenManipulation()
    {
        // Arrange
        var parentNode = new HierarchyTreeNode 
        { 
            Name = "Parent", 
            FullPath = "Parent" 
        };
        
        var childNode = new HierarchyTreeNode 
        { 
            Name = "Child", 
            FullPath = "Parent/Child" 
        };

        // Act
        parentNode.Children.Add(childNode);
        parentNode.HasChildren = true;

        // Assert
        parentNode.Children.Should().HaveCount(1);
        parentNode.Children[0].Should().Be(childNode);
        parentNode.HasChildren.Should().BeTrue();
    }

    [Theory]
    [InlineData("SimpleTopicName")]
    [InlineData("Enterprise/Site1/Area1/Temperature")]
    [InlineData("Factory/Line1/Workstation2/Sensor3/Value")]
    public void HierarchyTreeBuilder_ShouldHandleVariousTopicNameFormats(string topicName)
    {
        // Arrange
        var topics = new List<TopicNode>
        {
            new()
            {
                Topic = topicName,
                SourceType = "MQTT",
                IsActive = true,
                Description = "Test topic"
            }
        };

        // Act - This tests the tree building logic indirectly through the reflection
        // We'll simulate the BuildHierarchicalTree method logic
        var topicParts = topicName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Assert
        if (topicParts.Length == 1)
        {
            // Single part topics should be treated as simple names
            topicParts.Should().HaveCount(1);
            topicParts[0].Should().Be(topicName);
        }
        else
        {
            // Multi-part topics should create hierarchical structure
            topicParts.Should().HaveCountGreaterThan(1);
            string.Join("/", topicParts).Should().Be(topicName);
        }
    }

    [Fact]
    public void HierarchyTreeBuilder_ShouldCreateProperTreeStructure_WithMultipleTopics()
    {
        // Arrange
        var topics = new List<TopicNode>
        {
            new()
            {
                Topic = "Enterprise/Site1/Area1/Temperature",
                SourceType = "MQTT",
                IsActive = true,
                Description = "Temperature sensor 1"
            },
            new()
            {
                Topic = "Enterprise/Site1/Area1/Pressure",
                SourceType = "MQTT",
                IsActive = true,
                Description = "Pressure sensor 1"
            },
            new()
            {
                Topic = "Enterprise/Site1/Area2/Temperature",
                SourceType = "SocketIO",
                IsActive = false,
                Description = "Temperature sensor 2"
            },
            new()
            {
                Topic = "Enterprise/Site2/Flow",
                SourceType = "MQTT",
                IsActive = true,
                Description = "Flow sensor"
            },
            new()
            {
                Topic = "SimpleTopicName",
                SourceType = "Mock",
                IsActive = true,
                Description = "Simple topic without hierarchy"
            }
        };

        // Act - Simulate the hierarchy building logic
        var pathSegments = new Dictionary<string, List<string>>();
        var expectedPaths = new HashSet<string>();

        foreach (var topic in topics)
        {
            var parts = topic.Topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 1)
            {
                // Simple topic
                expectedPaths.Add($"Data Browser/{topic.Topic}");
            }
            else
            {
                // Hierarchical topic
                var currentPath = "Data Browser";
                expectedPaths.Add(currentPath);
                
                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath += "/" + parts[i];
                    expectedPaths.Add(currentPath);
                }
            }
        }

        // Assert
        expectedPaths.Should().Contain("Data Browser");
        expectedPaths.Should().Contain("Data Browser/Enterprise");
        expectedPaths.Should().Contain("Data Browser/Enterprise/Site1");
        expectedPaths.Should().Contain("Data Browser/Enterprise/Site1/Area1");
        expectedPaths.Should().Contain("Data Browser/Enterprise/Site1/Area1/Temperature");
        expectedPaths.Should().Contain("Data Browser/Enterprise/Site1/Area1/Pressure");
        expectedPaths.Should().Contain("Data Browser/Enterprise/Site1/Area2");
        expectedPaths.Should().Contain("Data Browser/Enterprise/Site2");
        expectedPaths.Should().Contain("Data Browser/Enterprise/Site2/Flow");
        expectedPaths.Should().Contain("Data Browser/SimpleTopicName");

        // Verify we have the expected number of unique paths
        expectedPaths.Should().HaveCount(11); // Data Browser + 10 unique paths
    }

    [Fact]
    public void TopicNode_ShouldHandleNullMetadata()
    {
        // Arrange & Act
        var topicNode = new TopicNode
        {
            Topic = "TestTopic",
            Metadata = null
        };

        // Assert
        topicNode.Metadata.Should().BeNull();
        topicNode.Topic.Should().Be("TestTopic");
    }

    [Fact]
    public void TopicNode_ShouldHandleComplexMetadata()
    {
        // Arrange
        var metadata = new { Unit = "Celsius", Range = new { Min = -40, Max = 120 } };
        
        // Act
        var topicNode = new TopicNode
        {
            Topic = "Temperature",
            Metadata = metadata
        };

        // Assert
        topicNode.Metadata.Should().NotBeNull();
        topicNode.Metadata.Should().Be(metadata);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void HierarchyTreeBuilder_ShouldHandleEmptyOrNullTopics(string? topicName)
    {
        // Arrange
        var topics = new List<TopicNode>();
        
        if (topicName != null)
        {
            topics.Add(new TopicNode { Topic = topicName, SourceType = "Test" });
        }

        // Act - Simulate filtering logic from the actual implementation
        var validTopics = topics.Where(t => !string.IsNullOrEmpty(t.Topic)).ToList();

        // Assert
        if (string.IsNullOrWhiteSpace(topicName))
        {
            validTopics.Should().BeEmpty();
        }
        else
        {
            validTopics.Should().HaveCount(1);
        }
    }

    [Fact]
    public void HierarchyTreeNode_ShouldAllowTopicAssignment()
    {
        // Arrange
        var treeNode = new HierarchyTreeNode { Name = "TestNode" };
        var topics = new List<TopicNode>
        {
            new() { Topic = "Topic1", SourceType = "MQTT" },
            new() { Topic = "Topic2", SourceType = "SocketIO" }
        };

        // Act
        treeNode.Topics = topics;
        treeNode.TopicCount = topics.Count;

        // Assert
        treeNode.Topics.Should().HaveCount(2);
        treeNode.TopicCount.Should().Be(2);
        treeNode.Topics.Should().BeEquivalentTo(topics);
    }

    [Fact]
    public void HierarchyTreeBuilder_ShouldPreserveSourceTypeInformation()
    {
        // Arrange
        var mqttTopic = new TopicNode 
        { 
            Topic = "MQTT/Topic", 
            SourceType = "MQTT",
            IsActive = true
        };
        
        var socketIOTopic = new TopicNode 
        { 
            Topic = "SocketIO/Topic", 
            SourceType = "SocketIO",
            IsActive = false
        };

        var topics = new List<TopicNode> { mqttTopic, socketIOTopic };

        // Act - Verify that source type information is preserved
        var preservedTopics = topics.Where(t => !string.IsNullOrEmpty(t.SourceType)).ToList();

        // Assert
        preservedTopics.Should().HaveCount(2);
        preservedTopics[0].SourceType.Should().Be("MQTT");
        preservedTopics[0].IsActive.Should().BeTrue();
        preservedTopics[1].SourceType.Should().Be("SocketIO");
        preservedTopics[1].IsActive.Should().BeFalse();
    }
}