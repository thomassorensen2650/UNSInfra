namespace UNSInfra.Core.Configuration;

/// <summary>
/// Configuration options for storage providers.
/// </summary>
public class StorageConfiguration
{
    /// <summary>
    /// The storage provider to use for historical data (SQLite, InMemory).
    /// IRealtimeStorage is always InMemory for performance.
    /// </summary>
    public string Provider { get; set; } = "SQLite";

    /// <summary>
    /// Connection string for the storage provider. If empty, uses default paths.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Whether to enable WAL mode for SQLite (recommended for better concurrency).
    /// </summary>
    public bool EnableWalMode { get; set; } = true;

    /// <summary>
    /// Command timeout in seconds for storage operations.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Cache size for SQLite in pages (negative values are KB).
    /// </summary>
    public int CacheSize { get; set; } = 1000;
}