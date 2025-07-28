using UNSInfra.UI.Components;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.InMemory;
using UNSInfra.Services.V1.Descriptors;
using UNSInfra.Services.SocketIO.Descriptors;
using UNSInfra.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


// Register UNS Infrastructure services
builder.Services.AddSingleton<ISchemaRepository, InMemorySchemaRepository>();
builder.Services.AddSingleton<ITopicConfigurationRepository, InMemoryTopicConfigurationRepository>();
builder.Services.AddSingleton<IRealtimeStorage, InMemoryRealtimeStorage>();
builder.Services.AddSingleton<IHistoricalStorage, InMemoryHistoricalStorage>();
builder.Services.AddSingleton<ITopicDiscoveryService, TopicDiscoveryService>();
builder.Services.AddSingleton<ITopicBrowserService, TopicBrowserService>();

// Add new dynamic configuration system
builder.Services.AddUNSInfrastructureCore();

// Register service descriptors for MQTT and SocketIO
builder.Services.AddDataIngestionServiceDescriptor<UNSInfra.Services.V1.Descriptors.MqttServiceDescriptor>();
builder.Services.AddDataIngestionServiceDescriptor<UNSInfra.Services.SocketIO.Descriptors.SocketIOServiceDescriptor>();

// Register SparkplugB decoder for MQTT service
builder.Services.AddSingleton<UNSInfra.Services.V1.SparkplugB.SparkplugBDecoder>();

// Legacy services removed - now using dynamic configuration system only

var app = builder.Build();

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
