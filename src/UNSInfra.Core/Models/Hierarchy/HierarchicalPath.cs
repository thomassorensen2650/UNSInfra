namespace UNSInfra.Models.Hierarchy;

/// <summary>
/// Represents an ISA-S95 hierarchical path structure for manufacturing systems.
/// Follows the Enterprise/Site/Area/WorkCenter/WorkUnit/Property hierarchy.
/// </summary>
public class HierarchicalPath
{
    /// <summary>
    /// Gets or sets the enterprise level identifier (top level of the hierarchy).
    /// </summary>
    public string Enterprise { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the site level identifier (physical location within enterprise).
    /// </summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the area level identifier (production area within site).
    /// </summary>
    public string Area { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the work center identifier (group of equipment performing similar functions).
    /// </summary>
    public string WorkCenter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the work unit identifier (individual piece of equipment or process).
    /// </summary>
    public string WorkUnit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the property identifier (specific data point or measurement).
    /// </summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>
    /// Constructs the full hierarchical path as a forward-slash separated string.
    /// </summary>
    /// <returns>The complete path in format: Enterprise/Site/Area/WorkCenter/WorkUnit/Property</returns>
    public string GetFullPath() => $"{Enterprise}/{Site}/{Area}/{WorkCenter}/{WorkUnit}/{Property}";
    
    /// <summary>
    /// Creates a HierarchicalPath instance from a forward-slash separated path string.
    /// </summary>
    /// <param name="path">The path string to parse (e.g., "enterprise/factoryA/line1/robot1/temperature")</param>
    /// <returns>A new HierarchicalPath instance with populated hierarchy levels</returns>
    public static HierarchicalPath FromPath(string path)
    {
        var parts = path.Split('/');
        return new HierarchicalPath
        {
            Enterprise = parts.Length > 0 ? parts[0] : "",
            Site = parts.Length > 1 ? parts[1] : "",
            Area = parts.Length > 2 ? parts[2] : "",
            WorkCenter = parts.Length > 3 ? parts[3] : "",
            WorkUnit = parts.Length > 4 ? parts[4] : "",
            Property = parts.Length > 5 ? parts[5] : ""
        };
    }
}
