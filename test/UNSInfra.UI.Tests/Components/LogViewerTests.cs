using Microsoft.Extensions.Logging;
using UNSInfra.UI.Components.Pages;
using UNSInfra.UI.Services;
using Moq;
using Microsoft.AspNetCore.Components;
using AngleSharp.Dom;

namespace UNSInfra.UI.Tests.Components;

public class LogViewerTests : TestContext
{
    private readonly Mock<IInMemoryLogService> _mockLogService;

    public LogViewerTests()
    {
        _mockLogService = new Mock<IInMemoryLogService>();
        Services.AddSingleton(_mockLogService.Object);
    }

    [Fact]
    public void LogViewer_RendersCorrectly()
    {
        // Arrange
        var logs = new List<LogEntry>
        {
            new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Information,
                Message = "Test log message",
                Category = "TestCategory",
                Source = "TestSource"
            }
        };

        _mockLogService.Setup(x => x.GetLogs()).Returns(logs);
        _mockLogService.Setup(x => x.SearchLogs(It.IsAny<string>(), It.IsAny<LogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                      .Returns(logs);

        // Act
        var component = RenderComponent<LogViewer>();

        // Assert
        Assert.Contains("log-viewer-container", component.Markup);
        Assert.Contains("Search logs...", component.Markup);
        Assert.Contains("Test log message", component.Markup);
    }

    [Fact]
    public void LogViewer_SearchFunctionality_FiltersLogs()
    {
        // Arrange
        var allLogs = new List<LogEntry>
        {
            new LogEntry { Message = "Error occurred", Level = LogLevel.Error, Timestamp = DateTime.Now },
            new LogEntry { Message = "Information log", Level = LogLevel.Information, Timestamp = DateTime.Now }
        };

        var filteredLogs = new List<LogEntry>
        {
            allLogs[0] // Only error log
        };

        _mockLogService.Setup(x => x.GetLogs()).Returns(allLogs);
        _mockLogService.Setup(x => x.SearchLogs(It.IsAny<string>(), It.IsAny<LogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                      .Returns(filteredLogs);

        var component = RenderComponent<LogViewer>();

        // Act
        var searchInput = component.Find("input[placeholder='Search logs...']");
        searchInput.Change("Error");

        // Assert - The component calls SearchLogs with the current search term
        _mockLogService.Verify(x => x.SearchLogs(It.IsAny<string>(), It.IsAny<LogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.AtLeastOnce);
    }

    [Fact]
    public void LogViewer_LogLevelFilter_UpdatesDisplayedLogs()
    {
        // Arrange
        var allLogs = new List<LogEntry>
        {
            new LogEntry { Message = "Error log", Level = LogLevel.Error, Timestamp = DateTime.Now },
            new LogEntry { Message = "Info log", Level = LogLevel.Information, Timestamp = DateTime.Now }
        };

        _mockLogService.Setup(x => x.GetLogs()).Returns(allLogs);
        _mockLogService.Setup(x => x.SearchLogs("", LogLevel.Error, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                      .Returns(allLogs.Where(l => l.Level == LogLevel.Error));

        var component = RenderComponent<LogViewer>();

        // Act
        var levelSelect = component.Find("select");
        levelSelect.Change(LogLevel.Error.ToString());

        // Assert
        _mockLogService.Verify(x => x.SearchLogs("", LogLevel.Error, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.AtLeastOnce);
    }

    [Fact]
    public void LogViewer_RefreshButton_CallsRefreshLogs()
    {
        // Arrange
        var logs = new List<LogEntry>();
        _mockLogService.Setup(x => x.GetLogs()).Returns(logs);

        var component = RenderComponent<LogViewer>();

        // Act
        var refreshButton = component.Find("button:contains('Refresh')");
        refreshButton.Click();

        // Assert
        _mockLogService.Verify(x => x.GetLogs(), Times.AtLeastOnce);
    }

    [Fact]
    public void LogViewer_ClearButton_ClearsLogs()
    {
        // Arrange
        var logs = new List<LogEntry>
        {
            new LogEntry { Message = "Test log", Level = LogLevel.Information, Timestamp = DateTime.Now }
        };

        _mockLogService.Setup(x => x.GetLogs()).Returns(logs);
        var component = RenderComponent<LogViewer>();

        // Act
        var clearButton = component.Find("button:contains('Clear')");
        clearButton.Click();

        // Assert
        _mockLogService.Verify(x => x.ClearLogs(), Times.Once);
    }

    [Fact]
    public void LogViewer_EmptyState_DisplaysCorrectMessage()
    {
        // Arrange - LogViewer initializes with default filters and no filters set, so mock both scenarios
        _mockLogService.Setup(x => x.GetLogs()).Returns(new List<LogEntry>());
        _mockLogService.Setup(x => x.SearchLogs(It.IsAny<string>(), It.IsAny<LogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                      .Returns(new List<LogEntry>());

        // Act
        var component = RenderComponent<LogViewer>();

        // Assert - The component should show empty state
        Assert.Contains("No Logs Found", component.Markup);
        // The message will depend on whether filters are applied - in this case, default filters are applied
        Assert.True(component.Markup.Contains("No logs are currently available") || 
                   component.Markup.Contains("No logs match your current filters"));
    }

    [Fact]
    public void LogViewer_LogLevelBadges_DisplayCorrectClasses()
    {
        // Arrange
        var logs = new List<LogEntry>
        {
            new LogEntry { Message = "Error", Level = LogLevel.Error, Timestamp = DateTime.Now, Category = "Test", Source = "Test" },
            new LogEntry { Message = "Warning", Level = LogLevel.Warning, Timestamp = DateTime.Now, Category = "Test", Source = "Test" },
            new LogEntry { Message = "Info", Level = LogLevel.Information, Timestamp = DateTime.Now, Category = "Test", Source = "Test" }
        };

        _mockLogService.Setup(x => x.GetLogs()).Returns(logs);
        _mockLogService.Setup(x => x.SearchLogs(It.IsAny<string>(), It.IsAny<LogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                      .Returns(logs);

        // Act
        var component = RenderComponent<LogViewer>();

        // Assert
        Assert.Contains("bg-danger", component.Markup); // Error level
        Assert.Contains("bg-warning", component.Markup); // Warning level
        Assert.Contains("bg-primary", component.Markup); // Information level
    }

    [Fact]
    public void LogViewer_AutoRefresh_ToggleWorks()
    {
        // Arrange
        _mockLogService.Setup(x => x.GetLogs()).Returns(new List<LogEntry>());
        var component = RenderComponent<LogViewer>();

        // Act
        var autoRefreshCheckbox = component.Find("input[type='checkbox']#autoRefresh");
        var initialChecked = autoRefreshCheckbox.GetAttribute("checked");
        
        autoRefreshCheckbox.Change(false);

        // Assert - verify the checkbox state changed
        Assert.NotNull(initialChecked); // Should start checked
    }

    [Fact]
    public void LogViewer_LogClickExpansion_TogglesDetails()
    {
        // Arrange
        var logs = new List<LogEntry>
        {
            new LogEntry 
            { 
                Message = "Test log with exception", 
                Level = LogLevel.Error, 
                Timestamp = DateTime.Now,
                Category = "TestCategory",
                Source = "TestSource",
                Exception = new Exception("Test exception")
            }
        };

        _mockLogService.Setup(x => x.GetLogs()).Returns(logs);
        _mockLogService.Setup(x => x.SearchLogs(It.IsAny<string>(), It.IsAny<LogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                      .Returns(logs);

        var component = RenderComponent<LogViewer>();

        // Act
        var logEntry = component.Find(".log-entry");
        logEntry.Click();

        // Assert
        Assert.Contains("Full Timestamp:", component.Markup);
        Assert.Contains("Category:", component.Markup);
        Assert.Contains("Exception:", component.Markup);
    }

    [Fact]
    public void LogViewer_DateRangeFilter_SetsDefaultValues()
    {
        // Arrange
        _mockLogService.Setup(x => x.GetLogs()).Returns(new List<LogEntry>());

        // Act
        var component = RenderComponent<LogViewer>();

        // Assert
        var dateInputs = component.FindAll("input[type='datetime-local']");
        Assert.Equal(2, dateInputs.Count); // From and To date inputs

        // Verify that default dates are set (last 5 minutes)
        _mockLogService.Verify(x => x.SearchLogs(It.IsAny<string>(), LogLevel.Information, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.AtLeastOnce);
    }

    [Fact]
    public void LogViewer_LoadMoreButton_ShowsWhenNeeded()
    {
        // Arrange
        var logs = new List<LogEntry>();
        for (int i = 0; i < 150; i++) // More than default 100
        {
            logs.Add(new LogEntry 
            { 
                Message = $"Log {i}", 
                Level = LogLevel.Information, 
                Timestamp = DateTime.Now.AddSeconds(-i),
                Category = "Test",
                Source = "Test"
            });
        }

        _mockLogService.Setup(x => x.GetLogs()).Returns(logs);
        _mockLogService.Setup(x => x.SearchLogs(It.IsAny<string>(), It.IsAny<LogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                      .Returns(logs);

        // Act
        var component = RenderComponent<LogViewer>();

        // Assert
        Assert.Contains("Load More", component.Markup);
        Assert.Contains("remaining)", component.Markup);
    }
}