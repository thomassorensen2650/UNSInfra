using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Configuration;
using UNSInfra.Core.Repositories;
using UNSInfra.Services.SocketIO.Configuration;
using UNSInfra.Services.V1.Configuration;
using UNSInfra.Storage.SQLite.Entities;

namespace UNSInfra.Storage.SQLite.Repositories;

/// <summary>
/// SQLite implementation of the data ingestion configuration repository.
/// </summary>
public class SQLiteDataIngestionConfigurationRepository : IDataIngestionConfigurationRepository
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;
    private readonly ILogger<SQLiteDataIngestionConfigurationRepository> _logger;

    /// <summary>
    /// Event raised when a configuration changes.
    /// </summary>
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public SQLiteDataIngestionConfigurationRepository(
        IDbContextFactory<UNSInfraDbContext> contextFactory,
        ILogger<SQLiteDataIngestionConfigurationRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<IDataIngestionConfiguration>> GetAllConfigurationsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entities = await context.DataIngestionConfigurations.ToListAsync();
        var configurations = new List<IDataIngestionConfiguration>();

        foreach (var entity in entities)
        {
            try
            {
                var configuration = DeserializeConfiguration(entity);
                if (configuration != null)
                {
                    configurations.Add(configuration);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize configuration {ConfigId}", entity.Id);
            }
        }

        return configurations;
    }

    /// <inheritdoc />
    public async Task<IDataIngestionConfiguration?> GetConfigurationAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.DataIngestionConfigurations.FindAsync(id);
        if (entity == null)
        {
            return null;
        }

        try
        {
            return DeserializeConfiguration(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize configuration {ConfigId}", id);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IDataIngestionConfiguration?> GetConfigurationByIdAsync(string id)
    {
        return await GetConfigurationAsync(id);
    }

    /// <inheritdoc />
    public async Task<IDataIngestionConfiguration> SaveConfigurationAsync(IDataIngestionConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        // Validate the configuration
        var validationErrors = configuration.Validate();
        if (validationErrors.Any())
        {
            throw new ArgumentException($"Configuration validation failed: {string.Join(", ", validationErrors)}");
        }

        using var context = await _contextFactory.CreateDbContextAsync();
        
        var existingEntity = await context.DataIngestionConfigurations.FindAsync(configuration.Id);
        var isNew = existingEntity == null;

        if (isNew)
        {
            configuration.CreatedAt = DateTime.UtcNow;
        }
        configuration.ModifiedAt = DateTime.UtcNow;

        var entity = SerializeToEntity(configuration);

        if (isNew)
        {
            context.DataIngestionConfigurations.Add(entity);
        }
        else
        {
            context.Entry(existingEntity!).CurrentValues.SetValues(entity);
        }

        await context.SaveChangesAsync();

        // Fire change event
        var changeType = isNew ? ConfigurationChangeType.Added : ConfigurationChangeType.Updated;
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            ChangeType = changeType,
            Configuration = configuration,
            PreviousConfiguration = null // Could store the old version for updates if needed
        });

        _logger.LogInformation("Data ingestion configuration {ConfigId} was {Action}", 
            configuration.Id, isNew ? "created" : "updated");

        return configuration;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConfigurationAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.DataIngestionConfigurations.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        context.DataIngestionConfigurations.Remove(entity);
        await context.SaveChangesAsync();

        // Fire change event (create a dummy configuration for the event since we don't have the full object anymore)
        var dummyConfig = new DeletedConfigurationPlaceholder { Id = id };
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            ChangeType = ConfigurationChangeType.Deleted,
            Configuration = dummyConfig,
            PreviousConfiguration = null
        });

        _logger.LogInformation("Data ingestion configuration {ConfigId} was deleted", id);
        return true;
    }

    /// <inheritdoc />
    public async Task<List<IDataIngestionConfiguration>> GetConfigurationsByTypeAsync(string serviceType)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entities = await context.DataIngestionConfigurations
            .Where(e => e.ServiceType == serviceType)
            .ToListAsync();
        
        var configurations = new List<IDataIngestionConfiguration>();

        foreach (var entity in entities)
        {
            try
            {
                var configuration = DeserializeConfiguration(entity);
                if (configuration != null)
                {
                    configurations.Add(configuration);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize configuration {ConfigId}", entity.Id);
            }
        }

        return configurations;
    }

    /// <inheritdoc />
    public async Task<List<IDataIngestionConfiguration>> GetEnabledConfigurationsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entities = await context.DataIngestionConfigurations
            .Where(e => e.Enabled)
            .ToListAsync();
        
        var configurations = new List<IDataIngestionConfiguration>();

        foreach (var entity in entities)
        {
            try
            {
                var configuration = DeserializeConfiguration(entity);
                if (configuration != null)
                {
                    configurations.Add(configuration);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize configuration {ConfigId}", entity.Id);
            }
        }

        return configurations;
    }

    /// <inheritdoc />
    public async Task<bool> IsNameUniqueAsync(string name, string serviceType, string? excludeId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.DataIngestionConfigurations
            .Where(e => e.Name == name && e.ServiceType == serviceType);
        
        if (!string.IsNullOrEmpty(excludeId))
        {
            query = query.Where(e => e.Id != excludeId);
        }
        
        return !await query.AnyAsync();
    }

    /// <summary>
    /// Serializes a configuration to an entity for storage.
    /// </summary>
    private DataIngestionConfigurationEntity SerializeToEntity(IDataIngestionConfiguration configuration)
    {
        var configJson = JsonSerializer.Serialize((object)configuration, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var metadataJson = configuration.Metadata.Any() 
            ? JsonSerializer.Serialize((object)configuration.Metadata)
            : null;

        return new DataIngestionConfigurationEntity
        {
            Id = configuration.Id,
            Name = configuration.Name,
            Description = configuration.Description,
            ServiceType = configuration.ServiceType,
            Enabled = configuration.Enabled,
            ConfigurationJson = configJson,
            CreatedAt = configuration.CreatedAt,
            ModifiedAt = configuration.ModifiedAt,
            CreatedBy = configuration.CreatedBy,
            Metadata = metadataJson
        };
    }

    /// <summary>
    /// Deserializes an entity to a configuration object.
    /// </summary>
    private IDataIngestionConfiguration? DeserializeConfiguration(DataIngestionConfigurationEntity entity)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Deserialize based on service type
        return entity.ServiceType.ToUpperInvariant() switch
        {
            "SOCKETIO" => JsonSerializer.Deserialize<SocketIODataIngestionConfiguration>(entity.ConfigurationJson, options),
            "MQTT" => JsonSerializer.Deserialize<MqttDataIngestionConfiguration>(entity.ConfigurationJson, options),
            _ => throw new InvalidOperationException($"Unknown service type: {entity.ServiceType}")
        };
    }
}

/// <summary>
/// Placeholder configuration for deleted items.
/// </summary>
internal class DeletedConfigurationPlaceholder : IDataIngestionConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "[DELETED]";
    public string? Description { get; set; } = "Deleted configuration";
    public string ServiceType { get; set; } = "Unknown";
    public bool Enabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public Dictionary<string, object> Metadata { get; set; } = new();

    public List<string> Validate() => new();
    public IDataIngestionConfiguration Clone() => new DeletedConfigurationPlaceholder { Id = this.Id };
    public IDataIngestionConfiguration CloneAsNew() => new DeletedConfigurationPlaceholder { Id = Guid.NewGuid().ToString() };
}