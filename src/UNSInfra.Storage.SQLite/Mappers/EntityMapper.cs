using System.Text.Json;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Models.Schema;
using UNSInfra.Models.Data;
using UNSInfra.Models.Namespace;
using UNSInfra.Storage.SQLite.Entities;

namespace UNSInfra.Storage.SQLite.Mappers;

/// <summary>
/// Maps between domain models and SQLite entities.
/// </summary>
public static class EntityMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Maps a HierarchyConfiguration to HierarchyConfigurationEntity.
    /// </summary>
    public static HierarchyConfigurationEntity ToEntity(this HierarchyConfiguration config)
    {
        return new HierarchyConfigurationEntity
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            IsActive = config.IsActive,
            IsSystemDefined = config.IsSystemDefined,
            CreatedAt = config.CreatedAt,
            ModifiedAt = config.ModifiedAt,
            Nodes = config.Nodes.Select(n => n.ToEntity(config.Id)).ToList()
        };
    }

    /// <summary>
    /// Maps a HierarchyConfigurationEntity to HierarchyConfiguration.
    /// </summary>
    public static HierarchyConfiguration ToModel(this HierarchyConfigurationEntity entity)
    {
        return new HierarchyConfiguration
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            IsSystemDefined = entity.IsSystemDefined,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            Nodes = entity.Nodes.Select(n => n.ToModel()).OrderBy(n => n.Order).ToList()
        };
    }

    /// <summary>
    /// Maps a HierarchyNode to HierarchyNodeEntity.
    /// </summary>
    public static HierarchyNodeEntity ToEntity(this HierarchyNode node, string configId)
    {
        return new HierarchyNodeEntity
        {
            Id = node.Id,
            Name = node.Name,
            IsRequired = node.IsRequired,
            Order = node.Order,
            Description = node.Description,
            ParentNodeId = node.ParentNodeId,
            HierarchyConfigurationId = configId,
            MetadataJson = JsonSerializer.Serialize(node.Metadata, JsonOptions),
            AllowedChildNodeIdsJson = JsonSerializer.Serialize(node.AllowedChildNodeIds, JsonOptions)
        };
    }

    /// <summary>
    /// Maps a HierarchyNodeEntity to HierarchyNode.
    /// </summary>
    public static HierarchyNode ToModel(this HierarchyNodeEntity entity)
    {
        return new HierarchyNode
        {
            Id = entity.Id,
            Name = entity.Name,
            IsRequired = entity.IsRequired,
            Order = entity.Order,
            Description = entity.Description,
            ParentNodeId = entity.ParentNodeId,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.MetadataJson, JsonOptions) ?? new(),
            AllowedChildNodeIds = JsonSerializer.Deserialize<List<string>>(entity.AllowedChildNodeIdsJson, JsonOptions) ?? new()
        };
    }

    /// <summary>
    /// Maps a TopicConfiguration to TopicConfigurationEntity.
    /// </summary>
    public static TopicConfigurationEntity ToEntity(this TopicConfiguration config)
    {
        return new TopicConfigurationEntity
        {
            Id = config.Id,
            Topic = config.Topic,
            PathValuesJson = JsonSerializer.Serialize(config.Path.Values, JsonOptions),
            IsVerified = config.IsVerified,
            IsActive = config.IsActive,
            SourceType = config.SourceType,
            Description = config.Description,
            CreatedAt = config.CreatedAt,
            ModifiedAt = config.ModifiedAt,
            CreatedBy = config.CreatedBy,
            MetadataJson = JsonSerializer.Serialize(config.Metadata, JsonOptions),
            NamespaceConfigurationId = config.NamespaceConfigurationId,
            NSPath = config.NSPath
        };
    }

    /// <summary>
    /// Maps a TopicConfigurationEntity to TopicConfiguration.
    /// </summary>
    public static TopicConfiguration ToModel(this TopicConfigurationEntity entity)
    {
        var pathValues = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.PathValuesJson, JsonOptions) ?? new();
        
        return new TopicConfiguration
        {
            Id = entity.Id,
            Topic = entity.Topic,
            Path = new HierarchicalPath { Values = pathValues },
            IsVerified = entity.IsVerified,
            IsActive = entity.IsActive,
            SourceType = entity.SourceType,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            CreatedBy = entity.CreatedBy,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.MetadataJson, JsonOptions) ?? new(),
            NamespaceConfigurationId = entity.NamespaceConfigurationId,
            NSPath = entity.NSPath
        };
    }

    /// <summary>
    /// Maps a DataSchema to DataSchemaEntity.
    /// </summary>
    public static DataSchemaEntity ToEntity(this DataSchema schema)
    {
        return new DataSchemaEntity
        {
            SchemaId = schema.SchemaId,
            Topic = schema.Topic,
            JsonSchema = schema.JsonSchema,
            PropertyTypesJson = JsonSerializer.Serialize(schema.PropertyTypes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.FullName), JsonOptions),
            ValidationRulesJson = JsonSerializer.Serialize(schema.ValidationRules, JsonOptions),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps a DataSchemaEntity to DataSchema.
    /// </summary>
    public static DataSchema ToModel(this DataSchemaEntity entity)
    {
        var propertyTypeNames = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.PropertyTypesJson, JsonOptions) ?? new();
        var propertyTypes = new Dictionary<string, Type>();
        
        foreach (var kvp in propertyTypeNames)
        {
            if (Type.GetType(kvp.Value) is Type type)
            {
                propertyTypes[kvp.Key] = type;
            }
        }

        return new DataSchema
        {
            SchemaId = entity.SchemaId,
            Topic = entity.Topic,
            JsonSchema = entity.JsonSchema,
            PropertyTypes = propertyTypes,
            ValidationRules = JsonSerializer.Deserialize<List<ValidationRule>>(entity.ValidationRulesJson, JsonOptions) ?? new()
        };
    }

    /// <summary>
    /// Maps a DataPoint to DataPointEntity.
    /// </summary>
    public static DataPointEntity ToEntity(this DataPoint dataPoint)
    {
        return new DataPointEntity
        {
            Id = Guid.NewGuid().ToString(),
            Topic = dataPoint.Topic,
            PathValuesJson = JsonSerializer.Serialize(dataPoint.Path.Values, JsonOptions),
            ValueJson = JsonSerializer.Serialize(dataPoint.Value, JsonOptions),
            Source = dataPoint.Source,
            Timestamp = dataPoint.Timestamp,
            MetadataJson = JsonSerializer.Serialize(dataPoint.Metadata, JsonOptions)
        };
    }

    /// <summary>
    /// Maps a DataPointEntity to DataPoint.
    /// </summary>
    public static DataPoint ToModel(this DataPointEntity entity)
    {
        var pathValues = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.PathValuesJson, JsonOptions) ?? new();
        var value = JsonSerializer.Deserialize<JsonElement>(entity.ValueJson, JsonOptions);
        var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.MetadataJson, JsonOptions) ?? new();

        return new DataPoint
        {
            Topic = entity.Topic,
            Path = new HierarchicalPath { Values = pathValues },
            Value = value,
            Source = entity.Source,
            Timestamp = entity.Timestamp,
            Metadata = metadata
        };
    }

    #region NamespaceConfiguration Mappings

    /// <summary>
    /// Converts a NamespaceConfiguration model to a NamespaceConfigurationEntity.
    /// </summary>
    public static NamespaceConfigurationEntity ToEntity(this NamespaceConfiguration model)
    {
        return new NamespaceConfigurationEntity
        {
            Id = model.Id,
            Name = model.Name,
            Type = (int)model.Type,
            Description = model.Description,
            HierarchicalPathJson = JsonSerializer.Serialize(model.HierarchicalPath?.Values ?? new Dictionary<string, string>(), JsonOptions),
            ParentNamespaceId = model.ParentNamespaceId,
            AllowedParentHierarchyNodeId = model.AllowedParentHierarchyNodeId,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            ModifiedAt = model.ModifiedAt,
            CreatedBy = model.CreatedBy,
            MetadataJson = JsonSerializer.Serialize(model.Metadata ?? new Dictionary<string, object>(), JsonOptions)
        };
    }

    /// <summary>
    /// Converts a NamespaceConfigurationEntity to a NamespaceConfiguration model.
    /// </summary>
    public static NamespaceConfiguration ToModel(this NamespaceConfigurationEntity entity)
    {
        Dictionary<string, string> pathValues;
        Dictionary<string, object> metadata;

        try
        {
            pathValues = string.IsNullOrEmpty(entity.HierarchicalPathJson) 
                ? new() 
                : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.HierarchicalPathJson, JsonOptions) ?? new();
        }
        catch (JsonException)
        {
            pathValues = new();
        }

        try
        {
            metadata = string.IsNullOrEmpty(entity.MetadataJson) 
                ? new() 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.MetadataJson, JsonOptions) ?? new();
        }
        catch (JsonException)
        {
            metadata = new();
        }

        return new NamespaceConfiguration
        {
            Id = entity.Id,
            Name = entity.Name,
            Type = (NamespaceType)entity.Type,
            Description = entity.Description,
            HierarchicalPath = new HierarchicalPath { Values = pathValues },
            ParentNamespaceId = entity.ParentNamespaceId,
            AllowedParentHierarchyNodeId = entity.AllowedParentHierarchyNodeId,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            CreatedBy = entity.CreatedBy,
            Metadata = metadata
        };
    }

    #endregion

    #region NSTreeInstance Mappings

    /// <summary>
    /// Converts an NSTreeInstance model to an NSTreeInstanceEntity.
    /// </summary>
    public static NSTreeInstanceEntity ToEntity(this NSTreeInstance model)
    {
        return new NSTreeInstanceEntity
        {
            Id = model.Id,
            Name = model.Name,
            HierarchyNodeId = model.HierarchyNodeId,
            ParentInstanceId = model.ParentInstanceId,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            ModifiedAt = model.ModifiedAt,
            MetadataJson = JsonSerializer.Serialize(model.Metadata ?? new Dictionary<string, object>(), JsonOptions)
        };
    }

    /// <summary>
    /// Converts an NSTreeInstanceEntity to an NSTreeInstance model.
    /// </summary>
    public static NSTreeInstance ToModel(this NSTreeInstanceEntity entity)
    {
        Dictionary<string, object> metadata;

        try
        {
            metadata = string.IsNullOrEmpty(entity.MetadataJson) 
                ? new() 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.MetadataJson, JsonOptions) ?? new();
        }
        catch (JsonException)
        {
            metadata = new();
        }

        return new NSTreeInstance
        {
            Id = entity.Id,
            Name = entity.Name,
            HierarchyNodeId = entity.HierarchyNodeId,
            ParentInstanceId = entity.ParentInstanceId,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            Metadata = metadata
        };
    }

    #endregion
}