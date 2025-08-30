# UNSInfra MCP Server

The UNSInfra MCP Server provides Model Context Protocol (MCP) tools for accessing the UNS Infrastructure data through GraphQL.

## Features

- **GraphQL Integration**: Access UNS data through the UI server's GraphQL API
- **Comprehensive Logging**: File and console logging with configurable levels
- **Error Tracking**: Dedicated error log files with retention policies
- **MCP Tools**: Tools for hierarchy browsing, topic search, and system status

## Logging Configuration

The server uses Serilog for comprehensive logging with both file and console outputs.

### Log Files

By default, logs are written to:
- `logs/uns-mcp-server-YYYY-MM-DD.log` - General logs (Warning level and above)
- `logs/uns-mcp-server-errors-YYYY-MM-DD.log` - Error logs only (Error level and above)

### Configuration

Logging can be configured in `appsettings.json`:

```json
{
  "UNSInfra": {
    "Logging": {
      "FilePath": "logs/uns-mcp-server-.log",
      "ErrorFilePath": "logs/uns-mcp-server-errors-.log",
      "RetainedFileCountLimit": 30,
      "ErrorRetainedFileCountLimit": 90,
      "MinimumFileLogLevel": "Warning",
      "MinimumErrorLogLevel": "Error",
      "EnableConsoleLogging": true,
      "MinimumConsoleLogLevel": "Information"
    }
  }
}
```

### Log Levels

- **Verbose**: Detailed tracing information
- **Debug**: Debug information useful during development
- **Information**: General application flow information
- **Warning**: Indicates potential issues
- **Error**: Error conditions that don't stop the application
- **Fatal**: Critical errors that cause application termination

### Log Retention

- General logs are retained for 30 days by default
- Error logs are retained for 90 days by default
- Files are rotated daily with timestamps in the filename

## Available MCP Tools

### GetUnsHierarchyTreeAsync
Retrieves the complete UNS hierarchy structure as a clean tree showing all namespaces and topics organized hierarchically.

### GetTopicAsync
Gets detailed information for a specific topic by name.

### GetTopicsByNamespaceAsync
Returns all topics within a specified namespace path, including their latest values.

### GetTopicCurrentValueByPathAsync
Gets the current value and metadata for a specific topic by its path. The path parameter can match any of:
- Topic name
- UNS name 
- Namespace path
- Hierarchical path

### SearchTopicsAsync
Searches for topics matching a given search term.

### GetSystemStatusAsync
Provides system status information including topic counts, connections, and statistics.

## Error Handling

All MCP tools include comprehensive error handling:
- GraphQL query failures are logged and returned as structured error responses
- Network connectivity issues are captured and logged
- Invalid input parameters are validated and logged
- All exceptions are logged with full stack traces

## Usage

The MCP server connects to the UNS Infrastructure UI server's GraphQL endpoint at `http://localhost:5000/graphql` by default. This can be configured via:

```json
{
  "UNSInfra": {
    "ApiBaseUrl": "http://localhost:5000"
  }
}
```

## Monitoring

Monitor the log files for:
- Connection issues to the UI server
- GraphQL query failures
- Invalid MCP tool requests
- System performance issues

Error logs provide detailed information for troubleshooting production issues.