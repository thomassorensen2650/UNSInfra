using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Models.Configuration;
using UNSInfra.Storage.SQLite.Entities;

namespace UNSInfra.Storage.SQLite.Repositories;

/// <summary>
/// SQLite implementation of the input/output configuration repository.
/// </summary>
public class SQLiteInputOutputConfigurationRepository : IInputOutputConfigurationRepository
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;
    private readonly ILogger<SQLiteInputOutputConfigurationRepository> _logger;

    public SQLiteInputOutputConfigurationRepository(
        IDbContextFactory<UNSInfraDbContext> contextFactory,
        ILogger<SQLiteInputOutputConfigurationRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<InputOutputConfiguration>> GetAllConfigurationsAsync(
        string? serviceType = null, 
        InputOutputType? type = null, 
        bool enabledOnly = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.InputOutputConfigurations.AsQueryable();

        if (!string.IsNullOrEmpty(serviceType))
        {
            query = query.Where(e => e.ServiceType == serviceType);
        }

        if (type.HasValue)
        {
            query = query.Where(e => e.Type == type.Value.ToString());
        }

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConfiguration).Where(c => c != null)!;
    }

    public async Task<InputOutputConfiguration?> GetConfigurationByIdAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.InputOutputConfigurations.FindAsync(id);
        return entity != null ? DeserializeConfiguration(entity) : null;
    }

    public async Task<IEnumerable<InputConfiguration>> GetInputConfigurationsAsync(string serviceType, bool enabledOnly = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.InputOutputConfigurations
            .Where(e => e.ServiceType == serviceType && e.Type == InputOutputType.Input.ToString());

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConfiguration).OfType<InputConfiguration>();
    }

    public async Task<IEnumerable<OutputConfiguration>> GetOutputConfigurationsAsync(string serviceType, bool enabledOnly = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.InputOutputConfigurations
            .Where(e => e.ServiceType == serviceType && e.Type == InputOutputType.Output.ToString());

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConfiguration).OfType<OutputConfiguration>();
    }

    public async Task<IEnumerable<SocketIOInputConfiguration>> GetSocketIOInputConfigurationsAsync(bool enabledOnly = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.InputOutputConfigurations
            .Where(e => e.ServiceType == "SocketIO" && e.Type == InputOutputType.Input.ToString());

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConfiguration).OfType<SocketIOInputConfiguration>();
    }

    public async Task<IEnumerable<MqttInputConfiguration>> GetMqttInputConfigurationsAsync(bool enabledOnly = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.InputOutputConfigurations
            .Where(e => e.ServiceType == "MQTT" && e.Type == InputOutputType.Input.ToString());

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConfiguration).OfType<MqttInputConfiguration>();
    }

    public async Task<IEnumerable<MqttOutputConfiguration>> GetMqttOutputConfigurationsAsync(bool enabledOnly = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.InputOutputConfigurations
            .Where(e => e.ServiceType == "MQTT" && e.Type == InputOutputType.Output.ToString());

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConfiguration).OfType<MqttOutputConfiguration>();
    }

    public async Task SaveConfigurationAsync(InputOutputConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.Id))
        {
            configuration.Id = Guid.NewGuid().ToString();
        }

        using var context = await _contextFactory.CreateDbContextAsync();
        
        var existingEntity = await context.InputOutputConfigurations.FindAsync(configuration.Id);
        var isNew = existingEntity == null;

        if (isNew)
        {
            configuration.CreatedAt = DateTime.UtcNow;
        }
        configuration.ModifiedAt = DateTime.UtcNow;

        var entity = SerializeToEntity(configuration);

        if (isNew)
        {
            context.InputOutputConfigurations.Add(entity);
        }
        else
        {
            context.Entry(existingEntity!).CurrentValues.SetValues(entity);
        }

        await context.SaveChangesAsync();

        _logger.LogInformation("Input/Output configuration {ConfigId} was {Action}", 
            configuration.Id, isNew ? "created" : "updated");
    }

    public async Task<bool> DeleteConfigurationAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.InputOutputConfigurations.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        context.InputOutputConfigurations.Remove(entity);
        await context.SaveChangesAsync();

        _logger.LogInformation("Input/Output configuration {ConfigId} was deleted", id);
        return true;
    }

    public async Task<bool> SetConfigurationEnabledAsync(string id, bool isEnabled)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.InputOutputConfigurations.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        entity.IsEnabled = isEnabled;
        entity.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        _logger.LogInformation("Input/Output configuration {ConfigId} enabled status changed to {IsEnabled}", id, isEnabled);
        return true;
    }

    public async Task<IEnumerable<InputOutputConfiguration>> GetActiveConfigurationsAsync(
        string? serviceType = null, 
        InputOutputType? type = null)
    {
        return await GetAllConfigurationsAsync(serviceType, type, enabledOnly: true);
    }

    public async Task<IEnumerable<InputOutputConfiguration>> GetConfigurationsByConnectionIdAsync(
        string connectionId, 
        bool enabledOnly = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.InputOutputConfigurations.Where(e => e.ConnectionId == connectionId);

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConfiguration).Where(c => c != null)!;
    }

    /// <summary>
    /// Serializes a configuration to an entity for storage.
    /// </summary>
    private InputOutputConfigurationEntity SerializeToEntity(InputOutputConfiguration configuration)
    {
        var configJson = JsonSerializer.Serialize((object)configuration, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new InputOutputConfigurationEntity
        {
            Id = configuration.Id,
            Name = configuration.Name,
            Description = configuration.Description,
            IsEnabled = configuration.IsEnabled,
            Type = configuration.Type.ToString(),
            ServiceType = configuration.ServiceType,
            ConnectionId = configuration.ConnectionId,
            ConfigurationJson = configJson,
            CreatedAt = configuration.CreatedAt,
            ModifiedAt = configuration.ModifiedAt
        };
    }

    /// <summary>
    /// Deserializes an entity to a configuration object.
    /// </summary>
    private InputOutputConfiguration? DeserializeConfiguration(InputOutputConfigurationEntity entity)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            // Deserialize based on service type and configuration type
            return (entity.ServiceType.ToUpperInvariant(), entity.Type.ToUpperInvariant()) switch
            {
                ("MQTT", "INPUT") => JsonSerializer.Deserialize<MqttInputConfiguration>(entity.ConfigurationJson, options),
                ("MQTT", "OUTPUT") => JsonSerializer.Deserialize<MqttOutputConfiguration>(entity.ConfigurationJson, options),
                ("SOCKETIO", "INPUT") => JsonSerializer.Deserialize<SocketIOInputConfiguration>(entity.ConfigurationJson, options),
                _ => throw new InvalidOperationException($"Unknown service type/configuration type combination: {entity.ServiceType}/{entity.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize input/output configuration {ConfigId}", entity.Id);
            return null;
        }
    }
}