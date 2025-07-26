using UNSInfra.Services.DataIngestion.Mock;

namespace UNSInfra.Services.Processing
{
    using UNSInfra.Models.Data;
    using UNSInfra.Repositories;
    using UNSInfra.Storage.Abstractions;
    using UNSInfra.Validation;
    // ==================== MAIN DATA PROCESSING SERVICE ====================

    public class DataProcessingService
{
    private readonly IRealtimeStorage _realtimeStorage;
    private readonly IHistoricalStorage _historicalStorage;
    private readonly ISchemaValidator _validator;
    private readonly ISchemaRepository _schemaRepository;
    private readonly List<IDataIngestionService> _dataServices;

    public DataProcessingService(
        IRealtimeStorage realtimeStorage,
        IHistoricalStorage historicalStorage,
        ISchemaValidator validator,
        ISchemaRepository schemaRepository)
    {
        _realtimeStorage = realtimeStorage;
        _historicalStorage = historicalStorage;
        _validator = validator;
        _schemaRepository = schemaRepository;
        _dataServices = new List<IDataIngestionService>();
    }

    public void AddDataService(IDataIngestionService service)
    {
        _dataServices.Add(service);
        service.DataReceived += OnDataReceived;
    }

    public async Task StartAsync()
    {
        foreach (var service in _dataServices)
        {
            await service.StartAsync();
        }
    }

    public async Task StopAsync()
    {
        foreach (var service in _dataServices)
        {
            await service.StopAsync();
        }
    }

    private async void OnDataReceived(object sender, DataPoint dataPoint)
    {
        try
        {
            // Get schema for the topic
            var schema = await _schemaRepository.GetSchemaAsync(dataPoint.Topic);
            
            if (schema != null)
            {
                // Validate data against schema
                var isValid = await _validator.ValidateAsync(dataPoint, schema);
                
                if (!isValid)
                {
                    Console.WriteLine($"Data validation failed for topic {dataPoint.Topic}");
                    return;
                }
            }

            // Store in both realtime and historical storage
            await _realtimeStorage.StoreAsync(dataPoint);
            await _historicalStorage.StoreAsync(dataPoint);
            
            Console.WriteLine($"Stored data for {dataPoint.Path.GetFullPath()} from {dataPoint.Source}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing data: {ex.Message}");
        }
    }
    }
}
