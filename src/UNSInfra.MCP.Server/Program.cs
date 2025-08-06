using UNSInfra.MCP.Server.Services;
using UNSInfra.Core.Extensions;
using UNSInfra.Storage.InMemory.Extensions;
using UNSInfra.Storage.SQLite.Extensions;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Core.Services;
using UNSInfra.Services;
using UNSInfra.Extensions;
using UNSInfra.Services.V1.Descriptors;
using UNSInfra.Services.SocketIO.Descriptors;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "UNSInfra MCP Server", 
        Version = "v1",
        Description = "Model Context Protocol (MCP) server for UNSInfra - provides tools for querying UNS hierarchy, namespaces, and data"
    });
});

// Add CORS for web-based MCP clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add UNSInfra Core services
builder.Services.AddUNSInfrastructureCore();

// Register configurable storage services (use SQLite for standalone server)
builder.Services.AddConfigurableStorage(builder.Configuration);

// Register additional required services
builder.Services.AddScoped<ITopicDiscoveryService, TopicDiscoveryService>();

// Add event-driven services for better performance
builder.Services.AddEventDrivenServices();

// Register hierarchy service
builder.Services.AddScoped<IHierarchyService, HierarchyService>();

// Register namespace structure service
builder.Services.AddScoped<INamespaceStructureService, NamespaceStructureService>();

// Register topic configuration notification service
builder.Services.AddSingleton<ITopicConfigurationNotificationService, TopicConfigurationNotificationService>();

// Register schema validation services
builder.Services.AddScoped<UNSInfra.Repositories.ISchemaRepository, UNSInfra.Repositories.InMemorySchemaRepository>();
builder.Services.AddScoped<UNSInfra.Validation.ISchemaValidator, UNSInfra.Validation.JsonSchemaValidator>();

// Register service descriptors for MQTT and SocketIO (optional for MCP server)
builder.Services.AddDataIngestionServiceDescriptor<UNSInfra.Services.V1.Descriptors.MqttServiceDescriptor>();
builder.Services.AddDataIngestionServiceDescriptor<UNSInfra.Services.SocketIO.Descriptors.SocketIOServiceDescriptor>();

// Register SparkplugB decoder for MQTT service (if needed)
builder.Services.AddSingleton<UNSInfra.Services.V1.SparkplugB.SparkplugBDecoder>();

// Add MCP service
builder.Services.AddScoped<UnsMcpService>();

// Configure JSON options for MCP compatibility
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

// Initialize configurable storage for standalone server
await app.Services.InitializeConfigurableStorageAsync(app.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UNSInfra MCP Server v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// Add a simple root endpoint with server information
app.MapGet("/", () => new
{
    server = "UNSInfra MCP Server",
    version = "1.0.0",
    protocol = "MCP (Model Context Protocol)",
    description = "Provides tools for querying UNS hierarchy, namespaces, topics, and data",
    endpoints = new
    {
        mcp = "/api/mcp",
        health = "/api/health",
        swagger = "/swagger"
    },
    availableTools = new[]
    {
        "get_uns_hierarchy - Get the complete UNS hierarchy",
        "get_namespace_topics - Get topics for a specific namespace", 
        "get_topic_current_value - Get current value of a topic",
        "get_topic_historical_data - Get historical data with time range",
        "search_topics - Search topics by pattern"
    }
});

app.Run();
