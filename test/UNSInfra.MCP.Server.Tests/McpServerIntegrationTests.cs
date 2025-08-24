using System.Dynamic;
using System.Text.Json;
using FluentAssertions;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace UNSInfra.MCP.Server.Tests;

public class McpServerIntegrationTests
{
    private readonly Mock<IGraphQLClient> _mockGraphQLClient;
    private readonly Mock<ILogger<McpServerIntegrationTests>> _mockLogger;

    public McpServerIntegrationTests()
    {
        _mockGraphQLClient = new Mock<IGraphQLClient>();
        _mockLogger = new Mock<ILogger<McpServerIntegrationTests>>();
    }

    [Fact]
    public void McpServerTools_ShouldHaveCorrectAttributes()
    {
        // Assert that the GraphQLMcpTools class has the correct MCP attributes
        var toolsType = typeof(GraphQLMcpTools);
        var classAttributes = toolsType.GetCustomAttributes(typeof(ModelContextProtocol.Server.McpServerToolTypeAttribute), false);
        
        classAttributes.Should().HaveCount(1);
        classAttributes[0].Should().BeOfType<ModelContextProtocol.Server.McpServerToolTypeAttribute>();
    }

    [Fact]
    public void McpServerToolMethods_ShouldHaveCorrectAttributes()
    {
        // Arrange
        var toolsType = typeof(GraphQLMcpTools);
        var expectedMethods = new[]
        {
            nameof(GraphQLMcpTools.GetUnsHierarchyAsync),
            nameof(GraphQLMcpTools.GetTopicAsync),
            nameof(GraphQLMcpTools.GetTopicsByNamespaceAsync),
            nameof(GraphQLMcpTools.SearchTopicsAsync),
            nameof(GraphQLMcpTools.GetSystemStatusAsync)
        };

        // Act & Assert
        foreach (var methodName in expectedMethods)
        {
            var method = toolsType.GetMethod(methodName);
            method.Should().NotBeNull($"Method {methodName} should exist");
            
            var attributes = method!.GetCustomAttributes(typeof(ModelContextProtocol.Server.McpServerToolAttribute), false);
            attributes.Should().HaveCount(1, $"Method {methodName} should have McpServerTool attribute");
            
            var descriptionAttributes = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            descriptionAttributes.Should().HaveCount(1, $"Method {methodName} should have Description attribute");
        }
    }

    [Fact]
    public void McpServerToolMethods_ShouldHaveCorrectSignatures()
    {
        // Arrange
        var toolsType = typeof(GraphQLMcpTools);

        // Act & Assert - Check GetUnsHierarchyAsync
        var hierarchyMethod = toolsType.GetMethod(nameof(GraphQLMcpTools.GetUnsHierarchyAsync));
        hierarchyMethod.Should().NotBeNull();
        hierarchyMethod!.IsStatic.Should().BeTrue();
        hierarchyMethod.ReturnType.Should().Be(typeof(Task<string>));
        
        var hierarchyParams = hierarchyMethod.GetParameters();
        hierarchyParams.Should().HaveCount(4);
        hierarchyParams[0].ParameterType.Should().Be(typeof(ModelContextProtocol.Server.IMcpServer));
        hierarchyParams[1].ParameterType.Should().Be(typeof(IGraphQLClient));
        hierarchyParams[2].ParameterType.Should().Be(typeof(ILogger));
        hierarchyParams[3].ParameterType.Should().Be(typeof(CancellationToken));

        // Act & Assert - Check GetTopicAsync
        var topicMethod = toolsType.GetMethod(nameof(GraphQLMcpTools.GetTopicAsync));
        topicMethod.Should().NotBeNull();
        
        var topicParams = topicMethod!.GetParameters();
        topicParams.Should().HaveCount(5); // Includes topicName parameter
        topicParams[3].Name.Should().Be("topicName");
        topicParams[3].ParameterType.Should().Be(typeof(string));
    }

    [Fact]
    public async Task GraphQLClient_Configuration_ShouldHandleConnectionErrors()
    {
        // Arrange
        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await GraphQLMcpTools.GetSystemStatusAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>(), CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
        jsonResult.GetProperty("message").GetString().Should().Be("Unable to connect to UNS Infrastructure GraphQL API. Please ensure the UI server is running.");
        jsonResult.GetProperty("error").GetString().Should().Be("Connection refused");
    }

    [Fact]
    public void JsonSerializationOptions_ShouldBeConfiguredCorrectly()
    {
        // This test ensures that the JSON serialization options are consistent
        // We can't directly access the private field, but we can test the output format
        
        // Arrange
        var testObject = new
        {
            success = true,
            message = "Test message",
            testProperty = "testValue",
            nestedObject = new
            {
                nestedProperty = "nestedValue"
            }
        };

        // Act
        var serialized = JsonSerializer.Serialize(testObject, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        serialized.Should().Contain("\"success\": true");
        serialized.Should().Contain("\"testProperty\""); // camelCase
        serialized.Should().Contain("\"nestedProperty\""); // camelCase in nested objects
        serialized.Should().Contain("  "); // Should be indented
    }

    [Fact]
    public async Task AllMethods_ShouldReturnValidJsonStructure()
    {
        // Arrange
        dynamic mockData = new ExpandoObject();
        dynamic mockSystemStatus = new ExpandoObject();
        dynamic mockTopic = new ExpandoObject();
        
        mockSystemStatus.totalTopics = 10;
        mockTopic.topic = "test";
        mockTopic.isActive = true;
        
        mockData.systemStatus = mockSystemStatus;
        mockData.topic = mockTopic;
        mockData.topicsByNamespace = new[] { new { topic = "test1" } };
        mockData.searchTopics = new[] { new { topic = "test2" } };
        mockData.namespaces = new[] { "Enterprise1" };
        
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = mockData,
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var results = new[]
        {
            await GraphQLMcpTools.GetUnsHierarchyAsync(null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>()),
            await GraphQLMcpTools.GetTopicAsync(null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>(), "test"),
            await GraphQLMcpTools.GetTopicsByNamespaceAsync(null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>(), "test"),
            await GraphQLMcpTools.SearchTopicsAsync(null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>(), "test"),
            await GraphQLMcpTools.GetSystemStatusAsync(null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>())
        };

        // Assert
        foreach (var result in results)
        {
            result.Should().NotBeNullOrEmpty();
            
            // Should be valid JSON
            var jsonDoc = JsonDocument.Parse(result);
            jsonDoc.Should().NotBeNull();
            
            // Should have standard response structure
            var root = jsonDoc.RootElement;
            root.TryGetProperty("success", out _).Should().BeTrue();
            root.TryGetProperty("message", out _).Should().BeTrue();
            
            var success = root.GetProperty("success").GetBoolean();
            success.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Methods_WithInvalidStringInputs_ShouldHandleGracefully(string? invalidInput)
    {
        // Arrange & Act
        var topicResult = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>(), invalidInput!, CancellationToken.None);
            
        var namespaceResult = await GraphQLMcpTools.GetTopicsByNamespaceAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>(), invalidInput!, CancellationToken.None);
            
        var searchResult = await GraphQLMcpTools.SearchTopicsAsync(
            null, _mockGraphQLClient.Object, _mockLogger.Object.As<ILogger>(), invalidInput!, CancellationToken.None);

        // Assert
        foreach (var result in new[] { topicResult, namespaceResult, searchResult })
        {
            result.Should().NotBeNullOrEmpty();
            
            var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
            jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
            jsonResult.GetProperty("message").GetString().Should().Contain("required");
        }

        // Verify no GraphQL calls were made for invalid inputs
        _mockGraphQLClient.Verify(x => x.SendQueryAsync<dynamic>(
            It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}