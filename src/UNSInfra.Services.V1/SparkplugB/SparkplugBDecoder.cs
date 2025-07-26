using Google.Protobuf;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Services.V1.SparkplugB;

/// <summary>
/// Decoder for Sparkplug B protocol messages.
/// Supports NBIRTH, NDATA, DBIRTH, DDATA, and NDEATH message types.
/// </summary>
public class SparkplugBDecoder
{
    private readonly ILogger<SparkplugBDecoder> _logger;

    public SparkplugBDecoder(ILogger<SparkplugBDecoder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Decodes a Sparkplug B message from the provided payload.
    /// </summary>
    /// <param name="topic">The MQTT topic the message was received on</param>
    /// <param name="payload">The binary payload containing the Sparkplug B message</param>
    /// <returns>A collection of data points extracted from the message</returns>
    public IEnumerable<DataPoint> DecodeMessage(string topic, byte[] payload)
    {
        try
        {
            var sparkplugPayload = SparkplugBPayload.Parser.ParseFrom(payload);
            var topicParts = ParseSparkplugTopic(topic);
            
            if (topicParts == null)
            {
                _logger.LogWarning("Invalid Sparkplug B topic format: {Topic}", topic);
                return Enumerable.Empty<DataPoint>();
            }

            return ExtractDataPoints(sparkplugPayload, topicParts);
        }
        catch (InvalidProtocolBufferException ex)
        {
            _logger.LogError(ex, "Failed to parse Sparkplug B payload for topic: {Topic}", topic);
            return Enumerable.Empty<DataPoint>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error decoding Sparkplug B message for topic: {Topic}", topic);
            return Enumerable.Empty<DataPoint>();
        }
    }

    /// <summary>
    /// Parses a Sparkplug B topic to extract namespace, group ID, message type, edge node ID, and device ID.
    /// </summary>
    /// <param name="topic">The MQTT topic to parse</param>
    /// <returns>Parsed topic components or null if invalid</returns>
    private SparkplugTopicParts? ParseSparkplugTopic(string topic)
    {
        var parts = topic.Split('/');
        
        if (parts.Length < 4)
            return null;

        var result = new SparkplugTopicParts
        {
            Namespace = parts[0],
            GroupId = parts[1],
            MessageType = parts[2],
            EdgeNodeId = parts[3]
        };

        if (parts.Length > 4)
        {
            result.DeviceId = parts[4];
        }

        // Validate that this is a Sparkplug B topic
        if (result.Namespace != "spBv1.0")
            return null;

        if (!IsValidMessageType(result.MessageType))
            return null;

        return result;
    }

    /// <summary>
    /// Validates if the message type is a supported Sparkplug B message type.
    /// </summary>
    private bool IsValidMessageType(string messageType)
    {
        return messageType switch
        {
            "NBIRTH" or "NDATA" or "NDEATH" or "DBIRTH" or "DDATA" or "DDEATH" => true,
            _ => false
        };
    }

    /// <summary>
    /// Extracts data points from a Sparkplug B payload.
    /// </summary>
    private IEnumerable<DataPoint> ExtractDataPoints(SparkplugBPayload payload, SparkplugTopicParts topicParts)
    {
        var dataPoints = new List<DataPoint>();

        foreach (var metric in payload.Metrics)
        {
            var hierarchicalPath = CreateHierarchicalPath(topicParts, metric.Name);
            var value = ExtractMetricValue(metric);
            var topic = ConstructTopic(topicParts, metric.Name);

            if (value != null)
            {
                dataPoints.Add(new DataPoint
                {
                    Topic = topic,
                    Path = hierarchicalPath,
                    Value = value,
                    Source = "SparkplugB",
                    Timestamp = payload.Timestamp > 0 
                        ? DateTimeOffset.FromUnixTimeMilliseconds((long)payload.Timestamp).DateTime
                        : DateTime.UtcNow,
                    Metadata = CreateMetricMetadata(metric, topicParts)
                });
            }
        }

        return dataPoints;
    }

    /// <summary>
    /// Creates a hierarchical path from Sparkplug B topic components and metric name.
    /// </summary>
    private HierarchicalPath CreateHierarchicalPath(SparkplugTopicParts topicParts, string metricName)
    {
        return new HierarchicalPath
        {
            Enterprise = topicParts.GroupId,
            Site = topicParts.EdgeNodeId,
            Area = topicParts.DeviceId ?? "Node",
            WorkCenter = metricName,
            WorkUnit = string.Empty,
            Property = string.Empty
        };
    }

    /// <summary>
    /// Constructs the topic string from Sparkplug B components.
    /// </summary>
    private string ConstructTopic(SparkplugTopicParts topicParts, string metricName)
    {
        var baseTopic = $"{topicParts.GroupId}/{topicParts.EdgeNodeId}";
        
        if (!string.IsNullOrEmpty(topicParts.DeviceId))
        {
            baseTopic += $"/{topicParts.DeviceId}";
        }

        return $"{baseTopic}/{metricName}";
    }

    /// <summary>
    /// Extracts the value from a Sparkplug B metric based on its data type.
    /// </summary>
    private object? ExtractMetricValue(SparkplugBPayload.Types.Metric metric)
    {
        return metric.Datatype switch
        {
            1 => metric.IntValue,      // Int8
            2 => metric.IntValue,      // Int16
            3 => metric.IntValue,      // Int32
            4 => metric.LongValue,     // Int64
            5 => metric.IntValue,      // UInt8
            6 => metric.IntValue,      // UInt16
            7 => metric.IntValue,      // UInt32
            8 => metric.LongValue,     // UInt64
            9 => metric.FloatValue,    // Float
            10 => metric.DoubleValue,  // Double
            11 => metric.BooleanValue, // Boolean
            12 => metric.StringValue,  // String
            13 => metric.BytesValue?.ToByteArray(), // Bytes
            14 => metric.StringValue,  // File
            15 => CreateDataSet(metric.DatasetValue), // DataSet
            16 => CreateTemplate(metric.TemplateValue), // Template
            _ => metric.StringValue
        };
    }

    /// <summary>
    /// Creates metadata dictionary from metric properties.
    /// </summary>
    private Dictionary<string, object> CreateMetricMetadata(SparkplugBPayload.Types.Metric metric, SparkplugTopicParts topicParts)
    {
        var metadata = new Dictionary<string, object>
        {
            ["sparkplug_datatype"] = metric.Datatype,
            ["sparkplug_group"] = topicParts.GroupId,
            ["sparkplug_edge_node"] = topicParts.EdgeNodeId,
            ["sparkplug_message_type"] = topicParts.MessageType
        };

        if (!string.IsNullOrEmpty(topicParts.DeviceId))
        {
            metadata["sparkplug_device"] = topicParts.DeviceId;
        }

        if (metric.HasAlias)
        {
            metadata["sparkplug_alias"] = metric.Alias;
        }

        if (metric.HasIsHistorical)
        {
            metadata["sparkplug_is_historical"] = metric.IsHistorical;
        }

        if (metric.HasIsTransient)
        {
            metadata["sparkplug_is_transient"] = metric.IsTransient;
        }

        if (metric.HasIsNull)
        {
            metadata["sparkplug_is_null"] = metric.IsNull;
        }

        return metadata;
    }

    /// <summary>
    /// Creates a simplified representation of a Sparkplug B DataSet.
    /// </summary>
    private object? CreateDataSet(SparkplugBPayload.Types.DataSet? dataSet)
    {
        if (dataSet == null) return null;

        return new
        {
            NumOfColumns = dataSet.NumOfColumns,
            Columns = dataSet.Columns?.ToArray(),
            Rows = dataSet.Rows?.Select(row => row.Elements?.Select(e => ExtractDataSetValue(e)).ToArray()).ToArray()
        };
    }

    /// <summary>
    /// Creates a simplified representation of a Sparkplug B Template.
    /// </summary>
    private object? CreateTemplate(SparkplugBPayload.Types.Template? template)
    {
        if (template == null) return null;

        return new
        {
            Version = template.Version,
            IsDefinition = template.IsDefinition,
            Metrics = template.Metrics?.Select(m => new { m.Name, m.Datatype }).ToArray(),
            Parameters = template.Parameters?.Select(p => new { p.Name, p.Type }).ToArray()
        };
    }

    /// <summary>
    /// Extracts value from a DataSet element.
    /// </summary>
    private object? ExtractDataSetValue(SparkplugBPayload.Types.DataSet.Types.DataSetValue element)
    {
        if (element.HasIntValue) return element.IntValue;
        if (element.HasLongValue) return element.LongValue;
        if (element.HasFloatValue) return element.FloatValue;
        if (element.HasDoubleValue) return element.DoubleValue;
        if (element.HasBooleanValue) return element.BooleanValue;
        if (element.HasStringValue) return element.StringValue;
        
        return null;
    }
}

/// <summary>
/// Represents the parsed components of a Sparkplug B topic.
/// </summary>
public class SparkplugTopicParts
{
    public string Namespace { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string EdgeNodeId { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
}