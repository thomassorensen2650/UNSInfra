using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UNSInfra.Services.Events;
using UNSInfra.Services.TopicBrowser;

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
        
        // Replace the standard topic browser service with the event-driven version
        services.AddSingleton<EventDrivenTopicBrowserService>();
        services.AddSingleton<ITopicBrowserService>(provider => provider.GetRequiredService<EventDrivenTopicBrowserService>());
        
        return services;
    }
}