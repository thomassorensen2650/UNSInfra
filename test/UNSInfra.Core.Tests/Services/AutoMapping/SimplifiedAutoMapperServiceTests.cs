using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Models.Data;
using UNSInfra.Services;
using UNSInfra.Services.AutoMapping;
using Xunit;

namespace UNSInfra.Core.Tests.Services.AutoMapping;

public class SimplifiedAutoMapperServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<INamespaceStructureService> _namespaceServiceMock;
    private readonly Mock<ILogger<SimplifiedAutoMapperService>> _loggerMock;
    private readonly SimplifiedAutoMapperService _autoMapperService;

    public SimplifiedAutoMapperServiceTests()
    {
        _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _namespaceServiceMock = new Mock<INamespaceStructureService>();
        _loggerMock = new Mock<ILogger<SimplifiedAutoMapperService>>();

        // Setup service scope factory chain
        _serviceScopeFactoryMock.Setup(f => f.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceScopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(INamespaceStructureService)))
            .Returns(_namespaceServiceMock.Object);

        _autoMapperService = new SimplifiedAutoMapperService(
            _serviceScopeFactoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task InitializeCacheAsync_LoadsNamespaceStructure_SuccessfullyBuildsCache()
    {
        // Arrange
        var testNamespaces = CreateTestNamespaceStructure();
        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(testNamespaces);

        // Act
        await _autoMapperService.InitializeCacheAsync();

        // Assert
        var stats = _autoMapperService.GetStats();
        Assert.True(stats.CacheSize > 0);
        _namespaceServiceMock.Verify(n => n.GetNamespaceStructureAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeCacheAsync_WithNullNamespaceService_LogsErrorAndReturns()
    {
        // Arrange
        _serviceProviderMock.Setup(p => p.GetService(typeof(INamespaceStructureService)))
            .Returns((INamespaceStructureService?)null);

        // Act
        await _autoMapperService.InitializeCacheAsync();

        // Assert
        var stats = _autoMapperService.GetStats();
        Assert.Equal(0, stats.CacheSize);
        
        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("INamespaceStructureService not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("socket/virtualfactory/Enterprise1/KPI/MyKPI", "Enterprise1/KPI/MyKPI")]
    [InlineData("mqtt/factory/Enterprise1/Area1/WorkCenter1/Sensor1", "Enterprise1/Area1/WorkCenter1/Sensor1")]
    [InlineData("socketio/test/Enterprise1/KPI", "Enterprise1/KPI")]
    public async Task TryMapTopic_WithValidTopics_ReturnsCorrectNamespace(string topic, string expectedNamespace)
    {
        // Arrange
        var testNamespaces = CreateTestNamespaceStructure();
        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(testNamespaces);
        await _autoMapperService.InitializeCacheAsync();

        // Act
        var result = _autoMapperService.TryMapTopic(topic);

        // Assert
        Assert.Equal(expectedNamespace, result);
    }

    [Theory]
    [InlineData("socket/nonexistent/path")]
    [InlineData("mqtt/unknown/namespace")]
    [InlineData("invalid/topic")]
    public async Task TryMapTopic_WithInvalidTopics_ReturnsNull(string topic)
    {
        // Arrange
        var testNamespaces = CreateTestNamespaceStructure();
        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(testNamespaces);
        await _autoMapperService.InitializeCacheAsync();

        // Act
        var result = _autoMapperService.TryMapTopic(topic);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryMapTopic_WithLongestMatch_ReturnsSpecificNamespace()
    {
        // Arrange - Create namespace structure with nested paths
        var testNamespaces = new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "Enterprise1",
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "KPI",
                        Children = new List<NSTreeNode>
                        {
                            new NSTreeNode
                            {
                                Name = "Production",
                                Children = new List<NSTreeNode>()
                            }
                        }
                    }
                }
            }
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(testNamespaces);
        await _autoMapperService.InitializeCacheAsync();

        // Act - Topic should match "Enterprise1/KPI/Production"
        var result = _autoMapperService.TryMapTopic("socket/virtualfactory/Enterprise1/KPI/Production");

        // Assert - Should return the matching namespace
        Assert.Equal("Enterprise1/KPI/Production", result);
    }

    [Fact]
    public void TryMapTopic_WithNullOrEmptyTopic_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(_autoMapperService.TryMapTopic(null!));
        Assert.Null(_autoMapperService.TryMapTopic(""));
        Assert.Null(_autoMapperService.TryMapTopic("   "));
    }

    [Fact]
    public async Task TryMapTopic_WithSameTopic_OnlyProcessedOnce()
    {
        // Arrange
        var testNamespaces = CreateTestNamespaceStructure();
        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(testNamespaces);
        await _autoMapperService.InitializeCacheAsync();
        
        var topic = "socket/virtualfactory/Enterprise1/KPI/MyKPI";

        // Act - Call twice with same topic
        var result1 = _autoMapperService.TryMapTopic(topic);
        var result2 = _autoMapperService.TryMapTopic(topic);

        // Assert
        Assert.Equal("Enterprise1/KPI/MyKPI", result1);
        Assert.Null(result2); // Second call should return null as it's already processed
        
        var stats = _autoMapperService.GetStats();
        Assert.True(stats.CacheHits > 0); // Second call should be a cache hit
    }

    [Fact]
    public async Task RefreshCacheAsync_ClearsAndReloadsCache()
    {
        // Arrange
        var testNamespaces = CreateTestNamespaceStructure();
        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(testNamespaces);
        await _autoMapperService.InitializeCacheAsync();
        
        // Map a topic to add to processed cache
        _autoMapperService.TryMapTopic("socket/test/Enterprise1/KPI/MyKPI");

        // Act
        await _autoMapperService.RefreshCacheAsync();

        // Assert - Should be able to map the same topic again after refresh
        var result = _autoMapperService.TryMapTopic("socket/test/Enterprise1/KPI/MyKPI");
        Assert.Equal("Enterprise1/KPI/MyKPI", result);
        
        _namespaceServiceMock.Verify(n => n.GetNamespaceStructureAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GetStats_ReturnsAccurateStatistics()
    {
        // Arrange
        var testNamespaces = CreateTestNamespaceStructure();
        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(testNamespaces);
        await _autoMapperService.InitializeCacheAsync();

        // Act - Perform some mapping operations
        _autoMapperService.TryMapTopic("socket/test/Enterprise1/KPI/MyKPI"); // Hit
        _autoMapperService.TryMapTopic("socket/test/nonexistent/path"); // Miss
        _autoMapperService.TryMapTopic("socket/test/Enterprise1/KPI/MyKPI"); // Cache hit for processed topic

        var stats = _autoMapperService.GetStats();

        // Assert
        Assert.True(stats.CacheSize > 0);
        Assert.True(stats.CacheHits >= 1);
        Assert.True(stats.CacheMisses >= 1);
        Assert.True(stats.HitRatio >= 0.0 && stats.HitRatio <= 1.0);
    }

    [Fact]
    public async Task InitializeCacheAsync_WithException_LogsErrorAndContinues()
    {
        // Arrange
        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        await _autoMapperService.InitializeCacheAsync();

        // Assert - Should not throw, but log error
        var stats = _autoMapperService.GetStats();
        Assert.Equal(0, stats.CacheSize);
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to initialize namespace cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("a")]
    [InlineData("no/namespaces/here")]
    public async Task TryMapTopic_WithSinglePartOrInvalidPaths_ReturnsNull(string topic)
    {
        // Arrange
        var testNamespaces = CreateTestNamespaceStructure();
        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(testNamespaces);
        await _autoMapperService.InitializeCacheAsync();

        // Act
        var result = _autoMapperService.TryMapTopic(topic);

        // Assert
        Assert.Null(result);
    }

    private static List<NSTreeNode> CreateTestNamespaceStructure()
    {
        return new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "Enterprise1",
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "KPI",
                        Children = new List<NSTreeNode>
                        {
                            new NSTreeNode
                            {
                                Name = "MyKPI",
                                Children = new List<NSTreeNode>()
                            }
                        }
                    },
                    new NSTreeNode
                    {
                        Name = "Area1",
                        Children = new List<NSTreeNode>
                        {
                            new NSTreeNode
                            {
                                Name = "WorkCenter1",
                                Children = new List<NSTreeNode>
                                {
                                    new NSTreeNode
                                    {
                                        Name = "Sensor1",
                                        Children = new List<NSTreeNode>()
                                    }
                                }
                            }
                        }
                    }
                }
            },
            new NSTreeNode
            {
                Name = "Enterprise2",
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "Production",
                        Children = new List<NSTreeNode>()
                    }
                }
            }
        };
    }

    public void Dispose()
    {
        _autoMapperService?.Dispose();
    }
}