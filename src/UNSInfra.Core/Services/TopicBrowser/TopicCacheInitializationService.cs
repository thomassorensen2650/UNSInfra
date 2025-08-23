using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UNSInfra.Services.TopicBrowser;

/// <summary>
/// Background service that initializes the topic cache on application startup.
/// This ensures the cache is ready before any UI components try to access it.
/// </summary>
public class TopicCacheInitializationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TopicCacheInitializationService> _logger;

    public TopicCacheInitializationService(
        IServiceProvider serviceProvider,
        ILogger<TopicCacheInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait a moment for other services to be ready
            await Task.Delay(1000, stoppingToken);
            
            _logger.LogInformation("Initializing topic cache on startup...");
            
            // Get the cached topic browser service and initialize it
            using var scope = _serviceProvider.CreateScope();
            var cachedService = scope.ServiceProvider.GetService<CachedTopicBrowserService>();
            
            if (cachedService != null)
            {
                await cachedService.InitializeAsync();
                _logger.LogInformation("Topic cache initialization completed successfully");
                
                // Log cache statistics
                var stats = cachedService.GetCacheStatistics();
                _logger.LogInformation("Cache initialized with {TopicCount} topics, {NamespaceCount} namespaces", 
                    stats.CachedTopicCount, stats.NamespaceIndexCount);
            }
            else
            {
                _logger.LogWarning("CachedTopicBrowserService not found in service provider");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when application is shutting down
            _logger.LogInformation("Topic cache initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize topic cache");
        }
    }
}