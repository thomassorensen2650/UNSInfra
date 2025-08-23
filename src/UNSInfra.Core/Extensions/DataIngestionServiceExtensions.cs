using Microsoft.Extensions.DependencyInjection;
using UNSInfra.Services.DataIngestion;

namespace UNSInfra.Extensions;

/// <summary>
/// Extension methods for registering high-performance data ingestion services
/// </summary>
public static class DataIngestionServiceExtensions
{
    /// <summary>
    /// Adds high-performance data ingestion pipeline services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddHighPerformanceDataIngestion(this IServiceCollection services)
    {
        // Register the main pipeline as a hosted service (singleton)
        services.AddSingleton<DataIngestionPipeline>();
        services.AddHostedService<DataIngestionPipeline>(provider => provider.GetRequiredService<DataIngestionPipeline>());
        
        // Register individual components as scoped for proper lifetime management
        services.AddScoped<BulkDataProcessor>();
        
        // Register connection adapter as singleton
        services.AddSingleton<ConnectionDataIngestionAdapter>();
        
        return services;
    }
}