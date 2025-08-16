using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Core.Repositories;
using UNSInfra.Core.Configuration;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.V1.Mqtt;
using UNSInfra.Storage.Abstractions;
using Xunit;

namespace UNSInfra.Services.V1.Tests.Mqtt;

public class MqttDataExportServiceTests
{
    private readonly Mock<ILogger<MqttDataExportService>> _mockLogger;
    private readonly Mock<IInputOutputConfigurationRepository> _mockConfigRepository;
    private readonly Mock<ITopicConfigurationRepository> _mockTopicConfigurationRepository;
    private readonly Mock<IRealtimeStorage> _mockRealtimeStorage;
    private readonly MqttConnectionManager _connectionManager;
    private readonly MqttDataExportService _service;

    public MqttDataExportServiceTests()
    {
        _mockLogger = new Mock<ILogger<MqttDataExportService>>();
        _mockConfigRepository = new Mock<IInputOutputConfigurationRepository>();
        _mockTopicConfigurationRepository = new Mock<ITopicConfigurationRepository>();
        _mockRealtimeStorage = new Mock<IRealtimeStorage>();
        
        // Create a real MqttConnectionManager with mocked dependencies
        var mockConnectionLogger = new Mock<ILogger<MqttConnectionManager>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockDataIngestionRepo = new Mock<IDataIngestionConfigurationRepository>();
        
        _connectionManager = new MqttConnectionManager(
            mockConnectionLogger.Object,
            mockServiceProvider.Object,
            mockDataIngestionRepo.Object);
        
        _service = new MqttDataExportService(
            _mockLogger.Object,
            _mockConfigRepository.Object,
            _mockTopicConfigurationRepository.Object,
            _mockRealtimeStorage.Object,
            _connectionManager);
    }

    [Fact]
    public async Task StartAsync_WithNoEnabledConfigurations_ShouldReturnFalse()
    {
        // Arrange
        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration>());

        // Act
        var result = await _service.StartAsync();

        // Assert
        result.Should().BeFalse();
        VerifyLogContains("No enabled MQTT data export configurations found", LogLevel.Warning);
    }

    [Fact]
    public async Task StartAsync_WithEnabledDataExportConfigurations_ShouldReturnTrue()
    {
        // Arrange
        var config = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Data Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 1000,
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json,
                IncludeTimestamp = true
            }
        };

        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });

        // Act
        var result = await _service.StartAsync();

        // Assert
        result.Should().BeTrue();
        VerifyLogContains("Found 1 enabled MQTT data export configurations", LogLevel.Information);
        VerifyLogContains("MQTT data export service started successfully", LogLevel.Information);
    }

    [Fact]
    public async Task StartAsync_WithModelOnlyConfiguration_ShouldReturnFalse()
    {
        // Arrange - Configuration that only exports models, not data
        var config = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Model Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Model,
            ModelExportConfig = new MqttModelExportConfiguration
            {
                ModelAttributeName = "_model",
                RepublishIntervalMinutes = 60
            }
        };

        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });

        // Act
        var result = await _service.StartAsync();

        // Assert
        result.Should().BeFalse();
        VerifyLogContains("No enabled MQTT data export configurations found", LogLevel.Warning);
    }

    [Fact]
    public async Task StartAsync_WithBothDataAndModelConfiguration_ShouldReturnTrue()
    {
        // Arrange - Configuration that exports both models and data
        var config = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Both Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Both,
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 1000,
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json
            },
            ModelExportConfig = new MqttModelExportConfiguration
            {
                ModelAttributeName = "_model",
                RepublishIntervalMinutes = 60
            }
        };

        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });

        // Act
        var result = await _service.StartAsync();

        // Assert
        result.Should().BeTrue();
        VerifyLogContains("Found 1 enabled MQTT data export configurations", LogLevel.Information);
    }

    [Fact]
    public async Task IsRunningAsync_WhenServiceNotStarted_ShouldReturnFalse()
    {
        // Act
        var result = await _service.IsRunningAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_WhenServiceNotStarted_ShouldReturnNotRunningStatus()
    {
        // Act
        var status = await _service.GetStatusAsync();

        // Assert
        status["IsRunning"].Should().Be(false);
        status["MqttConnected"].Should().Be(false);
        status["ActiveConfigurations"].Should().Be(0);
        status["TrackedTopics"].Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ShouldReturnTrueWithWarning()
    {
        // Arrange - Start the service first
        var config = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            DataExportConfig = new MqttDataExportConfiguration()
        };

        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });

        await _service.StartAsync();

        // Act - Try to start again
        var result = await _service.StartAsync();

        // Assert
        result.Should().BeTrue();
        VerifyLogContains("MQTT data export service is already running", LogLevel.Warning);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_ShouldReturnTrue()
    {
        // Act
        var result = await _service.StopAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WhenRunning_ShouldStopSuccessfully()
    {
        // Arrange - Start the service first
        var config = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            DataExportConfig = new MqttDataExportConfiguration()
        };

        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });

        await _service.StartAsync();

        // Act
        var result = await _service.StopAsync();

        // Assert
        result.Should().BeTrue();
        VerifyLogContains("MQTT data export service stopped successfully", LogLevel.Information);
    }

    [Fact]
    public void Dispose_ShouldDisposeResourcesProperly()
    {
        // Act & Assert - Should not throw
        _service.Dispose();
    }

    [Theory]
    [InlineData(MqttDataFormat.Json)]
    [InlineData(MqttDataFormat.Raw)]
    [InlineData(MqttDataFormat.SparkplugB)]
    public async Task DataExportConfiguration_WithDifferentFormats_ShouldBeSupported(MqttDataFormat format)
    {
        // Arrange
        var config = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            DataExportConfig = new MqttDataExportConfiguration
            {
                DataFormat = format,
                PublishOnChange = true
            }
        };

        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });

        // Act
        var result = await _service.StartAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithMissingConnectionId_ShouldLogError()
    {
        // Arrange - Configuration without ConnectionId
        var config = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Data Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            ConnectionId = "", // Missing connection ID
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 100,
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json
            }
        };

        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });

        // Act
        var result = await _service.StartAsync();

        // Assert
        result.Should().BeTrue(); // Service starts but with warnings
        VerifyLogContains("MQTT data export configuration does not have a ConnectionId specified", LogLevel.Error);
    }

    [Fact]
    public async Task StartAsync_WithValidConfiguration_ShouldCreateActiveConfiguration()
    {
        // Arrange
        var config = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Data Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            ConnectionId = "test-connection",
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 1000,
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json,
                NamespaceFilter = new List<string> { "Enterprise1" },
                TopicFilter = new List<string> { "sensor/*" }
            }
        };

        _mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });

        // Act
        var result = await _service.StartAsync();
        var status = await _service.GetStatusAsync();
        await _service.StopAsync();

        // Assert
        result.Should().BeTrue();
        status["ActiveConfigurations"].Should().Be(1);
        
        var configurations = (List<object>)status["Configurations"];
        configurations.Should().HaveCount(1);
        
        dynamic firstConfig = configurations[0];
        firstConfig.Id.Should().Be("test-output-1");
        firstConfig.Name.Should().Be("Test Data Export");
        firstConfig.OutputType.Should().Be(MqttOutputType.Data);
    }

    private void VerifyLogContains(string message, LogLevel level)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}