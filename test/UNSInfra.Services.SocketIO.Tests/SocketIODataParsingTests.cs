using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using UNSInfra.Models.Data;
using UNSInfra.Services.SocketIO;
using UNSInfra.Services.SocketIO.Configuration;
using Xunit;

namespace UNSInfra.Services.SocketIO.Tests;

/// <summary>
/// Tests to verify SocketIO data parsing handles topic paths correctly and avoids duplication
/// </summary>
public class SocketIODataParsingTests
{
    [Fact]
    public async Task SocketIODataService_WithVirtualFactoryData_ShouldNotDuplicateEnterprisePath()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SocketIOPureDataService>>();
        var config = new SocketIODataIngestionConfiguration
        {
            ServerUrl = "https://test.com",
            BaseTopicPath = "virtualfactory", 
            EventNames = new[] { "update" },
            EnableDetailedLogging = true
        };

        var service = new SocketIOPureDataService(mockLogger.Object, config);
        var receivedTopics = new List<string>();
        
        // Subscribe to data received events
        service.DataReceived += (sender, dataPoint) =>
        {
            receivedTopics.Add(dataPoint.Topic);
        };

        // Act - Simulate receiving data with nested Enterprise structure
        var jsonData = JsonDocument.Parse("""
        {
            "Enterprise": {
                "Dallas": {
                    "BU": {
                        "OEE": 85.5,
                        "Availability": 92.3,
                        "Performance": 94.1,
                        "Quality": 98.7
                    }
                }
            }
        }
        """).RootElement;

        // Use reflection to call the private method
        var method = typeof(SocketIOPureDataService).GetMethod("ProcessJsonDataAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task?)method?.Invoke(service, new object[] { jsonData, config.BaseTopicPath, "update", "" });
        if (task != null) await task;

        // Assert - Topics should not have duplicated paths
        Assert.NotEmpty(receivedTopics);
        Assert.All(receivedTopics, topic =>
        {
            // Should not have "virtualfactory/update/Enterprise" followed by another "Enterprise"
            Assert.DoesNotContain("Enterprise/Enterprise", topic);
            Assert.DoesNotContain("virtualfactory/update/Enterprise/Enterprise", topic);
        });

        // Topics should be clean and follow expected structure
        var expectedTopics = new[]
        {
            "virtualfactory/update/Enterprise/Dallas/BU/OEE",
            "virtualfactory/update/Enterprise/Dallas/BU/Availability", 
            "virtualfactory/update/Enterprise/Dallas/BU/Performance",
            "virtualfactory/update/Enterprise/Dallas/BU/Quality"
        };

        foreach (var expectedTopic in expectedTopics)
        {
            Assert.Contains(expectedTopic, receivedTopics);
        }
    }

    [Fact]
    public async Task SocketIODataService_WithBasePathMatchingDataStructure_ShouldAvoidDuplication()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SocketIOPureDataService>>();
        var config = new SocketIODataIngestionConfiguration
        {
            ServerUrl = "https://test.com",
            BaseTopicPath = "Enterprise", // Base path matches the data structure
            EventNames = new[] { "data" },
            EnableDetailedLogging = true
        };

        var service = new SocketIOPureDataService(mockLogger.Object, config);
        var receivedTopics = new List<string>();
        
        service.DataReceived += (sender, dataPoint) =>
        {
            receivedTopics.Add(dataPoint.Topic);
        };

        // Act - Data structure that matches the base path
        var jsonData = JsonDocument.Parse("""
        {
            "Enterprise": {
                "Factory1": {
                    "Line1": {
                        "temperature": 75.2
                    }
                }
            }
        }
        """).RootElement;

        var method = typeof(SocketIOPureDataService).GetMethod("ProcessJsonDataAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task?)method?.Invoke(service, new object[] { jsonData, config.BaseTopicPath, "data", "" });
        if (task != null) await task;

        // Assert - Should not have Enterprise/Enterprise duplication
        Assert.NotEmpty(receivedTopics);
        Assert.All(receivedTopics, topic =>
        {
            Assert.DoesNotContain("Enterprise/Enterprise", topic);
        });

        // Should have clean path structure without duplication
        Assert.Contains("Enterprise/data/Factory1/Line1/temperature", receivedTopics);
    }

    [Fact]
    public async Task SocketIODataService_WithDifferentEventNames_ShouldCreateUniqueTopics()
    {
        // Arrange  
        var mockLogger = new Mock<ILogger<SocketIOPureDataService>>();
        var config = new SocketIODataIngestionConfiguration
        {
            ServerUrl = "https://test.com", 
            BaseTopicPath = "factory",
            EventNames = new[] { "sensor_data", "alarm_data" },
            EnableDetailedLogging = true
        };

        var service = new SocketIOPureDataService(mockLogger.Object, config);
        var receivedDataPoints = new List<DataPoint>();
        
        service.DataReceived += (sender, dataPoint) =>
        {
            receivedDataPoints.Add(dataPoint);
        };

        // Act - Process data from different events
        var sensorData = JsonDocument.Parse("""
        {
            "temperature": 25.5,
            "humidity": 65.2
        }
        """).RootElement;

        var alarmData = JsonDocument.Parse("""
        {
            "alarm_id": "ALM_001",
            "severity": "HIGH"
        }
        """).RootElement;

        var method = typeof(SocketIOPureDataService).GetMethod("ProcessJsonDataAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task1 = (Task?)method?.Invoke(service, new object[] { sensorData, config.BaseTopicPath, "sensor_data", "" });
        var task2 = (Task?)method?.Invoke(service, new object[] { alarmData, config.BaseTopicPath, "alarm_data", "" });
        
        if (task1 != null && task2 != null) await Task.WhenAll(task1, task2);

        // Assert - Should have different topics for different events
        Assert.Equal(4, receivedDataPoints.Count);
        
        var sensorTopics = receivedDataPoints.Where(dp => dp.Metadata["EventName"].ToString() == "sensor_data")
                                            .Select(dp => dp.Topic).ToList();
        var alarmTopics = receivedDataPoints.Where(dp => dp.Metadata["EventName"].ToString() == "alarm_data")
                                           .Select(dp => dp.Topic).ToList();

        Assert.Contains("factory/sensor_data/temperature", sensorTopics);
        Assert.Contains("factory/sensor_data/humidity", sensorTopics);
        Assert.Contains("factory/alarm_data/alarm_id", alarmTopics);
        Assert.Contains("factory/alarm_data/severity", alarmTopics);
    }

    [Theory]
    [InlineData("virtualfactory", "update", "Enterprise/Dallas/BU", "virtualfactory/update/Enterprise/Dallas/BU")]
    [InlineData("", "data", "Sensor/Temperature", "data/Sensor/Temperature")]  
    [InlineData("factory", "", "Line1/Status", "factory/Line1/Status")]
    [InlineData("", "", "Simple/Path", "Simple/Path")]
    public async Task SocketIODataService_PathConstruction_ShouldHandleVariousConfigurations(
        string basePath, string eventName, string dataPath, string expectedTopic)
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SocketIOPureDataService>>();
        var config = new SocketIODataIngestionConfiguration
        {
            ServerUrl = "https://test.com",
            BaseTopicPath = basePath,
            EventNames = new[] { eventName },
            EnableDetailedLogging = true
        };

        var service = new SocketIOPureDataService(mockLogger.Object, config);
        var receivedTopics = new List<string>();
        
        service.DataReceived += (sender, dataPoint) =>
        {
            receivedTopics.Add(dataPoint.Topic);
        };

        // Act - Create a simple JSON structure that matches the data path
        var pathParts = dataPath.Split('/');
        var jsonObj = new Dictionary<string, object>();
        var current = jsonObj;
        
        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            var nested = new Dictionary<string, object>();
            current[pathParts[i]] = nested;
            current = nested;
        }
        current[pathParts.Last()] = 42.0; // Leaf value

        var jsonString = JsonSerializer.Serialize(jsonObj);
        var jsonData = JsonDocument.Parse(jsonString).RootElement;

        var method = typeof(SocketIOPureDataService).GetMethod("ProcessJsonDataAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task?)method?.Invoke(service, new object[] { jsonData, config.BaseTopicPath, eventName, "" });
        if (task != null) await task;

        // Assert
        Assert.Contains(expectedTopic, receivedTopics);
    }
}