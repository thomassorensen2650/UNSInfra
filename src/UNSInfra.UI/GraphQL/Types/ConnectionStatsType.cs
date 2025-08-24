namespace UNSInfra.UI.GraphQL.Types;

/// <summary>
/// GraphQL type for connection statistics
/// </summary>
public class ConnectionStatsType : ObjectType<ConnectionStats>
{
    protected override void Configure(IObjectTypeDescriptor<ConnectionStats> descriptor)
    {
        descriptor.Name("ConnectionStats");
        descriptor.Description("Statistics about data connections");

        descriptor.Field(c => c.TotalConnections)
            .Description("Total number of configured connections");

        descriptor.Field(c => c.ActiveConnections)
            .Description("Number of currently active connections");

        descriptor.Field(c => c.InactiveConnections)
            .Description("Number of inactive connections");
    }
}

/// <summary>
/// Statistics model for connections
/// </summary>
public class ConnectionStats
{
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public int InactiveConnections { get; set; }
}