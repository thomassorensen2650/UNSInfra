using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

using ModelContextProtocol.Server;
using System.ComponentModel;
using GraphQL.Client.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});


/*
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();
return;

// Build a temporary configuration to read logging settings
var tempConfig = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();
*/
/*
// Get logging configuration values with defaults
var loggingConfig = tempConfig.GetSection("UNSInfra:Logging");
var filePath = loggingConfig["FilePath"] ?? "logs/uns-mcp-server-.log";
var errorFilePath = loggingConfig["ErrorFilePath"] ?? "logs/uns-mcp-server-errors-.log";
var retainedFileCount = int.Parse(loggingConfig["RetainedFileCountLimit"] ?? "30");
var errorRetainedFileCount = int.Parse(loggingConfig["ErrorRetainedFileCountLimit"] ?? "90");
var enableConsoleLogging = bool.Parse(loggingConfig["EnableConsoleLogging"] ?? "true");

// Parse log levels with fallbacks
Enum.TryParse<LogEventLevel>(loggingConfig["MinimumFileLogLevel"], out var minFileLogLevel);
if (minFileLogLevel == 0) minFileLogLevel = LogEventLevel.Warning;

Enum.TryParse<LogEventLevel>(loggingConfig["MinimumErrorLogLevel"], out var minErrorLogLevel);
if (minErrorLogLevel == 0) minErrorLogLevel = LogEventLevel.Error;

Enum.TryParse<LogEventLevel>(loggingConfig["MinimumConsoleLogLevel"], out var minConsoleLogLevel);
if (minConsoleLogLevel == 0) minConsoleLogLevel = LogEventLevel.Information;

// Ensure logs directory exists
Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "logs");
if (errorFilePath != filePath)
{
    Directory.CreateDirectory(Path.GetDirectoryName(errorFilePath) ?? "logs");
}

// Configure Serilog for comprehensive logging
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    
    .Enrich.WithProperty("Application", "UNSInfra-MCP-Server")
    .WriteTo.File(
        path: filePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: retainedFileCount,
        restrictedToMinimumLevel: minFileLogLevel,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Application} - {SourceContext}: {Message:lj}{NewLine}{Exception}",
        shared: true)
    .WriteTo.File(
        path: errorFilePath, 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: errorRetainedFileCount,
        restrictedToMinimumLevel: minErrorLogLevel,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Application} - {SourceContext}: {Message:lj}{NewLine}{Exception}",
        shared: true);

// Add console logging if enabled
if (enableConsoleLogging)
{
    loggerConfig.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: minConsoleLogLevel);
}

Log.Logger = loggerConfig.CreateLogger();
*/

// Replace default logging with Serilog
//builder.Services.AddSerilog();

// Clear default logging providers and add Serilog
//builder.Logging.ClearProviders();
//builder.Logging.AddSerilog();

// Add UNSInfra Core services
//builder.Services.AddUNSInfrastructureCore();

// Add HTTP client for API calls to UI server
builder.Services.AddHttpClient("UNSInfraAPI", client =>
{
    // Default to localhost, can be overridden via configuration
    var apiBaseUrl = builder.Configuration.GetValue<string>("UNSInfra:ApiBaseUrl") ?? "https://localhost:5001";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "UNSInfra-MCP-Server/1.0");
});

// Add GraphQL client for clean data access
builder.Services.AddSingleton<GraphQL.Client.Abstractions.IGraphQLClient>(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("UNSInfraAPI");
    var graphQLEndpoint = new Uri(httpClient.BaseAddress!, "/graphql");
    
    var graphQLClient = new GraphQL.Client.Http.GraphQLHttpClient(graphQLEndpoint.ToString(), new GraphQL.Client.Serializer.SystemTextJson.SystemTextJsonSerializer());
    return graphQLClient;
});

// Register configurable storage services (kept for backward compatibility)
//uilder.Services.AddConfigurableStorage(builder.Configuration);

// Register additional required services
//builder.Services.AddScoped<ITopicDiscoveryService, TopicDiscoveryService>();

// Register hierarchy service
//builder.Services.AddScoped<IHierarchyService, HierarchyService>();

// Register namespace structure service
//builder.Services.AddScoped<INamespaceStructureService, NamespaceStructureService>();

// Add event-driven services for better performance
//builder.Services.AddEventDrivenServices();

// Register topic configuration notification service
//builder.Services.AddSingleton<ITopicConfigurationNotificationService, TopicConfigurationNotificationService>();

// Register schema validation services
//builder.Services.AddScoped<UNSInfra.Repositories.ISchemaRepository, UNSInfra.Repositories.InMemorySchemaRepository>();
//builder.Services.AddScoped<UNSInfra.Validation.ISchemaValidator, UNSInfra.Validation.JsonSchemaValidator>();

// Register connection services
//builder.Services.AddConnectionServices();
//builder.Services.AddProductionMqttConnection();
//builder.Services.AddProductionSocketIOConnection();

// Register SparkplugB decoder (if needed)
//builder.Services.AddSingleton<UNSInfra.Services.V1.SparkplugB.SparkplugBDecoder>();


var app = builder.Build();

// Map MCP endpoints
app.MapMcp();

try
{
    Log.Information("Starting UNS Infrastructure MCP Server");
    //await app.RunAsync();
    app.Run("http://localhost:3001");
}
catch (Exception ex)
{
    Log.Fatal(ex, "UNS Infrastructure MCP Server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}