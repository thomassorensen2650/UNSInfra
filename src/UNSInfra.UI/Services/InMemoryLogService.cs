using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace UNSInfra.UI.Services;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public string? Source { get; set; }
}

public interface IInMemoryLogService
{
    IEnumerable<LogEntry> GetLogs();
    IEnumerable<LogEntry> SearchLogs(string searchTerm, LogLevel? minLevel = null, DateTime? fromDate = null, DateTime? toDate = null);
    void ClearLogs();
    event EventHandler<LogEntry>? LogAdded;
}

public class InMemoryLogService : IInMemoryLogService
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();
    private readonly int _maxLogEntries = 10000;

    public event EventHandler<LogEntry>? LogAdded;

    public void AddLog(LogEntry logEntry)
    {
        _logs.Enqueue(logEntry);
        
        // Keep only the most recent logs
        while (_logs.Count > _maxLogEntries)
        {
            _logs.TryDequeue(out _);
        }
        
        LogAdded?.Invoke(this, logEntry);
    }

    public IEnumerable<LogEntry> GetLogs()
    {
        return _logs.ToArray().Reverse();
    }

    public IEnumerable<LogEntry> SearchLogs(string searchTerm, LogLevel? minLevel = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var logs = _logs.ToArray().AsEnumerable();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLowerInvariant();
            logs = logs.Where(log => 
                log.Message.ToLowerInvariant().Contains(lowerSearch) ||
                log.Category.ToLowerInvariant().Contains(lowerSearch) ||
                (log.Source?.ToLowerInvariant().Contains(lowerSearch) ?? false));
        }
        
        if (minLevel.HasValue)
        {
            logs = logs.Where(log => log.Level >= minLevel.Value);
        }
        
        if (fromDate.HasValue)
        {
            logs = logs.Where(log => log.Timestamp >= fromDate.Value);
        }
        
        if (toDate.HasValue)
        {
            logs = logs.Where(log => log.Timestamp <= toDate.Value);
        }
        
        return logs.OrderByDescending(log => log.Timestamp);
    }

    public void ClearLogs()
    {
        while (_logs.TryDequeue(out _)) { }
    }
}

public class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly InMemoryLogService _logService;

    public InMemoryLoggerProvider(InMemoryLogService logService)
    {
        _logService = logService;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new InMemoryLogger(categoryName, _logService);
    }

    public void Dispose()
    {
        // No cleanup needed
    }
}

public class InMemoryLogger : ILogger
{
    private readonly string _categoryName;
    private readonly InMemoryLogService _logService;

    public InMemoryLogger(string categoryName, InMemoryLogService logService)
    {
        _categoryName = categoryName;
        _logService = logService;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Debug;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var logEntry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = logLevel,
            Category = _categoryName,
            Message = formatter(state, exception),
            Exception = exception,
            Source = ExtractSourceFromCategory(_categoryName)
        };

        _logService.AddLog(logEntry);
    }

    private static string ExtractSourceFromCategory(string category)
    {
        // Extract meaningful source names from category
        if (category.StartsWith("UNSInfra."))
        {
            var parts = category.Split('.');
            if (parts.Length >= 3)
            {
                return parts[1] + "." + parts[2]; // e.g., "Services.V1" from "UNSInfra.Services.V1.MqttDataService"
            }
            return parts.Length > 1 ? parts[1] : category;
        }
        
        // For Microsoft/System logs, just use the last part
        var lastDot = category.LastIndexOf('.');
        return lastDot > 0 ? category.Substring(lastDot + 1) : category;
    }
}