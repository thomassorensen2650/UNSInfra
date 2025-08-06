using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using UNSInfra.MCP.Server.Models;
using UNSInfra.MCP.Server.Services;

namespace UNSInfra.MCP.Server.Controllers;

/// <summary>
/// MCP (Model Context Protocol) controller for handling UNS queries
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class McpController : ControllerBase
{
    private readonly UnsMcpService _mcpService;
    private readonly ILogger<McpController> _logger;

    public McpController(UnsMcpService mcpService, ILogger<McpController> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }

    /// <summary>
    /// Handle MCP JSON-RPC requests
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleMcpRequest([FromBody] McpMessage request)
    {
        try
        {
            _logger.LogDebug("Received MCP request: {Method}", request.Method);

            var response = request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolCall(request),
                _ => CreateErrorResponse(request.Id, -32601, "Method not found")
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request");
            return Ok(CreateErrorResponse(request.Id, -32603, "Internal error", ex.Message));
        }
    }

    /// <summary>
    /// Handle initialization request
    /// </summary>
    private McpMessage HandleInitialize(McpMessage request)
    {
        var result = new McpInitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new McpServerCapabilities
            {
                Tools = new { }
            },
            ServerInfo = new McpServerInfo
            {
                Name = "UNSInfra MCP Server",
                Version = "1.0.0"
            }
        };

        return new McpMessage
        {
            Id = request.Id,
            Result = result
        };
    }

    /// <summary>
    /// Handle tools list request
    /// </summary>
    private McpMessage HandleToolsList(McpMessage request)
    {
        var tools = _mcpService.GetAvailableTools();
        
        var result = new
        {
            tools = tools
        };

        return new McpMessage
        {
            Id = request.Id,
            Result = result
        };
    }

    /// <summary>
    /// Handle tool call request
    /// </summary>
    private async Task<McpMessage> HandleToolCall(McpMessage request)
    {
        try
        {
            if (request.Params == null)
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid params");
            }

            // Parse tool call from params
            var paramsJson = JsonSerializer.Serialize(request.Params);
            var toolCall = JsonSerializer.Deserialize<McpToolCall>(paramsJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (toolCall == null)
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid tool call");
            }

            var result = await _mcpService.ExecuteToolAsync(toolCall);

            return new McpMessage
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool call");
            return CreateErrorResponse(request.Id, -32603, "Tool execution error", ex.Message);
        }
    }

    /// <summary>
    /// Create an error response
    /// </summary>
    private static McpMessage CreateErrorResponse(object? id, int code, string message, object? data = null)
    {
        return new McpMessage
        {
            Id = id,
            Error = new McpError
            {
                Code = code,
                Message = message,
                Data = data
            }
        };
    }
}

/// <summary>
/// Health check controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            server = "UNSInfra MCP Server",
            version = "1.0.0"
        });
    }
}