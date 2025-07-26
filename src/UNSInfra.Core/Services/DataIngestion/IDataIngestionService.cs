namespace UNSInfra.Services.DataIngestion.Mock;

using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
    
public interface IDataIngestionService
{
    Task StartAsync();
    Task StopAsync();
    event EventHandler<DataPoint> DataReceived;
}