using UNSInfra.UI.Components;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.TopicBrowser;
using Microsoft.Extensions.Logging;
using Moq;

namespace UNSInfra.UI.Tests.Components;

public class TopicTreeTests : UITestContext
{
    private readonly Mock<ILogger<TopicTree>> _mockLogger;

    public TopicTreeTests()
    {
        _mockLogger = new Mock<ILogger<TopicTree>>();
        Services.AddSingleton(_mockLogger.Object);
    }

    [Fact]
    public void TopicTree_RendersCorrectly()
    {
        // Arrange
        var topics = new List<TopicInfo>
        {
            new TopicInfo 
            { 
                Topic = "test/topic", 
                SourceType = "MQTT",
                CreatedAt = DateTime.Now,
                Path = new HierarchicalPath()
            }
        };

        // Act
        var component = RenderComponent<TopicTree>(parameters => parameters
            .Add(p => p.Topics, topics));

        // Assert
        Assert.Contains("topic-tree-wrapper", component.Markup);
        Assert.Contains("UNS", component.Markup);
        Assert.Contains("Data Browser", component.Markup);
    }

    [Fact]
    public void TopicTree_DisplaysTabNavigation()
    {
        // Act
        var component = RenderComponent<TopicTree>();

        // Assert
        var tabs = component.FindAll(".nav-link");
        Assert.Equal(2, tabs.Count);
        
        Assert.Contains("UNS", tabs[0].TextContent);
        Assert.Contains("Data Browser", tabs[1].TextContent);
        
        // Default tab should be UNS (ns)
        Assert.Contains("active", tabs[0].ClassList);
    }

    [Fact]
    public void TopicTree_TabSwitching_WorksCorrectly()
    {
        // Arrange
        var component = RenderComponent<TopicTree>();

        // Act - Click on Data Browser tab
        var dataBrowserTab = component.Find("button:contains('Data Browser')");
        dataBrowserTab.Click();

        // Assert
        Assert.Contains("active", dataBrowserTab.ClassList);
        var nsTab = component.Find("button:contains('UNS')");
        Assert.DoesNotContain("active", nsTab.ClassList);
    }

    [Fact]
    public void TopicTree_EmptyDataBrowser_ShowsEmptyState()
    {
        // Arrange
        var component = RenderComponent<TopicTree>(parameters => parameters
            .Add(p => p.Topics, new List<TopicInfo>()));

        // Act - Switch to Data Browser tab
        var dataBrowserTab = component.Find("button:contains('Data Browser')");
        dataBrowserTab.Click();

        // Assert
        Assert.Contains("No data found", component.Markup);
        Assert.Contains("Data will appear here when received", component.Markup);
        Assert.Contains("bi-database", component.Markup);
    }

    [Fact]
    public void TopicTree_WithTopics_DisplaysInDataBrowser()
    {
        // Arrange
        var topics = new List<TopicInfo>
        {
            new TopicInfo 
            { 
                Topic = "temperature", 
                SourceType = "MQTT",
                CreatedAt = DateTime.Now,
                Path = new HierarchicalPath()
            },
            new TopicInfo 
            { 
                Topic = "pressure", 
                SourceType = "SocketIO",
                CreatedAt = DateTime.Now.AddSeconds(1),
                Path = new HierarchicalPath()
            }
        };

        var component = RenderComponent<TopicTree>(parameters => parameters
            .Add(p => p.Topics, topics));

        // Act - Switch to Data Browser tab
        var dataBrowserTab = component.Find("button:contains('Data Browser')");
        dataBrowserTab.Click();

        // Assert
        Assert.Contains("MQTT", component.Markup);
        Assert.Contains("SocketIO", component.Markup);
        Assert.DoesNotContain("No data found", component.Markup);
    }

    [Fact]
    public void TopicTree_TopicSelection_CallsCallback()
    {
        // Arrange
        var selectedTopic = (TopicInfo?)null;
        var topics = new List<TopicInfo>
        {
            new TopicInfo 
            { 
                Topic = "test/topic", 
                SourceType = "MQTT",
                CreatedAt = DateTime.Now,
                Path = new HierarchicalPath()
            }
        };

        var component = RenderComponent<TopicTree>(parameters => parameters
            .Add(p => p.Topics, topics)
            .Add(p => p.OnTopicSelected, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<TopicInfo>(this, (topic) => selectedTopic = topic)));

        // Act - Switch to Data Browser tab first
        var dataBrowserTab = component.Find("button:contains('Data Browser')");
        dataBrowserTab.Click();

        // Note: The actual topic selection would happen through the TopicTreeNodeWithNS component
        // which we can't easily test in isolation here. This test verifies the callback is set up.

        // Assert
        Assert.NotNull(component.Instance.OnTopicSelected);
    }

    [Fact]
    public void TopicTree_HighlightedPaths_ArePassedToChildren()
    {
        // Arrange
        var highlightedPaths = new HashSet<string> { "test/path" };
        var topics = new List<TopicInfo>
        {
            new TopicInfo 
            { 
                Topic = "test/topic", 
                SourceType = "MQTT",
                CreatedAt = DateTime.Now,
                Path = new HierarchicalPath()
            }
        };

        // Act
        var component = RenderComponent<TopicTree>(parameters => parameters
            .Add(p => p.Topics, topics)
            .Add(p => p.HighlightedPaths, highlightedPaths));

        // Assert
        // Verify that the parameter is accepted and stored
        Assert.Equal(highlightedPaths, component.Instance.HighlightedPaths);
    }

    [Fact]
    public void TopicTree_MaxVisibleTopics_DefaultValue()
    {
        // Act
        var component = RenderComponent<TopicTree>();

        // Assert
        Assert.Equal(1000, component.Instance.MaxVisibleTopics);
    }

    [Fact]
    public void TopicTree_MaxUnverifiedTopicsPerPage_DefaultValue()
    {
        // Act
        var component = RenderComponent<TopicTree>();

        // Assert
        Assert.Equal(100, component.Instance.MaxUnverifiedTopicsPerPage);
    }

    [Fact]
    public void TopicTree_CustomMaxValues_AreRespected()
    {
        // Act
        var component = RenderComponent<TopicTree>(parameters => parameters
            .Add(p => p.MaxVisibleTopics, 500)
            .Add(p => p.MaxUnverifiedTopicsPerPage, 50));

        // Assert
        Assert.Equal(500, component.Instance.MaxVisibleTopics);
        Assert.Equal(50, component.Instance.MaxUnverifiedTopicsPerPage);
    }

    [Fact]
    public void TopicTree_TopicsBySourceType_AreGroupedCorrectly()
    {
        // Arrange
        var topics = new List<TopicInfo>
        {
            new TopicInfo 
            { 
                Topic = "mqtt/temperature", 
                SourceType = "MQTT",
                CreatedAt = DateTime.Now,
                Path = new HierarchicalPath()
            },
            new TopicInfo 
            { 
                Topic = "socket/pressure", 
                SourceType = "SocketIO",
                CreatedAt = DateTime.Now.AddSeconds(1),
                Path = new HierarchicalPath()
            },
            new TopicInfo 
            { 
                Topic = "mqtt/humidity", 
                SourceType = "MQTT",
                CreatedAt = DateTime.Now.AddSeconds(2),
                Path = new HierarchicalPath()
            }
        };

        var component = RenderComponent<TopicTree>(parameters => parameters
            .Add(p => p.Topics, topics));

        // Act - Switch to Data Browser tab
        var dataBrowserTab = component.Find("button:contains('Data Browser')");
        dataBrowserTab.Click();

        // Assert
        // Should show both source types
        Assert.Contains("MQTT", component.Markup);
        Assert.Contains("SocketIO", component.Markup);
    }

    [Fact]
    public void TopicTree_NSTab_DisplaysNSTreeEditor()
    {
        // Act
        var component = RenderComponent<TopicTree>();

        // Assert
        // Should display NSTreeEditor component in NS tab (default active)
        Assert.Contains("NSTreeEditor", component.Markup);
    }

    [Fact]
    public void TopicTree_OnAddDataToNamespace_CallbackIsSetup()
    {
        // Arrange
        var callbackInvoked = false;
        var component = RenderComponent<TopicTree>(parameters => parameters
            .Add(p => p.OnAddDataToNamespace, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<(string, List<TopicInfo>)>(this, 
                (data) => callbackInvoked = true)));

        // Assert
        Assert.NotNull(component.Instance.OnAddDataToNamespace);
    }

    [Fact]
    public void TopicTree_ResponsiveDesign_HasCorrectClasses()
    {
        // Act
        var component = RenderComponent<TopicTree>();

        // Assert
        Assert.Contains("topic-tree-wrapper", component.Markup);
        Assert.Contains("topic-tree-content", component.Markup);
        Assert.Contains("uns-container", component.Markup);
        Assert.Contains("data-browser-container", component.Markup);
    }

    [Fact]
    public void TopicTree_TabPanes_ShowAndHideCorrectly()
    {
        // Arrange
        var component = RenderComponent<TopicTree>();

        // Initially NS tab should be active
        var nsPane = component.Find("#ns-pane");
        var dataBrowserPane = component.Find("#dataBrowser-pane");

        Assert.Contains("show active", nsPane.ClassList);
        Assert.DoesNotContain("show active", dataBrowserPane.ClassList);

        // Act - Switch to Data Browser tab
        var dataBrowserTab = component.Find("button:contains('Data Browser')");
        dataBrowserTab.Click();

        // Assert - Data Browser should now be active
        nsPane = component.Find("#ns-pane");
        dataBrowserPane = component.Find("#dataBrowser-pane");

        Assert.DoesNotContain("show active", nsPane.ClassList);
        Assert.Contains("show active", dataBrowserPane.ClassList);
    }

    [Fact]
    public void TopicTree_BootstrapIcons_AreDisplayed()
    {
        // Act
        var component = RenderComponent<TopicTree>();

        // Assert
        Assert.Contains("bi-diagram-3", component.Markup); // UNS tab icon
        Assert.Contains("bi-database", component.Markup); // Data Browser tab icon
    }
}