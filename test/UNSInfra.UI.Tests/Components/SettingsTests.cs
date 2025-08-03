using UNSInfra.UI.Components.Pages;
using UNSInfra.Repositories;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Validation;
using UNSInfra.Models.Schema;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using Moq;
using Microsoft.Extensions.Logging;

namespace UNSInfra.UI.Tests.Components;

public class SettingsTests : UITestContext
{
    private readonly Mock<ISchemaRepository> _mockSchemaRepository;
    private readonly Mock<ITopicBrowserService> _mockTopicBrowserService;
    private readonly Mock<ISchemaValidator> _mockSchemaValidator;
    private readonly Mock<ILogger<Settings>> _mockLogger;

    public SettingsTests()
    {
        _mockSchemaRepository = new Mock<ISchemaRepository>();
        _mockTopicBrowserService = new Mock<ITopicBrowserService>();
        _mockSchemaValidator = new Mock<ISchemaValidator>();
        _mockLogger = new Mock<ILogger<Settings>>();

        Services.AddSingleton(_mockSchemaRepository.Object);
        Services.AddSingleton(_mockTopicBrowserService.Object);
        Services.AddSingleton(_mockSchemaValidator.Object);
        Services.AddSingleton(_mockLogger.Object);
    }

    [Fact]
    public void Settings_RendersCorrectly()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var component = RenderComponent<Settings>();

        // Assert
        Assert.Contains("Settings", component.Markup);
        Assert.Contains("Configure your UNS Infrastructure system", component.Markup);
    }

    [Fact]
    public void Settings_DisplaysAllTabs()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var component = RenderComponent<Settings>();

        // Assert
        Assert.Contains("Storage", component.Markup);
        Assert.Contains("Hierarchy", component.Markup);
        Assert.Contains("Connections", component.Markup);
        Assert.Contains("Schemas", component.Markup);
        Assert.Contains("System", component.Markup);
    }

    [Fact]
    public void Settings_DefaultTab_IsStorage()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var component = RenderComponent<Settings>();

        // Assert
        var storageTab = component.Find("button:contains('Storage')");
        Assert.Contains("active", storageTab.ClassList);
        
        Assert.Contains("Storage Configuration", component.Markup);
        Assert.Contains("Configure data storage settings", component.Markup);
    }

    [Fact]
    public void Settings_TabNavigation_WorksCorrectly()
    {
        // Arrange
        SetupDefaultMocks();
        var component = RenderComponent<Settings>();

        // Act - Click on Schemas tab
        var schemasTab = component.Find("button:contains('Schemas')");
        schemasTab.Click();

        // Assert
        Assert.Contains("active", schemasTab.ClassList);
        Assert.Contains("Data Schema Validation", component.Markup);
        Assert.Contains("Create and manage JSON schemas", component.Markup);
    }

    [Fact]
    public void Settings_SchemasTab_DisplaysSchemaList()
    {
        // Arrange
        var schemas = new List<DataSchema>
        {
            new DataSchema
            {
                SchemaId = "test-schema",
                Topic = "test/topic",
                JsonSchema = "{}",
                PropertyTypes = new Dictionary<string, Type> { { "temperature", typeof(double) } },
                ValidationRules = new List<ValidationRule>()
            }
        };

        _mockSchemaRepository.Setup(x => x.GetAllSchemasAsync()).ReturnsAsync(schemas);
        _mockTopicBrowserService.Setup(x => x.GetLatestTopicStructureAsync())
            .ReturnsAsync(new List<TopicInfo> { new TopicInfo { Topic = "test/topic" } });

        var component = RenderComponent<Settings>();

        // Act - Navigate to schemas tab
        var schemasTab = component.Find("button:contains('Schemas')");
        schemasTab.Click();

        // Assert
        Assert.Contains("test-schema", component.Markup);
        Assert.Contains("test/topic", component.Markup);
    }

    [Fact]
    public void Settings_SchemasTab_SearchFunctionality()
    {
        // Arrange
        SetupSchemasWithData();
        var component = RenderComponent<Settings>();

        // Navigate to schemas tab
        var schemasTab = component.Find("button:contains('Schemas')");
        schemasTab.Click();

        // Act - Search for schema
        var searchInput = component.Find("input[placeholder='Search schemas...']");
        searchInput.Change("temperature");

        // Assert - The search functionality should filter results
        // Note: The actual filtering happens in the component code
        Assert.NotNull(searchInput);
        Assert.Equal("temperature", searchInput.GetAttribute("value"));
    }

    [Fact]
    public void Settings_SchemasTab_CreateSchemaButton()
    {
        // Arrange
        SetupDefaultMocks();
        var component = RenderComponent<Settings>();

        // Navigate to schemas tab
        var schemasTab = component.Find("button:contains('Schemas')");
        schemasTab.Click();

        // Act
        var createButton = component.Find("button:contains('Create Schema')");

        // Assert
        Assert.NotNull(createButton);
        Assert.Contains("Create Schema", createButton.TextContent);
    }

    [Fact]
    public void Settings_SchemasTab_EmptyState()
    {
        // Arrange
        _mockSchemaRepository.Setup(x => x.GetAllSchemasAsync()).ReturnsAsync(new List<DataSchema>());
        _mockTopicBrowserService.Setup(x => x.GetLatestTopicStructureAsync())
            .ReturnsAsync(new List<TopicInfo>());

        var component = RenderComponent<Settings>();

        // Act - Navigate to schemas tab
        var schemasTab = component.Find("button:contains('Schemas')");
        schemasTab.Click();

        // Assert
        Assert.Contains("No Schemas Found", component.Markup);
        Assert.Contains("No data schemas have been created yet", component.Markup);
    }

    [Fact]
    public void Settings_SystemTab_DisplaysConfiguration()
    {
        // Arrange
        SetupDefaultMocks();
        var component = RenderComponent<Settings>();

        // Act - Navigate to system tab
        var systemTab = component.Find("button:contains('System')");
        systemTab.Click();

        // Assert
        Assert.Contains("System Configuration", component.Markup);
        Assert.Contains("Refresh Settings", component.Markup);
        Assert.Contains("Appearance", component.Markup);
        Assert.Contains("Security & Logging", component.Markup);
    }

    [Fact]
    public void Settings_SystemTab_RefreshIntervalOptions()
    {
        // Arrange
        SetupDefaultMocks();
        var component = RenderComponent<Settings>();

        // Act - Navigate to system tab
        var systemTab = component.Find("button:contains('System')");
        systemTab.Click();

        // Assert
        Assert.Contains("Auto-refresh Interval", component.Markup);
        Assert.Contains("5 seconds", component.Markup);
        Assert.Contains("10 seconds", component.Markup);
        Assert.Contains("30 seconds", component.Markup);
        Assert.Contains("1 minute", component.Markup);
    }

    [Fact]
    public void Settings_SystemTab_ThemeOptions()
    {
        // Arrange
        SetupDefaultMocks();
        var component = RenderComponent<Settings>();

        // Act - Navigate to system tab
        var systemTab = component.Find("button:contains('System')");
        systemTab.Click();

        // Assert
        Assert.Contains("Theme", component.Markup);
        Assert.Contains("Auto (System)", component.Markup);
        Assert.Contains("Light", component.Markup);
        Assert.Contains("Dark", component.Markup);
    }

    [Fact]
    public void Settings_ConnectionsTab_DisplaysConnectionInfo()
    {
        // Arrange
        SetupDefaultMocks();
        var component = RenderComponent<Settings>();

        // Act - Navigate to connections tab
        var connectionsTab = component.Find("button:contains('Connections')");
        connectionsTab.Click();

        // Assert
        Assert.Contains("Data Connections", component.Markup);
        Assert.Contains("Configure MQTT, Socket.IO", component.Markup);
        Assert.Contains("Active Connections", component.Markup);
        Assert.Contains("Connection management is available", component.Markup);
    }

    [Fact]
    public void Settings_HierarchyTab_DisplaysHierarchyEditor()
    {
        // Arrange
        SetupDefaultMocks();
        var component = RenderComponent<Settings>();

        // Act - Navigate to hierarchy tab
        var hierarchyTab = component.Find("button:contains('Hierarchy')");
        hierarchyTab.Click();

        // Assert
        Assert.Contains("Hierarchy Configuration", component.Markup);
        Assert.Contains("Define and manage ISA-S95", component.Markup);
    }

    [Fact]
    public void Settings_RefreshButton_InSchemasTab()
    {
        // Arrange
        SetupSchemasWithData();
        var component = RenderComponent<Settings>();

        // Navigate to schemas tab
        var schemasTab = component.Find("button:contains('Schemas')");
        schemasTab.Click();

        // Act
        var refreshButton = component.Find("button:contains('Refresh')");
        refreshButton.Click();

        // Assert
        _mockSchemaRepository.Verify(x => x.GetAllSchemasAsync(), Times.AtLeastOnce);
    }

    private void SetupDefaultMocks()
    {
        _mockSchemaRepository.Setup(x => x.GetAllSchemasAsync()).ReturnsAsync(new List<DataSchema>());
        _mockTopicBrowserService.Setup(x => x.GetLatestTopicStructureAsync())
            .ReturnsAsync(new List<TopicInfo>());
    }

    private void SetupSchemasWithData()
    {
        var schemas = new List<DataSchema>
        {
            new DataSchema
            {
                SchemaId = "temperature-schema",
                Topic = "sensors/temperature",
                JsonSchema = "{}",
                PropertyTypes = new Dictionary<string, Type> { { "value", typeof(double) } },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule { PropertyName = "value", RuleType = "Range", RuleValue = new double[] { -50, 100 } }
                }
            },
            new DataSchema
            {
                SchemaId = "pressure-schema",
                Topic = "sensors/pressure",
                JsonSchema = "{}",
                PropertyTypes = new Dictionary<string, Type> { { "value", typeof(double) } },
                ValidationRules = new List<ValidationRule>()
            }
        };

        var topics = new List<TopicInfo>
        {
            new TopicInfo { Topic = "sensors/temperature" },
            new TopicInfo { Topic = "sensors/pressure" }
        };

        _mockSchemaRepository.Setup(x => x.GetAllSchemasAsync()).ReturnsAsync(schemas);
        _mockTopicBrowserService.Setup(x => x.GetLatestTopicStructureAsync()).ReturnsAsync(topics);
    }
}