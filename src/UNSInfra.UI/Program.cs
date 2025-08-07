using UNSInfra.UI.Components;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.InMemory;
using UNSInfra.Storage.SQLite.Extensions;
using UNSInfra.Services.V1.Descriptors;
using UNSInfra.Services.SocketIO.Descriptors;
using UNSInfra.Core.Extensions;
using UNSInfra.Core.Services;
using UNSInfra.Services;
using UNSInfra.Extensions;
using UNSInfra.UI;

// Force IPv4 to avoid dual-stack socket issues on macOS
Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS", "0");
Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:5000;https://localhost:5001");

// Configure GC for better SignalR performance
Environment.SetEnvironmentVariable("DOTNET_gcServer", "1");
Environment.SetEnvironmentVariable("DOTNET_GCRetainVM", "1");
Environment.SetEnvironmentVariable("DOTNET_GCConserveMemory", "5");

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to avoid IPv4/IPv6 dual-stack issues on macOS
builder.WebHost.ConfigureKestrel(options =>
{
    // Bind explicitly to IPv4 localhost to avoid dual-stack issues
    options.ListenLocalhost(5000, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    
    // Configure for HTTPS as well
    options.ListenLocalhost(5001, listenOptions =>
    {
        listenOptions.UseHttps();
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

// Configure logging to suppress Entity Framework debug messages
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);

// Add in-memory logging service for log viewer
builder.Services.AddSingleton<UNSInfra.UI.Services.InMemoryLogService>();
builder.Services.AddSingleton<UNSInfra.UI.Services.IInMemoryLogService>(provider => 
    provider.GetRequiredService<UNSInfra.UI.Services.InMemoryLogService>());
builder.Logging.AddProvider(new UNSInfra.UI.Services.InMemoryLoggerProvider(
    builder.Services.BuildServiceProvider().GetRequiredService<UNSInfra.UI.Services.InMemoryLogService>()));

// Add comprehensive SignalR connection logging for debugging
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR.HubConnectionContext", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher", LogLevel.Debug);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Blazor Server Circuit options for better stability
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = true;
    options.DisconnectedCircuitMaxRetained = 5; // Reduced from default 100
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3); // Reduced from default 20 minutes
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2); // Increased timeout
    options.MaxBufferedUnacknowledgedRenderBatches = 5; // Reduced from default 10
});

// Configure SignalR for high-volume scenarios with socket fixes
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(10); // Longer timeout to prevent drops
    options.KeepAliveInterval = TimeSpan.FromSeconds(10); // More frequent keep-alive
    options.HandshakeTimeout = TimeSpan.FromSeconds(45); // Longer handshake timeout
    options.MaximumReceiveMessageSize = 512 * 1024; // Reduced to 512KB to prevent memory issues
    options.StreamBufferCapacity = 25; // Further reduced buffer capacity
    options.MaximumParallelInvocationsPerClient = 1; // Single invocation to prevent overload
    options.EnableDetailedErrors = true; // Enable detailed errors for debugging
});

// Add SignalR exception filter
//builder.Services.AddSingleton<SignalRExceptionFilter>();


// Register configurable storage services (IRealtimeStorage is always InMemory, IHistoricalStorage uses appsettings.json)
builder.Services.AddConfigurableStorage(builder.Configuration);

// Register UNS Infrastructure services as scoped to work with SQLite repositories
builder.Services.AddScoped<ITopicDiscoveryService, TopicDiscoveryService>();

// Add event-driven services for better performance
builder.Services.AddEventDrivenServices();

// Add the event-driven background service for non-blocking data processing
builder.Services.AddHostedService<UNSInfra.UI.Services.EventDrivenDataIngestionBackgroundService>();

// Add MCP server background service
builder.Services.AddHostedService<UNSInfra.UI.Services.McpServerBackgroundService>();
builder.Services.AddSingleton<UNSInfra.UI.Services.McpServerBackgroundService>(provider =>
    (UNSInfra.UI.Services.McpServerBackgroundService)provider.GetServices<IHostedService>()
        .First(s => s is UNSInfra.UI.Services.McpServerBackgroundService));

// Register hierarchy service
builder.Services.AddScoped<IHierarchyService, HierarchyService>();

// Register namespace structure service
builder.Services.AddScoped<INamespaceStructureService, NamespaceStructureService>();

// Register topic configuration notification service
builder.Services.AddSingleton<ITopicConfigurationNotificationService, TopicConfigurationNotificationService>();

// Register schema validation services
builder.Services.AddScoped<UNSInfra.Repositories.ISchemaRepository, UNSInfra.Repositories.InMemorySchemaRepository>();
builder.Services.AddScoped<UNSInfra.Validation.ISchemaValidator, UNSInfra.Validation.JsonSchemaValidator>();

// Add new dynamic configuration system
builder.Services.AddUNSInfrastructureCore();

// Register service descriptors for MQTT and SocketIO
builder.Services.AddDataIngestionServiceDescriptor<UNSInfra.Services.V1.Descriptors.MqttServiceDescriptor>();
builder.Services.AddDataIngestionServiceDescriptor<UNSInfra.Services.SocketIO.Descriptors.SocketIOServiceDescriptor>();

// Register SparkplugB decoder for MQTT service
builder.Services.AddSingleton<UNSInfra.Services.V1.SparkplugB.SparkplugBDecoder>();

// Legacy services removed - now using dynamic configuration system only

var app = builder.Build();

// Initialize configurable storage and default configuration
await app.Services.InitializeConfigurableStorageAsync(app.Configuration);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add health check endpoint for Docker containers
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown"
}));

app.Run();
