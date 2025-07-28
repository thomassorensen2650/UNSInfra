namespace UNSInfra.Storage.Abstractions;

/// <summary>
/// Interface for storage implementations that support data cleanup operations.
/// Used to manage storage size and performance by removing old data.
/// </summary>
public interface ICleanableStorage
{
    /// <summary>
    /// Removes data older than the specified cutoff time.
    /// </summary>
    /// <param name="cutoffTime">Remove data older than this time</param>
    /// <returns>A task representing the cleanup operation</returns>
    Task CleanupOldDataAsync(DateTime cutoffTime);
    
    /// <summary>
    /// Gets the count of data points that would be removed by cleanup.
    /// </summary>
    /// <param name="cutoffTime">The cutoff time for cleanup</param>
    /// <returns>Number of data points that would be removed</returns>
    Task<int> GetCleanupCountAsync(DateTime cutoffTime);
}