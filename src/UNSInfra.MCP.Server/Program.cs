using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using System.ComponentModel;
using UNSInfra.Core.Extensions;
using UNSInfra.Storage.InMemory.Extensions;
using UNSInfra.Storage.SQLite.Extensions;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
// using UNSInfra.Core.Services; // Removed - old data ingestion services
using UNSInfra.Services;
using UNSInfra.Extensions;
using UNSInfra.Services.V1.Extensions;
using UNSInfra.Services.SocketIO.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add UNSInfra Core services
builder.Services.AddUNSInfrastructureCore();

// Register configurable storage services
builder.Services.AddConfigurableStorage(builder.Configuration);

// Register additional required services
builder.Services.AddScoped<ITopicDiscoveryService, TopicDiscoveryService>();

// Register hierarchy service
builder.Services.AddScoped<IHierarchyService, HierarchyService>();

// Register namespace structure service
builder.Services.AddScoped<INamespaceStructureService, NamespaceStructureService>();

// Add event-driven services for better performance
builder.Services.AddEventDrivenServices();

// Register topic configuration notification service
builder.Services.AddSingleton<ITopicConfigurationNotificationService, TopicConfigurationNotificationService>();

// Register schema validation services
builder.Services.AddScoped<UNSInfra.Repositories.ISchemaRepository, UNSInfra.Repositories.InMemorySchemaRepository>();
builder.Services.AddScoped<UNSInfra.Validation.ISchemaValidator, UNSInfra.Validation.JsonSchemaValidator>();

// Register connection services
builder.Services.AddConnectionServices();
builder.Services.AddProductionMqttConnection();
builder.Services.AddProductionSocketIOConnection();

// Register SparkplugB decoder (if needed)
builder.Services.AddSingleton<UNSInfra.Services.V1.SparkplugB.SparkplugBDecoder>();

// Register MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

// Initialize configurable storage
await host.Services.InitializeConfigurableStorageAsync(builder.Configuration);

// Register connection types
host.Services.RegisterConnectionTypes();

await host.RunAsync();