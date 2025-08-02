    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using UNSInfra.Configuration;
    using UNSInfra.Models.Hierarchy;
    using UNSInfra.Models.Schema;
    using UNSInfra.Repositories;
    using UNSInfra.Services.DataIngestion.Mock;
    using UNSInfra.Services.TopicDiscovery;
    using UNSInfra.Storage.InMemory;
    using UNSInfra.Validation;
    using UNSInfra.Services;

    public class Program
{
    public static async Task Main(string[] args)
    {
        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<TopicDiscoveryService>();
        
        // Setup dependencies
        var realtimeStorage = new InMemoryRealtimeStorage();
        
        // Create default configuration for historical storage
        var historicalStorageConfig = new HistoricalStorageConfiguration
        {
            Enabled = true,
            StorageType = HistoricalStorageType.InMemory,
            InMemory = new InMemoryHistoricalStorageOptions
            {
                MaxValuesPerDataPoint = 1000,
                MaxTotalValues = 10000,
                AutoCleanup = true
            }
        };
        var options = Options.Create(historicalStorageConfig);
        var historicalStorage = new InMemoryHistoricalStorage(options);
        
        var validator = new JsonSchemaValidator();
        var schemaRepository = new InMemorySchemaRepository();
        
        // Setup hierarchy services
        var hierarchyRepo = new InMemoryHierarchyConfigurationRepository();
        await hierarchyRepo.EnsureDefaultConfigurationAsync();
        var hierarchyService = new HierarchyService(hierarchyRepo);
        
        var topicDiscovery = new TopicDiscoveryService(new InMemoryTopicConfigurationRepository(), hierarchyService, logger);
        
        
        // Setup data services
        var mqttService = new MockMqttDataService(topicDiscovery);
        var kafkaLogger = loggerFactory.CreateLogger<MockKafkaDataService>();
        var kafkaService = new MockKafkaDataService(topicDiscovery, kafkaLogger);

        // Define hierarchy path using dynamic hierarchy service
        var robotPath = await hierarchyService.CreatePathFromStringAsync("enterprise/factoryA/assemblyLine1/robot123/temperature");
        
        // Subscribe to topics
        await mqttService.SubscribeToTopicAsync("sensors/temperature", robotPath);
        await kafkaService.SubscribeToTopicAsync("production/data", robotPath);

        // Define schema for temperature data
        var tempSchema = new DataSchema
        {
            SchemaId = "temperature-v1",
            Topic = "sensors/temperature",
            PropertyTypes = new Dictionary<string, Type>
            {
                { "value", typeof(double) },
                { "unit", typeof(string) },
                { "timestamp", typeof(DateTime) }
            },
            ValidationRules = new List<ValidationRule>
            {
                new() { PropertyName = "value", RuleType = "Range", RuleValue = new double[] { -50, 150 } }
            }
        };
        
        await schemaRepository.SaveSchemaAsync(tempSchema);

        // Start services
        await mqttService.StartAsync();
        await kafkaService.StartAsync();

        // Simulate data reception
        var tempData = JsonSerializer.SerializeToElement(new 
        { 
            value = 23.5, 
            unit = "Celsius", 
            timestamp = DateTime.UtcNow 
        });
        
        mqttService.SimulateDataReceived("sensors/temperature", tempData);
        kafkaService.SimulateDataReceived("production/data", tempData);

        // Wait a bit for processing
        await Task.Delay(1000);

        // Query data
        var latestTemp = await realtimeStorage.GetLatestAsync("sensors/temperature");
        if (latestTemp != null)
        {
            Console.WriteLine($"Latest temperature: {latestTemp.Value} at {latestTemp.Path.GetFullPath()}");
        }

        // Get historical data
        var history = await historicalStorage.GetHistoryByPathAsync(
            robotPath, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        
        Console.WriteLine($"Historical data points: {history.Count()}");

        await mqttService.StopAsync();
        await kafkaService.StopAsync();
    }
}