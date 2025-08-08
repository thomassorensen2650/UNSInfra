using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace UNSInfra.Services.Events;

/// <summary>
/// In-memory event bus implementation for high-performance event dispatching
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Func<object, Task>>> _handlers = new();
    private readonly SemaphoreSlim _publishSemaphore = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(T eventData) where T : IEvent
    {
        if (eventData == null) return;

        var eventType = typeof(T);
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        // Use semaphore to limit concurrent event processing to prevent overwhelming the system
        await _publishSemaphore.WaitAsync();
        
        try
        {
            // Process events in parallel but with limited concurrency
            var tasks = handlers.Select(handler => 
                Task.Run(async () =>
                {
                    try
                    {
                        await handler(eventData);
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't break other handlers
                        _logger.LogError(ex, "Error in event handler for {EventType}", eventType.Name);
                    }
                }));
            
            await Task.WhenAll(tasks);
        }
        finally
        {
            _publishSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public void Subscribe<T>(Func<T, Task> handler) where T : IEvent
    {
        var eventType = typeof(T);
        var wrappedHandler = new Func<object, Task>(obj => handler((T)obj));
        
        _handlers.AddOrUpdate(
            eventType,
            new ConcurrentBag<Func<object, Task>> { wrappedHandler },
            (key, existing) =>
            {
                existing.Add(wrappedHandler);
                return existing;
            });
    }

    /// <inheritdoc />
    public void Unsubscribe<T>(Func<T, Task> handler) where T : IEvent
    {
        var eventType = typeof(T);
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        // Note: ConcurrentBag doesn't support removal, so we'd need a different approach
        // For now, handlers will remain subscribed until the service is restarted
        // In production, consider using a different data structure if dynamic unsubscription is needed
    }
}