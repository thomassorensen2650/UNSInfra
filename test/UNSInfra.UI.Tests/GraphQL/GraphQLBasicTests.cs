using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Abstractions;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.UI.GraphQL;
using UNSInfra.UI.GraphQL.Types;

namespace UNSInfra.UI.Tests.GraphQL;

public class GraphQLBasicTests
{
    [Fact]
    public void GraphQL_Query_ShouldBeInstantiable()
    {
        // Arrange
        var mockTopicBrowserService = new Mock<CachedTopicBrowserService>(
            Mock.Of<IServiceProvider>(), 
            Mock.Of<ILogger<CachedTopicBrowserService>>());
        
        var mockConnectionManager = new Mock<IConnectionManager>();

        // Act
        var query = new Query();
        
        // Assert
        query.Should().NotBeNull();
    }

    [Fact]
    public void GraphQL_TopicType_ShouldBeInstantiable()
    {
        // Act
        var topicType = new TopicType();
        
        // Assert
        topicType.Should().NotBeNull();
    }

    [Fact]
    public void GraphQL_SystemStatusType_ShouldBeInstantiable()
    {
        // Act  
        var systemStatusType = new SystemStatusType();
        
        // Assert
        systemStatusType.Should().NotBeNull();
    }

    [Fact]
    public void GraphQL_ConnectionStatsType_ShouldBeInstantiable()
    {
        // Act
        var connectionStatsType = new ConnectionStatsType();
        
        // Assert
        connectionStatsType.Should().NotBeNull();
    }

    [Fact]
    public void GraphQLServer_ShouldBeConfigurableInDI()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockTopicBrowserService = new Mock<CachedTopicBrowserService>(
            Mock.Of<IServiceProvider>(), 
            Mock.Of<ILogger<CachedTopicBrowserService>>());
        
        var mockConnectionManager = new Mock<IConnectionManager>();

        services.AddSingleton(mockTopicBrowserService.Object);
        services.AddSingleton(mockConnectionManager.Object);

        // Act
        var serviceProvider = services
            .AddGraphQLServer()
            .AddQueryType<Query>()
            .AddType<TopicType>()
            .AddType<SystemStatusType>()
            .AddType<ConnectionStatsType>()
            .Services
            .BuildServiceProvider();

        // Assert
        serviceProvider.Should().NotBeNull();
        
        // Verify GraphQL services are registered
        var graphqlService = serviceProvider.GetService<HotChocolate.Execution.IRequestExecutorResolver>();
        graphqlService.Should().NotBeNull();
    }
}