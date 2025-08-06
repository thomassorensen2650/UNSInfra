using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using UNSInfra.MCP.Server.Services;
using UNSInfra.MCP.Server.Controllers;
using UNSInfra.MCP.Server.Models;
using UNSInfra.Services;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Storage.Abstractions;

namespace UNSInfra.UI.Services;

/// <summary>
/// Background service that hosts the MCP server within the main UI application
/// </summary>
public class McpServerBackgroundService : BackgroundService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpServerBackgroundService> _logger;
    private WebApplication? _mcpApp;
    private CancellationTokenSource? _mcpCancellationTokenSource;
    private Task? _mcpServerTask;
    
    // Configuration
    private int _mcpPort = 5001;
    private bool _isStartRequested = false;
    private bool _isStopRequested = false;
    
    // Status tracking
    public bool IsRunning { get; private set; } = false;
    public bool IsStarting { get; private set; } = false;
    public bool IsStopping { get; private set; } = false;
    public int Port => _mcpPort;
    public string BaseUrl => $"https://localhost:{_mcpPort}";
    public DateTime? StartedAt { get; private set; }
    public string? LastError { get; private set; }
    
    // Events for UI updates
    public event EventHandler<McpServerStatusChangedEventArgs>? StatusChanged;

    public McpServerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<McpServerBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MCP Server background service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check if start is requested
                if (_isStartRequested && !IsRunning && !IsStarting)
                {
                    _isStartRequested = false;
                    await StartMcpServerInternalAsync();
                }
                
                // Check if stop is requested
                if (_isStopRequested && IsRunning && !IsStopping)
                {
                    _isStopRequested = false;
                    await StopMcpServerInternalAsync();
                }
                
                // Monitor MCP server task
                if (_mcpServerTask != null && _mcpServerTask.IsCompleted)
                {
                    if (_mcpServerTask.IsFaulted)
                    {
                        LastError = _mcpServerTask.Exception?.GetBaseException().Message;
                        _logger.LogError(_mcpServerTask.Exception, "MCP server task failed");
                    }
                    
                    await CleanupMcpServer();
                }
                
                await Task.Delay(1000, stoppingToken); // Check every second
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MCP server background service");
                LastError = ex.Message;
                await Task.Delay(5000, stoppingToken); // Wait longer on error
            }
        }
        
        // Cleanup on shutdown
        if (IsRunning)
        {
            await StopMcpServerInternalAsync();
        }
    }

    /// <summary>
    /// Request to start the MCP server
    /// </summary>
    public async Task<bool> StartMcpServerAsync(int port = 5001)
    {
        if (IsRunning || IsStarting)
        {
            _logger.LogWarning("MCP server is already running or starting");
            return false;
        }
        
        _mcpPort = port;
        _isStartRequested = true;
        LastError = null;
        
        _logger.LogInformation("MCP server start requested on port {Port}", port);
        
        // Wait a bit for the start to be processed
        var timeout = TimeSpan.FromSeconds(10);
        var start = DateTime.UtcNow;
        
        while (!IsRunning && LastError == null && DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(100);
        }
        
        return IsRunning;
    }

    /// <summary>
    /// Request to stop the MCP server
    /// </summary>
    public async Task<bool> StopMcpServerAsync()
    {
        if (!IsRunning || IsStopping)
        {
            _logger.LogWarning("MCP server is not running or already stopping");
            return false;
        }
        
        _isStopRequested = true;
        _logger.LogInformation("MCP server stop requested");
        
        // Wait a bit for the stop to be processed
        var timeout = TimeSpan.FromSeconds(10);
        var start = DateTime.UtcNow;
        
        while (IsRunning && DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(100);
        }
        
        return !IsRunning;
    }

    private async Task StartMcpServerInternalAsync()
    {
        try
        {
            IsStarting = true;
            OnStatusChanged();
            
            _logger.LogInformation("Starting MCP server on port {Port}", _mcpPort);
            
            // Create WebApplication builder for MCP server
            var builder = WebApplication.CreateBuilder();
            
            // Configure services for MCP server
            ConfigureMcpServices(builder.Services);
            
            // Configure Kestrel to use specific port
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, _mcpPort, listenOptions =>
                {
                    listenOptions.UseHttps();
                });
            });
            
            _mcpApp = builder.Build();
            
            // Configure MCP server pipeline
            ConfigureMcpApp(_mcpApp);
            
            _mcpCancellationTokenSource = new CancellationTokenSource();
            
            // Start the MCP server in a separate task
            _mcpServerTask = _mcpApp.RunAsync(_mcpCancellationTokenSource.Token);
            
            // Wait a moment for the server to start
            await Task.Delay(1000);
            
            IsRunning = true;
            IsStarting = false;
            StartedAt = DateTime.UtcNow;
            
            _logger.LogInformation("MCP server started successfully on {BaseUrl}", BaseUrl);
            OnStatusChanged();
        }
        catch (Exception ex)
        {
            IsStarting = false;
            LastError = ex.Message;
            _logger.LogError(ex, "Failed to start MCP server");
            
            await CleanupMcpServer();
            OnStatusChanged();
        }
    }

    private async Task StopMcpServerInternalAsync()
    {
        try
        {
            IsStopping = true;
            OnStatusChanged();
            
            _logger.LogInformation("Stopping MCP server");
            
            _mcpCancellationTokenSource?.Cancel();
            
            if (_mcpServerTask != null)
            {
                await _mcpServerTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            
            await CleanupMcpServer();
            
            _logger.LogInformation("MCP server stopped successfully");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "Error stopping MCP server");
        }
        finally
        {
            IsStopping = false;
            OnStatusChanged();
        }
    }

    private async Task CleanupMcpServer()
    {
        try
        {
            _mcpApp?.DisposeAsync();
            _mcpCancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during MCP server cleanup");
        }
        finally
        {
            _mcpApp = null;
            _mcpCancellationTokenSource = null;
            _mcpServerTask = null;
            IsRunning = false;
            StartedAt = null;
        }
    }

    private void ConfigureMcpServices(IServiceCollection services)
    {
        // Add basic services for MCP server
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { 
                Title = "UNSInfra MCP Server", 
                Version = "v1",
                Description = "Model Context Protocol (MCP) server for UNSInfra"
            });
        });

        // Add CORS for web-based MCP clients
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Copy services from main application
        // This allows MCP server to use the same services as the main UI
        services.AddScoped(provider => _serviceProvider.GetRequiredService<INamespaceStructureService>());
        services.AddScoped(provider => _serviceProvider.GetRequiredService<ITopicBrowserService>());
        services.AddScoped(provider => _serviceProvider.GetRequiredService<IRealtimeStorage>());
        services.AddScoped(provider => _serviceProvider.GetRequiredService<IHistoricalStorage>());
        
        // Add MCP-specific service
        services.AddScoped<UnsMcpService>();

        // Configure JSON options for MCP compatibility
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.SerializerOptions.WriteIndented = true;
        });
    }

    private static void ConfigureMcpApp(WebApplication app)
    {
        // Configure the MCP server pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "UNSInfra MCP Server v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseRouting();
        app.UseAuthorization();
        app.MapControllers();

        // Add root endpoint with server information
        app.MapGet("/", () => new
        {
            server = "UNSInfra MCP Server",
            version = "1.0.0",
            protocol = "MCP (Model Context Protocol)",
            description = "Embedded MCP server for UNSInfra",
            endpoints = new
            {
                mcp = "/api/mcp",
                health = "/api/health",
                swagger = "/swagger"
            },
            status = "running",
            timestamp = DateTime.UtcNow
        });
    }

    private void OnStatusChanged()
    {
        StatusChanged?.Invoke(this, new McpServerStatusChangedEventArgs
        {
            IsRunning = IsRunning,
            IsStarting = IsStarting,
            IsStopping = IsStopping,
            Port = Port,
            BaseUrl = BaseUrl,
            StartedAt = StartedAt,
            LastError = LastError
        });
    }

    public override void Dispose()
    {
        _mcpCancellationTokenSource?.Cancel();
        _mcpApp?.DisposeAsync();
        _mcpCancellationTokenSource?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Event arguments for MCP server status changes
/// </summary>
public class McpServerStatusChangedEventArgs : EventArgs
{
    public bool IsRunning { get; set; }
    public bool IsStarting { get; set; }
    public bool IsStopping { get; set; }
    public int Port { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public string? LastError { get; set; }
}