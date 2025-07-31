namespace UNSInfra.Services.Events;

/// <summary>
/// Event bus interface for decoupled communication between services
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribers
    /// </summary>
    Task PublishAsync<T>(T eventData) where T : IEvent;
    
    /// <summary>
    /// Subscribes to events of a specific type
    /// </summary>
    void Subscribe<T>(Func<T, Task> handler) where T : IEvent;
    
    /// <summary>
    /// Unsubscribes from events of a specific type
    /// </summary>
    void Unsubscribe<T>(Func<T, Task> handler) where T : IEvent;
}

/// <summary>
/// Base interface for all events
/// </summary>
public interface IEvent
{
    DateTime Timestamp { get; }
    string EventId { get; }
}

/// <summary>
/// Base event implementation
/// </summary>
public abstract record BaseEvent : IEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string EventId { get; init; } = Guid.NewGuid().ToString();
}