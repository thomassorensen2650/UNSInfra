using UNSInfra.UI.Components;
using UNSInfra.UI.Services;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Services.DataIngestion.Mock;
using UNSInfra.Repositories;
using UNSInfra.Storage.Abstractions;
using UNSInfra.Storage.InMemory;
using UNSInfra.Services.V1;

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

// Add MQTT data service from UNSInfra.Services.V1
builder.Services.AddMqttDataService(builder.Configuration);

// Add hosted service to manage MQTT connection and data processing
builder.Services.AddHostedService<MqttBackgroundService>();

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
