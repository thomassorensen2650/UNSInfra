using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;

namespace UNSInfra.Storage.InMemory;

/// <summary>
/// No-operation implementation of IHistoricalStorage that discards all data
/// Used when historical storage is disabled
/// </summary>
public class NoOpHistoricalStorage : IHistoricalStorage
{
    public Task StoreAsync(DataPoint dataPoint)
    {
        // No-op: Discard the data point
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DataPoint>> GetHistoryAsync(string topic, DateTime from, DateTime to)
    {
        // No-op: Return empty collection
        return Task.FromResult(Enumerable.Empty<DataPoint>());
    }

    public Task<IEnumerable<DataPoint>> GetHistoryByPathAsync(HierarchicalPath path, DateTime from, DateTime to)
    {
        // No-op: Return empty collection
        return Task.FromResult(Enumerable.Empty<DataPoint>());
    }

    public Task ArchiveAsync(DateTime before)
    {
        // No-op: Nothing to clean up
        return Task.CompletedTask;
    }
}