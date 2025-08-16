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
/// Test class to reproduce and verify the MQTT data publishing issue.
/// 
/// Issue: When MQTT output data is configured, it should publish topics when data is updated,
/// but currently nothing is being output even though model export works as expected.
/// 
/// Root Cause: The MqttDataExportService fails to add configurations to _activeConfigurations
/// when MQTT connections cannot be established, which prevents the data change monitoring
/// from finding any applicable configurations to publish.
/// </summary>
public class MqttDataPublishingIssueReproduction
{
    [Fact]
    public async Task ReproduceIssue_MqttConnectionFailure_PreventsDataPublishing()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MqttDataExportService>>();
        var mockConfigRepository = new Mock<IInputOutputConfigurationRepository>();
        var mockTopicConfigRepository = new Mock<ITopicConfigurationRepository>();
        var mockRealtimeStorage = new Mock<IRealtimeStorage>();
        
        // Create a real MqttConnectionManager that will fail to create connections
        var mockConnectionLogger = new Mock<ILogger<MqttConnectionManager>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockDataIngestionRepo = new Mock<IDataIngestionConfigurationRepository>();
        
        var connectionManager = new MqttConnectionManager(
            mockConnectionLogger.Object,
            mockServiceProvider.Object,
            mockDataIngestionRepo.Object);

        // Configure a valid MQTT output configuration
        var validConfig = new MqttOutputConfiguration
        {
            Id = "test-output-1",
            Name = "Test Data Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            ConnectionId = "non-existent-connection", // This connection doesn't exist
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 1000,
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json,
                NamespaceFilter = new List<string>(),
                TopicFilter = new List<string>()
            }
        };

        mockConfigRepository.Setup(x => x.GetMqttOutputConfigurationsAsync(true))
            .ReturnsAsync(new List<MqttOutputConfiguration> { validConfig });

        var service = new MqttDataExportService(
            mockLogger.Object,
            mockConfigRepository.Object,
            mockTopicConfigRepository.Object,
            mockRealtimeStorage.Object,
            connectionManager);

        // Act
        var startResult = await service.StartAsync();
        var status = await service.GetStatusAsync();
        await service.StopAsync();

        // Assert - This demonstrates the issue
        startResult.Should().BeTrue(); // Service reports success
        
        // BUT: No active configurations are created because MQTT connection failed
        status["ActiveConfigurations"].Should().Be(0);
        
        // This means data change monitoring will never find any configurations to publish to
        var configurations = status["Configurations"];
        ((System.Collections.ICollection)configurations).Count.Should().Be(0);
        
        // Verify that error was logged for connection failure
        mockConnectionLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ExplainTheIssue_WhenMqttConnectionFails_DataPublishingStops()
    {
        /*
         * ISSUE EXPLANATION:
         * 
         * When configuring MQTT output data publishing, users expect that:
         * 1. Data updates for topics matching the filters should be published to MQTT
         * 2. The system should continue monitoring for data changes
         * 3. Publishing should happen according to the PublishOnChange and timing settings
         * 
         * WHAT ACTUALLY HAPPENS:
         * 
         * 1. MqttDataExportService.StartAsync() loads enabled output configurations
         * 2. For each configuration, it tries to get/create an MQTT connection via MqttConnectionManager
         * 3. If the MQTT connection fails (broker unreachable, authentication issues, etc.):
         *    - GetOrCreateConnectionAsync() returns null
         *    - The service logs an error and continues to next configuration
         *    - The configuration is NEVER added to _activeConfigurations
         * 4. The data change monitoring starts successfully but _activeConfigurations is empty
         * 5. In CheckForDataChanges(), when it looks for applicable configurations:
         *    ```csharp
         *    var applicableConfigs = _activeConfigurations.Values
         *        .Where(c => ShouldExportTopic(c, topicConfig))
         *        .ToList();
         *    ```
         *    This always returns an empty list, so no data is ever published
         * 
         * POTENTIAL SOLUTIONS:
         * 
         * 1. Retry Logic: Implement retry mechanism for failed MQTT connections
         * 2. Graceful Degradation: Allow configurations to be active even with failed connections,
         *    and retry publishing periodically
         * 3. Connection Health Monitoring: Periodically check and restore failed connections
         * 4. Better Error Reporting: Make it clearer to users when MQTT connections fail
         *    and data publishing is disabled
         * 
         * The model export likely works because it uses a different code path that doesn't
         * depend on real-time MQTT connections for the export process.
         */
    }
}