# UNSInfra MCP Server

A Model Context Protocol (MCP) server for UNSInfra that provides tools for querying UNS hierarchy, namespaces, topics, and data.

## Overview

This MCP server exposes the UNSInfra system through standardized MCP tools, allowing AI assistants and other MCP clients to:

- Query the complete UNS (Unified Namespace) hierarchy
- Get topics assigned to specific namespaces
- Retrieve current values for topics
- Query historical data with time ranges
- Search for topics by patterns

## Available Tools

### 1. get_uns_hierarchy
Get the complete UNS hierarchy including hierarchy nodes and namespaces.

**Parameters:**
- `includeTopics` (boolean, optional): Whether to include topics assigned to namespaces
- `includeCurrentValues` (boolean, optional): Whether to include current values for topics

### 2. get_namespace_topics
Get all topics assigned to a specific namespace.

**Parameters:**
- `namespacePath` (string, required): The full path to the namespace
- `includeCurrentValues` (boolean, optional): Whether to include current values for topics

### 3. get_topic_current_value
Get the current value of a specific topic.

**Parameters:**
- `topic` (string, required): The topic name to query

### 4. get_topic_historical_data
Get historical data for a topic within a time range.

**Parameters:**
- `topic` (string, required): The topic name to query
- `startTime` (string, required): Start time in ISO 8601 format
- `endTime` (string, required): End time in ISO 8601 format
- `maxPoints` (integer, optional): Maximum number of data points to return (default: 1000)
- `aggregation` (string, optional): Aggregation method (avg, min, max, first, last)

### 5. search_topics
Search for topics by name or path pattern.

**Parameters:**
- `searchPattern` (string, required): Search pattern for topic names (supports wildcards)
- `sourceType` (string, optional): Filter by data source type (e.g., 'MQTT', 'SocketIO')
- `includeCurrentValues` (boolean, optional): Whether to include current values for topics

## Running the Server

```bash
# Build the project
dotnet build

# Run the server
dotnet run --project src/UNSInfra.MCP.Server

# The server will be available at:
# - HTTP: http://localhost:5000
# - HTTPS: https://localhost:5001
# - Swagger UI: https://localhost:5001 (in development)
```

## API Endpoints

### MCP Protocol
- `POST /api/mcp` - Main MCP JSON-RPC endpoint

### Utility Endpoints
- `GET /` - Server information and available tools
- `GET /api/health` - Health check endpoint
- `GET /swagger` - API documentation (development only)

## MCP Protocol Usage

The server implements the Model Context Protocol (MCP) specification. Here's an example of how to call a tool:

```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "method": "tools/call",
  "params": {
    "name": "get_uns_hierarchy",
    "arguments": {
      "includeTopics": true,
      "includeCurrentValues": false
    }
  }
}
```

## Configuration

The MCP server uses the same configuration as the main UNSInfra system:

- **Storage**: Uses InMemory storage by default, can be configured to use SQLite or other storage providers
- **Services**: Integrates with UNSInfra Core services for namespace structure and topic browsing
- **CORS**: Enabled for web-based MCP clients

## Integration with AI Assistants

This MCP server can be integrated with AI assistants that support the MCP protocol. The tools provide comprehensive access to:

- **Hierarchical Data Structure**: Query the ISA-S95 compliant hierarchy
- **Namespace Management**: Access namespace configurations and assignments
- **Real-time Data**: Get current values from industrial data sources
- **Historical Analysis**: Query time-series data for trend analysis
- **Topic Discovery**: Search and discover available data points

## Future Enhancements

- GraphQL endpoint for more flexible queries
- REST API for broader compatibility
- WebSocket support for real-time updates
- Advanced aggregation and filtering options
- Custom query builders for complex data analysis