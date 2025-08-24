namespace UNSInfra.UI.GraphQL.Types;

/// <summary>
/// GraphQL type for system status
/// </summary>
public class SystemStatusType : ObjectType<SystemStatus>
{
    protected override void Configure(IObjectTypeDescriptor<SystemStatus> descriptor)
    {
        descriptor.Name("SystemStatus");
        descriptor.Description("Overall system status and statistics");

        descriptor.Field(s => s.TotalTopics)
            .Description("Total number of topics in the system");

        descriptor.Field(s => s.AssignedTopics)
            .Description("Number of topics assigned to UNS namespaces");

        descriptor.Field(s => s.ActiveTopics)
            .Description("Number of currently active topics");

        descriptor.Field(s => s.TotalConnections)
            .Description("Total number of configured connections");

        descriptor.Field(s => s.ActiveConnections)
            .Description("Number of currently active connections");

        descriptor.Field(s => s.Namespaces)
            .Description("Number of distinct namespaces");

        descriptor.Field(s => s.Timestamp)
            .Description("When this status was generated");

        descriptor.Field(s => s.ConnectionStats)
            .Description("Detailed connection statistics");
    }
}

/// <summary>
/// System status model
/// </summary>
public class SystemStatus
{
    public int TotalTopics { get; set; }
    public int AssignedTopics { get; set; }
    public int ActiveTopics { get; set; }
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public int Namespaces { get; set; }
    public DateTime Timestamp { get; set; }
    public ConnectionStats ConnectionStats { get; set; } = new();
}