using System.Text.Json;
using FluentAssertions;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Moq;
using UNSInfra.MCP.Server;
using Xunit;

namespace UNSInfra.MCP.Server.Tests;

/// <summary>
/// Unit tests for GraphQL MCP Tools
/// </summary>
public class GraphQLMcpToolsTests
{
    private readonly Mock<IGraphQLClient> _mockGraphQLClient;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IMcpServer> _mockMcpServer;

    public GraphQLMcpToolsTests()
    {
        _mockGraphQLClient = new Mock<IGraphQLClient>();
        _mockLogger = new Mock<ILogger>();
        _mockMcpServer = new Mock<IMcpServer>();
    }

    [Fact]
    public async Task GetUnsHierarchyAsync_ShouldReturnSuccess_WhenGraphQLReturnsValidData()
    {
        // Arrange
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new
            {
                systemStatus = new
                {
                    totalTopics = 10,
                    assignedTopics = 8,
                    activeTopics = 6,
                    namespaces = 2,
                    timestamp = "2024-01-01T12:00:00Z"
                },
                namespaces = new[] { "Enterprise", "Test" },
                topics = new[]
                {
                    new
                    {
                        topic = "Enterprise/Site1/Area1/Temperature",
                        unsName = "Site1 Temperature",
                        nsPath = "Enterprise/Site1/Area1",
                        path = "Enterprise/Site1/Area1/Temperature",
                        isActive = true,
                        sourceType = "MQTT",
                        createdAt = "2024-01-01T10:00:00Z",
                        modifiedAt = "2024-01-01T11:00:00Z",
                        lastDataTimestamp = "2024-01-01T11:30:00Z",
                        description = "Temperature sensor data"
                    }
                }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetUnsHierarchyAsync(_mockGraphQLClient.Object);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonDoc.RootElement.GetProperty("message").GetString()
            .Should().Contain("UNS hierarchy retrieved successfully");
        
        // Verify hierarchyData structure
        var hierarchyData = jsonDoc.RootElement.GetProperty("hierarchyData");
        hierarchyData.Should().NotBeNull();
        hierarchyData.GetProperty("systemStatus").Should().NotBeNull();
        hierarchyData.GetProperty("namespaces").Should().NotBeNull();
    }

    [Fact]
    public async Task GetUnsHierarchyTreeAsync_ShouldBuildCorrectHierarchy_WhenGivenTopics()
    {
        // Arrange
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new
            {
                systemStatus = new { totalTopics = 3 },
                namespaces = new[] { "Enterprise" },
                topics = new[]
                {
                    new
                    {
                        topic = "temperature_sensor_001",
                        unsName = "Site1 Area1 Temperature",
                        nsPath = "Enterprise/Site1/Area1",
                        path = "Enterprise/Site1/Area1/temperature_sensor_001",
                        isActive = true,
                        sourceType = "MQTT",
                        description = "Temperature sensor in Area 1"
                    },
                    new
                    {
                        topic = "pressure_sensor_001", 
                        unsName = "Site1 Area1 Pressure",
                        nsPath = "Enterprise/Site1/Area1",
                        path = "Enterprise/Site1/Area1/pressure_sensor_001",
                        isActive = true,
                        sourceType = "MQTT",
                        description = "Pressure sensor in Area 1"
                    },
                    new
                    {
                        topic = "temp_001",
                        unsName = "Site2 Temperature", 
                        nsPath = "Enterprise/Site2",
                        path = "Enterprise/Site2/temp_001",
                        isActive = false,
                        sourceType = "SocketIO",
                        description = "Temperature sensor in Site 2"
                    }
                }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetUnsHierarchyTreeAsync(_mockGraphQLClient.Object);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        
        // Verify success
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonDoc.RootElement.GetProperty("topicCount").GetInt32().Should().Be(3);
        
        // Verify tree structure
        var hierarchyTree = jsonDoc.RootElement.GetProperty("hierarchyTree");
        hierarchyTree.Should().NotBeNull();
        
        var rootNode = hierarchyTree;
        rootNode.GetProperty("name").GetString().Should().Be("Root");
        rootNode.GetProperty("hasChildren").GetBoolean().Should().BeTrue();
        
        // Should have Data Browser as root child
        var children = rootNode.GetProperty("children");
        children.Should().NotBeNull();
        children.GetArrayLength().Should().Be(1);
        
        var dataBrowserNode = children[0];
        dataBrowserNode.GetProperty("name").GetString().Should().Be("Data Browser");
        dataBrowserNode.GetProperty("hasChildren").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetTopicAsync_ShouldReturnTopicData_WhenTopicExists()
    {
        // Arrange
        const string topicName = "Enterprise/Site1/Area1/Temperature";
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new
            {
                topic = new
                {
                    topic = topicName,
                    unsName = "Site1 Temperature",
                    nsPath = "Enterprise/Site1/Area1",
                    isActive = true,
                    sourceType = "MQTT",
                    description = "Temperature sensor data"
                }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetTopicAsync(
            _mockMcpServer.Object, 
            _mockGraphQLClient.Object, 
            _mockLogger.Object, 
            topicName);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonDoc.RootElement.GetProperty("topicName").GetString().Should().Be(topicName);
        
        var topicData = jsonDoc.RootElement.GetProperty("topic");
        topicData.GetProperty("topic").GetString().Should().Be(topicName);
        topicData.GetProperty("sourceType").GetString().Should().Be("MQTT");
    }

    [Fact]
    public async Task GetTopicAsync_ShouldReturnNotFound_WhenTopicDoesNotExist()
    {
        // Arrange
        const string topicName = "NonExistent/Topic";
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new { topic = (object?)null }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetTopicAsync(
            _mockMcpServer.Object, 
            _mockGraphQLClient.Object, 
            _mockLogger.Object, 
            topicName);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        jsonDoc.RootElement.GetProperty("message").GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task GetTopicsByNamespaceAsync_ShouldReturnFilteredTopics_WhenNamespaceExists()
    {
        // Arrange
        const string namespaceName = "Enterprise";
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new
            {
                topicsByNamespace = new[]
                {
                    new
                    {
                        topic = "Enterprise/Site1/Temperature",
                        nsPath = "Enterprise/Site1",
                        sourceType = "MQTT"
                    },
                    new
                    {
                        topic = "Enterprise/Site2/Pressure", 
                        nsPath = "Enterprise/Site2",
                        sourceType = "SocketIO"
                    }
                }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetTopicsByNamespaceAsync(
            _mockMcpServer.Object, 
            _mockGraphQLClient.Object, 
            _mockLogger.Object, 
            namespaceName);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonDoc.RootElement.GetProperty("namespaceName").GetString().Should().Be(namespaceName);
        
        var topics = jsonDoc.RootElement.GetProperty("topics");
        topics.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task SearchTopicsAsync_ShouldReturnMatchingTopics_WhenSearchTermMatches()
    {
        // Arrange
        const string searchTerm = "Temperature";
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new
            {
                searchTopics = new[]
                {
                    new
                    {
                        topic = "Enterprise/Site1/Temperature",
                        description = "Temperature sensor"
                    },
                    new
                    {
                        topic = "Factory/Line1/Temperature",
                        description = "Another temperature sensor"
                    }
                }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.SearchTopicsAsync(
            _mockMcpServer.Object, 
            _mockGraphQLClient.Object, 
            _mockLogger.Object, 
            searchTerm);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonDoc.RootElement.GetProperty("searchTerm").GetString().Should().Be(searchTerm);
        
        var topics = jsonDoc.RootElement.GetProperty("topics");
        topics.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetSystemStatusAsync_ShouldReturnSystemStatus_WhenDataAvailable()
    {
        // Arrange
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new
            {
                systemStatus = new
                {
                    totalTopics = 100,
                    assignedTopics = 85,
                    activeTopics = 70,
                    totalConnections = 5,
                    activeConnections = 3,
                    namespaces = 3,
                    timestamp = "2024-01-01T12:00:00Z",
                    connectionStats = new
                    {
                        totalConnections = 5,
                        activeConnections = 3,
                        inactiveConnections = 2
                    }
                }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetSystemStatusAsync(
            _mockMcpServer.Object, 
            _mockGraphQLClient.Object, 
            _mockLogger.Object);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        
        var systemStatus = jsonDoc.RootElement.GetProperty("systemStatus");
        systemStatus.GetProperty("totalTopics").GetInt32().Should().Be(100);
        systemStatus.GetProperty("activeConnections").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task TestGraphQLConnectivityAsync_ShouldReturnSuccess_WhenConnectivityWorks()
    {
        // Arrange
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new
            {
                namespaces = new[] { "Enterprise", "Test" }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.TestGraphQLConnectivityAsync(_mockGraphQLClient.Object);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonDoc.RootElement.GetProperty("connectivity").GetString().Should().Be("GraphQL endpoint accessible");
    }

    [Fact]
    public async Task AllTools_ShouldReturnErrorResponse_WhenGraphQLClientThrowsException()
    {
        // Arrange
        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act & Assert - Test each tool handles exceptions properly
        var connectivityResult = await GraphQLMcpTools.TestGraphQLConnectivityAsync(_mockGraphQLClient.Object);
        var hierarchyResult = await GraphQLMcpTools.GetUnsHierarchyAsync(_mockGraphQLClient.Object);
        var treeResult = await GraphQLMcpTools.GetUnsHierarchyTreeAsync(_mockGraphQLClient.Object);

        // All should return error responses
        foreach (var result in new[] { connectivityResult, hierarchyResult, treeResult })
        {
            result.Should().NotBeNullOrEmpty();
            var jsonDoc = JsonDocument.Parse(result);
            jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            jsonDoc.RootElement.GetProperty("error").GetString().Should().Contain("Connection refused");
        }
    }

    [Fact]
    public void Echo_ShouldReturnFormattedMessage()
    {
        // Arrange
        const string testMessage = "test message";

        // Act
        var result = GraphQLMcpTools.Echo(testMessage);

        // Assert
        result.Should().Be($"hello {testMessage}");
    }
}