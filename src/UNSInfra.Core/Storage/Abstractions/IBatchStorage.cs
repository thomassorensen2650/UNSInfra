using UNSInfra.Models.Data;

namespace UNSInfra.Storage.Abstractions;

/// <summary>
/// Interface for storage implementations that support batch operations.
/// Used to improve performance when storing large numbers of data points.
/// </summary>
public interface IBatchStorage
{
    /// <summary>
    /// Stores multiple data points in a single operation for better performance.
    /// </summary>
    /// <param name="dataPoints">Collection of data points to store</param>
    /// <returns>A task representing the batch storage operation</returns>
    Task StoreBatchAsync(IEnumerable<DataPoint> dataPoints);
}