using Xunit;
using UNSInfra.Services;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Namespace;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.Events;
using UNSInfra.Models.Data;
using Microsoft.Extensions.DependencyInjection;

namespace UNSInfra.Core.Tests;

public class NamespaceValidationTest
{
    [Fact]
    public async Task CreateNamespaceAsync_ShouldAllowMESAtDifferentWorkCenters()
    {
        // Arrange
        var namespaceRepo = new Mock<INamespaceConfigurationRepository>();
        var hierarchyRepo = new Mock<IHierarchyConfigurationRepository>();
        var nsTreeRepo = new Mock<INSTreeInstanceRepository>();
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
        hierarchyRepo.Setup(x => x.GetActiveConfigurationAsync())
                    .ReturnsAsync(hierarchyConfig);

        // Create real CachedTopicBrowserService for first test
        var serviceCollection1 = new ServiceCollection();
        serviceCollection1.AddLogging(builder => builder.AddConsole());
        var serviceProvider1 = serviceCollection1.BuildServiceProvider();
        var topicBrowserLogger1 = serviceProvider1.GetRequiredService<ILogger<CachedTopicBrowserService>>();
        var cachedTopicBrowserInstance1 = new CachedTopicBrowserService(serviceProvider1, topicBrowserLogger1);

        var service = new NamespaceStructureService(
            hierarchyRepo.Object,
            namespaceRepo.Object,
            nsTreeRepo.Object,
            eventBus.Object,
            cachedTopicBrowserInstance1);

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
        var namespaceRepo = new Mock<INamespaceConfigurationRepository>();
        var hierarchyRepo = new Mock<IHierarchyConfigurationRepository>();
        var nsTreeRepo = new Mock<INSTreeInstanceRepository>();
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
        hierarchyRepo.Setup(x => x.GetActiveConfigurationAsync())
                    .ReturnsAsync(hierarchyConfig);


        // Create real CachedTopicBrowserService instance for second test
        var serviceCollection2 = new ServiceCollection();
        serviceCollection2.AddLogging(builder => builder.AddConsole());
        var serviceProvider2 = serviceCollection2.BuildServiceProvider();
        var topicBrowserLogger2 = serviceProvider2.GetRequiredService<ILogger<CachedTopicBrowserService>>();
        var cachedTopicBrowserInstance2 = new CachedTopicBrowserService(serviceProvider2, topicBrowserLogger2);

        var service = new NamespaceStructureService(
            hierarchyRepo.Object,
            namespaceRepo.Object,
            nsTreeRepo.Object,
            eventBus.Object,
            cachedTopicBrowserInstance2);

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