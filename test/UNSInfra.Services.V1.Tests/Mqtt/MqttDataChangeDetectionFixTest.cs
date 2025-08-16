using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Core.Repositories;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.V1.Mqtt;
using UNSInfra.Storage.Abstractions;
using Xunit;

namespace UNSInfra.Services.V1.Tests.Mqtt;

/// <summary>
/// Test to verify that the MQTT data export service now properly detects value changes
/// and only publishes when data actually changes, not just on timing intervals.
/// </summary>
public class MqttDataChangeDetectionFixTest
{
    [Fact]
    public async Task FixedImplementation_OnlyPublishesWhenValueChanges()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MqttDataExportService>>();
        var mockConfigRepository = new Mock<IInputOutputConfigurationRepository>();
        var mockTopicConfigRepository = new Mock<ITopicConfigurationRepository>();
        var mockRealtimeStorage = new Mock<IRealtimeStorage>();
        var mockConnectionManager = new Mock<MqttConnectionManager>(
            Mock.Of<ILogger<MqttConnectionManager>>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<IDataIngestionConfigurationRepository>());
        
        var config = new MqttOutputConfiguration
        {
            Id = "test-output",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            ConnectionId = "test-connection",
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 50, // Very short interval for testing
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json,
                UseUNSPathAsTopic = true // Use UNS path for topic structure
            }
        };

        var hierarchicalPath = new HierarchicalPath();
        hierarchicalPath.SetValue("Enterprise", "Enterprise1");
        hierarchicalPath.SetValue("Site", "Site1");
        hierarchicalPath.SetValue("Area", "Area1");

        var topicConfig = new TopicConfiguration
        {
            Topic = "sensor/temperature",
            NSPath = "Enterprise1/Site1/Area1",
            UNSName = "Temperature",
            Path = hierarchicalPath
        };

        mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });
        
        mockTopicConfigRepository.Setup(x => x.GetAllTopicConfigurationsAsync(false))
            .ReturnsAsync(new List<TopicConfiguration> { topicConfig });

        var mockMqttService = new Mock<IMqttDataIngestionService>();
        mockMqttService.Setup(x => x.IsConnectedAsync()).ReturnsAsync(true);
        mockMqttService.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        mockConnectionManager.Setup(x => x.GetOrCreateConnectionAsync("test-connection", It.IsAny<string>()))
            .ReturnsAsync(mockMqttService.Object);

        // Simulate different data values over time
        var dataSequence = new Queue<DataPoint>(new[]
        {
            new DataPoint { Topic = "sensor/temperature", Value = 23.5, Timestamp = DateTime.UtcNow },
            new DataPoint { Topic = "sensor/temperature", Value = 23.5, Timestamp = DateTime.UtcNow }, // Same value
            new DataPoint { Topic = "sensor/temperature", Value = 24.0, Timestamp = DateTime.UtcNow }, // Changed!
            new DataPoint { Topic = "sensor/temperature", Value = 24.0, Timestamp = DateTime.UtcNow }, // Same again
            new DataPoint { Topic = "sensor/temperature", Value = 22.8, Timestamp = DateTime.UtcNow }  // Changed!
        });

        mockRealtimeStorage.Setup(x => x.GetLatestAsync("sensor/temperature"))
            .Returns(() => Task.FromResult(dataSequence.Count > 0 ? dataSequence.Dequeue() : null));

        var service = new MqttDataExportService(
            mockLogger.Object,
            mockConfigRepository.Object,
            mockTopicConfigRepository.Object,
            mockRealtimeStorage.Object,
            mockConnectionManager.Object);

        // Act - Run the service for multiple check cycles
        await service.StartAsync();
        
        // Let it run for enough time to process all the data points
        // The service checks every 1000ms, so we need enough time for multiple checks
        await Task.Delay(6000); // 6 seconds should be enough for 5+ checks
        
        await service.StopAsync();

        // Assert - Should only publish when values actually changed
        // Expected publishes: 23.5 (first), 24.0 (changed), 22.8 (changed) = 3 times
        mockMqttService.Verify(x => x.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<int>(),
            It.IsAny<bool>()), 
            Times.Exactly(3)); // Only 3 publishes for 3 unique values

        // Verify the UNS path structure is used in the MQTT topic
        mockMqttService.Verify(x => x.PublishAsync(
            "Enterprise1/Site1/Area1/Temperature", // Should use UNS hierarchical path
            It.IsAny<byte[]>(),
            It.IsAny<int>(),
            It.IsAny<bool>()), 
            Times.AtLeast(1));
    }

    [Fact]
    public async Task FixedImplementation_RespectsMinimumPublishInterval()
    {
        // This test verifies that even when values change rapidly,
        // the minimum publish interval is still respected for rate limiting

        // Arrange
        var mockLogger = new Mock<ILogger<MqttDataExportService>>();
        var mockConfigRepository = new Mock<IInputOutputConfigurationRepository>();
        var mockTopicConfigRepository = new Mock<ITopicConfigurationRepository>();
        var mockRealtimeStorage = new Mock<IRealtimeStorage>();
        var mockConnectionManager = new Mock<MqttConnectionManager>(
            Mock.Of<ILogger<MqttConnectionManager>>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<IDataIngestionConfigurationRepository>());
        
        var config = new MqttOutputConfiguration
        {
            Id = "test-output",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            ConnectionId = "test-connection",
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 2000, // 2 second minimum interval
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json
            }
        };

        var topicConfig = new TopicConfiguration
        {
            Topic = "sensor/pressure",
            NSPath = "Enterprise1/Site1/Area1"
        };

        mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });
        
        mockTopicConfigRepository.Setup(x => x.GetAllTopicConfigurationsAsync(false))
            .ReturnsAsync(new List<TopicConfiguration> { topicConfig });

        // Rapidly changing values
        var changeCounter = 0;
        mockRealtimeStorage.Setup(x => x.GetLatestAsync("sensor/pressure"))
            .Returns(() => Task.FromResult<DataPoint?>(new DataPoint 
            { 
                Topic = "sensor/pressure", 
                Value = 100 + (++changeCounter), // Always different value
                Timestamp = DateTime.UtcNow 
            }));

        var mockMqttService = new Mock<IMqttDataIngestionService>();
        mockMqttService.Setup(x => x.IsConnectedAsync()).ReturnsAsync(true);
        mockMqttService.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        mockConnectionManager.Setup(x => x.GetOrCreateConnectionAsync("test-connection", It.IsAny<string>()))
            .ReturnsAsync(mockMqttService.Object);

        var service = new MqttDataExportService(
            mockLogger.Object,
            mockConfigRepository.Object,
            mockTopicConfigRepository.Object,
            mockRealtimeStorage.Object,
            mockConnectionManager.Object);

        // Act - Run for 5 seconds with 2-second minimum interval
        await service.StartAsync();
        await Task.Delay(5000);
        await service.StopAsync();

        // Assert - Even though values are changing rapidly, 
        // should only publish at most every 2 seconds due to rate limiting
        // In 5 seconds: first publish immediately, then max 1-2 more
        mockMqttService.Verify(x => x.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<int>(),
            It.IsAny<bool>()), 
            Times.AtMost(3)); // Rate limited by MinPublishIntervalMs
    }
}