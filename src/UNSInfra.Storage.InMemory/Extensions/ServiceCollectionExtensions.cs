using Microsoft.Extensions.DependencyInjection;
using UNSInfra.Storage.Abstractions;

namespace UNSInfra.Storage.InMemory.Extensions;

/// <summary>
/// Service collection extensions for InMemory storage
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add InMemory storage services
    /// </summary>
    public static IServiceCollection AddInMemoryStorage(this IServiceCollection services)
    {
        services.AddSingleton<IRealtimeStorage, InMemoryRealtimeStorage>();
        services.AddSingleton<IHistoricalStorage, InMemoryHistoricalStorage>();
        
        return services;
    }
}