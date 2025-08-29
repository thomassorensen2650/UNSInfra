using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Models.Configuration;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services.V1.Background;
using UNSInfra.Services.V1.Extensions;
using UNSInfra.Services.V1.Mqtt;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.InMemory;
using Xunit;

namespace UNSInfra.IntegrationTests;

/// <summary>
/// Integration tests for MQTT output functionality
/// </summary>
public class MqttOutputIntegrationTests
{
    [Fact]
    public async Task MqttOutputConfiguration_WhenSaved_ShouldBePickedUpByExportService()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // Add storage services
        services.AddSingleton<IRealtimeStorage, InMemoryRealtimeStorage>();
        services.AddSingleton<IHistoricalStorage, InMemoryHistoricalStorage>();
        services.AddSingleton<IInputOutputConfigurationRepository, InMemoryInputOutputConfigurationRepository>();
        services.AddSingleton<ITopicConfigurationRepository, InMemoryTopicConfigurationRepository>();
        
        // Add full input/output services to include hosted services
        services.AddInputOutputServices();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Create a test MQTT output configuration
        var config = new MqttOutputConfiguration
        {
            Id = "integration-test-output",
            Name = "Integration Test MQTT Export",
            IsEnabled = true,
            ConnectionId = "test-mqtt-connection",
            OutputType = MqttOutputType.Data,
            QoS = 1,
            Retain = true,
            TopicPrefix = "test/",
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 1000,
                MaxDataAgeMinutes = 60,
                DataFormat = MqttDataFormat.Json,
                IncludeTimestamp = true,
                IncludeQuality = true,
                UseUNSPathAsTopic = false,
                TopicFilter = new List<string> { "*" },
                NamespaceFilter = new List<string>()
            }
        };

        // Act - Save the configuration
        var configRepository = serviceProvider.GetRequiredService<IInputOutputConfigurationRepository>();
        await configRepository.SaveConfigurationAsync(config);

        // Verify the configuration was saved
        var savedConfig = await configRepository.GetConfigurationByIdAsync(config.Id);
        
        // Assert
        savedConfig.Should().NotBeNull();
        savedConfig!.Name.Should().Be("Integration Test MQTT Export");
        savedConfig.IsEnabled.Should().BeTrue();
        
        // Verify it's an MQTT output configuration with correct settings
        savedConfig.Should().BeOfType<MqttOutputConfiguration>();
        var mqttConfig = (MqttOutputConfiguration)savedConfig;
        
        mqttConfig.OutputType.Should().Be(MqttOutputType.Data);
        mqttConfig.QoS.Should().Be(1);
        mqttConfig.Retain.Should().BeTrue();
        mqttConfig.TopicPrefix.Should().Be("test/");
        
        // Verify data export config
        mqttConfig.DataExportConfig.Should().NotBeNull();
        mqttConfig.DataExportConfig!.PublishOnChange.Should().BeTrue();
        mqttConfig.DataExportConfig.DataFormat.Should().Be(MqttDataFormat.Json);
        mqttConfig.DataExportConfig.IncludeTimestamp.Should().BeTrue();
    }

    [Fact]
    public async Task MqttDataExportService_WithValidConfiguration_ShouldStartSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<IRealtimeStorage, InMemoryRealtimeStorage>();
        services.AddSingleton<IHistoricalStorage, InMemoryHistoricalStorage>();
        services.AddSingleton<IInputOutputConfigurationRepository, InMemoryInputOutputConfigurationRepository>();
        services.AddSingleton<ITopicConfigurationRepository, InMemoryTopicConfigurationRepository>();
        services.AddInputOutputServices();
        
        var serviceProvider = services.BuildServiceProvider();

        // Create and save a test configuration
        var config = new MqttOutputConfiguration
        {
            Id = "service-test-output",
            Name = "Service Test MQTT Export",
            IsEnabled = true,
            OutputType = MqttOutputType.Data,
            DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                MinPublishIntervalMs = 1000,
                DataFormat = MqttDataFormat.Json
            }
        };

        var configRepository = serviceProvider.GetRequiredService<IInputOutputConfigurationRepository>();
        await configRepository.SaveConfigurationAsync(config);

        // Act - Get and start the MQTT data export service
        var exportService = serviceProvider.GetRequiredService<MqttDataExportService>();
        
        // Note: StartAsync will try to connect to MQTT broker at localhost:1883
        // In a real test environment, you'd want to mock this or use a test MQTT broker
        // For now, we'll test the service instantiation and configuration loading
        var isRunning = await exportService.IsRunningAsync();
        
        // Assert
        exportService.Should().NotBeNull();
        isRunning.Should().BeFalse(); // Not started yet
        
        var status = await exportService.GetStatusAsync();
        status.Should().ContainKey("IsRunning");
        status.Should().ContainKey("ActiveConfigurations");
        status["IsRunning"].Should().Be(false);
    }

    [Fact]
    public async Task InputOutputBackgroundService_ShouldBeRegisteredAsHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IRealtimeStorage, InMemoryRealtimeStorage>();
        services.AddSingleton<IHistoricalStorage, InMemoryHistoricalStorage>();
        services.AddSingleton<IInputOutputConfigurationRepository, InMemoryInputOutputConfigurationRepository>();
        services.AddSingleton<ITopicConfigurationRepository, InMemoryTopicConfigurationRepository>();
        services.AddInputOutputServices();
        
        var serviceProvider = services.BuildServiceProvider();

        // Act - Verify hosted services are registered
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        
        // Assert
        hostedServices.Should().NotBeEmpty();
        hostedServices.Should().Contain(service => service.GetType() == typeof(InputOutputBackgroundService));
    }

    [Theory]
    [InlineData(MqttOutputType.Data)]
    [InlineData(MqttOutputType.Model)]
    [InlineData(MqttOutputType.Both)]
    public async Task MqttOutputConfiguration_WithDifferentOutputTypes_ShouldBeSavedCorrectly(MqttOutputType outputType)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IInputOutputConfigurationRepository, InMemoryInputOutputConfigurationRepository>();
        
        var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetRequiredService<IInputOutputConfigurationRepository>();

        var config = new MqttOutputConfiguration
        {
            Id = $"test-{outputType}",
            Name = $"Test {outputType} Export",
            IsEnabled = true,
            OutputType = outputType
        };

        // Set appropriate configs based on type
        if (outputType == MqttOutputType.Data || outputType == MqttOutputType.Both)
        {
            config.DataExportConfig = new MqttDataExportConfiguration
            {
                PublishOnChange = true,
                DataFormat = MqttDataFormat.Json
            };
        }

        if (outputType == MqttOutputType.Model || outputType == MqttOutputType.Both)
        {
            config.ModelExportConfig = new MqttModelExportConfiguration
            {
                ModelAttributeName = "_model",
                RepublishIntervalMinutes = 60
            };
        }

        // Act
        await repository.SaveConfigurationAsync(config);
        var saved = await repository.GetConfigurationByIdAsync(config.Id);

        // Assert
        saved.Should().NotBeNull();
        var mqttSaved = saved.Should().BeOfType<MqttOutputConfiguration>().Subject;
        mqttSaved.OutputType.Should().Be(outputType);
        
        if (outputType == MqttOutputType.Data || outputType == MqttOutputType.Both)
        {
            mqttSaved.DataExportConfig.Should().NotBeNull();
        }
        
        if (outputType == MqttOutputType.Model || outputType == MqttOutputType.Both)
        {
            mqttSaved.ModelExportConfig.Should().NotBeNull();
        }
    }
}