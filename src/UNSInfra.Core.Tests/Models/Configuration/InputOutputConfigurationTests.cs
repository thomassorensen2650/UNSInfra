using FluentAssertions;
using UNSInfra.Models.Configuration;
using Xunit;

namespace UNSInfra.Core.Tests.Models.Configuration;

public class InputOutputConfigurationTests
{
    [Fact]
    public void InputConfiguration_Constructor_ShouldSetTypeToInput()
    {
        // Act
        var config = new TestInputConfiguration();
        
        // Assert
        config.Type.Should().Be(InputOutputType.Input);
    }
    
    [Fact]
    public void OutputConfiguration_Constructor_ShouldSetTypeToOutput()
    {
        // Act
        var config = new TestOutputConfiguration();
        
        // Assert
        config.Type.Should().Be(InputOutputType.Output);
    }
    
    [Fact]
    public void InputOutputConfiguration_DefaultValues_ShouldBeSetCorrectly()
    {
        // Act
        var config = new TestInputConfiguration();
        
        // Assert
        config.Id.Should().Be(string.Empty);
        config.Name.Should().Be(string.Empty);
        config.Description.Should().Be(string.Empty);
        config.IsEnabled.Should().BeTrue();
        config.ServiceType.Should().Be("TestInput");
        config.ConnectionId.Should().BeNull();
        config.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        config.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public void InputOutputConfiguration_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var config = new TestInputConfiguration();
        var testId = "test-id";
        var testName = "Test Name";
        var testDescription = "Test Description";
        var testServiceType = "TestService";
        var testConnectionId = "connection-123";
        var testCreated = DateTime.UtcNow.AddDays(-1);
        var testModified = DateTime.UtcNow.AddHours(-1);
        
        // Act
        config.Id = testId;
        config.Name = testName;
        config.Description = testDescription;
        config.IsEnabled = false;
        config.ServiceType = testServiceType;
        config.ConnectionId = testConnectionId;
        config.CreatedAt = testCreated;
        config.ModifiedAt = testModified;
        
        // Assert
        config.Id.Should().Be(testId);
        config.Name.Should().Be(testName);
        config.Description.Should().Be(testDescription);
        config.IsEnabled.Should().BeFalse();
        config.ServiceType.Should().Be(testServiceType);
        config.ConnectionId.Should().Be(testConnectionId);
        config.CreatedAt.Should().Be(testCreated);
        config.ModifiedAt.Should().Be(testModified);
    }
}

// Test implementations for abstract classes
public class TestInputConfiguration : InputConfiguration
{
    public TestInputConfiguration()
    {
        ServiceType = "TestInput";
    }
}

public class TestOutputConfiguration : OutputConfiguration
{
    public TestOutputConfiguration()
    {
        ServiceType = "TestOutput";
    }
}