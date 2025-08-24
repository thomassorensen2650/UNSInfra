using System.Dynamic;
using System.Text.Json;
using FluentAssertions;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.MCP.Server;

namespace UNSInfra.MCP.Server.Tests;

public class GraphQLMcpToolsTests
{
    private readonly Mock<IGraphQLClient> _mockGraphQLClient;
    private readonly Mock<ILogger> _mockLogger;
    private readonly CancellationToken _cancellationToken;

    public GraphQLMcpToolsTests()
    {
        _mockGraphQLClient = new Mock<IGraphQLClient>();
        _mockLogger = new Mock<ILogger>();
        _cancellationToken = CancellationToken.None;
    }

    #region GetUnsHierarchyAsync Tests

    [Fact]
    public async Task GetUnsHierarchyAsync_WithValidResponse_ShouldReturnSuccessResult()
    {
        // Arrange
        dynamic mockData = new ExpandoObject();
        dynamic mockSystemStatus = new ExpandoObject();
        mockSystemStatus.totalTopics = 10;
        mockSystemStatus.assignedTopics = 8;
        mockSystemStatus.activeTopics = 6;
        mockSystemStatus.namespaces = 3;
        mockSystemStatus.timestamp = DateTime.UtcNow.ToString();
        mockData.systemStatus = mockSystemStatus;
        mockData.namespaces = new[] { "Enterprise1", "Enterprise2", "Factory" };

        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = mockData,
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetUnsHierarchyAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonResult.GetProperty("message").GetString().Should().Be("UNS hierarchy retrieved successfully via GraphQL");
        jsonResult.TryGetProperty("hierarchyData", out _).Should().BeTrue();

        // Verify GraphQL query was called
        _mockGraphQLClient.Verify(x => x.SendQueryAsync<dynamic>(
            It.Is<GraphQLRequest>(q => q.Query.Contains("systemStatus") && q.Query.Contains("namespaces")), 
            _cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetUnsHierarchyAsync_WithGraphQLErrors_ShouldReturnErrorResult()
    {
        // Arrange
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = null,
            Errors = new[]
            {
                new GraphQLError { Message = "Database connection failed" },
                new GraphQLError { Message = "Timeout occurred" }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetUnsHierarchyAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
        jsonResult.GetProperty("message").GetString().Should().Contain("GraphQL query failed");
        jsonResult.GetProperty("hierarchyData").ValueKind.Should().Be(JsonValueKind.Null);

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GraphQL query failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUnsHierarchyAsync_WithException_ShouldReturnErrorResult()
    {
        // Arrange
        var expectedException = new HttpRequestException("Connection refused");
        
        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ThrowsAsync(expectedException);

        // Act
        var result = await GraphQLMcpTools.GetUnsHierarchyAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
        jsonResult.GetProperty("message").GetString().Should().Be("Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.");
        jsonResult.GetProperty("error").GetString().Should().Be(expectedException.Message);
        jsonResult.GetProperty("hierarchyData").ValueKind.Should().Be(JsonValueKind.Null);

        // Verify exception logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving UNS hierarchy via GraphQL")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetTopicAsync Tests

    [Fact]
    public async Task GetTopicAsync_WithValidTopicName_ShouldReturnSuccessResult()
    {
        // Arrange
        const string topicName = "sensor/temperature";
        
        dynamic mockData = new ExpandoObject();
        dynamic mockTopic = new ExpandoObject();
        mockTopic.topic = topicName;
        mockTopic.unsName = "Temperature Sensor";
        mockTopic.nsPath = "Factory/Area1/Sensor1";
        mockTopic.path = new { Enterprise = "Factory", Site = "Area1", Area = "Sensor1" };
        mockTopic.isActive = true;
        mockTopic.sourceType = "MQTT";
        mockTopic.createdAt = DateTime.UtcNow.ToString();
        mockTopic.modifiedAt = DateTime.UtcNow.ToString();
        mockTopic.lastDataTimestamp = DateTime.UtcNow.ToString();
        mockTopic.description = "Factory temperature sensor";
        mockData.topic = mockTopic;

        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = mockData,
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, topicName, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonResult.GetProperty("message").GetString().Should().Be("Topic information retrieved successfully via GraphQL");
        jsonResult.GetProperty("topicName").GetString().Should().Be(topicName);
        jsonResult.TryGetProperty("topic", out _).Should().BeTrue();

        // Verify GraphQL query was called with correct variables
        _mockGraphQLClient.Verify(x => x.SendQueryAsync<dynamic>(
            It.Is<GraphQLRequest>(q => 
                q.Query.Contains("query($topicName: String!)") && 
                q.Variables != null), 
            _cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetTopicAsync_WithNullOrEmptyTopicName_ShouldReturnErrorResult()
    {
        // Arrange & Act
        var resultNull = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, null!, _cancellationToken);
        
        var resultEmpty = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "", _cancellationToken);

        var resultWhitespace = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "   ", _cancellationToken);

        // Assert
        foreach (var result in new[] { resultNull, resultEmpty, resultWhitespace })
        {
            result.Should().NotBeNullOrEmpty();
            
            var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
            jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
            jsonResult.GetProperty("message").GetString().Should().Be("Topic name is required");
            jsonResult.GetProperty("topic").ValueKind.Should().Be(JsonValueKind.Null);
        }

        // Verify GraphQL client was never called
        _mockGraphQLClient.Verify(x => x.SendQueryAsync<dynamic>(
            It.IsAny<GraphQLRequest>(), _cancellationToken), Times.Never);
    }

    [Fact]
    public async Task GetTopicAsync_WithNonExistentTopic_ShouldReturnNotFoundResult()
    {
        // Arrange
        const string topicName = "nonexistent/topic";
        
        dynamic mockData = new ExpandoObject();
        mockData.topic = null;
        
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = mockData,
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, topicName, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
        jsonResult.GetProperty("message").GetString().Should().Be($"Topic '{topicName}' not found");
        jsonResult.GetProperty("topicName").GetString().Should().Be(topicName);
        jsonResult.GetProperty("topic").ValueKind.Should().Be(JsonValueKind.Null);
    }

    #endregion

    #region GetTopicsByNamespaceAsync Tests

    [Fact]
    public async Task GetTopicsByNamespaceAsync_WithValidNamespace_ShouldReturnSuccessResult()
    {
        // Arrange
        const string namespaceName = "Factory";
        
        dynamic mockData = new ExpandoObject();
        mockData.topicsByNamespace = new[]
        {
            new { topic = "sensor/temperature", unsName = "Temperature Sensor", isActive = true },
            new { topic = "sensor/pressure", unsName = "Pressure Sensor", isActive = false }
        };

        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = mockData,
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetTopicsByNamespaceAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, namespaceName, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonResult.GetProperty("message").GetString().Should().Be($"Topics for namespace '{namespaceName}' retrieved successfully via GraphQL");
        jsonResult.GetProperty("namespaceName").GetString().Should().Be(namespaceName);
        jsonResult.TryGetProperty("topics", out var topicsElement).Should().BeTrue();
        topicsElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetTopicsByNamespaceAsync_WithNullOrEmptyNamespace_ShouldReturnErrorResult()
    {
        // Arrange & Act
        var resultNull = await GraphQLMcpTools.GetTopicsByNamespaceAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, null!, _cancellationToken);

        var resultEmpty = await GraphQLMcpTools.GetTopicsByNamespaceAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "", _cancellationToken);

        // Assert
        foreach (var result in new[] { resultNull, resultEmpty })
        {
            result.Should().NotBeNullOrEmpty();
            
            var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
            jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
            jsonResult.GetProperty("message").GetString().Should().Be("Namespace name is required");
            jsonResult.TryGetProperty("topics", out var topicsElement).Should().BeTrue();
            topicsElement.ValueKind.Should().Be(JsonValueKind.Array);
            topicsElement.GetArrayLength().Should().Be(0);
        }
    }

    #endregion

    #region SearchTopicsAsync Tests

    [Fact]
    public async Task SearchTopicsAsync_WithValidSearchTerm_ShouldReturnSuccessResult()
    {
        // Arrange
        const string searchTerm = "temperature";
        
        dynamic mockData = new ExpandoObject();
        mockData.searchTopics = new[]
        {
            new { topic = "sensor/temperature", unsName = "Temperature Sensor" },
            new { topic = "ambient/temperature", unsName = "Ambient Temperature" }
        };

        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = mockData,
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.SearchTopicsAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, searchTerm, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonResult.GetProperty("message").GetString().Should().Be($"Search for '{searchTerm}' completed successfully via GraphQL");
        jsonResult.GetProperty("searchTerm").GetString().Should().Be(searchTerm);
        jsonResult.TryGetProperty("topics", out var topicsElement).Should().BeTrue();
        topicsElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task SearchTopicsAsync_WithNullOrEmptySearchTerm_ShouldReturnErrorResult()
    {
        // Arrange & Act
        var resultNull = await GraphQLMcpTools.SearchTopicsAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, null!, _cancellationToken);

        var resultEmpty = await GraphQLMcpTools.SearchTopicsAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "", _cancellationToken);

        // Assert
        foreach (var result in new[] { resultNull, resultEmpty })
        {
            result.Should().NotBeNullOrEmpty();
            
            var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
            jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
            jsonResult.GetProperty("message").GetString().Should().Be("Search term is required");
            jsonResult.TryGetProperty("topics", out var topicsElement).Should().BeTrue();
            topicsElement.ValueKind.Should().Be(JsonValueKind.Array);
            topicsElement.GetArrayLength().Should().Be(0);
        }
    }

    #endregion

    #region GetSystemStatusAsync Tests

    [Fact]
    public async Task GetSystemStatusAsync_WithValidResponse_ShouldReturnSuccessResult()
    {
        // Arrange
        dynamic mockData = new ExpandoObject();
        dynamic mockSystemStatus = new ExpandoObject();
        dynamic mockConnectionStats = new ExpandoObject();
        
        mockConnectionStats.totalConnections = 5;
        mockConnectionStats.activeConnections = 3;
        mockConnectionStats.inactiveConnections = 2;
        
        mockSystemStatus.totalTopics = 25;
        mockSystemStatus.assignedTopics = 20;
        mockSystemStatus.activeTopics = 15;
        mockSystemStatus.totalConnections = 5;
        mockSystemStatus.activeConnections = 3;
        mockSystemStatus.namespaces = 4;
        mockSystemStatus.timestamp = DateTime.UtcNow.ToString();
        mockSystemStatus.connectionStats = mockConnectionStats;
        
        mockData.systemStatus = mockSystemStatus;

        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = mockData,
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetSystemStatusAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonResult.GetProperty("message").GetString().Should().Be("System status retrieved successfully via GraphQL");
        jsonResult.TryGetProperty("systemStatus", out _).Should().BeTrue();

        // Verify GraphQL query contains expected fields
        _mockGraphQLClient.Verify(x => x.SendQueryAsync<dynamic>(
            It.Is<GraphQLRequest>(q => 
                q.Query.Contains("systemStatus") && 
                q.Query.Contains("totalTopics") &&
                q.Query.Contains("connectionStats")), 
            _cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetSystemStatusAsync_WithGraphQLErrors_ShouldReturnErrorResult()
    {
        // Arrange
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = null,
            Errors = new[]
            {
                new GraphQLError { Message = "Service unavailable" }
            }
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await GraphQLMcpTools.GetSystemStatusAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, _cancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
        jsonResult.GetProperty("message").GetString().Should().Contain("GraphQL query failed");
        jsonResult.GetProperty("systemStatus").ValueKind.Should().Be(JsonValueKind.Null);
    }

    #endregion

    #region Error Handling and Edge Cases Tests

    [Fact]
    public async Task AllMethods_WithCancelledToken_ShouldHandleCancellation()
    {
        // Arrange
        var cancelledToken = new CancellationToken(true);
        
        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), cancelledToken))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var hierarchyTask = GraphQLMcpTools.GetUnsHierarchyAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, cancelledToken);
        
        var topicTask = GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "test", cancelledToken);
        
        var namespaceTask = GraphQLMcpTools.GetTopicsByNamespaceAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "test", cancelledToken);
        
        var searchTask = GraphQLMcpTools.SearchTopicsAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "test", cancelledToken);
        
        var statusTask = GraphQLMcpTools.GetSystemStatusAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, cancelledToken);

        // All tasks should complete and return error responses rather than throwing
        var results = await Task.WhenAll(hierarchyTask, topicTask, namespaceTask, searchTask, statusTask);
        
        foreach (var result in results)
        {
            result.Should().NotBeNullOrEmpty();
            var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
            jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public async Task AllMethods_ShouldLogDebugMessages()
    {
        // Arrange
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = new { },
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), _cancellationToken))
            .ReturnsAsync(mockResponse);

        // Act
        await GraphQLMcpTools.GetUnsHierarchyAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, _cancellationToken);
        
        await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "test-topic", _cancellationToken);
        
        await GraphQLMcpTools.GetTopicsByNamespaceAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "test-namespace", _cancellationToken);
        
        await GraphQLMcpTools.SearchTopicsAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, "test-search", _cancellationToken);
        
        await GraphQLMcpTools.GetSystemStatusAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object, _cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting UNS hierarchy via GraphQL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting topic information via GraphQL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}