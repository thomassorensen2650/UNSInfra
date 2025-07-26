namespace UNSInfra.Storage.InMemory;

using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Storage.Abstractions;

// ==================== STORAGE IMPLEMENTATIONS ====================
public class InMemoryRealtimeStorage : IRealtimeStorage
{
    private readonly Dictionary<string, DataPoint> _storage = new();

    public Task StoreAsync(DataPoint dataPoint)
    {
        _storage[dataPoint.Topic] = dataPoint;
        return Task.CompletedTask;
    }

    public Task<DataPoint> GetLatestAsync(string topic)
    {
        _storage.TryGetValue(topic, out var dataPoint);
        return Task.FromResult(dataPoint);
    }

    public Task<IEnumerable<DataPoint>> GetLatestByPathAsync(HierarchicalPath path)
    {
        var pathStr = path.GetFullPath();
        var results = _storage.Values
            .Where(dp => dp.Path.GetFullPath().StartsWith(pathStr))
            .ToList();
        return Task.FromResult(results.AsEnumerable());
    }

    public Task DeleteAsync(string id)
    {
        var item = _storage.FirstOrDefault(kvp => kvp.Value.Id == id);
        if (!item.Equals(default))
        {
            _storage.Remove(item.Key);
        }
        return Task.CompletedTask;
    }
}