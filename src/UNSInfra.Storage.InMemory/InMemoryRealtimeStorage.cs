namespace UNSInfra.Storage.InMemory;

using System.Collections.Concurrent;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;

// ==================== STORAGE IMPLEMENTATIONS ====================
public class InMemoryRealtimeStorage : IRealtimeStorage
{
    private readonly ConcurrentDictionary<string, DataPoint> _storage = new();

    public Task StoreAsync(DataPoint dataPoint)
    {
        _storage.AddOrUpdate(dataPoint.Topic, dataPoint, (key, existing) => dataPoint);
        return Task.CompletedTask;
    }

    public Task<DataPoint?> GetLatestAsync(string topic)
    {
        _storage.TryGetValue(topic, out var dataPoint);
        return Task.FromResult(dataPoint);
    }

    public Task<IEnumerable<DataPoint>> GetLatestByPathAsync(HierarchicalPath path)
    {
        var pathStr = path.GetFullPath();
        // Create a snapshot to avoid collection modified exceptions
        var results = _storage.Values.ToList()
            .Where(dp => dp.Path.GetFullPath().StartsWith(pathStr))
            .ToList();
        return Task.FromResult(results.AsEnumerable());
    }

    public Task DeleteAsync(string id)
    {
        var item = _storage.ToList().FirstOrDefault(kvp => kvp.Value.Id == id);
        if (!item.Equals(default))
        {
            _storage.TryRemove(item.Key, out _);
        }
        return Task.CompletedTask;
    }
}