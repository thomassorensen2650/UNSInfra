namespace UNSInfra.Models.Namespace;

/// <summary>
/// Defines the different types of namespaces available in the UNS system.
/// </summary>
public enum NamespaceType
{
    /// <summary>
    /// Functional namespaces contain operational data like KPIs, production metrics, etc.
    /// </summary>
    Functional,
    
    /// <summary>
    /// Informative namespaces contain reference data, documentation, or metadata.
    /// </summary>
    Informative,
    
    /// <summary>
    /// Definitional namespaces contain master data, configurations, or structural definitions.
    /// </summary>
    Definitional,
    
    /// <summary>
    /// Ad-hoc namespaces for temporary or experimental data organization.
    /// </summary>
    AdHoc
}

/// <summary>
/// Extension methods for NamespaceType enum.
/// </summary>
public static class NamespaceTypeExtensions
{
    /// <summary>
    /// Gets the display name for a namespace type.
    /// </summary>
    public static string GetDisplayName(this NamespaceType type) => type switch
    {
        NamespaceType.Functional => "Functional",
        NamespaceType.Informative => "Informative", 
        NamespaceType.Definitional => "Definitional",
        NamespaceType.AdHoc => "Ad-Hoc",
        _ => type.ToString()
    };

    /// <summary>
    /// Gets the CSS color class for a namespace type.
    /// </summary>
    public static string GetColorClass(this NamespaceType type) => type switch
    {
        NamespaceType.Functional => "text-primary",
        NamespaceType.Informative => "text-info",
        NamespaceType.Definitional => "text-success", 
        NamespaceType.AdHoc => "text-warning",
        _ => "text-secondary"
    };

    /// <summary>
    /// Gets the icon class for a namespace type.
    /// </summary>
    public static string GetIconClass(this NamespaceType type) => type switch
    {
        NamespaceType.Functional => "bi-graph-up",
        NamespaceType.Informative => "bi-info-circle",
        NamespaceType.Definitional => "bi-gear",
        NamespaceType.AdHoc => "bi-lightning",
        _ => "bi-folder"
    };
}