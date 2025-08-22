using FluentAssertions;
using UNSInfra.Core.Repositories;
using UNSInfra.Repositories;
using UNSInfra.Models.Configuration;
using Xunit;

namespace UNSInfra.Core.Tests.Repositories;

// TODO: Rewrite tests for ConnectionSDK MQTT system
/* MQTT input/output configuration tests moved to ConnectionSDK system
public class InMemoryInputOutputConfigurationRepositoryTests
{
    private InMemoryInputOutputConfigurationRepository CreateRepository()
    {
        return new InMemoryInputOutputConfigurationRepository();
    }
    
    private MqttInputConfiguration CreateTestMqttInput(string id = "test-mqtt-input", string connectionId = "conn-1")
    {
        return new MqttInputConfiguration
        {
            Id = id,
            Name = "Test MQTT Input",
            Description = "Test Description",
            ConnectionId = connectionId,
            IsEnabled = true,
            TopicFilter = "#"
        };
    }
    
    private SocketIOInputConfiguration CreateTestSocketIOInput(string id = "test-socketio-input", string connectionId = "conn-2")
    {
        return new SocketIOInputConfiguration
        {
            Id = id,
            Name = "Test SocketIO Input",
            Description = "Test Description",
            ConnectionId = connectionId,
            IsEnabled = true,
            EventNamesString = "updated,data"
        };
    }
    
    private MqttOutputConfiguration CreateTestMqttOutput(string id = "test-mqtt-output", string connectionId = "conn-1")
    {
        return new MqttOutputConfiguration
        {
            Id = id,
            Name = "Test MQTT Output",
            Description = "Test Description",
            ConnectionId = connectionId,
            IsEnabled = true,
            ExportUNSModel = true
        };
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_NewConfiguration_ShouldAddToRepository()
    {
        // Arrange
        var repository = CreateRepository();
        var config = CreateTestMqttInput();
        
        // Act
        await repository.SaveConfigurationAsync(config);
        
        // Assert
        var result = await repository.GetConfigurationByIdAsync(config.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(config.Id);
        result.Name.Should().Be(config.Name);
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_ExistingConfiguration_ShouldUpdateInRepository()
    {
        // Arrange
        var repository = CreateRepository();
        var config = CreateTestMqttInput();
        await repository.SaveConfigurationAsync(config);
        
        // Act
        config.Name = "Updated Name";
        config.Description = "Updated Description";
        await repository.SaveConfigurationAsync(config);
        
        // Assert
        var result = await repository.GetConfigurationByIdAsync(config.Id);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
    }
    
    [Fact]
    public async Task GetConfigurationByIdAsync_ExistingId_ShouldReturnConfiguration()
    {
        // Arrange
        var repository = CreateRepository();
        var config = CreateTestMqttInput();
        await repository.SaveConfigurationAsync(config);
        
        // Act
        var result = await repository.GetConfigurationByIdAsync(config.Id);
        
        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(config.Id);
    }
    
    [Fact]
    public async Task GetConfigurationByIdAsync_NonExistingId_ShouldReturnNull()
    {
        // Arrange
        var repository = CreateRepository();
        
        // Act
        var result = await repository.GetConfigurationByIdAsync("non-existing-id");
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task DeleteConfigurationAsync_ExistingConfiguration_ShouldRemoveFromRepository()
    {
        // Arrange
        var repository = CreateRepository();
        var config = CreateTestMqttInput();
        await repository.SaveConfigurationAsync(config);
        
        // Act
        var deleteResult = await repository.DeleteConfigurationAsync(config.Id);
        
        // Assert
        deleteResult.Should().BeTrue();
        var getResult = await repository.GetConfigurationByIdAsync(config.Id);
        getResult.Should().BeNull();
    }
    
    [Fact]
    public async Task DeleteConfigurationAsync_NonExistingConfiguration_ShouldReturnFalse()
    {
        // Arrange
        var repository = CreateRepository();
        
        // Act
        var result = await repository.DeleteConfigurationAsync("non-existing-id");
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task GetAllConfigurationsAsync_NoFilters_ShouldReturnAllConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var mqttInput = CreateTestMqttInput("mqtt-input", "conn-1");
        var socketInput = CreateTestSocketIOInput("socket-input", "conn-2");
        var mqttOutput = CreateTestMqttOutput("mqtt-output", "conn-1");
        
        await repository.SaveConfigurationAsync(mqttInput);
        await repository.SaveConfigurationAsync(socketInput);
        await repository.SaveConfigurationAsync(mqttOutput);
        
        // Act
        var result = await repository.GetAllConfigurationsAsync();
        
        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(c => c.Id == "mqtt-input");
        result.Should().Contain(c => c.Id == "socket-input");
        result.Should().Contain(c => c.Id == "mqtt-output");
    }
    
    [Fact]
    public async Task GetAllConfigurationsAsync_FilterByConnectionId_ShouldReturnMatchingConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var mqttInput = CreateTestMqttInput("mqtt-input", "conn-1");
        var socketInput = CreateTestSocketIOInput("socket-input", "conn-2");
        var mqttOutput = CreateTestMqttOutput("mqtt-output", "conn-1");
        
        await repository.SaveConfigurationAsync(mqttInput);
        await repository.SaveConfigurationAsync(socketInput);
        await repository.SaveConfigurationAsync(mqttOutput);
        
        // Act
        var result = await repository.GetConfigurationsByConnectionIdAsync("conn-1");
        
        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Id == "mqtt-input");
        result.Should().Contain(c => c.Id == "mqtt-output");
        result.Should().NotContain(c => c.Id == "socket-input");
    }
    
    [Fact]
    public async Task GetAllConfigurationsAsync_FilterByType_ShouldReturnMatchingConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var mqttInput = CreateTestMqttInput("mqtt-input", "conn-1");
        var socketInput = CreateTestSocketIOInput("socket-input", "conn-2");
        var mqttOutput = CreateTestMqttOutput("mqtt-output", "conn-1");
        
        await repository.SaveConfigurationAsync(mqttInput);
        await repository.SaveConfigurationAsync(socketInput);
        await repository.SaveConfigurationAsync(mqttOutput);
        
        // Act
        var inputResult = await repository.GetAllConfigurationsAsync(null, InputOutputType.Input);
        var outputResult = await repository.GetAllConfigurationsAsync(null, InputOutputType.Output);
        
        // Assert
        inputResult.Should().HaveCount(2);
        inputResult.Should().Contain(c => c.Id == "mqtt-input");
        inputResult.Should().Contain(c => c.Id == "socket-input");
        
        outputResult.Should().HaveCount(1);
        outputResult.Should().Contain(c => c.Id == "mqtt-output");
    }
    
    [Fact]
    public async Task GetAllConfigurationsAsync_FilterByEnabledOnly_ShouldReturnOnlyEnabledConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var enabledConfig = CreateTestMqttInput("enabled", "conn-1");
        var disabledConfig = CreateTestMqttInput("disabled", "conn-1");
        disabledConfig.IsEnabled = false;
        
        await repository.SaveConfigurationAsync(enabledConfig);
        await repository.SaveConfigurationAsync(disabledConfig);
        
        // Act
        var allResult = await repository.GetAllConfigurationsAsync(enabledOnly: false);
        var enabledResult = await repository.GetAllConfigurationsAsync(enabledOnly: true);
        
        // Assert
        allResult.Should().HaveCount(2);
        enabledResult.Should().HaveCount(1);
        enabledResult.Should().Contain(c => c.Id == "enabled");
    }
    
    [Fact]
    public async Task GetInputConfigurationsAsync_ShouldReturnOnlyInputConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var mqttInput = CreateTestMqttInput("mqtt-input", "conn-1");
        var socketInput = CreateTestSocketIOInput("socket-input", "conn-1");
        var mqttOutput = CreateTestMqttOutput("mqtt-output", "conn-1");
        
        await repository.SaveConfigurationAsync(mqttInput);
        await repository.SaveConfigurationAsync(socketInput);
        await repository.SaveConfigurationAsync(mqttOutput);
        
        // Act
        var mqttResult = await repository.GetInputConfigurationsAsync("MQTT", false);
        var socketResult = await repository.GetInputConfigurationsAsync("SocketIO", false);
        
        // Assert
        mqttResult.Should().HaveCount(1);
        mqttResult.Should().Contain(c => c.Id == "mqtt-input");
        
        socketResult.Should().HaveCount(1);
        socketResult.Should().Contain(c => c.Id == "socket-input");
    }
    
    [Fact]
    public async Task GetOutputConfigurationsAsync_ShouldReturnOnlyOutputConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var mqttInput = CreateTestMqttInput("mqtt-input", "conn-1");
        var mqttOutput = CreateTestMqttOutput("mqtt-output", "conn-1");
        
        await repository.SaveConfigurationAsync(mqttInput);
        await repository.SaveConfigurationAsync(mqttOutput);
        
        // Act
        var result = await repository.GetOutputConfigurationsAsync("MQTT", false);
        
        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(c => c.Id == "mqtt-output");
        result.Should().NotContain(c => c.Id == "mqtt-input");
    }
    
    [Fact]
    public async Task GetSocketIOInputConfigurationsAsync_ShouldReturnOnlySocketIOInputConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var mqttInput = CreateTestMqttInput("mqtt-input", "conn-1");
        var socketInput = CreateTestSocketIOInput("socket-input", "conn-1");
        
        await repository.SaveConfigurationAsync(mqttInput);
        await repository.SaveConfigurationAsync(socketInput);
        
        // Act
        var result = await repository.GetSocketIOInputConfigurationsAsync();
        
        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(c => c.Id == "socket-input");
        result.Should().NotContain(c => c.Id == "mqtt-input");
        result.First().Should().BeOfType<SocketIOInputConfiguration>();
    }
    
    [Fact]
    public async Task GetMqttInputConfigurationsAsync_ShouldReturnOnlyMqttInputConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var mqttInput = CreateTestMqttInput("mqtt-input", "conn-1");
        var socketInput = CreateTestSocketIOInput("socket-input", "conn-1");
        
        await repository.SaveConfigurationAsync(mqttInput);
        await repository.SaveConfigurationAsync(socketInput);
        
        // Act
        var result = await repository.GetMqttInputConfigurationsAsync();
        
        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(c => c.Id == "mqtt-input");
        result.Should().NotContain(c => c.Id == "socket-input");
        result.First().Should().BeOfType<MqttInputConfiguration>();
    }
    
    [Fact]
    public async Task GetMqttOutputConfigurationsAsync_ShouldReturnOnlyMqttOutputConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var mqttInput = CreateTestMqttInput("mqtt-input", "conn-1");
        var mqttOutput = CreateTestMqttOutput("mqtt-output", "conn-1");
        
        await repository.SaveConfigurationAsync(mqttInput);
        await repository.SaveConfigurationAsync(mqttOutput);
        
        // Act
        var result = await repository.GetMqttOutputConfigurationsAsync();
        
        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(c => c.Id == "mqtt-output");
        result.Should().NotContain(c => c.Id == "mqtt-input");
        result.First().Should().BeOfType<MqttOutputConfiguration>();
    }
    
    [Fact]
    public async Task SetConfigurationEnabledAsync_ExistingConfiguration_ShouldUpdateEnabledStatus()
    {
        // Arrange
        var repository = CreateRepository();
        var config = CreateTestMqttInput();
        await repository.SaveConfigurationAsync(config);
        
        // Act
        var result = await repository.SetConfigurationEnabledAsync(config.Id, false);
        
        // Assert
        result.Should().BeTrue();
        var updatedConfig = await repository.GetConfigurationByIdAsync(config.Id);
        updatedConfig!.IsEnabled.Should().BeFalse();
    }
    
    [Fact]
    public async Task SetConfigurationEnabledAsync_NonExistingConfiguration_ShouldReturnFalse()
    {
        // Arrange
        var repository = CreateRepository();
        
        // Act
        var result = await repository.SetConfigurationEnabledAsync("non-existing", false);
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task GetActiveConfigurationsAsync_ShouldReturnOnlyEnabledConfigurations()
    {
        // Arrange
        var repository = CreateRepository();
        var enabledConfig = CreateTestMqttInput("enabled", "conn-1");
        var disabledConfig = CreateTestMqttInput("disabled", "conn-1");
        disabledConfig.IsEnabled = false;
        
        await repository.SaveConfigurationAsync(enabledConfig);
        await repository.SaveConfigurationAsync(disabledConfig);
        
        // Act
        var result = await repository.GetActiveConfigurationsAsync();
        
        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(c => c.Id == "enabled");
        result.Should().NotContain(c => c.Id == "disabled");
    }
}
*/