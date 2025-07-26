namespace UNSInfra.Services.DataIngestion.Mock;

using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;

public interface IKafkaDataService : IDataIngestionService
{
    Task SubscribeToTopicAsync(string topic, HierarchicalPath path);
    Task UnsubscribeFromTopicAsync(string topic);
}