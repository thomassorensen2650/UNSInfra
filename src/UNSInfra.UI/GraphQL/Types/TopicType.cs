using UNSInfra.Services.TopicBrowser;

namespace UNSInfra.UI.GraphQL.Types;

/// <summary>
/// GraphQL type for UNS topics
/// </summary>
public class TopicType : ObjectType<TopicInfo>
{
    protected override void Configure(IObjectTypeDescriptor<TopicInfo> descriptor)
    {
        descriptor.Name("Topic");
        descriptor.Description("A topic in the UNS (Unified Namespace) infrastructure");

        descriptor.Field(t => t.Topic)
            .Description("The topic name/identifier");

        descriptor.Field(t => t.UNSName)
            .Description("The display name for this topic when used in UNS");

        descriptor.Field(t => t.NSPath)
            .Description("The namespace path this topic is assigned to");

        descriptor.Field(t => t.Path)
            .Description("The hierarchical path for this topic")
            .Type<StringType>()
            .Resolve(ctx => ctx.Parent<TopicInfo>().Path?.ToString());

        descriptor.Field(t => t.IsActive)
            .Description("Whether this topic is currently active");

        descriptor.Field(t => t.SourceType)
            .Description("The source type (MQTT, SocketIO, etc.)");

        descriptor.Field(t => t.CreatedAt)
            .Description("When this topic was first created");

        descriptor.Field(t => t.ModifiedAt)
            .Description("When this topic was last modified");

        descriptor.Field(t => t.LastDataTimestamp)
            .Description("Timestamp of the latest data point for this topic");

        descriptor.Field(t => t.Description)
            .Description("Optional description of this topic");

        descriptor.Field(t => t.Metadata)
            .Description("Additional metadata for this topic")
            .Type<AnyType>();

        descriptor.Field("currentValue")
            .Description("The latest/current value for this topic")
            .Type<AnyType>()
            .Resolve(async ctx =>
            {
                var topic = ctx.Parent<TopicInfo>();
                var realtimeStorage = ctx.Service<UNSInfra.Storage.Abstractions.IRealtimeStorage>();
                var latestDataPoint = await realtimeStorage.GetLatestAsync(topic.Topic);
                return latestDataPoint?.Value;
            });
    }
}