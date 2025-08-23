namespace UNSInfra.Storage.Abstractions;

using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;

// ==================== STORAGE ABSTRACTIONS ====================

public interface IRealtimeStorage
{
    Task StoreAsync(DataPoint dataPoint);
    Task<DataPoint?> GetLatestAsync(string topic);
    Task<IEnumerable<DataPoint>> GetLatestByPathAsync(HierarchicalPath path);
    Task<IEnumerable<string>> GetAllTopicsAsync();
    Task DeleteAsync(string id);
}
    
