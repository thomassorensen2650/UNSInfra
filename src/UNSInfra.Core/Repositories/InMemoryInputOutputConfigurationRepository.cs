using UNSInfra.Models.Configuration;

namespace UNSInfra.Core.Repositories;

/// <summary>
/// In-memory implementation of input/output configuration repository for development and testing
/// </summary>
public class InMemoryInputOutputConfigurationRepository : IInputOutputConfigurationRepository
{
    private readonly Dictionary<string, InputOutputConfiguration> _configurations = new();
    private readonly object _lock = new();

    public Task<IEnumerable<InputOutputConfiguration>> GetAllConfigurationsAsync(
        string? serviceType = null, 
        InputOutputType? type = null, 
        bool enabledOnly = false)
    {
        lock (_lock)
        {
            var query = _configurations.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(serviceType))
            {
                query = query.Where(c => c.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase));
            }

            if (type.HasValue)
            {
                query = query.Where(c => c.Type == type.Value);
            }

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<InputOutputConfiguration>>(query.OrderBy(c => c.Name));
        }
    }

    public Task<InputOutputConfiguration?> GetConfigurationByIdAsync(string id)
    {
        lock (_lock)
        {
            _configurations.TryGetValue(id, out var configuration);
            return Task.FromResult(configuration);
        }
    }

    public Task<IEnumerable<InputConfiguration>> GetInputConfigurationsAsync(string serviceType, bool enabledOnly = true)
    {
        lock (_lock)
        {
            var query = _configurations.Values
                .OfType<InputConfiguration>()
                .Where(c => c.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase));

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<InputConfiguration>>(query.OrderBy(c => c.Name));
        }
    }

    public Task<IEnumerable<OutputConfiguration>> GetOutputConfigurationsAsync(string serviceType, bool enabledOnly = true)
    {
        lock (_lock)
        {
            var query = _configurations.Values
                .OfType<OutputConfiguration>()
                .Where(c => c.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase));

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<OutputConfiguration>>(query.OrderBy(c => c.Name));
        }
    }

    public Task<IEnumerable<SocketIOInputConfiguration>> GetSocketIOInputConfigurationsAsync(bool enabledOnly = true)
    {
        lock (_lock)
        {
            var query = _configurations.Values.OfType<SocketIOInputConfiguration>();

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<SocketIOInputConfiguration>>(query.OrderBy(c => c.Name));
        }
    }

    public Task<IEnumerable<MqttInputConfiguration>> GetMqttInputConfigurationsAsync(bool enabledOnly = true)
    {
        lock (_lock)
        {
            var query = _configurations.Values.OfType<MqttInputConfiguration>();

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<MqttInputConfiguration>>(query.OrderBy(c => c.Name));
        }
    }

    public Task<IEnumerable<MqttOutputConfiguration>> GetMqttOutputConfigurationsAsync(bool enabledOnly = true)
    {
        lock (_lock)
        {
            var query = _configurations.Values.OfType<MqttOutputConfiguration>();

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<MqttOutputConfiguration>>(query.OrderBy(c => c.Name));
        }
    }

    public Task SaveConfigurationAsync(InputOutputConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.Id))
        {
            configuration.Id = Guid.NewGuid().ToString();
        }

        configuration.ModifiedAt = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_configurations.ContainsKey(configuration.Id))
            {
                configuration.CreatedAt = DateTime.UtcNow;
            }

            _configurations[configuration.Id] = configuration;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteConfigurationAsync(string id)
    {
        lock (_lock)
        {
            return Task.FromResult(_configurations.Remove(id));
        }
    }

    public Task<bool> SetConfigurationEnabledAsync(string id, bool isEnabled)
    {
        lock (_lock)
        {
            if (_configurations.TryGetValue(id, out var configuration))
            {
                configuration.IsEnabled = isEnabled;
                configuration.ModifiedAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    public Task<IEnumerable<InputOutputConfiguration>> GetActiveConfigurationsAsync(
        string? serviceType = null, 
        InputOutputType? type = null)
    {
        return GetAllConfigurationsAsync(serviceType, type, enabledOnly: true);
    }

    public Task<IEnumerable<InputOutputConfiguration>> GetConfigurationsByConnectionIdAsync(
        string connectionId, 
        bool enabledOnly = false)
    {
        lock (_lock)
        {
            var query = _configurations.Values
                .Where(c => c.ConnectionId == connectionId);

            if (enabledOnly)
            {
                query = query.Where(c => c.IsEnabled);
            }

            return Task.FromResult<IEnumerable<InputOutputConfiguration>>(query.OrderBy(c => c.Name));
        }
    }
}