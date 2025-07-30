namespace UNSInfra.Models.Hierarchy;

/// <summary>
/// Represents a dynamic hierarchical path structure for manufacturing systems.
/// Can follow any configured hierarchy structure.
/// </summary>
public class HierarchicalPath
{
    /// <summary>
    /// Gets or sets the path values for the hierarchy.
    /// Key is the hierarchy level name, value is the path component.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new();

    /// <summary>
    /// Constructs the full hierarchical path as a forward-slash separated string.
    /// </summary>
    /// <returns>The complete path in hierarchical order</returns>
    public string GetFullPath()
    {
        return string.Join("/", Values.Values.Where(v => !string.IsNullOrEmpty(v)));
    }

    /// <summary>
    /// Creates a HierarchicalPath instance from a path string.
    /// Uses default ISA-S95 hierarchy levels for parsing.
    /// </summary>
    /// <param name="path">The path string to parse</param>
    /// <returns>A new HierarchicalPath instance</returns>
    public static HierarchicalPath FromPath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var hierarchicalPath = new HierarchicalPath();
        
        // Default ISA-S95 hierarchy levels
        var levelNames = new[] { "Enterprise", "Site", "Area", "WorkCenter", "WorkUnit", "Property" };
        
        for (int i = 0; i < Math.Min(parts.Length, levelNames.Length); i++)
        {
            hierarchicalPath.Values[levelNames[i]] = parts[i];
        }

        return hierarchicalPath;
    }

    /// <summary>
    /// Gets the value for a specific hierarchy level.
    /// </summary>
    /// <param name="levelName">The hierarchy level name</param>
    /// <returns>The value for the level, or empty string if not found</returns>
    public string GetValue(string levelName)
    {
        return Values.TryGetValue(levelName, out var value) ? value : string.Empty;
    }

    /// <summary>
    /// Sets the value for a specific hierarchy level.
    /// </summary>
    /// <param name="levelName">The hierarchy level name</param>
    /// <param name="value">The value to set</param>
    public void SetValue(string levelName, string value)
    {
        Values[levelName] = value;
    }

    /// <summary>
    /// Gets all non-empty hierarchy levels and their values.
    /// </summary>
    /// <returns>Dictionary of level names to values</returns>
    public Dictionary<string, string> GetAllValues()
    {
        return Values.Where(kvp => !string.IsNullOrEmpty(kvp.Value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    // Legacy property accessors for existing code that hasn't been updated yet
    public string Enterprise
    {
        get => GetValue("Enterprise");
        set => SetValue("Enterprise", value);
    }

    public string Site
    {
        get => GetValue("Site");
        set => SetValue("Site", value);
    }

    public string Area
    {
        get => GetValue("Area");
        set => SetValue("Area", value);
    }

    public string WorkCenter
    {
        get => GetValue("WorkCenter");
        set => SetValue("WorkCenter", value);
    }

    public string WorkUnit
    {
        get => GetValue("WorkUnit");
        set => SetValue("WorkUnit", value);
    }

    public string Property
    {
        get => GetValue("Property");
        set => SetValue("Property", value);
    }
}