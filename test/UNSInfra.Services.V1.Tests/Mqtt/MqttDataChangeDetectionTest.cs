using System;
using System.Collections.Generic;
using System.Threading;
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
/// Test to demonstrate and verify the MQTT data change detection issue.
/// 
/// Issue: The current implementation doesn't detect when data has actually changed.
/// It only checks timing intervals, which means:
/// 1. It might republish the same data repeatedly if timing conditions are met
/// 2. It won't publish when data changes if timing conditions aren't met
/// 3. There's no actual "PublishOnChange" behavior - it's really "PublishOnTimer"
/// </summary>
public class MqttDataChangeDetectionTest
{
    [Fact]
    public void AnalyzeDataChangeDetectionIssue()
    {
        /*
         * CURRENT IMPLEMENTATION ANALYSIS:
         * 
         * The MqttDataExportService.CheckForDataChanges() method:
         * 
         * 1. Gets all topic configurations
         * 2. For each topic, gets the "latest" data from storage
         * 3. Checks if data is recent enough (MaxDataAgeMinutes)
         * 4. Calls ShouldPublishData() which ONLY checks:
         *    - Whether enough time has passed since last publish (MinPublishIntervalMs)
         * 5. If timing is OK, publishes the data
         * 
         * WHAT'S MISSING:
         * 
         * - No tracking of last published VALUE
         * - No comparison of current value vs last published value
         * - No actual change detection
         * 
         * RESULT:
         * 
         * - Same data gets republished repeatedly every MinPublishIntervalMs
         * - OR no data gets published if MinPublishIntervalMs hasn't elapsed
         * - The "PublishOnChange" setting is misleading - it doesn't detect changes
         * 
         * WHAT SHOULD HAPPEN:
         * 
         * 1. Track last published value for each topic
         * 2. Only publish when:
         *    a) Data value has changed AND
         *    b) MinPublishIntervalMs has elapsed since last publish (rate limiting)
         * 3. Optionally support periodic republishing of unchanged data (heartbeat)
         * 
         * PROPOSED FIX:
         * 
         * Add a dictionary to track last published values:
         * private readonly Dictionary<string, object?> _lastPublishedValues = new();
         * 
         * Modify ShouldPublishData to check value changes:
         * 
         * private bool ShouldPublishData(MqttOutputConfiguration config, string topic, DataPoint dataPoint)
         * {
         *     var exportConfig = config.DataExportConfig!;
         *     var key = $"{config.Id}:{topic}";
         *     
         *     // Check if value has changed
         *     if (_lastPublishedValues.TryGetValue(key, out var lastValue))
         *     {
         *         if (Equals(lastValue, dataPoint.Value))
         *         {
         *             // Value hasn't changed, don't publish unless it's time for heartbeat
         *             if (!exportConfig.EnableHeartbeat) return false;
         *             
         *             // Check heartbeat interval
         *             if (_lastPublishTimes.TryGetValue(key, out var lastHeartbeat))
         *             {
         *                 var timeSinceLastHeartbeat = DateTime.UtcNow - lastHeartbeat;
         *                 var heartbeatInterval = TimeSpan.FromMinutes(exportConfig.HeartbeatIntervalMinutes);
         *                 return timeSinceLastHeartbeat >= heartbeatInterval;
         *             }
         *         }
         *     }
         *     
         *     // Value has changed or first publish, check minimum interval
         *     if (_lastPublishTimes.TryGetValue(key, out var lastPublish))
         *     {
         *         var timeSinceLastPublish = DateTime.UtcNow - lastPublish;
         *         var minInterval = TimeSpan.FromMilliseconds(exportConfig.MinPublishIntervalMs);
         *         
         *         if (timeSinceLastPublish < minInterval)
         *         {
         *             return false; // Rate limiting
         *         }
         *     }
         *     
         *     return true;
         * }
         * 
         * And update PublishDataPoint to track the published value:
         * 
         * _lastPublishedValues[key] = dataPoint.Value;
         */
    }
    
    [Fact]
    public async Task CurrentImplementation_RepublishesSameDataRepeatedly()
    {
        // This test would show that the current implementation keeps publishing
        // the same data value over and over again, as long as timing conditions are met
        
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
                MinPublishIntervalMs = 100, // Very short interval for testing
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json
            }
        };

        var topicConfig = new TopicConfiguration
        {
            Topic = "sensor/temperature",
            NSPath = "Enterprise1/Site1/Area1"
        };

        // Same data point returned multiple times (no change)
        var unchangedData = new DataPoint
        {
            Topic = "sensor/temperature",
            Value = 23.5, // Always the same value
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>()
        };

        mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { config });
        
        mockTopicConfigRepository.Setup(x => x.GetAllTopicConfigurationsAsync(false))
            .ReturnsAsync(new List<TopicConfiguration> { topicConfig });
        
        mockRealtimeStorage.Setup(x => x.GetLatestAsync("sensor/temperature"))
            .ReturnsAsync(unchangedData);

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

        // Act - Let the service run and check for data multiple times
        await service.StartAsync();
        await Task.Delay(500); // Let it run for 500ms with 100ms intervals
        await service.StopAsync();

        // Assert - The same data should have been published multiple times
        // even though the value never changed
        mockMqttService.Verify(x => x.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<int>(),
            It.IsAny<bool>()), 
            Times.AtLeast(2)); // Should publish multiple times for same data

        /*
         * This demonstrates the problem: the system publishes the same data
         * repeatedly just because time intervals have passed, not because
         * the data has actually changed.
         * 
         * In a real system, this would cause:
         * - Unnecessary MQTT traffic
         * - Confused downstream consumers
         * - Wasted bandwidth and processing
         */
    }
}