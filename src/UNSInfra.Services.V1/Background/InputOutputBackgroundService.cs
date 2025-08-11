using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Services.V1.Mqtt;
using UNSInfra.Services.SocketIO;

namespace UNSInfra.Services.V1.Background;

/// <summary>
/// Background service that manages input/output data services
/// </summary>
public class InputOutputBackgroundService : BackgroundService
{
    private readonly ILogger<InputOutputBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<object> _runningServices = new();

    public InputOutputBackgroundService(
        ILogger<InputOutputBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Input/Output background service");

        try
        {
            // Start all input/output services
            await StartInputOutputServices(stoppingToken);

            // Keep running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                // Monitor services and restart if needed
                await MonitorServices(stoppingToken);
                
                // Wait before next check
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Input/Output background service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Input/Output background service");
        }
        finally
        {
            await StopAllServices();
        }
    }

    private async Task StartInputOutputServices(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            // Start SocketIO configurable input service
            await StartSocketIOInputService(scope, cancellationToken);

            // Start MQTT configurable input service  
            await StartMqttInputService(scope, cancellationToken);

            // Start MQTT model export service
            await StartMqttModelExportService(scope, cancellationToken);

            // Start MQTT data export service
            await StartMqttDataExportService(scope, cancellationToken);

            _logger.LogInformation("All input/output services started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting input/output services");
        }
    }

    private async Task StartSocketIOInputService(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var service = scope.ServiceProvider.GetService<SocketIOConfigurableDataService>();
            if (service != null)
            {
                await service.StartAsync();
                _runningServices.Add(service);
                _logger.LogInformation("SocketIO configurable input service started");
            }
            else
            {
                _logger.LogDebug("SocketIO configurable input service not registered");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting SocketIO configurable input service");
        }
    }

    private async Task StartMqttInputService(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var service = scope.ServiceProvider.GetService<MqttConfigurableDataService>();
            if (service != null)
            {
                await service.StartAsync();
                _runningServices.Add(service);
                _logger.LogInformation("MQTT configurable input service started");
            }
            else
            {
                _logger.LogDebug("MQTT configurable input service not registered");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting MQTT configurable input service");
        }
    }

    private async Task StartMqttModelExportService(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var service = scope.ServiceProvider.GetService<MqttModelExportService>();
            if (service != null)
            {
                await service.StartAsync();
                _runningServices.Add(service);
                _logger.LogInformation("MQTT model export service started");
            }
            else
            {
                _logger.LogDebug("MQTT model export service not registered");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting MQTT model export service");
        }
    }

    private async Task StartMqttDataExportService(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var service = scope.ServiceProvider.GetService<MqttDataExportService>();
            if (service != null)
            {
                await service.StartAsync();
                _runningServices.Add(service);
                _logger.LogInformation("MQTT data export service started");
            }
            else
            {
                _logger.LogDebug("MQTT data export service not registered");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting MQTT data export service");
        }
    }

    private async Task MonitorServices(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            // Check SocketIO service
            var socketIOService = scope.ServiceProvider.GetService<SocketIOConfigurableDataService>();
            if (socketIOService != null)
            {
                var isRunning = await socketIOService.IsRunningAsync();
                if (!isRunning)
                {
                    _logger.LogWarning("SocketIO service is not running, attempting restart");
                    await socketIOService.StartAsync();
                }
            }

            // Check MQTT input service
            var mqttInputService = scope.ServiceProvider.GetService<MqttConfigurableDataService>();
            if (mqttInputService != null)
            {
                var isRunning = await mqttInputService.IsRunningAsync();
                if (!isRunning)
                {
                    _logger.LogWarning("MQTT input service is not running, attempting restart");
                    await mqttInputService.StartAsync();
                }
            }

            // Check MQTT model export service
            var mqttModelService = scope.ServiceProvider.GetService<MqttModelExportService>();
            if (mqttModelService != null)
            {
                var isRunning = await mqttModelService.IsRunningAsync();
                if (!isRunning)
                {
                    _logger.LogWarning("MQTT model export service is not running, attempting restart");
                    await mqttModelService.StartAsync();
                }
            }

            // Check MQTT data export service
            var mqttDataService = scope.ServiceProvider.GetService<MqttDataExportService>();
            if (mqttDataService != null)
            {
                var isRunning = await mqttDataService.IsRunningAsync();
                if (!isRunning)
                {
                    _logger.LogWarning("MQTT data export service is not running, attempting restart");
                    await mqttDataService.StartAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring services");
        }
    }

    private async Task StopAllServices()
    {
        _logger.LogInformation("Stopping all input/output services");

        foreach (var service in _runningServices)
        {
            try
            {
                // Try to stop gracefully if the service supports it
                if (service is SocketIOConfigurableDataService socketIOService)
                {
                    await socketIOService.StopAsync();
                }
                else if (service is MqttConfigurableDataService mqttInputService)
                {
                    await mqttInputService.StopAsync();
                }
                else if (service is MqttModelExportService mqttModelService)
                {
                    await mqttModelService.StopAsync();
                }
                else if (service is MqttDataExportService mqttDataService)
                {
                    await mqttDataService.StopAsync();
                }

                // Dispose the service if it implements IDisposable
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping service {ServiceType}", service.GetType().Name);
            }
        }

        _runningServices.Clear();
        _logger.LogInformation("All input/output services stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Input/Output background service is stopping");
        
        await base.StopAsync(cancellationToken);
        await StopAllServices();
        
        _logger.LogInformation("Input/Output background service stopped");
    }
}