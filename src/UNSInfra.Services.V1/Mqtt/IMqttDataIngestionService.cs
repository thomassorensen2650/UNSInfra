using MQTTnet;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services.DataIngestion.Mock;

namespace UNSInfra.Services.V1.Mqtt;

/// <summary>
/// Enhanced MQTT data service that supports both ingestion (subscribing) and publishing
/// </summary>
public interface IMqttDataIngestionService : IMqttDataService
{
    /// <summary>
    /// Publishes a message to the specified MQTT topic
    /// </summary>
    /// <param name="topic">The MQTT topic to publish to</param>
    /// <param name="payload">The message payload</param>
    /// <param name="qos">Quality of Service level (0, 1, or 2)</param>
    /// <param name="retain">Whether to retain the message</param>
    /// <returns>True if published successfully</returns>
    Task<bool> PublishAsync(string topic, byte[] payload, int qos = 1, bool retain = false);

    /// <summary>
    /// Publishes a data point as an MQTT message
    /// </summary>
    /// <param name="dataPoint">The data point to publish</param>
    /// <param name="topic">Optional custom topic (uses dataPoint.Topic if not provided)</param>
    /// <param name="qos">Quality of Service level</param>
    /// <param name="retain">Whether to retain the message</param>
    /// <returns>True if published successfully</returns>
    Task<bool> PublishDataPointAsync(DataPoint dataPoint, string? topic = null, int qos = 1, bool retain = false);

    /// <summary>
    /// Publishes an MQTT application message directly
    /// </summary>
    /// <param name="message">The MQTT message to publish</param>
    /// <returns>True if published successfully</returns>
    Task<bool> PublishMessageAsync(MqttApplicationMessage message);

    /// <summary>
    /// Gets the current MQTT connection status
    /// </summary>
    /// <returns>True if connected to MQTT broker</returns>
    Task<bool> IsConnectedAsync();
}