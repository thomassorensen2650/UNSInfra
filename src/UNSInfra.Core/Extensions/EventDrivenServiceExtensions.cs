using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UNSInfra.Services.Events;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.UI;

namespace UNSInfra.Extensions;

/// <summary>
/// Extension methods for registering event-driven services
/// </summary>
public static class EventDrivenServiceExtensions
{
    /// <summary>
    /// Adds event-driven architecture services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddEventDrivenServices(this IServiceCollection services)
    {
        // Register event bus as singleton for high performance
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        // Register the high-performance cached topic browser service
        services.AddSingleton<CachedTopicBrowserService>();
        services.AddSingleton<ITopicBrowserService>(provider => provider.GetRequiredService<CachedTopicBrowserService>());
        
        // Register batched UI update service for better performance
        services.AddSingleton<BatchedUIUpdateService>();
        
        // Register background service to initialize the cache on startup
        services.AddHostedService<TopicCacheInitializationService>();
        
        return services;
    }
}