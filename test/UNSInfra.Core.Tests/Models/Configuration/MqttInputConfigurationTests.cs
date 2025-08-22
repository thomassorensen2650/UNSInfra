using FluentAssertions;
using UNSInfra.Models.Configuration;
using Xunit;

namespace UNSInfra.Core.Tests.Models.Configuration;

// TODO: Rewrite tests for ConnectionSDK MQTT system
/* MQTT input configuration tests moved to ConnectionSDK system
public class MqttInputConfigurationTests
{
    [Fact]
    public void Constructor_ShouldSetDefaults()
    {
        // Act
        var config = new MqttInputConfiguration();
        
        // Assert
        config.ServiceType.Should().Be("MQTT");
        config.Type.Should().Be(InputOutputType.Input);
        config.TopicFilter.Should().Be(string.Empty);
        config.QoS.Should().Be(1);
        config.AutoMapTopicToUNS.Should().BeTrue();
        config.UseSparkplugB.Should().BeFalse();
        config.RetainLastKnownValue.Should().BeTrue();
        config.TopicPattern.Should().BeNull();
        config.DefaultNamespace.Should().BeNull();
        config.TopicPrefix.Should().BeNull();
    }
    
    [Theory]
    [InlineData("#", "Wildcard all topics")]
    [InlineData("Enterprise1/+/Line1", "Single level wildcard")]
    [InlineData("sensors/temperature/room1", "Specific topic")]
    [InlineData("spBv1.0/group1/NDATA/edge1/device1", "Sparkplug B topic")]
    public void TopicFilter_SetValue_ShouldUpdateCorrectly(string topicFilter, string description)
    {
        // Arrange
        var config = new MqttInputConfiguration();
        
        // Act
        config.TopicFilter = topicFilter;
        
        // Assert
        config.TopicFilter.Should().Be(topicFilter, description);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void QoS_SetValue_ShouldUpdateCorrectly(int qos)
    {
        // Arrange
        var config = new MqttInputConfiguration();
        
        // Act
        config.QoS = qos;
        
        // Assert
        config.QoS.Should().Be(qos);
    }
    
    [Fact]
    public void UseSparkplugB_SetToTrue_ShouldEnableSparkplugProtocol()
    {
        // Arrange
        var config = new MqttInputConfiguration();
        
        // Act
        config.UseSparkplugB = true;
        
        // Assert
        config.UseSparkplugB.Should().BeTrue();
    }
    
    [Fact]
    public void TopicPattern_SetValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var config = new MqttInputConfiguration();
        var pattern = "{Enterprise}/{Site}/{Area}";
        
        // Act
        config.TopicPattern = pattern;
        
        // Assert
        config.TopicPattern.Should().Be(pattern);
    }
    
    [Fact]
    public void DefaultNamespace_SetValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var config = new MqttInputConfiguration();
        var defaultNamespace = "Manufacturing";
        
        // Act
        config.DefaultNamespace = defaultNamespace;
        
        // Assert
        config.DefaultNamespace.Should().Be(defaultNamespace);
    }
    
    [Fact]
    public void TopicPrefix_SetValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var config = new MqttInputConfiguration();
        var prefix = "spBv1.0";
        
        // Act
        config.TopicPrefix = prefix;
        
        // Assert
        config.TopicPrefix.Should().Be(prefix);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AutoMapTopicToUNS_SetValue_ShouldUpdateCorrectly(bool autoMap)
    {
        // Arrange
        var config = new MqttInputConfiguration();
        
        // Act
        config.AutoMapTopicToUNS = autoMap;
        
        // Assert
        config.AutoMapTopicToUNS.Should().Be(autoMap);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RetainLastKnownValue_SetValue_ShouldUpdateCorrectly(bool retain)
    {
        // Arrange
        var config = new MqttInputConfiguration();
        
        // Act
        config.RetainLastKnownValue = retain;
        
        // Assert
        config.RetainLastKnownValue.Should().Be(retain);
    }
}
*/