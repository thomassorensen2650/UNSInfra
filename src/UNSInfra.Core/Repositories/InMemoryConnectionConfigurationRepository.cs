using UNSInfra.Models;

namespace UNSInfra.Core.Repositories;

/// <summary>
/// In-memory implementation of connection configuration repository for development and testing
/// </summary>
public class InMemoryConnectionConfigurationRepository : IConnectionConfigurationRepository
{
    private readonly Dictionary<string, ConnectionConfiguration> _connections = new();
    private readonly object _lock = new();

    public Task<IEnumerable<ConnectionConfiguration>> GetAllConnectionsAsync(bool enabledOnly = false)
    {
        lock (_lock)
        {
            var query = _connections.Values.AsEnumerable();

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<ConnectionConfiguration>>(query.OrderBy(c => c.Name));
        }
    }

    public Task<ConnectionConfiguration?> GetConnectionByIdAsync(string id)
    {
        lock (_lock)
        {
            _connections.TryGetValue(id, out var connection);
            return Task.FromResult(connection);
        }
    }

    public Task<IEnumerable<ConnectionConfiguration>> GetConnectionsByTypeAsync(string connectionType, bool enabledOnly = false)
    {
        lock (_lock)
        {
            var query = _connections.Values
                .Where(c => c.ConnectionType.Equals(connectionType, StringComparison.OrdinalIgnoreCase));

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<ConnectionConfiguration>>(query.OrderBy(c => c.Name));
        }
    }

    public Task SaveConnectionAsync(ConnectionConfiguration connection)
    {
        if (string.IsNullOrEmpty(connection.Id))
        {
            connection.Id = Guid.NewGuid().ToString();
        }

        connection.ModifiedAt = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_connections.ContainsKey(connection.Id))
            {
                connection.CreatedAt = DateTime.UtcNow;
            }

            _connections[connection.Id] = connection;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteConnectionAsync(string id)
    {
        lock (_lock)
        {
            return Task.FromResult(_connections.Remove(id));
        }
    }

    public Task<bool> SetConnectionEnabledAsync(string id, bool isEnabled)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(id, out var connection))
            {
                connection.IsEnabled = isEnabled;
                connection.ModifiedAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    public Task<IEnumerable<ConnectionConfiguration>> GetAutoStartConnectionsAsync()
    {
        lock (_lock)
        {
            var autoStartConnections = _connections.Values
                .Where(c => c.IsEnabled && c.AutoStart)
                .OrderBy(c => c.Name);

            return Task.FromResult<IEnumerable<ConnectionConfiguration>>(autoStartConnections);
        }
    }
}