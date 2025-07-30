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
using UNSInfra.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to suppress Entity Framework debug messages
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


// Register SQLite storage services (replaces in-memory implementations)
builder.Services.AddSQLiteStorage();

// Register UNS Infrastructure services as scoped to work with SQLite repositories
builder.Services.AddScoped<ITopicDiscoveryService, TopicDiscoveryService>();
builder.Services.AddScoped<ITopicBrowserService, TopicBrowserService>();

// Register hierarchy service
builder.Services.AddScoped<IHierarchyService, HierarchyService>();

// Add new dynamic configuration system
builder.Services.AddUNSInfrastructureCore();

// Register service descriptors for MQTT and SocketIO
builder.Services.AddDataIngestionServiceDescriptor<UNSInfra.Services.V1.Descriptors.MqttServiceDescriptor>();
builder.Services.AddDataIngestionServiceDescriptor<UNSInfra.Services.SocketIO.Descriptors.SocketIOServiceDescriptor>();

// Register SparkplugB decoder for MQTT service
builder.Services.AddSingleton<UNSInfra.Services.V1.SparkplugB.SparkplugBDecoder>();

// Legacy services removed - now using dynamic configuration system only

var app = builder.Build();

// Initialize SQLite database and default configuration
await app.Services.InitializeSQLiteDatabaseAsync();

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


app.Run();
