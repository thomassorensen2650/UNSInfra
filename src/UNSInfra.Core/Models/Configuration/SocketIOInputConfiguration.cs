namespace UNSInfra.Models.Configuration;

/// <summary>
/// SocketIO input configuration for event-based data ingestion
/// </summary>
public class SocketIOInputConfiguration : InputConfiguration
{
    public SocketIOInputConfiguration()
    {
        ServiceType = "SocketIO";
    }

    /// <summary>
    /// List of event names to subscribe to (e.g., "updated", "data", "sensor_reading")
    /// </summary>
    public List<string> EventNames { get; set; } = new();

    /// <summary>
    /// Whether to automatically map event data to UNS hierarchy
    /// </summary>
    public bool AutoMapToUNS { get; set; } = true;

    /// <summary>
    /// Default namespace to use when mapping events to UNS
    /// </summary>
    public string? DefaultNamespace { get; set; }

    /// <summary>
    /// JSON path mappings for extracting hierarchical path from event data
    /// Key: Hierarchy level name (e.g., "Enterprise", "Site", "Area")
    /// Value: JSON path to extract the value (e.g., "$.enterprise", "$.location.site")
    /// </summary>
    public Dictionary<string, string> HierarchyPathMappings { get; set; } = new();

    /// <summary>
    /// JSON path to extract the topic/identifier from event data
    /// </summary>
    public string? TopicPathMapping { get; set; }

    /// <summary>
    /// JSON path to extract the data value from event data
    /// </summary>
    public string? DataValuePathMapping { get; set; } = "$.value";
    
    /// <summary>
    /// Helper property for UI binding - comma-separated event names
    /// </summary>
    public string EventNamesString
    {
        get => string.Join(", ", EventNames);
        set => EventNames = value?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim())
                                .ToList() ?? new List<string>();
    }
    
    /// <summary>
    /// Helper property for UI binding - formatted JSON path mappings
    /// </summary>
    public string JsonPathMappingsString
    {
        get => string.Join("\n", HierarchyPathMappings.Select(kvp => $"{kvp.Value} -> {kvp.Key}"));
        set
        {
            HierarchyPathMappings.Clear();
            if (!string.IsNullOrWhiteSpace(value))
            {
                var lines = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(" -> ", StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        HierarchyPathMappings[parts[1].Trim()] = parts[0].Trim();
                    }
                }
            }
        }
    }
}