namespace UNSInfra.Configuration;

/// <summary>
/// Configuration options for historical data storage
/// </summary>
public class HistoricalStorageConfiguration
{
    /// <summary>
    /// Gets or sets whether historical storage is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the type of historical storage to use
    /// </summary>
    public HistoricalStorageType StorageType { get; set; } = HistoricalStorageType.InMemory;

    /// <summary>
    /// Gets or sets the connection string for database-based storage types
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets configuration options for InMemory historical storage
    /// </summary>
    public InMemoryHistoricalStorageOptions InMemory { get; set; } = new();

    /// <summary>
    /// Gets or sets configuration options for SQLite historical storage
    /// </summary>
    public SQLiteHistoricalStorageOptions SQLite { get; set; } = new();
}

/// <summary>
/// Types of historical storage available
/// </summary>
public enum HistoricalStorageType
{
    /// <summary>
    /// In-memory storage (data lost on restart)
    /// </summary>
    InMemory,
    
    /// <summary>
    /// SQLite database storage
    /// </summary>
    SQLite,
    
    /// <summary>
    /// Disabled - no historical storage
    /// </summary>
    None
}

/// <summary>
/// Configuration options specific to InMemory historical storage
/// </summary>
public class InMemoryHistoricalStorageOptions
{
    /// <summary>
    /// Gets or sets the maximum number of historical values to store per data point
    /// Default is 1000
    /// </summary>
    public int MaxValuesPerDataPoint { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum total number of historical values to store across all data points
    /// Default is 100,000. Set to -1 for unlimited.
    /// </summary>
    public int MaxTotalValues { get; set; } = 100000;

    /// <summary>
    /// Gets or sets whether to automatically clean up old values when limits are reached
    /// Default is true (FIFO - oldest values are removed first)
    /// </summary>
    public bool AutoCleanup { get; set; } = true;
}

/// <summary>
/// Configuration options specific to SQLite historical storage
/// </summary>
public class SQLiteHistoricalStorageOptions
{
    /// <summary>
    /// Gets or sets the database file path for SQLite storage
    /// If not specified, uses the connection string from HistoricalStorageConfiguration
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// Gets or sets whether to enable Write-Ahead Logging for better performance
    /// Default is true
    /// </summary>
    public bool EnableWAL { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of days to retain historical data
    /// Default is 365 days. Set to -1 for unlimited retention.
    /// </summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets whether to automatically clean up expired data
    /// Default is true
    /// </summary>
    public bool AutoCleanup { get; set; } = true;
}