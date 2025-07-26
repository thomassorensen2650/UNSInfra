namespace UNSInfra.Storage.InMemory;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;
public class InMemoryHistoricalStorage : IHistoricalStorage
{
    private readonly List<DataPoint> _storage = new();

    public Task StoreAsync(DataPoint dataPoint)
    {
        _storage.Add(dataPoint);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DataPoint>> GetHistoryAsync(string topic, DateTime from, DateTime to)
    {
        var results = _storage
            .Where(dp => dp.Topic == topic && dp.Timestamp >= from && dp.Timestamp <= to)
            .OrderBy(dp => dp.Timestamp)
            .ToList();
        return Task.FromResult(results.AsEnumerable());
    }

    public Task<IEnumerable<DataPoint>> GetHistoryByPathAsync(HierarchicalPath path, DateTime from, DateTime to)
    {
        var pathStr = path.GetFullPath();
        var results = _storage
            .Where(dp => dp.Path.GetFullPath().StartsWith(pathStr) && 
                         dp.Timestamp >= from && dp.Timestamp <= to)
            .OrderBy(dp => dp.Timestamp)
            .ToList();
        return Task.FromResult(results.AsEnumerable());
    }

    public Task ArchiveAsync(DateTime before)
    {
        _storage.RemoveAll(dp => dp.Timestamp < before);
        return Task.CompletedTask;
    }
}
