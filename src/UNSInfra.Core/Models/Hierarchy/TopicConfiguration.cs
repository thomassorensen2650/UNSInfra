namespace UNSInfra.Models.Hierarchy;

    /// <summary>
    /// Represents the mapping configuration between MQTT/Kafka topics and ISA-S95 hierarchical paths.
    /// Includes verification status and metadata about the topic mapping.
    /// </summary>
    public class TopicConfiguration
    {
        /// <summary>
        /// Gets or sets the unique identifier for this topic configuration.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the MQTT/Kafka topic pattern or exact topic name.
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ISA-S95 hierarchical path this topic maps to.
        /// </summary>
        public HierarchicalPath Path { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this topic mapping has been verified by an administrator.
        /// New topics discovered automatically are marked as unverified.
        /// </summary>
        public bool IsVerified { get; set; } = false;

        /// <summary>
        /// Gets or sets whether this topic configuration is currently active.
        /// Inactive topics will not process incoming data.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the timestamp when this configuration was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp when this configuration was last modified.
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the source system type (MQTT, Kafka, etc.).
        /// </summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional metadata and configuration options for this topic.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets optional description or notes about this topic mapping.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the user or system that created this configuration.
        /// </summary>
        public string CreatedBy { get; set; } = "System";
    }
