using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Services;
using UNSInfra.Services.AutoMapping;
using Xunit;

namespace UNSInfra.Core.Tests.Services;

/// <summary>
/// Tests for the simplified auto-mapper service.
/// </summary>
public class SimplifiedAutoMapperTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<INamespaceStructureService> _mockNamespaceService;
    private readonly Mock<ILogger<SimplifiedAutoMapperService>> _mockLogger;
    private readonly SimplifiedAutoMapperService _service;

    public SimplifiedAutoMapperTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockNamespaceService = new Mock<INamespaceStructureService>();
        _mockLogger = new Mock<ILogger<SimplifiedAutoMapperService>>();

        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(INamespaceStructureService)))
            .Returns(_mockNamespaceService.Object);

        _service = new SimplifiedAutoMapperService(_mockScopeFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task TryMapTopic_WithMatchingNamespace_ShouldReturnNamespacePath()
    {
        // Arrange
        var namespaces = new List<NSTreeNode>
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
                            new NSTreeNode { Name = "MyKPI", Children = new List<NSTreeNode>() }
                        }
                    }
                }
            }
        };

        _mockNamespaceService.Setup(x => x.GetNamespaceStructureAsync())
            .ReturnsAsync(namespaces);

        await _service.InitializeCacheAsync();

        // Act
        var result = _service.TryMapTopic("socket/virtualfactory/Enterprise1/KPI/MyKPI");

        // Assert
        Assert.Equal("Enterprise1/KPI/MyKPI", result);
    }

    [Fact]
    public async Task TryMapTopic_WithPartialMatch_ShouldReturnLongestMatch()
    {
        // Arrange
        var namespaces = new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "Enterprise1",
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "Area1",
                        Children = new List<NSTreeNode>()
                    }
                }
            }
        };

        _mockNamespaceService.Setup(x => x.GetNamespaceStructureAsync())
            .ReturnsAsync(namespaces);

        await _service.InitializeCacheAsync();

        // Act - Topic has more levels than namespace
        var result = _service.TryMapTopic("mqtt/plant/Enterprise1/Area1/Line1/Temperature");

        // Assert - Should match the longest available namespace path
        Assert.Equal("Enterprise1/Area1", result);
    }

    [Fact]
    public async Task TryMapTopic_WithNoMatch_ShouldReturnNull()
    {
        // Arrange
        var namespaces = new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "DifferentEnterprise",
                Children = new List<NSTreeNode>()
            }
        };

        _mockNamespaceService.Setup(x => x.GetNamespaceStructureAsync())
            .ReturnsAsync(namespaces);

        await _service.InitializeCacheAsync();

        // Act
        var result = _service.TryMapTopic("socket/virtualfactory/Enterprise1/KPI/MyKPI");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryMapTopic_WithMultipleLevels_ShouldMatchMostSpecific()
    {
        // Arrange
        var namespaces = new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "Enterprise1",
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "Dallas",
                        Children = new List<NSTreeNode>
                        {
                            new NSTreeNode
                            {
                                Name = "Area1",
                                Children = new List<NSTreeNode>
                                {
                                    new NSTreeNode { Name = "Line1", Children = new List<NSTreeNode>() }
                                }
                            }
                        }
                    }
                }
            }
        };

        _mockNamespaceService.Setup(x => x.GetNamespaceStructureAsync())
            .ReturnsAsync(namespaces);

        await _service.InitializeCacheAsync();

        // Act
        var result = _service.TryMapTopic("socketio/update/Enterprise1/Dallas/Area1/Line1/temperature");

        // Assert
        Assert.Equal("Enterprise1/Dallas/Area1/Line1", result);
    }

    [Fact]
    public async Task TryMapTopic_WithDuplicateTopic_ShouldReturnNullSecondTime()
    {
        // Arrange
        var namespaces = new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "Enterprise1",
                Children = new List<NSTreeNode>()
            }
        };

        _mockNamespaceService.Setup(x => x.GetNamespaceStructureAsync())
            .ReturnsAsync(namespaces);

        await _service.InitializeCacheAsync();

        // Act
        var firstResult = _service.TryMapTopic("socket/Enterprise1/KPI");
        var secondResult = _service.TryMapTopic("socket/Enterprise1/KPI"); // Same topic again

        // Assert
        Assert.Equal("Enterprise1", firstResult);
        Assert.Null(secondResult); // Should return null for duplicate
    }

    [Fact]
    public async Task GetStats_ShouldReturnCorrectPerformanceMetrics()
    {
        // Arrange
        var namespaces = new List<NSTreeNode>
        {
            new NSTreeNode { Name = "Enterprise1", Children = new List<NSTreeNode>() }
        };

        _mockNamespaceService.Setup(x => x.GetNamespaceStructureAsync())
            .ReturnsAsync(namespaces);

        await _service.InitializeCacheAsync();

        // Act - Generate some hits and misses
        _service.TryMapTopic("socket/Enterprise1/test1"); // Hit
        _service.TryMapTopic("socket/NonExistent/test2"); // Miss
        _service.TryMapTopic("socket/Enterprise1/test3"); // Hit

        var stats = _service.GetStats();

        // Assert
        Assert.Equal(2, stats.CacheHits);
        Assert.Equal(1, stats.CacheMisses);
        Assert.Equal(1, stats.CacheSize); // One namespace in cache
        Assert.Equal(2.0/3.0, stats.HitRatio, 2); // 2 hits out of 3 attempts
    }

    [Fact]
    public async Task RefreshCache_ShouldClearMappedTopicsAndReloadNamespaces()
    {
        // Arrange
        var initialNamespaces = new List<NSTreeNode>
        {
            new NSTreeNode { Name = "Enterprise1", Children = new List<NSTreeNode>() }
        };

        var updatedNamespaces = new List<NSTreeNode>
        {
            new NSTreeNode { Name = "Enterprise1", Children = new List<NSTreeNode>() },
            new NSTreeNode { Name = "Enterprise2", Children = new List<NSTreeNode>() }
        };

        _mockNamespaceService.SetupSequence(x => x.GetNamespaceStructureAsync())
            .ReturnsAsync(initialNamespaces)
            .ReturnsAsync(updatedNamespaces);

        await _service.InitializeCacheAsync();

        // Map a topic so it gets cached as "already processed"
        var firstResult = _service.TryMapTopic("socket/Enterprise1/test");
        Assert.Equal("Enterprise1", firstResult);

        // Act - Refresh cache
        await _service.RefreshCacheAsync();

        // Assert - Same topic should be processed again after cache refresh
        var secondResult = _service.TryMapTopic("socket/Enterprise1/test");
        Assert.Equal("Enterprise1", secondResult); // Should work again, not return null

        // New namespace should be available
        var newResult = _service.TryMapTopic("socket/Enterprise2/test");
        Assert.Equal("Enterprise2", newResult);

        var stats = _service.GetStats();
        Assert.Equal(2, stats.CacheSize); // Should have both enterprises now
    }

    [Theory]
    [InlineData("socket/virtualfactory/Enterprise1/KPI", "Enterprise1/KPI")]
    [InlineData("mqtt/plant/Dallas/Area1/Line1", "Dallas/Area1/Line1")]
    [InlineData("Enterprise1/Direct/Path", "Enterprise1/Direct/Path")]
    [InlineData("simple", null)] // Single segment, shouldn't match
    [InlineData("", null)] // Empty topic
    [InlineData("a/b/NonExistent", null)] // No match
    public async Task TryMapTopic_WithVariousTopicFormats_ShouldMatchCorrectly(string topic, string? expectedResult)
    {
        // Arrange
        var namespaces = new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "Enterprise1",
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "KPI",
                        Children = new List<NSTreeNode>()
                    },
                    new NSTreeNode
                    {
                        Name = "Direct",
                        Children = new List<NSTreeNode>
                        {
                            new NSTreeNode { Name = "Path", Children = new List<NSTreeNode>() }
                        }
                    }
                }
            },
            new NSTreeNode
            {
                Name = "Dallas",
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "Area1",
                        Children = new List<NSTreeNode>
                        {
                            new NSTreeNode { Name = "Line1", Children = new List<NSTreeNode>() }
                        }
                    }
                }
            }
        };

        _mockNamespaceService.Setup(x => x.GetNamespaceStructureAsync())
            .ReturnsAsync(namespaces);

        await _service.InitializeCacheAsync();

        // Act
        var result = _service.TryMapTopic(topic);

        // Assert
        Assert.Equal(expectedResult, result);
    }
}