using Xunit;
using UNSInfra.Services;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Namespace;
using UNSInfra.Core.Repositories;
using UNSInfra.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.Events;

namespace UNSInfra.Core.Tests;

public class NamespaceValidationTest
{
    [Fact]
    public async Task CreateNamespaceAsync_ShouldAllowMESAtDifferentWorkCenters()
    {
        // Arrange
        var logger = new Mock<ILogger<NamespaceStructureService>>();
        var namespaceRepo = new Mock<INamespaceRepository>();
        var hierarchyRepo = new Mock<IHierarchyRepository>();
        var cachedTopicBrowser = new Mock<ICachedTopicBrowserService>();
        var eventBus = new Mock<IEventBus>();

        // Setup existing MES at Line2
        var existingMES = new NamespaceConfiguration
        {
            Id = "existing-mes-id",
            Name = "MES",
            ParentNamespaceId = null,
            HierarchicalPath = new HierarchicalPath()
        };
        existingMES.HierarchicalPath.SetValue("Enterprise", "Enterprise");
        existingMES.HierarchicalPath.SetValue("Site", "Dallas");
        existingMES.HierarchicalPath.SetValue("Area", "Press");
        existingMES.HierarchicalPath.SetValue("WorkCenter", "Line2");

        var existingNamespaces = new List<NamespaceConfiguration> { existingMES };
        namespaceRepo.Setup(x => x.GetAllNamespaceConfigurationsAsync(true))
                    .ReturnsAsync(existingNamespaces);

        // Setup hierarchy config
        var hierarchyConfig = new HierarchyConfiguration
        {
            Id = "test-hierarchy",
            IsActive = true,
            Nodes = new List<HierarchyNode>
            {
                new HierarchyNode { Name = "Enterprise", Order = 0 },
                new HierarchyNode { Name = "Site", Order = 1 },
                new HierarchyNode { Name = "Area", Order = 2 },
                new HierarchyNode { Name = "WorkCenter", Order = 3 },
                new HierarchyNode { Name = "WorkUnit", Order = 4 }
            }
        };
        hierarchyRepo.Setup(x => x.GetActiveHierarchyConfigurationAsync())
                    .ReturnsAsync(hierarchyConfig);

        // Setup empty NS tree structure (no parent namespaces)
        cachedTopicBrowser.Setup(x => x.GetCachedTopicTreeAsync())
                         .ReturnsAsync(new Dictionary<string, object>());

        var service = new NamespaceStructureService(
            logger.Object,
            namespaceRepo.Object,
            hierarchyRepo.Object,
            cachedTopicBrowser.Object,
            eventBus.Object);

        // Act - Try to create MES at Line1
        var newMES = new NamespaceConfiguration
        {
            Name = "MES",
            ParentNamespaceId = null,
            HierarchicalPath = new HierarchicalPath()
        };
        newMES.HierarchicalPath.SetValue("Enterprise", "Enterprise");
        newMES.HierarchicalPath.SetValue("Site", "Dallas");
        newMES.HierarchicalPath.SetValue("Area", "Press");
        newMES.HierarchicalPath.SetValue("WorkCenter", "Line1");

        // This should NOT throw an exception since Line1 and Line2 are different WorkCenters
        var result = await service.CreateNamespaceAsync("Enterprise/Dallas/Press/Line1", newMES);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MES", result.Name);
        Assert.Equal("Enterprise/Dallas/Press/Line1/MES", result.FullPath);
    }

    [Fact]
    public async Task CreateNamespaceAsync_ShouldPreventDuplicateInSameLocation()
    {
        // Arrange
        var logger = new Mock<ILogger<NamespaceStructureService>>();
        var namespaceRepo = new Mock<INamespaceRepository>();
        var hierarchyRepo = new Mock<IHierarchyRepository>();
        var cachedTopicBrowser = new Mock<ICachedTopicBrowserService>();
        var eventBus = new Mock<IEventBus>();

        // Setup existing MES at Line1 (same location)
        var existingMES = new NamespaceConfiguration
        {
            Id = "existing-mes-id",
            Name = "MES",
            ParentNamespaceId = null,
            HierarchicalPath = new HierarchicalPath()
        };
        existingMES.HierarchicalPath.SetValue("Enterprise", "Enterprise");
        existingMES.HierarchicalPath.SetValue("Site", "Dallas");
        existingMES.HierarchicalPath.SetValue("Area", "Press");
        existingMES.HierarchicalPath.SetValue("WorkCenter", "Line1");

        var existingNamespaces = new List<NamespaceConfiguration> { existingMES };
        namespaceRepo.Setup(x => x.GetAllNamespaceConfigurationsAsync(true))
                    .ReturnsAsync(existingNamespaces);

        // Setup hierarchy config
        var hierarchyConfig = new HierarchyConfiguration
        {
            Id = "test-hierarchy",
            IsActive = true,
            Nodes = new List<HierarchyNode>
            {
                new HierarchyNode { Name = "Enterprise", Order = 0 },
                new HierarchyNode { Name = "Site", Order = 1 },
                new HierarchyNode { Name = "Area", Order = 2 },
                new HierarchyNode { Name = "WorkCenter", Order = 3 },
                new HierarchyNode { Name = "WorkUnit", Order = 4 }
            }
        };
        hierarchyRepo.Setup(x => x.GetActiveHierarchyConfigurationAsync())
                    .ReturnsAsync(hierarchyConfig);

        // Setup NS tree structure that shows existing MES
        var nsTreeStructure = new List<NSTreeNode>
        {
            new NSTreeNode
            {
                Name = "Enterprise",
                FullPath = "Enterprise",
                NodeType = NSNodeType.HierarchyNode,
                Children = new List<NSTreeNode>
                {
                    new NSTreeNode
                    {
                        Name = "Dallas",
                        FullPath = "Enterprise/Dallas",
                        NodeType = NSNodeType.HierarchyNode,
                        Children = new List<NSTreeNode>
                        {
                            new NSTreeNode
                            {
                                Name = "Press",
                                FullPath = "Enterprise/Dallas/Press",
                                NodeType = NSNodeType.HierarchyNode,
                                Children = new List<NSTreeNode>
                                {
                                    new NSTreeNode
                                    {
                                        Name = "Line1",
                                        FullPath = "Enterprise/Dallas/Press/Line1",
                                        NodeType = NSNodeType.HierarchyNode,
                                        Children = new List<NSTreeNode>
                                        {
                                            new NSTreeNode
                                            {
                                                Name = "MES",
                                                FullPath = "Enterprise/Dallas/Press/Line1/MES",
                                                NodeType = NSNodeType.Namespace,
                                                Namespace = existingMES
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        cachedTopicBrowser.Setup(x => x.GetCachedTopicTreeAsync())
                         .ReturnsAsync(new Dictionary<string, object>());

        var service = new NamespaceStructureService(
            logger.Object,
            namespaceRepo.Object,
            hierarchyRepo.Object,
            cachedTopicBrowser.Object,
            eventBus.Object);

        // Use reflection to set the tree structure (since GetNamespaceStructureAsync is called internally)
        var serviceType = typeof(NamespaceStructureService);
        var getNamespaceStructureMethod = serviceType.GetMethod("GetNamespaceStructureAsync");
        
        // Act & Assert - Try to create another MES at same Line1 location
        var duplicateMES = new NamespaceConfiguration
        {
            Name = "MES",
            ParentNamespaceId = null,
            HierarchicalPath = existingMES.HierarchicalPath
        };

        // This SHOULD throw an exception since we're trying to create MES at the same Line1 location
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.CreateNamespaceAsync("Enterprise/Dallas/Press/Line1", duplicateMES);
        });
    }
}