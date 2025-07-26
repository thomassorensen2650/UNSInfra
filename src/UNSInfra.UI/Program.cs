using UNSInfra.UI.Components;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Repositories;
using UNSInfra.Storage.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register UNS Infrastructure services
builder.Services.AddSingleton<ITopicConfigurationRepository, InMemoryTopicConfigurationRepository>();
builder.Services.AddSingleton<IRealtimeStorage, InMemoryRealtimeStorage>();
builder.Services.AddSingleton<IHistoricalStorage, InMemoryHistoricalStorage>();
builder.Services.AddSingleton<ITopicBrowserService, TopicBrowserService>();

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
