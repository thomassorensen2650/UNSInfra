namespace UNSInfra.Models.Configuration;

/// <summary>
/// MQTT output configuration for publishing UNS model and data
/// </summary>
public class MqttOutputConfiguration : OutputConfiguration
{
    public MqttOutputConfiguration()
    {
        ServiceType = "MQTT";
    }

    /// <summary>
    /// Quality of Service level for MQTT publishing
    /// </summary>
    public int QoS { get; set; } = 1;

    /// <summary>
    /// Whether to publish with retain flag
    /// </summary>
    public bool Retain { get; set; } = true;

    /// <summary>
    /// Type of output to publish
    /// </summary>
    public MqttOutputType OutputType { get; set; } = MqttOutputType.Data;

    /// <summary>
    /// Topic prefix to use when publishing (optional)
    /// </summary>
    public string? TopicPrefix { get; set; }

    /// <summary>
    /// Configuration specific to model export
    /// </summary>
    public MqttModelExportConfiguration? ModelExportConfig { get; set; }

    /// <summary>
    /// Configuration specific to data export
    /// </summary>
    public MqttDataExportConfiguration? DataExportConfig { get; set; }
    
    /// <summary>
    /// Helper property for UI - whether to export UNS model
    /// </summary>
    public bool ExportUNSModel
    {
        get => OutputType == MqttOutputType.Model || OutputType == MqttOutputType.Both;
        set
        {
            // Check if data export has been explicitly configured (has config object)
            var hasDataExportConfig = DataExportConfig != null;
            var currentExportData = (OutputType == MqttOutputType.Data || OutputType == MqttOutputType.Both) && hasDataExportConfig;
            
            if (value && currentExportData)
                OutputType = MqttOutputType.Both;
            else if (value)
                OutputType = MqttOutputType.Model;
            else if (currentExportData)
                OutputType = MqttOutputType.Data;
            else
                OutputType = MqttOutputType.Data; // Default
                
            // Ensure model config exists if needed
            if (value && ModelExportConfig == null)
                ModelExportConfig = new MqttModelExportConfiguration();
        }
    }
    
    /// <summary>
    /// Helper property for UI - whether to export UNS data
    /// </summary>
    public bool ExportUNSData
    {
        get => OutputType == MqttOutputType.Data || OutputType == MqttOutputType.Both;
        set
        {
            // Check if model export has been explicitly configured (has config object)
            var hasModelExportConfig = ModelExportConfig != null;
            var currentExportModel = (OutputType == MqttOutputType.Model || OutputType == MqttOutputType.Both) && hasModelExportConfig;
            
            if (value && currentExportModel)
                OutputType = MqttOutputType.Both;
            else if (value)
                OutputType = MqttOutputType.Data;
            else if (currentExportModel)
                OutputType = MqttOutputType.Model;
            else
                OutputType = MqttOutputType.Data; // Default
                
            // Ensure data config exists if needed
            if (value && DataExportConfig == null)
                DataExportConfig = new MqttDataExportConfiguration();
        }
    }
    
    /// <summary>
    /// Helper property for UI - model attribute name
    /// </summary>
    public string ModelAttributeName
    {
        get => ModelExportConfig?.ModelAttributeName ?? "_model";
        set
        {
            ModelExportConfig ??= new MqttModelExportConfiguration();
            ModelExportConfig.ModelAttributeName = value;
        }
    }
    
    /// <summary>
    /// Helper property for UI - model republish interval in hours
    /// </summary>
    public int ModelRepublishIntervalHours
    {
        get => (ModelExportConfig?.RepublishIntervalMinutes ?? 60) / 60;
        set
        {
            ModelExportConfig ??= new MqttModelExportConfiguration();
            ModelExportConfig.RepublishIntervalMinutes = Math.Max(1, value) * 60;
        }
    }
}

/// <summary>
/// Type of MQTT output
/// </summary>
public enum MqttOutputType
{
    /// <summary>
    /// Export UNS model structure and metadata
    /// </summary>
    Model,
    
    /// <summary>
    /// Export UNS data values
    /// </summary>
    Data,
    
    /// <summary>
    /// Export both model and data
    /// </summary>
    Both
}

/// <summary>
/// Configuration for MQTT model export
/// </summary>
public class MqttModelExportConfiguration
{
    /// <summary>
    /// Attribute name to append to the topic for model information
    /// (e.g., "_model" results in "Enterprise1/Site/_model")
    /// </summary>
    public string ModelAttributeName { get; set; } = "_model";

    /// <summary>
    /// How often to republish model information (in minutes)
    /// Default is 60 minutes (1 hour)
    /// </summary>
    public int RepublishIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to include description in model payload
    /// </summary>
    public bool IncludeDescription { get; set; } = true;

    /// <summary>
    /// Whether to include metadata in model payload
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Whether to include child node information
    /// </summary>
    public bool IncludeChildren { get; set; } = false;

    /// <summary>
    /// Custom fields to include in model payload
    /// </summary>
    public Dictionary<string, string> CustomFields { get; set; } = new();

    /// <summary>
    /// Namespace filter - only export models for specific namespaces (empty = all)
    /// </summary>
    public List<string> NamespaceFilter { get; set; } = new();

    /// <summary>
    /// Hierarchy level filter - only export models for specific levels (empty = all)
    /// </summary>
    public List<string> HierarchyLevelFilter { get; set; } = new();
}

/// <summary>
/// Configuration for MQTT data export
/// </summary>
public class MqttDataExportConfiguration
{
    /// <summary>
    /// Whether to publish data immediately when it changes
    /// </summary>
    public bool PublishOnChange { get; set; } = true;

    /// <summary>
    /// Minimum interval between publishes for the same topic (in milliseconds)
    /// Prevents flooding with rapid updates
    /// </summary>
    public int MinPublishIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Maximum age of data to publish (in minutes)
    /// Data older than this will not be published
    /// </summary>
    public int MaxDataAgeMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to include timestamp in payload
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Whether to include data quality/source information
    /// </summary>
    public bool IncludeQuality { get; set; } = false;

    /// <summary>
    /// Data format for publishing
    /// </summary>
    public MqttDataFormat DataFormat { get; set; } = MqttDataFormat.Json;

    /// <summary>
    /// Namespace filter - only export data for specific namespaces (empty = all)
    /// </summary>
    public List<string> NamespaceFilter { get; set; } = new();

    /// <summary>
    /// Topic filter - only export data for topics matching these patterns (empty = all)
    /// </summary>
    public List<string> TopicFilter { get; set; } = new();

    /// <summary>
    /// Whether to use UNS path as MQTT topic (true) or original topic name (false)
    /// </summary>
    public bool UseUNSPathAsTopic { get; set; } = true;
}

/// <summary>
/// Format for MQTT data publishing
/// </summary>
public enum MqttDataFormat
{
    /// <summary>
    /// Simple JSON with value and timestamp
    /// </summary>
    Json,
    
    /// <summary>
    /// Raw value only (string/number)
    /// </summary>
    Raw,
    
    /// <summary>
    /// Sparkplug B format
    /// </summary>
    SparkplugB
}