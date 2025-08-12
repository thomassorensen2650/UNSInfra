using FluentAssertions;
using UNSInfra.Models.Configuration;
using Xunit;

namespace UNSInfra.Core.Tests.Models.Configuration;

public class SocketIOInputConfigurationTests
{
    [Fact]
    public void Constructor_ShouldSetDefaults()
    {
        // Act
        var config = new SocketIOInputConfiguration();
        
        // Assert
        config.ServiceType.Should().Be("SocketIO");
        config.Type.Should().Be(InputOutputType.Input);
        config.EventNames.Should().NotBeNull().And.BeEmpty();
        config.AutoMapToUNS.Should().BeTrue();
        config.HierarchyPathMappings.Should().NotBeNull().And.BeEmpty();
        config.DataValuePathMapping.Should().Be("$.value");
        config.DefaultNamespace.Should().BeNull();
        config.TopicPathMapping.Should().BeNull();
    }
    
    [Fact]
    public void EventNamesString_SetValue_ShouldParseCommaSeparatedValues()
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        var eventNames = "updated, data, status, sensor_reading";
        
        // Act
        config.EventNamesString = eventNames;
        
        // Assert
        config.EventNames.Should().HaveCount(4);
        config.EventNames.Should().Contain("updated");
        config.EventNames.Should().Contain("data");
        config.EventNames.Should().Contain("status");
        config.EventNames.Should().Contain("sensor_reading");
    }
    
    [Fact]
    public void EventNamesString_GetValue_ShouldReturnCommaSeparatedString()
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        config.EventNames.AddRange(new[] { "updated", "data", "status" });
        
        // Act
        var result = config.EventNamesString;
        
        // Assert
        result.Should().Be("updated, data, status");
    }
    
    [Fact]
    public void EventNamesString_SetEmptyValue_ShouldClearEventNames()
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        config.EventNames.AddRange(new[] { "updated", "data" });
        
        // Act
        config.EventNamesString = "";
        
        // Assert
        config.EventNames.Should().BeEmpty();
    }
    
    [Fact]
    public void EventNamesString_SetNullValue_ShouldClearEventNames()
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        config.EventNames.AddRange(new[] { "updated", "data" });
        
        // Act
        config.EventNamesString = null;
        
        // Assert
        config.EventNames.Should().BeEmpty();
    }
    
    [Fact]
    public void JsonPathMappingsString_SetValue_ShouldParseLineSeparatedMappings()
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        var mappings = "$.data -> /value\n$.timestamp -> /ts\n$.location.site -> Site";
        
        // Act
        config.JsonPathMappingsString = mappings;
        
        // Assert
        config.HierarchyPathMappings.Should().HaveCount(3);
        config.HierarchyPathMappings.Should().ContainKey("/value").WhoseValue.Should().Be("$.data");
        config.HierarchyPathMappings.Should().ContainKey("/ts").WhoseValue.Should().Be("$.timestamp");
        config.HierarchyPathMappings.Should().ContainKey("Site").WhoseValue.Should().Be("$.location.site");
    }
    
    [Fact]
    public void JsonPathMappingsString_GetValue_ShouldReturnFormattedMappings()
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        config.HierarchyPathMappings["/value"] = "$.data";
        config.HierarchyPathMappings["/ts"] = "$.timestamp";
        
        // Act
        var result = config.JsonPathMappingsString;
        
        // Assert
        result.Should().Contain("$.data -> /value");
        result.Should().Contain("$.timestamp -> /ts");
    }
    
    [Fact]
    public void JsonPathMappingsString_SetEmptyValue_ShouldClearMappings()
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        config.HierarchyPathMappings["/value"] = "$.data";
        
        // Act
        config.JsonPathMappingsString = "";
        
        // Assert
        config.HierarchyPathMappings.Should().BeEmpty();
    }
    
    [Fact]
    public void JsonPathMappingsString_SetInvalidFormat_ShouldIgnoreInvalidLines()
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        var mappings = "$.data -> /value\ninvalid line\n$.timestamp -> /ts";
        
        // Act
        config.JsonPathMappingsString = mappings;
        
        // Assert
        config.HierarchyPathMappings.Should().HaveCount(2);
        config.HierarchyPathMappings.Should().ContainKey("/value");
        config.HierarchyPathMappings.Should().ContainKey("/ts");
    }
    
    [Theory]
    [InlineData("$.value")]
    [InlineData("$.data")]
    [InlineData("$.payload.value")]
    public void DataValuePathMapping_SetValue_ShouldUpdateCorrectly(string path)
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        
        // Act
        config.DataValuePathMapping = path;
        
        // Assert
        config.DataValuePathMapping.Should().Be(path);
    }
    
    [Theory]
    [InlineData("$.id")]
    [InlineData("$.deviceId")]
    [InlineData("$.topic")]
    public void TopicPathMapping_SetValue_ShouldUpdateCorrectly(string path)
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        
        // Act
        config.TopicPathMapping = path;
        
        // Assert
        config.TopicPathMapping.Should().Be(path);
    }
    
    [Theory]
    [InlineData("Manufacturing")]
    [InlineData("Operations")]
    [InlineData("Quality")]
    public void DefaultNamespace_SetValue_ShouldUpdateCorrectly(string defaultNamespace)
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        
        // Act
        config.DefaultNamespace = defaultNamespace;
        
        // Assert
        config.DefaultNamespace.Should().Be(defaultNamespace);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AutoMapToUNS_SetValue_ShouldUpdateCorrectly(bool autoMap)
    {
        // Arrange
        var config = new SocketIOInputConfiguration();
        
        // Act
        config.AutoMapToUNS = autoMap;
        
        // Assert
        config.AutoMapToUNS.Should().Be(autoMap);
    }
}