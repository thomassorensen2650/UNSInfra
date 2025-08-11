using FluentAssertions;
using UNSInfra.Models.Configuration;
using Xunit;

namespace UNSInfra.Core.Tests.Models.Configuration;

public class MqttOutputConfigurationTests
{
    [Fact]
    public void Constructor_ShouldSetDefaults()
    {
        // Act
        var config = new MqttOutputConfiguration();
        
        // Assert
        config.ServiceType.Should().Be("MQTT");
        config.Type.Should().Be(InputOutputType.Output);
        config.QoS.Should().Be(1);
        config.Retain.Should().BeTrue();
        config.OutputType.Should().Be(MqttOutputType.Data);
        config.TopicPrefix.Should().BeNull();
        config.ModelExportConfig.Should().BeNull();
        config.DataExportConfig.Should().BeNull();
    }
    
    [Fact]
    public void ExportUNSModel_SetToTrue_ShouldUpdateOutputTypeAndCreateConfig()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.ExportUNSModel = true;
        
        // Assert
        config.ExportUNSModel.Should().BeTrue();
        config.OutputType.Should().Be(MqttOutputType.Model);
        config.ModelExportConfig.Should().NotBeNull();
    }
    
    [Fact]
    public void ExportUNSData_SetToTrue_ShouldUpdateOutputTypeAndCreateConfig()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.ExportUNSData = true;
        
        // Assert
        config.ExportUNSData.Should().BeTrue();
        config.OutputType.Should().Be(MqttOutputType.Data);
        config.DataExportConfig.Should().NotBeNull();
    }
    
    [Fact]
    public void ExportUNSModel_And_ExportUNSData_SetToTrue_ShouldSetBothType()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.ExportUNSModel = true;
        config.ExportUNSData = true;
        
        // Assert
        config.ExportUNSModel.Should().BeTrue();
        config.ExportUNSData.Should().BeTrue();
        config.OutputType.Should().Be(MqttOutputType.Both);
        config.ModelExportConfig.Should().NotBeNull();
        config.DataExportConfig.Should().NotBeNull();
    }
    
    [Fact]
    public void ExportUNSModel_GetFromModelType_ShouldReturnTrue()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        config.OutputType = MqttOutputType.Model;
        
        // Act & Assert
        config.ExportUNSModel.Should().BeTrue();
    }
    
    [Fact]
    public void ExportUNSData_GetFromDataType_ShouldReturnTrue()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        config.OutputType = MqttOutputType.Data;
        
        // Act & Assert
        config.ExportUNSData.Should().BeTrue();
    }
    
    [Fact]
    public void ExportUNSModel_And_ExportUNSData_GetFromBothType_ShouldReturnTrue()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        config.OutputType = MqttOutputType.Both;
        
        // Act & Assert
        config.ExportUNSModel.Should().BeTrue();
        config.ExportUNSData.Should().BeTrue();
    }
    
    [Fact]
    public void ModelAttributeName_SetValue_ShouldCreateConfigAndUpdateAttribute()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        var attributeName = "_metadata";
        
        // Act
        config.ModelAttributeName = attributeName;
        
        // Assert
        config.ModelAttributeName.Should().Be(attributeName);
        config.ModelExportConfig.Should().NotBeNull();
        config.ModelExportConfig.ModelAttributeName.Should().Be(attributeName);
    }
    
    [Fact]
    public void ModelAttributeName_GetDefaultValue_ShouldReturn_Model()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act & Assert
        config.ModelAttributeName.Should().Be("_model");
    }
    
    [Fact]
    public void ModelAttributeName_GetFromExistingConfig_ShouldReturnConfigValue()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        config.ModelExportConfig = new MqttModelExportConfiguration
        {
            ModelAttributeName = "_custom"
        };
        
        // Act & Assert
        config.ModelAttributeName.Should().Be("_custom");
    }
    
    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    [InlineData(24, 1440)]
    public void ModelRepublishIntervalHours_SetValue_ShouldUpdateConfigInMinutes(int hours, int expectedMinutes)
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.ModelRepublishIntervalHours = hours;
        
        // Assert
        config.ModelRepublishIntervalHours.Should().Be(hours);
        config.ModelExportConfig.Should().NotBeNull();
        config.ModelExportConfig.RepublishIntervalMinutes.Should().Be(expectedMinutes);
    }
    
    [Fact]
    public void ModelRepublishIntervalHours_GetDefaultValue_ShouldReturnOneHour()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act & Assert
        config.ModelRepublishIntervalHours.Should().Be(1);
    }
    
    [Fact]
    public void ModelRepublishIntervalHours_GetFromExistingConfig_ShouldReturnHoursFromMinutes()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        config.ModelExportConfig = new MqttModelExportConfiguration
        {
            RepublishIntervalMinutes = 180 // 3 hours
        };
        
        // Act & Assert
        config.ModelRepublishIntervalHours.Should().Be(3);
    }
    
    [Fact]
    public void ModelRepublishIntervalHours_SetZeroOrNegative_ShouldSetToOneHour()
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.ModelRepublishIntervalHours = 0;
        
        // Assert
        config.ModelRepublishIntervalHours.Should().Be(1);
        config.ModelExportConfig.RepublishIntervalMinutes.Should().Be(60);
        
        // Act
        config.ModelRepublishIntervalHours = -5;
        
        // Assert
        config.ModelRepublishIntervalHours.Should().Be(1);
        config.ModelExportConfig.RepublishIntervalMinutes.Should().Be(60);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void QoS_SetValue_ShouldUpdateCorrectly(int qos)
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.QoS = qos;
        
        // Assert
        config.QoS.Should().Be(qos);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Retain_SetValue_ShouldUpdateCorrectly(bool retain)
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.Retain = retain;
        
        // Assert
        config.Retain.Should().Be(retain);
    }
    
    [Theory]
    [InlineData("prefix/")]
    [InlineData("spBv1.0/")]
    [InlineData("")]
    public void TopicPrefix_SetValue_ShouldUpdateCorrectly(string prefix)
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.TopicPrefix = prefix;
        
        // Assert
        config.TopicPrefix.Should().Be(prefix);
    }
    
    [Theory]
    [InlineData(MqttOutputType.Model)]
    [InlineData(MqttOutputType.Data)]
    [InlineData(MqttOutputType.Both)]
    public void OutputType_SetValue_ShouldUpdateCorrectly(MqttOutputType outputType)
    {
        // Arrange
        var config = new MqttOutputConfiguration();
        
        // Act
        config.OutputType = outputType;
        
        // Assert
        config.OutputType.Should().Be(outputType);
    }
}