using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var stdioMode = !args.Contains("--stdio");

var urls = builder.Configuration.GetValue<string>("Urls", "http://localhost:3001;https://localhost:3002");
if (!stdioMode)
{
    builder.WebHost.UseUrls(urls);
}
// Set URLs explicitly to avoid conflicts
builder.Services.AddMcpServer()
    .WithToolsFromAssembly()
    .WithStdioServerTransport()
     .WithHttpTransport();
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

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

// Configure HTTPS - URLs will be read from appsettings.json

var app = builder.Build();

if (!stdioMode)
{
    // Map MCP endpoints
    app.MapMcp();
}

try
{
    Log.Information("Starting UNS Infrastructure MCP Server on: {Urls}", urls);
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "UNS Infrastructure MCP Server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}