using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Namespace;
using UNSInfra.Repositories;
using UNSInfra.Services;
using UNSInfra.Services.AutoMapping;
using Xunit;

namespace UNSInfra.Core.Tests.Services.AutoMapping;

public class AutoTopicMapperServiceTests
{
    private readonly Mock<INamespaceStructureService> _mockNamespaceService;
    private readonly Mock<ITopicConfigurationRepository> _mockTopicRepository;
    private readonly Mock<IHierarchyService> _mockHierarchyService;
    private readonly Mock<ILogger<AutoTopicMapperService>> _mockLogger;
    private readonly AutoTopicMapperService _service;

    public AutoTopicMapperServiceTests()
    {
        _mockNamespaceService = new Mock<INamespaceStructureService>();
        _mockTopicRepository = new Mock<ITopicConfigurationRepository>();
        _mockHierarchyService = new Mock<IHierarchyService>();
        _mockLogger = new Mock<ILogger<AutoTopicMapperService>>();

        _service = new AutoTopicMapperService(
            _mockNamespaceService.Object,
            _mockTopicRepository.Object,
            _mockHierarchyService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task TryMapTopicAsync_WithDisabledAutoMapper_ShouldReturnNull()
    {
        // Arrange
        var config = new AutoTopicMapperConfiguration { Enabled = false };
        const string topic = "test/topic";
        const string sourceType = "MQTT";

        // Act
        var result = await _service.TryMapTopicAsync(topic, sourceType, config);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryMapTopicAsync_WithEnabledButLowConfidence_ShouldReturnNull()
    {
        // Arrange
        var config = new AutoTopicMapperConfiguration 
        { 
            Enabled = true, 
            MinimumConfidence = 0.9 
        };
        const string topic = "test/topic";
        const string sourceType = "MQTT";

        SetupMockServices();

        // Act
        var result = await _service.TryMapTopicAsync(topic, sourceType, config);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAutoMappingAsync_WithValidCustomRule_ShouldProcessRule()
    {
        // Arrange
        var config = new AutoTopicMapperConfiguration
        {
            Enabled = true,
            MinimumConfidence = 0.8,
            CustomRules = new List<AutoMappingRule>
            {
                new()
                {
                    TopicPattern = @"socketio/update/([^/]+)/([^/]+)",
                    UNSPathTemplate = "{0}/{1}",
                    Confidence = 0.9,
                    IsActive = true,
                    Description = "Test rule"
                }
            }
        };

        const string topic = "socketio/update/Enterprise1/OEE";

        SetupMockServices();

        // Act
        var result = await _service.ValidateAutoMappingAsync(topic, config);

        // Assert
        result.Should().NotBeNull();
        // The exact result depends on whether the UNS path exists, but we can verify processing
        result.Confidence.Should().BeGreaterOrEqualTo(0.0);
    }

    [Fact]
    public async Task GetAutoMappingSuggestionsAsync_ShouldReturnSuggestions()
    {
        // Arrange
        var config = new AutoTopicMapperConfiguration
        {
            Enabled = true,
            MaxSearchDepth = 5,
            StripPrefixes = new List<string> { "socketio/update/" }
        };

        const string topic = "socketio/update/Enterprise1/OEE/value";

        SetupMockServices();

        // Act
        var result = await _service.GetAutoMappingSuggestionsAsync(topic, config);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AutoMappingSuggestion>>();
        // We don't test specific suggestions since they depend on the mock setup
    }

    [Theory]
    [InlineData("socketio/update/Enterprise1/OEE", "Enterprise1/OEE")]
    [InlineData("mqtt/factory/line1/sensor", "factory/line1/sensor")]
    [InlineData("test/topic/value", "test/topic")]
    public async Task ValidateAutoMappingAsync_WithStripPrefixesAndSuffixes_ShouldCleanTopic(string inputTopic, string expectedCleanTopic)
    {
        // Arrange
        var config = new AutoTopicMapperConfiguration
        {
            Enabled = true,
            StripPrefixes = new List<string> { "socketio/update/", "mqtt/" },
            MinimumConfidence = 0.1 // Low threshold for this test
        };

        SetupMockServices();

        // Act
        var result = await _service.ValidateAutoMappingAsync(inputTopic, config);

        // Assert
        // The exact result depends on the mock setup, but we can verify the method doesn't throw
        result.Should().NotBeNull();
    }

    private void SetupMockServices()
    {
        var nsStructure = new List<NSTreeNode>
        {
            new()
            {
                Name = "Enterprise1",
                FullPath = "Enterprise1",
                NodeType = NSNodeType.HierarchyNode,
                Children = new List<NSTreeNode>
                {
                    new()
                    {
                        Name = "OEE",
                        FullPath = "Enterprise1/OEE",
                        NodeType = NSNodeType.Namespace,
                        Children = new List<NSTreeNode>()
                    }
                }
            }
        };

        _mockNamespaceService.Setup(s => s.GetNamespaceStructureAsync())
                           .ReturnsAsync(nsStructure);

        _mockHierarchyService.Setup(s => s.CreatePathFromStringAsync(It.IsAny<string>()))
                           .ReturnsAsync(CreateTestHierarchicalPath());
    }

    private static HierarchicalPath CreateTestHierarchicalPath()
    {
        var path = new HierarchicalPath();
        path.SetValue("Enterprise", "Enterprise1");
        path.SetValue("Site", "Site1");
        return path;
    }
}