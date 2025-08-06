using System.Text.Json.Serialization;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Data;

namespace UNSInfra.MCP.Server.Models;

/// <summary>
/// UNS hierarchy node for MCP responses
/// </summary>
public record UnsHierarchyNode
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("fullPath")]
    public string FullPath { get; init; } = string.Empty;
    
    [JsonPropertyName("nodeType")]
    public string NodeType { get; init; } = string.Empty;
    
    [JsonPropertyName("children")]
    public List<UnsHierarchyNode> Children { get; init; } = new();
    
    [JsonPropertyName("namespaces")]
    public List<UnsNamespace> Namespaces { get; init; } = new();
    
    [JsonPropertyName("canHaveHierarchyChildren")]
    public bool CanHaveHierarchyChildren { get; init; }
    
    [JsonPropertyName("canHaveNamespaceChildren")]
    public bool CanHaveNamespaceChildren { get; init; }
}

/// <summary>
/// UNS namespace for MCP responses
/// </summary>
public record UnsNamespace
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
    
    [JsonPropertyName("hierarchicalPath")]
    public string HierarchicalPath { get; init; } = string.Empty;
    
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
    
    [JsonPropertyName("topics")]
    public List<UnsTopic> Topics { get; init; } = new();
}

/// <summary>
/// UNS topic for MCP responses
/// </summary>
public record UnsTopic
{
    [JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;
    
    [JsonPropertyName("unsName")]
    public string UnsName { get; init; } = string.Empty;
    
    [JsonPropertyName("nsPath")]
    public string NsPath { get; init; } = string.Empty;
    
    [JsonPropertyName("sourceType")]
    public string SourceType { get; init; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
    
    [JsonPropertyName("currentValue")]
    public UnsDataPoint? CurrentValue { get; init; }
}

/// <summary>
/// UNS data point for MCP responses
/// </summary>
public record UnsDataPoint
{
    [JsonPropertyName("value")]
    public object? Value { get; init; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }
    
    [JsonPropertyName("quality")]
    public string Quality { get; init; } = "Good";
    
    [JsonPropertyName("sourceTimestamp")]
    public DateTime? SourceTimestamp { get; init; }
}

/// <summary>
/// Historical data query parameters
/// </summary>
public record HistoricalDataQuery
{
    [JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;
    
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; init; }
    
    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; init; }
    
    [JsonPropertyName("maxPoints")]
    public int? MaxPoints { get; init; }
    
    [JsonPropertyName("aggregation")]
    public string? Aggregation { get; init; }
}

/// <summary>
/// Historical data response
/// </summary>
public record HistoricalDataResponse
{
    [JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;
    
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; init; }
    
    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; init; }
    
    [JsonPropertyName("dataPoints")]
    public List<UnsDataPoint> DataPoints { get; init; } = new();
    
    [JsonPropertyName("totalPoints")]
    public int TotalPoints { get; init; }
}