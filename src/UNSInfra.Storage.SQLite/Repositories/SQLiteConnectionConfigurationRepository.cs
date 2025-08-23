using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UNSInfra.Core.Repositories;
using UNSInfra.Models;
using UNSInfra.Storage.SQLite.Entities;

namespace UNSInfra.Storage.SQLite.Repositories;

/// <summary>
/// SQLite implementation of the connection configuration repository.
/// </summary>
public class SQLiteConnectionConfigurationRepository : IConnectionConfigurationRepository
{
    private readonly IDbContextFactory<UNSInfraDbContext> _contextFactory;
    private readonly ILogger<SQLiteConnectionConfigurationRepository> _logger;

    public SQLiteConnectionConfigurationRepository(
        IDbContextFactory<UNSInfraDbContext> contextFactory,
        ILogger<SQLiteConnectionConfigurationRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<ConnectionConfiguration>> GetAllConnectionsAsync(bool enabledOnly = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.ConnectionConfigurations.AsQueryable();

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConnection).Where(c => c != null)!;
    }

    public async Task<ConnectionConfiguration?> GetConnectionByIdAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.ConnectionConfigurations.FindAsync(id);
        return entity != null ? DeserializeConnection(entity) : null;
    }

    public async Task<IEnumerable<ConnectionConfiguration>> GetConnectionsByTypeAsync(string connectionType, bool enabledOnly = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.ConnectionConfigurations
            .Where(e => e.ConnectionType == connectionType);

        if (enabledOnly)
        {
            query = query.Where(e => e.IsEnabled);
        }

        var entities = await query.OrderBy(e => e.Name).ToListAsync();
        return entities.Select(DeserializeConnection).Where(c => c != null)!;
    }

    public async Task SaveConnectionAsync(ConnectionConfiguration connection)
    {
        if (string.IsNullOrEmpty(connection.Id))
        {
            connection.Id = Guid.NewGuid().ToString();
        }

        using var context = await _contextFactory.CreateDbContextAsync();
        
        var existingEntity = await context.ConnectionConfigurations.FindAsync(connection.Id);
        var isNew = existingEntity == null;

        if (isNew)
        {
            connection.CreatedAt = DateTime.UtcNow;
        }
        connection.ModifiedAt = DateTime.UtcNow;

        var entity = SerializeToEntity(connection);

        if (isNew)
        {
            context.ConnectionConfigurations.Add(entity);
        }
        else
        {
            context.Entry(existingEntity!).CurrentValues.SetValues(entity);
        }

        await context.SaveChangesAsync();

        _logger.LogInformation("Connection configuration {ConnectionId} was {Action}", 
            connection.Id, isNew ? "created" : "updated");
    }

    public async Task<bool> DeleteConnectionAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.ConnectionConfigurations.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        context.ConnectionConfigurations.Remove(entity);
        await context.SaveChangesAsync();

        _logger.LogInformation("Connection configuration {ConnectionId} was deleted", id);
        return true;
    }

    public async Task<bool> SetConnectionEnabledAsync(string id, bool isEnabled)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.ConnectionConfigurations.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        entity.IsEnabled = isEnabled;
        entity.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        _logger.LogInformation("Connection configuration {ConnectionId} enabled status changed to {IsEnabled}", id, isEnabled);
        return true;
    }

    public async Task<IEnumerable<ConnectionConfiguration>> GetAutoStartConnectionsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var entities = await context.ConnectionConfigurations
            .Where(e => e.IsEnabled && e.AutoStart)
            .OrderBy(e => e.Name)
            .ToListAsync();

        return entities.Select(DeserializeConnection).Where(c => c != null)!;
    }

    /// <summary>
    /// Serializes a connection configuration to an entity for storage.
    /// </summary>
    private ConnectionConfigurationEntity SerializeToEntity(ConnectionConfiguration connection)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return new ConnectionConfigurationEntity
        {
            Id = connection.Id,
            Name = connection.Name,
            ConnectionType = connection.ConnectionType,
            IsEnabled = connection.IsEnabled,
            AutoStart = connection.AutoStart,
            ConnectionConfigJson = JsonSerializer.Serialize(connection.ConnectionConfig, options),
            InputsJson = JsonSerializer.Serialize(connection.Inputs, options),
            OutputsJson = JsonSerializer.Serialize(connection.Outputs, options),
            TagsJson = JsonSerializer.Serialize(connection.Tags, options),
            Description = connection.Description,
            CreatedAt = connection.CreatedAt,
            ModifiedAt = connection.ModifiedAt,
            MetadataJson = JsonSerializer.Serialize(connection.Metadata, options)
        };
    }

    /// <summary>
    /// Deserializes an entity to a connection configuration object.
    /// </summary>
    private ConnectionConfiguration? DeserializeConnection(ConnectionConfigurationEntity entity)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var connectionConfig = DeserializeConnectionConfigForType(entity.ConnectionConfigJson, entity.ConnectionType, options);

            var inputs = DeserializeInputsForConnectionType(entity.InputsJson, entity.ConnectionType, options);
            var outputs = DeserializeOutputsForConnectionType(entity.OutputsJson, entity.ConnectionType, options);

            var tags = string.IsNullOrEmpty(entity.TagsJson) 
                ? new List<string>() 
                : JsonSerializer.Deserialize<List<string>>(entity.TagsJson, options) ?? new List<string>();

            var metadata = string.IsNullOrEmpty(entity.MetadataJson) 
                ? new Dictionary<string, object>() 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.MetadataJson, options) ?? new Dictionary<string, object>();

            return new ConnectionConfiguration
            {
                Id = entity.Id,
                Name = entity.Name,
                ConnectionType = entity.ConnectionType,
                IsEnabled = entity.IsEnabled,
                AutoStart = entity.AutoStart,
                ConnectionConfig = connectionConfig,
                Inputs = inputs,
                Outputs = outputs,
                Tags = tags,
                Description = entity.Description,
                CreatedAt = entity.CreatedAt,
                ModifiedAt = entity.ModifiedAt,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize connection configuration {ConnectionId}", entity.Id);
            return null;
        }
    }

    /// <summary>
    /// Deserializes connection configuration for a specific connection type to the correct strongly-typed object.
    /// </summary>
    private object DeserializeConnectionConfigForType(string connectionConfigJson, string connectionType, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(connectionConfigJson))
            return new object();

        try
        {
            return connectionType.ToLowerInvariant() switch
            {
                "mqtt" => JsonSerializer.Deserialize<UNSInfra.Services.V1.Models.MqttConnectionConfiguration>(connectionConfigJson, options) ?? new object(),
                "socketio" => JsonSerializer.Deserialize<UNSInfra.Services.SocketIO.Models.SocketIOConnectionConfiguration>(connectionConfigJson, options) ?? new object(),
                _ => JsonSerializer.Deserialize<object>(connectionConfigJson, options) ?? new object()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize connection config for connection type {ConnectionType}", connectionType);
            return new object();
        }
    }

    /// <summary>
    /// Deserializes inputs for a specific connection type to the correct strongly-typed objects.
    /// </summary>
    private List<object> DeserializeInputsForConnectionType(string inputsJson, string connectionType, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(inputsJson))
            return new List<object>();

        try
        {
            return connectionType.ToLowerInvariant() switch
            {
                "socketio" => JsonSerializer.Deserialize<List<UNSInfra.Services.SocketIO.Models.SocketIOInputConfiguration>>(inputsJson, options)?
                    .Cast<object>().ToList() ?? new List<object>(),
                "mqtt" => JsonSerializer.Deserialize<List<UNSInfra.Services.V1.Models.MqttInputConfiguration>>(inputsJson, options)?
                    .Cast<object>().ToList() ?? new List<object>(),
                _ => new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize inputs for connection type {ConnectionType}", connectionType);
            return new List<object>();
        }
    }

    /// <summary>
    /// Deserializes outputs for a specific connection type to the correct strongly-typed objects.
    /// </summary>
    private List<object> DeserializeOutputsForConnectionType(string outputsJson, string connectionType, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(outputsJson))
            return new List<object>();

        try
        {
            return connectionType.ToLowerInvariant() switch
            {
                "socketio" => JsonSerializer.Deserialize<List<UNSInfra.Services.SocketIO.Models.SocketIOOutputConfiguration>>(outputsJson, options)?
                    .Cast<object>().ToList() ?? new List<object>(),
                "mqtt" => JsonSerializer.Deserialize<List<UNSInfra.Services.V1.Models.MqttOutputConfiguration>>(outputsJson, options)?
                    .Cast<object>().ToList() ?? new List<object>(),
                _ => new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize outputs for connection type {ConnectionType}", connectionType);
            return new List<object>();
        }
    }
}