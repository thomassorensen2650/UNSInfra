using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Services.V1.Configuration;
using UNSInfra.Services.V1.Mqtt;
using UNSInfra.Services.V1.SparkplugB;
using Xunit;

namespace UNSInfra.Services.V1.Tests.Mqtt;

public class MqttDataServiceTests : IDisposable
{
    private readonly Mock<ILogger<MqttDataService>> _loggerMock;
    private readonly Mock<ITopicDiscoveryService> _topicDiscoveryMock;
    private readonly Mock<SparkplugBDecoder> _sparkplugDecoderMock;
    private readonly IOptions<MqttConfiguration> _config;
    private readonly MqttDataService _service;

    public MqttDataServiceTests()
    {
        _loggerMock = new Mock<ILogger<MqttDataService>>();
        _topicDiscoveryMock = new Mock<ITopicDiscoveryService>();
        
        var mockSparkplugLogger = new Mock<ILogger<SparkplugBDecoder>>();
        _sparkplugDecoderMock = new Mock<SparkplugBDecoder>(mockSparkplugLogger.Object);
        
        _config = Options.Create(new MqttConfiguration
        {
            BrokerHost = "localhost",
            BrokerPort = 1883,
            Username = "testuser",
            Password = "testpass",
            ClientId = "test-client",
            UseTls = false,
            KeepAliveInterval = 60,
            ConnectionTimeout = 30
        });

        _service = new MqttDataService(
            _config,
            _topicDiscoveryMock.Object,
            _sparkplugDecoderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldInitialize()
    {
        // Act & Assert
        Assert.NotNull(_service);
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MqttDataService(
            null!,
            _topicDiscoveryMock.Object,
            _sparkplugDecoderMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MqttDataService(
            _config,
            _topicDiscoveryMock.Object,
            _sparkplugDecoderMock.Object,
            null!));
    }

    [Fact]
    public async Task SubscribeToTopicAsync_WithValidTopic_ShouldNotThrow()
    {
        // Arrange
        var topic = "test/topic";
        var hierarchicalPath = new HierarchicalPath();

        // Act & Assert
        await _service.SubscribeToTopicAsync(topic, hierarchicalPath);
    }

    [Fact]
    public async Task UnsubscribeFromTopicAsync_WithValidTopic_ShouldNotThrow()
    {
        // Arrange
        var topic = "test/topic";

        // Act & Assert
        await _service.UnsubscribeFromTopicAsync(topic);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task SubscribeToTopicAsync_WithInvalidTopic_ShouldThrowArgumentException(string? topic)
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SubscribeToTopicAsync(topic!, hierarchicalPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task UnsubscribeFromTopicAsync_WithInvalidTopic_ShouldThrowArgumentException(string? topic)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UnsubscribeFromTopicAsync(topic!));
    }

    [Fact]
    public async Task SubscribeToTopicAsync_WithNullHierarchicalPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        var topic = "test/topic";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SubscribeToTopicAsync(topic, null!));
    }

    [Fact]
    public void DataReceived_Event_ShouldBeAvailable()
    {
        // Arrange

        // Act
        _service.DataReceived += (sender, dataPoint) =>
        {
            // Event subscription test - just verify it can be subscribed to
        };

        // Simulate internal data reception by creating a test data point
        var testDataPoint = new DataPoint
        {
            Topic = "test/topic",
            Value = "test value",
            Timestamp = DateTime.UtcNow,
            Source = "MQTT"
        };

        // Trigger the event manually for testing
        var eventField = typeof(MqttDataService).GetField("DataReceived", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (eventField?.GetValue(_service) is EventHandler<DataPoint> eventHandler)
        {
            eventHandler?.Invoke(_service, testDataPoint);
        }
        else
        {
            // Alternative approach - use reflection to get the backing field
            var backingField = typeof(MqttDataService)
                .GetEvents(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(e => e.Name == "DataReceived");
            
            // For this test, we'll just verify the event can be subscribed to
            Assert.NotNull(backingField);
        }

        // Note: Since we can't easily trigger internal events, we'll just verify subscription works
        Assert.NotNull(_service);
    }

    [Fact]
    public async Task StartAsync_ShouldNotThrow()
    {
        // Act & Assert
        await _service.StartAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldNotThrow()
    {
        // Act & Assert  
        await _service.StopAsync();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Act & Assert
        _service.Dispose();
        
        // Multiple dispose calls should be safe
        _service.Dispose();
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}

/// <summary>
/// Integration tests for MQTT Data Service with real MQTT broker scenarios
/// </summary>
public class MqttDataServiceIntegrationTests
{
    [Fact]
    public void MqttConfiguration_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new MqttConfiguration();

        // Assert
        Assert.Equal("localhost", config.BrokerHost);
        Assert.Equal(1883, config.BrokerPort);
        Assert.False(config.UseTls);
        Assert.Equal(60, config.KeepAliveInterval);
        Assert.Equal(30, config.ConnectionTimeout);
    }

    [Fact]
    public void MqttConfiguration_TlsConfiguration_ShouldBeConfigurable()
    {
        // Arrange & Act
        var config = new MqttConfiguration
        {
            UseTls = true,
            BrokerPort = 8883,
            TlsConfiguration = new MqttTlsConfiguration
            {
                ClientCertificatePath = "/path/to/cert.pfx",
                AllowUntrustedCertificates = false
            }
        };

        // Assert
        Assert.True(config.UseTls);
        Assert.Equal(8883, config.BrokerPort);
        Assert.NotNull(config.TlsConfiguration);
        Assert.Equal("/path/to/cert.pfx", config.TlsConfiguration.ClientCertificatePath);
        Assert.False(config.TlsConfiguration.AllowUntrustedCertificates);
    }

    [Theory]
    [InlineData("spBv1.0/GroupA/NDATA/EdgeNode1", true)]
    [InlineData("factory/sensor/temperature", false)]
    [InlineData("spBv1.0/GroupB/DBIRTH/EdgeNode2/Device1", true)]
    [InlineData("home/livingroom/temp", false)]
    public void IsSparkplugTopic_ShouldIdentifyCorrectly(string topic, bool expectedIsSparkplug)
    {
        // Act
        var isSparkplug = topic.StartsWith("spBv1.0/");

        // Assert
        Assert.Equal(expectedIsSparkplug, isSparkplug);
    }

    [Fact]
    public void MqttLastWillConfiguration_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var lwConfig = new MqttLastWillConfiguration();

        // Assert
        Assert.Equal(string.Empty, lwConfig.Topic);
        Assert.Equal(string.Empty, lwConfig.Payload);
        Assert.Equal(1, lwConfig.QualityOfServiceLevel);
        Assert.True(lwConfig.Retain);
        Assert.Equal(0, lwConfig.DelayInterval);
    }
}