using UNSInfra.Services.AutoMapping;
using UNSInfra.Services.DataIngestion.Mock;

namespace UNSInfra.Core.Services.DataIngestion;

/// <summary>
/// Extended interface for data ingestion services that support auto topic mapping functionality.
/// </summary>
public interface IAutoMappableDataIngestionService : IDataIngestionService
{
    /// <summary>
    /// Gets the auto topic mapper configuration for this service.
    /// </summary>
    AutoTopicMapperConfiguration? AutoMapperConfiguration { get; }

    /// <summary>
    /// Updates the auto topic mapper configuration for this service.
    /// </summary>
    /// <param name="configuration">The new auto mapper configuration</param>
    Task UpdateAutoMapperConfigurationAsync(AutoTopicMapperConfiguration? configuration);

    /// <summary>
    /// Gets statistics about auto mapping operations.
    /// </summary>
    /// <returns>Auto mapping statistics</returns>
    AutoMappingStatistics GetAutoMappingStatistics();

    /// <summary>
    /// Event fired when a topic is successfully auto-mapped.
    /// </summary>
    event EventHandler<TopicAutoMappedEventArgs>? TopicAutoMapped;

    /// <summary>
    /// Event fired when auto mapping fails for a topic.
    /// </summary>
    event EventHandler<AutoMappingFailedEventArgs>? AutoMappingFailed;
}

/// <summary>
/// Statistics about auto mapping operations.
/// </summary>
public class AutoMappingStatistics
{
    /// <summary>
    /// Total number of topics processed.
    /// </summary>
    public long TotalTopicsProcessed { get; set; }

    /// <summary>
    /// Number of topics successfully auto-mapped.
    /// </summary>
    public long SuccessfullyMapped { get; set; }

    /// <summary>
    /// Number of topics that failed auto mapping.
    /// </summary>
    public long MappingFailed { get; set; }

    /// <summary>
    /// Number of topics that were already mapped.
    /// </summary>
    public long AlreadyMapped { get; set; }

    /// <summary>
    /// Average confidence level of successful mappings.
    /// </summary>
    public double AverageConfidence { get; set; }

    /// <summary>
    /// When statistics were last reset.
    /// </summary>
    public DateTime LastReset { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Success rate as a percentage.
    /// </summary>
    public double SuccessRate => TotalTopicsProcessed > 0 ? (double)SuccessfullyMapped / TotalTopicsProcessed * 100 : 0;
}

/// <summary>
/// Event arguments for successful topic auto mapping.
/// </summary>
public class TopicAutoMappedEventArgs : EventArgs
{
    /// <summary>
    /// The original topic that was mapped.
    /// </summary>
    public string OriginalTopic { get; set; } = string.Empty;

    /// <summary>
    /// The UNS path the topic was mapped to.
    /// </summary>
    public string MappedPath { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level of the mapping.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The auto mapping rule that was used (if any).
    /// </summary>
    public AutoMappingRule? UsedRule { get; set; }

    /// <summary>
    /// When the mapping occurred.
    /// </summary>
    public DateTime MappedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for failed auto mapping.
/// </summary>
public class AutoMappingFailedEventArgs : EventArgs
{
    /// <summary>
    /// The topic that failed to be mapped.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the mapping failure.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Whether the topic was processed using fallback logic.
    /// </summary>
    public bool UsedFallback { get; set; }

    /// <summary>
    /// When the failure occurred.
    /// </summary>
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Suggestions that were generated (if any).
    /// </summary>
    public List<AutoMappingSuggestion> Suggestions { get; set; } = new();
}