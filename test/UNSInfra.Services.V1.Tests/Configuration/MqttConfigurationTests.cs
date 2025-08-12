using UNSInfra.Services.V1.Configuration;
using Xunit;

namespace UNSInfra.Services.V1.Tests.Configuration;

public class MqttConfigurationTests
{
    [Fact]
    public void MqttConfiguration_DefaultConstructor_ShouldSetDefaultValues()
    {
        // Act
        var config = new MqttConfiguration();

        // Assert
        Assert.Equal("localhost", config.BrokerHost);
        Assert.Equal(1883, config.BrokerPort);
        Assert.False(config.UseTls);
        Assert.Equal(60, config.KeepAliveInterval);
        Assert.Equal(30, config.ConnectionTimeout);
        Assert.Null(config.Username);
        Assert.Null(config.Password);
        Assert.NotNull(config.ClientId); // ClientId has a default value
        Assert.Null(config.TlsConfiguration);
        Assert.True(config.CleanSession);
        Assert.True(config.AutoReconnect);
        Assert.Equal(10, config.MaxReconnectAttempts);
        Assert.Equal(5, config.ReconnectDelay);
        Assert.Equal(1000, config.MessageBufferSize);
        Assert.False(config.EnableDetailedLogging);
    }

    [Fact]
    public void MqttConfiguration_Properties_ShouldBeSettable()
    {
        // Arrange
        var config = new MqttConfiguration();

        // Act
        config.BrokerHost = "mqtt.example.com";
        config.BrokerPort = 8883;
        config.Username = "testuser";
        config.Password = "testpass";
        config.ClientId = "test-client-123";
        config.UseTls = true;
        config.KeepAliveInterval = 120;
        config.ConnectionTimeout = 60;
        config.CleanSession = false;
        config.AutoReconnect = false;
        config.MaxReconnectAttempts = 5;
        config.ReconnectDelay = 10;
        config.MessageBufferSize = 2000;
        config.EnableDetailedLogging = true;

        // Assert
        Assert.Equal("mqtt.example.com", config.BrokerHost);
        Assert.Equal(8883, config.BrokerPort);
        Assert.Equal("testuser", config.Username);
        Assert.Equal("testpass", config.Password);
        Assert.Equal("test-client-123", config.ClientId);
        Assert.True(config.UseTls);
        Assert.Equal(120, config.KeepAliveInterval);
        Assert.Equal(60, config.ConnectionTimeout);
        Assert.False(config.CleanSession);
        Assert.False(config.AutoReconnect);
        Assert.Equal(5, config.MaxReconnectAttempts);
        Assert.Equal(10, config.ReconnectDelay);
        Assert.Equal(2000, config.MessageBufferSize);
        Assert.True(config.EnableDetailedLogging);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MqttConfiguration_BrokerHost_ShouldAcceptEmptyValues(string? host)
    {
        // Arrange
        var config = new MqttConfiguration();

        // Act
        config.BrokerHost = host!;

        // Assert
        Assert.Equal(host, config.BrokerHost);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1883)]
    [InlineData(8883)]
    [InlineData(65535)]
    public void MqttConfiguration_BrokerPort_ShouldAcceptValidPorts(int port)
    {
        // Arrange
        var config = new MqttConfiguration();

        // Act
        config.BrokerPort = port;

        // Assert
        Assert.Equal(port, config.BrokerPort);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void MqttConfiguration_BrokerPort_InvalidValuesShouldBeHandledByImplementation(int port)
    {
        // Arrange
        var config = new MqttConfiguration();

        // Act - Configuration class doesn't validate, but network layer should
        config.BrokerPort = port;

        // Assert
        Assert.Equal(port, config.BrokerPort);
    }

    [Fact]
    public void MqttConfiguration_TlsConfiguration_ShouldWorkTogether()
    {
        // Arrange
        var config = new MqttConfiguration();
        var tlsConfig = new MqttTlsConfiguration
        {
            ClientCertificatePath = "/path/to/certificate.pfx",
            AllowUntrustedCertificates = true
        };

        // Act
        config.UseTls = true;
        config.BrokerPort = 8883;
        config.TlsConfiguration = tlsConfig;

        // Assert
        Assert.True(config.UseTls);
        Assert.Equal(8883, config.BrokerPort);
        Assert.NotNull(config.TlsConfiguration);
        Assert.Equal("/path/to/certificate.pfx", config.TlsConfiguration.ClientCertificatePath);
        Assert.True(config.TlsConfiguration.AllowUntrustedCertificates);
    }

    [Fact]
    public void MqttConfiguration_AuthenticationConfiguration_ShouldWorkTogether()
    {
        // Arrange
        var config = new MqttConfiguration();

        // Act
        config.Username = "admin";
        config.Password = "secret123";

        // Assert
        Assert.Equal("admin", config.Username);
        Assert.Equal("secret123", config.Password);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(300)]
    public void MqttConfiguration_KeepAliveInterval_ShouldAcceptPositiveValues(int interval)
    {
        // Arrange
        var config = new MqttConfiguration();

        // Act
        config.KeepAliveInterval = interval;

        // Assert
        Assert.Equal(interval, config.KeepAliveInterval);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(120)]
    public void MqttConfiguration_ConnectionTimeout_ShouldAcceptPositiveValues(int timeout)
    {
        // Arrange
        var config = new MqttConfiguration();

        // Act
        config.ConnectionTimeout = timeout;

        // Assert
        Assert.Equal(timeout, config.ConnectionTimeout);
    }

    [Fact]
    public void MqttTlsConfiguration_ShouldSetProperties()
    {
        // Arrange & Act
        var tlsConfig = new MqttTlsConfiguration
        {
            ClientCertificatePath = "/path/to/client.pfx",
            ClientCertificatePassword = "password123",
            CaCertificatePath = "/path/to/ca.crt",
            AllowUntrustedCertificates = true,
            IgnoreCertificateChainErrors = true,
            IgnoreCertificateRevocationErrors = true,
            TlsVersion = "1.3"
        };

        // Assert
        Assert.Equal("/path/to/client.pfx", tlsConfig.ClientCertificatePath);
        Assert.Equal("password123", tlsConfig.ClientCertificatePassword);
        Assert.Equal("/path/to/ca.crt", tlsConfig.CaCertificatePath);
        Assert.True(tlsConfig.AllowUntrustedCertificates);
        Assert.True(tlsConfig.IgnoreCertificateChainErrors);
        Assert.True(tlsConfig.IgnoreCertificateRevocationErrors);
        Assert.Equal("1.3", tlsConfig.TlsVersion);
    }

    [Fact]
    public void MqttLastWillConfiguration_ShouldSetProperties()
    {
        // Arrange & Act
        var lwConfig = new MqttLastWillConfiguration
        {
            Topic = "device/status",
            Payload = "offline",
            QualityOfServiceLevel = 2,
            Retain = false,
            DelayInterval = 30
        };

        // Assert
        Assert.Equal("device/status", lwConfig.Topic);
        Assert.Equal("offline", lwConfig.Payload);
        Assert.Equal(2, lwConfig.QualityOfServiceLevel);
        Assert.False(lwConfig.Retain);
        Assert.Equal(30, lwConfig.DelayInterval);
    }
}