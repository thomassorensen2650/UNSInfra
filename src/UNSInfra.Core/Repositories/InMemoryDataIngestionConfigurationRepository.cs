using UNSInfra.Core.Configuration;
using UNSInfra.Core.Repositories;

namespace UNSInfra.Core.Repositories;

/// <summary>
/// In-memory implementation of the data ingestion configuration repository.
/// Stores configurations in memory with automatic change notifications.
/// </summary>
public class InMemoryDataIngestionConfigurationRepository : IDataIngestionConfigurationRepository
{
    private readonly Dictionary<string, IDataIngestionConfiguration> _configurations = new();
    private readonly object _lock = new object();

    /// <summary>
    /// Event fired when a configuration changes.
    /// </summary>
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// Gets all configurations asynchronously.
    /// </summary>
    /// <returns>List of all configurations</returns>
    public Task<List<IDataIngestionConfiguration>> GetAllConfigurationsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_configurations.Values.ToList());
        }
    }

    /// <summary>
    /// Gets a configuration by ID asynchronously.
    /// </summary>
    /// <param name="id">The configuration ID</param>
    /// <returns>The configuration if found, null otherwise</returns>
    public Task<IDataIngestionConfiguration?> GetConfigurationAsync(string id)
    {
        lock (_lock)
        {
            _configurations.TryGetValue(id, out var configuration);
            return Task.FromResult(configuration);
        }
    }

    /// <summary>
    /// Gets configurations by service type asynchronously.
    /// </summary>
    /// <param name="serviceType">The service type to filter by</param>
    /// <returns>List of configurations for the specified service type</returns>
    public Task<List<IDataIngestionConfiguration>> GetConfigurationsByTypeAsync(string serviceType)
    {
        lock (_lock)
        {
            var filtered = _configurations.Values
                .Where(c => c.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult(filtered);
        }
    }

    /// <summary>
    /// Saves a configuration asynchronously.
    /// </summary>
    /// <param name="configuration">The configuration to save</param>
    /// <returns>The saved configuration</returns>
    public Task<IDataIngestionConfiguration> SaveConfigurationAsync(IDataIngestionConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        // Validate the configuration
        var validationErrors = configuration.Validate();
        if (validationErrors.Any())
        {
            throw new ArgumentException($"Configuration validation failed: {string.Join(", ", validationErrors)}");
        }

        bool isNew;
        IDataIngestionConfiguration savedConfiguration;

        lock (_lock)
        {
            isNew = !_configurations.ContainsKey(configuration.Id);
            
            // Update timestamps
            if (isNew)
            {
                configuration.CreatedAt = DateTime.UtcNow;
            }
            configuration.ModifiedAt = DateTime.UtcNow;

            // Clone the configuration to avoid external modifications
            savedConfiguration = configuration.Clone();
            _configurations[savedConfiguration.Id] = savedConfiguration;
        }

        // Fire change event outside of lock
        var changeType = isNew ? ConfigurationChangeType.Added : ConfigurationChangeType.Updated;
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            ChangeType = changeType,
            Configuration = savedConfiguration
        });

        return Task.FromResult(savedConfiguration);
    }

    /// <summary>
    /// Deletes a configuration by ID asynchronously.
    /// </summary>
    /// <param name="id">The configuration ID to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    public Task<bool> DeleteConfigurationAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Configuration ID cannot be null or empty", nameof(id));

        bool deleted;

        lock (_lock)
        {
            deleted = _configurations.Remove(id);
        }

        if (deleted)
        {
            // Fire change event outside of lock - need to create a placeholder configuration for the event
            var placeholderConfig = new DeletedConfigurationPlaceholder { Id = id };
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
            {
                ChangeType = ConfigurationChangeType.Deleted,
                Configuration = placeholderConfig
            });
        }

        return Task.FromResult(deleted);
    }

    /// <summary>
    /// Checks if a configuration name is unique within a service type.
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <param name="serviceType">The service type</param>
    /// <param name="excludeId">Configuration ID to exclude from the check (for updates)</param>
    /// <returns>True if the name is unique</returns>
    public Task<bool> IsNameUniqueAsync(string name, string serviceType, string? excludeId = null)
    {
        lock (_lock)
        {
            var exists = _configurations.Values.Any(c => 
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                c.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase) &&
                (excludeId == null || !c.Id.Equals(excludeId, StringComparison.OrdinalIgnoreCase)));
            
            return Task.FromResult(!exists);
        }
    }

    /// <summary>
    /// Checks if a configuration with the given ID exists.
    /// </summary>
    /// <param name="id">The configuration ID</param>
    /// <returns>True if exists, false otherwise</returns>
    public Task<bool> ConfigurationExistsAsync(string id)
    {
        lock (_lock)
        {
            return Task.FromResult(_configurations.ContainsKey(id));
        }
    }

    /// <summary>
    /// Gets all enabled configurations asynchronously.
    /// </summary>
    /// <returns>List of enabled configurations</returns>
    public Task<List<IDataIngestionConfiguration>> GetEnabledConfigurationsAsync()
    {
        lock (_lock)
        {
            var enabled = _configurations.Values.Where(c => c.Enabled).ToList();
            return Task.FromResult(enabled);
        }
    }

    /// <summary>
    /// Validates all configurations and returns any with validation errors.
    /// </summary>
    /// <returns>Dictionary of configuration ID to validation errors</returns>
    public Task<Dictionary<string, List<string>>> ValidateAllConfigurationsAsync()
    {
        lock (_lock)
        {
            var validationResults = new Dictionary<string, List<string>>();

            foreach (var config in _configurations.Values)
            {
                var errors = config.Validate();
                if (errors.Any())
                {
                    validationResults[config.Id] = errors;
                }
            }

            return Task.FromResult(validationResults);
        }
    }
}

/// <summary>
/// Placeholder configuration used for delete events.
/// </summary>
internal class DeletedConfigurationPlaceholder : IDataIngestionConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Deleted Configuration";
    public string? Description { get; set; } = "This configuration has been deleted";
    public bool Enabled { get; set; } = false;
    public string ServiceType => "Deleted";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public Dictionary<string, object> Metadata { get; set; } = new();

    public List<string> Validate() => new();
    public IDataIngestionConfiguration Clone() => new DeletedConfigurationPlaceholder { Id = this.Id };
    public IDataIngestionConfiguration CloneAsNew() => new DeletedConfigurationPlaceholder { Id = Guid.NewGuid().ToString() };
}