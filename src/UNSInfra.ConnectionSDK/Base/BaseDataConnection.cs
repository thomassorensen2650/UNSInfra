using Microsoft.Extensions.Logging;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Models;

namespace UNSInfra.ConnectionSDK.Base;

/// <summary>
/// Base implementation of IDataConnection that provides common functionality
/// </summary>
public abstract class BaseDataConnection : IDataConnection
{
    /// <summary>
    /// Logger for this connection
    /// </summary>
    protected readonly ILogger Logger;
    
    /// <summary>
    /// Lock object for thread safety
    /// </summary>
    protected readonly object _lockObject = new();
    
    /// <summary>
    /// Configured inputs for this connection
    /// </summary>
    protected readonly Dictionary<string, object> _inputConfigurations = new();
    
    /// <summary>
    /// Configured outputs for this connection
    /// </summary>
    protected readonly Dictionary<string, object> _outputConfigurations = new();

    private bool _disposed;
    private ConnectionStatus _status = ConnectionStatus.Disabled;
    private string _statusMessage = "Initialized";
    private long _dataPointsReceived = 0;
    private long _dataPointsSent = 0;

    /// <summary>
    /// Initializes the base connection
    /// </summary>
    /// <param name="connectionId">Unique identifier for this connection</param>
    /// <param name="name">Display name for this connection</param>
    /// <param name="logger">Logger instance</param>
    protected BaseDataConnection(string connectionId, string name, ILogger logger)
    {
        ConnectionId = connectionId;
        Name = name;
        Logger = logger;
    }

    /// <inheritdoc />
    public string ConnectionId { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <inheritdoc />
    public event EventHandler<ConnectionStatusChangedEventArgs>? StatusChanged;

    /// <inheritdoc />
    public ConnectionStatus Status => _status;

    /// <inheritdoc />
    public virtual async Task<bool> InitializeAsync(object configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateStatus(ConnectionStatus.Connecting, "Initializing...");

            var validationResult = ValidateConfiguration(configuration);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors);
                UpdateStatus(ConnectionStatus.Error, $"Configuration invalid: {errors}");
                return false;
            }

            var result = await OnInitializeAsync(configuration, cancellationToken);
            if (result)
            {
                UpdateStatus(ConnectionStatus.Disconnected, "Configured");
            }
            else
            {
                UpdateStatus(ConnectionStatus.Error, "Initialization failed");
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing connection {ConnectionId}", ConnectionId);
            UpdateStatus(ConnectionStatus.Error, $"Initialization error: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateStatus(ConnectionStatus.Connecting, "Starting...");

            var result = await OnStartAsync(cancellationToken);
            if (result)
            {
                UpdateStatus(ConnectionStatus.Connected, "Connected");
            }
            else
            {
                UpdateStatus(ConnectionStatus.Error, "Failed to start");
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error starting connection {ConnectionId}", ConnectionId);
            UpdateStatus(ConnectionStatus.Error, $"Start error: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateStatus(ConnectionStatus.Stopping, "Stopping...");

            var result = await OnStopAsync(cancellationToken);
            UpdateStatus(result ? ConnectionStatus.Disconnected : ConnectionStatus.Error, 
                         result ? "Stopped" : "Stop failed");

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error stopping connection {ConnectionId}", ConnectionId);
            UpdateStatus(ConnectionStatus.Error, $"Stop error: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> ConfigureInputAsync(object inputConfig, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await OnConfigureInputAsync(inputConfig, cancellationToken);
            if (result)
            {
                var inputId = ExtractInputId(inputConfig);
                lock (_lockObject)
                {
                    _inputConfigurations[inputId] = inputConfig;
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error configuring input for connection {ConnectionId}", ConnectionId);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> RemoveInputAsync(string inputId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await OnRemoveInputAsync(inputId, cancellationToken);
            if (result)
            {
                lock (_lockObject)
                {
                    _inputConfigurations.Remove(inputId);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing input {InputId} from connection {ConnectionId}", inputId, ConnectionId);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> ConfigureOutputAsync(object outputConfig, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await OnConfigureOutputAsync(outputConfig, cancellationToken);
            if (result)
            {
                var outputId = ExtractOutputId(outputConfig);
                lock (_lockObject)
                {
                    _outputConfigurations[outputId] = outputConfig;
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error configuring output for connection {ConnectionId}", ConnectionId);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> RemoveOutputAsync(string outputId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await OnRemoveOutputAsync(outputId, cancellationToken);
            if (result)
            {
                lock (_lockObject)
                {
                    _outputConfigurations.Remove(outputId);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing output {OutputId} from connection {ConnectionId}", outputId, ConnectionId);
            return false;
        }
    }

    /// <inheritdoc />
    public abstract Task<bool> SendDataAsync(DataPoint dataPoint, string? outputId = null, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract ValidationResult ValidateConfiguration(object configuration);

    /// <summary>
    /// Override this to implement connection-specific initialization logic
    /// </summary>
    protected abstract Task<bool> OnInitializeAsync(object configuration, CancellationToken cancellationToken);

    /// <summary>
    /// Override this to implement connection-specific start logic
    /// </summary>
    protected abstract Task<bool> OnStartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Override this to implement connection-specific stop logic
    /// </summary>
    protected abstract Task<bool> OnStopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Override this to implement connection-specific input configuration logic
    /// </summary>
    protected abstract Task<bool> OnConfigureInputAsync(object inputConfig, CancellationToken cancellationToken);

    /// <summary>
    /// Override this to implement connection-specific input removal logic
    /// </summary>
    protected abstract Task<bool> OnRemoveInputAsync(string inputId, CancellationToken cancellationToken);

    /// <summary>
    /// Override this to implement connection-specific output configuration logic
    /// </summary>
    protected abstract Task<bool> OnConfigureOutputAsync(object outputConfig, CancellationToken cancellationToken);

    /// <summary>
    /// Override this to implement connection-specific output removal logic
    /// </summary>
    protected abstract Task<bool> OnRemoveOutputAsync(string outputId, CancellationToken cancellationToken);

    /// <summary>
    /// Extract the ID from an input configuration object
    /// </summary>
    protected abstract string ExtractInputId(object inputConfig);

    /// <summary>
    /// Extract the ID from an output configuration object
    /// </summary>
    protected abstract string ExtractOutputId(object outputConfig);

    /// <summary>
    /// Helper method to raise DataReceived event
    /// </summary>
    protected virtual void OnDataReceived(DataPoint dataPoint, string inputId)
    {
        try
        {
            Interlocked.Increment(ref _dataPointsReceived);

            var args = new DataReceivedEventArgs
            {
                DataPoint = dataPoint,
                InputId = inputId,
                ConnectionId = ConnectionId,
                ReceivedAt = DateTime.UtcNow
            };

            DataReceived?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error raising DataReceived event for connection {ConnectionId}", ConnectionId);
        }
    }

    /// <summary>
    /// Helper method to increment data sent counter
    /// </summary>
    protected virtual void IncrementDataSent()
    {
        Interlocked.Increment(ref _dataPointsSent);
    }

    /// <summary>
    /// Updates the connection status and raises StatusChanged event
    /// </summary>
    protected virtual void UpdateStatus(ConnectionStatus newStatus, string statusMessage)
    {
        try
        {
            ConnectionStatus oldStatus;
            lock (_lockObject)
            {
                oldStatus = _status;
                _status = newStatus;
                _statusMessage = statusMessage;
            }

            if (oldStatus != newStatus)
            {
                Logger.LogDebug("Connection {ConnectionId} status changed: {OldStatus} -> {NewStatus}: {Message}",
                    ConnectionId, oldStatus, newStatus, statusMessage);

                var args = new ConnectionStatusChangedEventArgs
                {
                    ConnectionId = ConnectionId,
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    Message = statusMessage,
                    Timestamp = DateTime.UtcNow
                };

                StatusChanged?.Invoke(this, args);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating status for connection {ConnectionId}", ConnectionId);
        }
    }

    /// <summary>
    /// Disposes the connection and releases resources
    /// </summary>
    public virtual void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error stopping connection {ConnectionId} during disposal", ConnectionId);
            }

            _disposed = true;
        }
    }
}