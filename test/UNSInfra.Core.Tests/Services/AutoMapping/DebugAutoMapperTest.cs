using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services;
using UNSInfra.Services.AutoMapping;
using UNSInfra.Services.TopicBrowser;
using Xunit;

using NSNodeType = UNSInfra.Services.NSNodeType;

namespace UNSInfra.Core.Tests.Services.AutoMapping;

/// <summary>
/// Debug test to understand auto-mapping behavior
/// </summary>
public class DebugAutoMapperTest : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<INamespaceStructureService> _namespaceServiceMock;
    private readonly Mock<ILogger<SimplifiedAutoMapperService>> _loggerMock;
    private readonly SimplifiedAutoMapperService _autoMapperService;

    public DebugAutoMapperTest()
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
    public async Task DebugCacheAndMapping()
    {
        // Arrange: Simple namespace structure
        var namespaces = new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "Enterprise1",
                NodeType = NSNodeType.HierarchyNode,
                FullPath = "Enterprise1",
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "Site1",
                        NodeType = NSNodeType.HierarchyNode,
                        FullPath = "Enterprise1/Site1",
                        Children = new List<NSTreeNode>
                        {
                            new NSTreeNode
                            {
                                Name = "Area1",
                                NodeType = NSNodeType.HierarchyNode,
                                FullPath = "Enterprise1/Site1/Area1",
                                Children = new List<NSTreeNode>()
                            }
                        }
                    }
                }
            }
        };

        _namespaceServiceMock.Setup(n => n.GetNamespaceStructureAsync())
            .ReturnsAsync(namespaces);

        // Act
        await _autoMapperService.InitializeCacheAsync();

        // Check cache stats
        var stats = _autoMapperService.GetStats();
        Console.WriteLine($"Cache size: {stats.CacheSize}");

        // Test simple mapping
        var testTopic = "mqtt/factory/Enterprise1/Site1/Area1/Temperature";
        var mapping = _autoMapperService.TryMapTopic(testTopic);

        // Debug output
        Console.WriteLine($"Topic: {testTopic}");
        Console.WriteLine($"Mapping result: {mapping}");

        // Assert
        Assert.True(stats.CacheSize > 0, $"Cache should have entries, but has {stats.CacheSize}");
    }

    public void Dispose()
    {
        _autoMapperService?.Dispose();
    }
}