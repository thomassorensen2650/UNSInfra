namespace UNSInfra.Storage.Abstractions;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;


public interface IHistoricalStorage
{
    Task StoreAsync(DataPoint dataPoint);
    Task StoreBulkAsync(IEnumerable<DataPoint> dataPoints);
    Task<IEnumerable<DataPoint>> GetHistoryAsync(string topic, DateTime from, DateTime to);
    Task<IEnumerable<DataPoint>> GetHistoryByPathAsync(HierarchicalPath path, DateTime from, DateTime to);
    Task ArchiveAsync(DateTime before);
}
