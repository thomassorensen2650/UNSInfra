using System.Dynamic;
using System.Text.Json;
using FluentAssertions;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.MCP.Server;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using MicrosoftLogger = Microsoft.Extensions.Logging.ILogger;

namespace UNSInfra.MCP.Server.Tests;

public class LoggingIntegrationTests : IDisposable
{
    private readonly Mock<IGraphQLClient> _mockGraphQLClient;
    private readonly Mock<MicrosoftLogger> _mockLogger;
    private readonly IDisposable _testCorrelator;

    public LoggingIntegrationTests()
    {
        _mockGraphQLClient = new Mock<IGraphQLClient>();
        _mockLogger = new Mock<ILogger>();
        
        // Set up Serilog test correlator for capturing log events
        _testCorrelator = TestCorrelator.CreateContext();
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestCorrelator()
            .CreateLogger();
    }

    public void Dispose()
    {
        _testCorrelator?.Dispose();
        Log.CloseAndFlush();
    }

    [Fact]
    public async Task GraphQLMcpTools_WhenException_ShouldLogError()
    {
        // Arrange
        const string topicName = "test-topic";
        var expectedException = new HttpRequestException("Connection refused");
        
        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Create a real logger for testing
        var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
        var logger = loggerFactory.CreateLogger<LoggingIntegrationTests>();

        // Act
        var result = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, logger, topicName, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
        
        // Verify that error was logged using TestCorrelator
        var logEvents = TestCorrelator.GetLogEventsFromCurrentContext().ToArray();
        logEvents.Should().NotBeEmpty();
        
        var errorEvent = logEvents.FirstOrDefault(e => e.Level == LogEventLevel.Error);
        errorEvent.Should().NotBeNull();
        errorEvent!.Exception.Should().Be(expectedException);
        errorEvent.MessageTemplate.Text.Should().Contain("Error getting topic information via GraphQL");
    }

    [Fact]
    public async Task GraphQLMcpTools_WhenDebugEnabled_ShouldLogDebugMessages()
    {
        // Arrange
        const string topicName = "test-topic";
        
        dynamic mockData = new ExpandoObject();
        dynamic mockTopic = new ExpandoObject();
        mockTopic.topic = topicName;
        mockTopic.isActive = true;
        mockData.topic = mockTopic;
        
        var mockResponse = new GraphQLResponse<dynamic>
        {
            Data = mockData,
            Errors = null
        };

        _mockGraphQLClient
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Create a real logger for testing
        var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
        var logger = loggerFactory.CreateLogger<LoggingIntegrationTests>();

        // Act
        var result = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, logger, topicName, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeTrue();
        
        // Verify that debug message was logged
        var logEvents = TestCorrelator.GetLogEventsFromCurrentContext().ToArray();
        logEvents.Should().NotBeEmpty();
        
        var debugEvent = logEvents.FirstOrDefault(e => 
            e.Level == LogEventLevel.Debug && 
            e.MessageTemplate.Text.Contains("Getting topic information via GraphQL"));
        debugEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task GraphQLMcpTools_WhenGraphQLError_ShouldLogErrorWithDetails()
    {
        // Arrange
        const string topicName = "test-topic";
        
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
            .Setup(x => x.SendQueryAsync<dynamic>(It.IsAny<GraphQLRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Create a real logger for testing
        var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
        var logger = loggerFactory.CreateLogger<LoggingIntegrationTests>();

        // Act
        var result = await GraphQLMcpTools.GetTopicAsync(
            null, _mockGraphQLClient.Object, logger, topicName, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        jsonResult.GetProperty("success").GetBoolean().Should().BeFalse();
        
        // Verify that GraphQL errors were logged
        var logEvents = TestCorrelator.GetLogEventsFromCurrentContext().ToArray();
        logEvents.Should().NotBeEmpty();
        
        var errorEvent = logEvents.FirstOrDefault(e => 
            e.Level == LogEventLevel.Error && 
            e.MessageTemplate.Text.Contains("GraphQL query failed"));
        errorEvent.Should().NotBeNull();
        
        // Check that error details are included in the log message
        var renderedMessage = errorEvent!.RenderMessage();
        renderedMessage.Should().Contain("Database connection failed");
        renderedMessage.Should().Contain("Timeout occurred");
    }

    [Fact]
    public void LoggingConfiguration_ShouldSupportStructuredLogging()
    {
        // Arrange
        var logger = Log.ForContext<LoggingIntegrationTests>();
        var topicName = "sensor/temperature";
        var errorMessage = "Connection failed";
        
        // Act
        logger.Error("Failed to process topic {TopicName} with error {ErrorMessage}", topicName, errorMessage);
        
        // Assert
        var logEvents = TestCorrelator.GetLogEventsFromCurrentContext().ToArray();
        var errorEvent = logEvents.LastOrDefault(e => e.Level == LogEventLevel.Error);
        
        errorEvent.Should().NotBeNull();
        errorEvent!.Properties.Should().ContainKey("TopicName");
        errorEvent.Properties.Should().ContainKey("ErrorMessage");
        errorEvent.Properties["TopicName"].ToString().Should().Contain(topicName);
        errorEvent.Properties["ErrorMessage"].ToString().Should().Contain(errorMessage);
    }
}