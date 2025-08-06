using System.Text.Json.Serialization;

namespace UNSInfra.MCP.Server.Models;

/// <summary>
/// Base MCP message structure
/// </summary>
public record McpMessage
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("method")]
    public string? Method { get; init; }
    
    [JsonPropertyName("params")]
    public object? Params { get; init; }
    
    [JsonPropertyName("result")]
    public object? Result { get; init; }
    
    [JsonPropertyName("error")]
    public McpError? Error { get; init; }
}

/// <summary>
/// MCP error structure
/// </summary>
public record McpError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    
    [JsonPropertyName("data")]
    public object? Data { get; init; }
}

/// <summary>
/// MCP tool definition
/// </summary>
public record McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
    
    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; init; } = new();
}

/// <summary>
/// MCP tool call parameters
/// </summary>
public record McpToolCall
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; init; } = new();
}

/// <summary>
/// MCP tool result
/// </summary>
public record McpToolResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; init; } = new();
    
    [JsonPropertyName("isError")]
    public bool IsError { get; init; }
}

/// <summary>
/// MCP content structure
/// </summary>
public record McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";
    
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// Server capabilities
/// </summary>
public record McpServerCapabilities
{
    [JsonPropertyName("tools")]
    public object? Tools { get; init; }
}

/// <summary>
/// Initialize request parameters
/// </summary>
public record McpInitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = string.Empty;
    
    [JsonPropertyName("capabilities")]
    public object? Capabilities { get; init; }
    
    [JsonPropertyName("clientInfo")]
    public McpClientInfo? ClientInfo { get; init; }
}

/// <summary>
/// Client information
/// </summary>
public record McpClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}

/// <summary>
/// Initialize result
/// </summary>
public record McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "2024-11-05";
    
    [JsonPropertyName("capabilities")]
    public McpServerCapabilities Capabilities { get; init; } = new();
    
    [JsonPropertyName("serverInfo")]
    public McpServerInfo ServerInfo { get; init; } = new();
}

/// <summary>
/// Server information
/// </summary>
public record McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "UNSInfra MCP Server";
    
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";
}