using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.ConnectionSDK.Base;
using UNSInfra.ConnectionSDK.Models;
using UNSInfra.Services.OPCUA.Models;

namespace UNSInfra.Services.OPCUA.Connections;

/// <summary>
/// Production OPC UA connection implementation using OPC Foundation .NET Standard library
/// </summary>
public class OPCUAConnection : BaseDataConnection
{
    private OPCUAConnectionConfiguration? _connectionConfig;
    private readonly Dictionary<string, OPCUAInputConfiguration> _inputs = new();
    private readonly Dictionary<string, OPCUAOutputConfiguration> _outputs = new();
    private readonly Dictionary<string, Subscription> _subscriptions = new();

    private ApplicationInstance? _application;
    private Session? _session;
    private bool _isConnected;

    /// <summary>
    /// Initializes a new OPC UA connection
    /// </summary>
    public OPCUAConnection(string connectionId, string name, ILogger<OPCUAConnection> logger) 
        : base(connectionId, name, logger)
    {
    }

    /// <inheritdoc />
    public override ValidationResult ValidateConfiguration(object configuration)
    {
        if (configuration is not OPCUAConnectionConfiguration config)
        {
            return ValidationResult.Failure("Configuration must be of type OPCUAConnectionConfiguration");
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.EndpointUrl))
            errors.Add("EndpointUrl is required");

        if (!Uri.TryCreate(config.EndpointUrl, UriKind.Absolute, out var uri) || 
            (!uri.Scheme.Equals("opc.tcp", StringComparison.OrdinalIgnoreCase) && 
             !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && 
             !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            errors.Add("EndpointUrl must be a valid OPC UA endpoint (opc.tcp://, http://, or https://)");

        if (config.ConnectionTimeoutSeconds < 1 || config.ConnectionTimeoutSeconds > 300)
            errors.Add("Connection timeout must be between 1 and 300 seconds");

        if (config.SessionTimeoutSeconds < 30 || config.SessionTimeoutSeconds > 3600)
            errors.Add("Session timeout must be between 30 and 3600 seconds");

        if (config.ReconnectionIntervalSeconds < 1 || config.ReconnectionIntervalSeconds > 60)
            errors.Add("Reconnection interval must be between 1 and 60 seconds");

        return errors.Any() ? ValidationResult.Failure(errors) : ValidationResult.Success();
    }

    /// <inheritdoc />
    protected override async Task<bool> OnInitializeAsync(object configuration, CancellationToken cancellationToken)
    {
        _connectionConfig = (OPCUAConnectionConfiguration)configuration;
        
        Logger.LogInformation("Initialized OPC UA connection to {EndpointUrl}", 
            _connectionConfig.EndpointUrl);
        
        return await Task.FromResult(true);
    }

    /// <inheritdoc />
    protected override async Task<bool> OnStartAsync(CancellationToken cancellationToken)
    {
        if (_connectionConfig == null)
            return false;

        try
        {
            Logger.LogInformation("Connecting to OPC UA server {EndpointUrl}", 
                _connectionConfig.EndpointUrl);

            // Create application instance
            var applicationConfig = new ApplicationConfiguration
            {
                ApplicationName = _connectionConfig.ApplicationName,
                ApplicationUri = _connectionConfig.ApplicationUri,
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    TrustedIssuerCertificates = new CertificateTrustList(),
                    TrustedPeerCertificates = new CertificateTrustList(),
                    RejectedCertificateStore = new CertificateStoreIdentifier(),
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TraceConfiguration = new TraceConfiguration()
            };

            _application = new ApplicationInstance(applicationConfig);

            // Discover endpoints
            var endpoints = await DiscoverEndpoints(_connectionConfig.EndpointUrl);
            if (!endpoints.Any())
            {
                Logger.LogError("No endpoints found for OPC UA server {EndpointUrl}", _connectionConfig.EndpointUrl);
                return false;
            }

            // Select endpoint based on security policy
            var endpoint = SelectEndpoint(endpoints);
            if (endpoint == null)
            {
                Logger.LogError("No suitable endpoint found for security policy {SecurityPolicy}", _connectionConfig.SecurityPolicy);
                return false;
            }

            // Create session
            var sessionTimeout = (uint)(_connectionConfig.SessionTimeoutSeconds * 1000);
            var identity = CreateUserIdentity();

            _session = await Session.Create(
                applicationConfig,
                new ConfiguredEndpoint(null, endpoint),
                false,
                _connectionConfig.ApplicationName,
                sessionTimeout,
                identity,
                null);

            if (_session == null)
            {
                Logger.LogError("Failed to create OPC UA session");
                return false;
            }

            _isConnected = true;
            UpdateStatus(ConnectionStatus.Connected, "Connected to OPC UA server");
            Logger.LogInformation("Successfully connected to OPC UA server");

            // Set up session event handlers
            _session.KeepAlive += OnSessionKeepAlive;
            _session.SessionClosing += OnSessionClosing;

            // Subscribe to all configured inputs
            await SubscribeToConfiguredInputs();

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to OPC UA server");
            UpdateStatus(ConnectionStatus.Error, $"Connection failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnStopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Clean up subscriptions
            foreach (var subscription in _subscriptions.Values)
            {
                subscription?.Delete(true);
            }
            _subscriptions.Clear();

            // Close session
            if (_session != null)
            {
                _session.KeepAlive -= OnSessionKeepAlive;
                _session.SessionClosing -= OnSessionClosing;
                _session.Close();
                _session.Dispose();
                _session = null;
            }

            _isConnected = false;
            UpdateStatus(ConnectionStatus.Disconnected, "Disconnected from OPC UA server");
            Logger.LogInformation("Disconnected from OPC UA server");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disconnecting from OPC UA server");
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnConfigureInputAsync(object inputConfig, CancellationToken cancellationToken)
    {
        if (inputConfig is not OPCUAInputConfiguration config)
            return false;

        try
        {
            if (!config.IsEnabled)
            {
                Logger.LogInformation("Input {InputId} is disabled, skipping configuration", config.Id);
                return true;
            }

            Logger.LogInformation("Configuring OPC UA input {InputId} for nodes: {NodeIds}", 
                config.Id, string.Join(", ", config.NodeIds));

            lock (_lockObject)
            {
                _inputs[config.Id] = config;
            }

            // Create subscription if connected
            if (_isConnected && _session != null)
            {
                await CreateSubscription(config);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error configuring OPC UA input {InputId}", config.Id);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnRemoveInputAsync(string inputId, CancellationToken cancellationToken)
    {
        try
        {
            OPCUAInputConfiguration? config;
            lock (_lockObject)
            {
                _inputs.TryGetValue(inputId, out config);
                _inputs.Remove(inputId);
            }

            // Remove subscription
            if (_subscriptions.TryGetValue(inputId, out var subscription))
            {
                subscription?.Delete(true);
                _subscriptions.Remove(inputId);
                Logger.LogInformation("Removed OPC UA subscription for input {InputId}", inputId);
            }

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing OPC UA input {InputId}", inputId);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnConfigureOutputAsync(object outputConfig, CancellationToken cancellationToken)
    {
        if (outputConfig is not OPCUAOutputConfiguration config)
            return false;

        try
        {
            Logger.LogInformation("Configured OPC UA output {OutputId} for node: {NodeId}", 
                config.Id, config.NodeId);

            lock (_lockObject)
            {
                _outputs[config.Id] = config;
            }

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error configuring OPC UA output {OutputId}", config.Id);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> OnRemoveOutputAsync(string outputId, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Removing OPC UA output {OutputId}", outputId);

            lock (_lockObject)
            {
                _outputs.Remove(outputId);
            }

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing OPC UA output {OutputId}", outputId);
            return false;
        }
    }

    /// <inheritdoc />
    public override async Task<bool> SendDataAsync(DataPoint dataPoint, string? outputId = null, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _session == null)
        {
            Logger.LogWarning("Cannot send data - OPC UA session not connected");
            return false;
        }

        try
        {
            var applicableOutputs = GetApplicableOutputs(dataPoint, outputId);

            foreach (var output in applicableOutputs)
            {
                await WriteToOutput(output, dataPoint, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending data point {Topic}", dataPoint.Topic);
            return false;
        }
    }

    /// <inheritdoc />
    protected override string ExtractInputId(object inputConfig)
    {
        return ((OPCUAInputConfiguration)inputConfig).Id;
    }

    /// <inheritdoc />
    protected override string ExtractOutputId(object outputConfig)
    {
        return ((OPCUAOutputConfiguration)outputConfig).Id;
    }

    private async Task<EndpointDescriptionCollection> DiscoverEndpoints(string endpointUrl)
    {
        try
        {
            var endpointConfiguration = EndpointConfiguration.Create();
            var discoveryUrl = Utils.ParseUri(endpointUrl);
            using var discoveryClient = DiscoveryClient.Create(discoveryUrl, endpointConfiguration);
            return await discoveryClient.GetEndpointsAsync(null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to discover endpoints for {EndpointUrl}", endpointUrl);
            return new EndpointDescriptionCollection();
        }
    }

    private EndpointDescription? SelectEndpoint(EndpointDescriptionCollection endpoints)
    {
        // Select endpoint based on configured security policy
        var targetSecurityPolicyUri = _connectionConfig!.SecurityPolicy switch
        {
            OPCUASecurityPolicy.None => SecurityPolicies.None,
            OPCUASecurityPolicy.Basic128Rsa15 => SecurityPolicies.Basic128Rsa15,
            OPCUASecurityPolicy.Basic256 => SecurityPolicies.Basic256,
            OPCUASecurityPolicy.Basic256Sha256 => SecurityPolicies.Basic256Sha256,
            OPCUASecurityPolicy.Aes128Sha256RsaOaep => SecurityPolicies.Aes128_Sha256_RsaOaep,
            OPCUASecurityPolicy.Aes256Sha256RsaPss => SecurityPolicies.Aes256_Sha256_RsaPss,
            _ => SecurityPolicies.None
        };

        var targetSecurityMode = _connectionConfig.MessageSecurityMode switch
        {
            OPCUAMessageSecurityMode.None => MessageSecurityMode.None,
            OPCUAMessageSecurityMode.Sign => MessageSecurityMode.Sign,
            OPCUAMessageSecurityMode.SignAndEncrypt => MessageSecurityMode.SignAndEncrypt,
            _ => MessageSecurityMode.None
        };

        return endpoints
            .Where(e => e.SecurityPolicyUri == targetSecurityPolicyUri && e.SecurityMode == targetSecurityMode)
            .OrderBy(e => e.SecurityLevel)
            .FirstOrDefault();
    }

    private IUserIdentity CreateUserIdentity()
    {
        if (!string.IsNullOrEmpty(_connectionConfig!.Username) && !string.IsNullOrEmpty(_connectionConfig.Password))
        {
            return new UserIdentity(_connectionConfig.Username, _connectionConfig.Password);
        }

        return new UserIdentity(new AnonymousIdentityToken());
    }

    private async Task SubscribeToConfiguredInputs()
    {
        List<OPCUAInputConfiguration> inputs;
        lock (_lockObject)
        {
            inputs = _inputs.Values.Where(i => i.IsEnabled).ToList();
        }

        Logger.LogInformation("Creating subscriptions for {Count} enabled inputs", inputs.Count);

        foreach (var input in inputs)
        {
            await CreateSubscription(input);
        }
    }

    private async Task CreateSubscription(OPCUAInputConfiguration config)
    {
        if (_session == null) return;

        try
        {
            // Create subscription
            var subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = config.PublishingIntervalMs,
                LifetimeCount = 0,
                MaxNotificationsPerPublish = 0,
                PublishingEnabled = true,
                TimestampsToReturn = TimestampsToReturn.Both
            };

            // Add monitored items for each node ID
            foreach (var nodeIdString in config.NodeIds)
            {
                try
                {
                    var nodeId = new NodeId(nodeIdString);
                    var monitoredItem = new MonitoredItem(subscription.DefaultItem)
                    {
                        StartNodeId = nodeId,
                        AttributeId = Attributes.Value,
                        SamplingInterval = config.SamplingIntervalMs,
                        QueueSize = 10,
                        DiscardOldest = true
                    };

                    monitoredItem.Notification += (item, e) => OnDataChange(item, e, config);
                    subscription.AddItem(monitoredItem);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Invalid node ID format: {NodeId}", nodeIdString);
                }
            }

            // Add subscription to session
            _session.AddSubscription(subscription);
            await Task.Run(() => subscription.Create());

            _subscriptions[config.Id] = subscription;
            Logger.LogInformation("Created OPC UA subscription {InputId} with {Count} monitored items", 
                config.Id, subscription.MonitoredItemCount);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create subscription for input {InputId}", config.Id);
        }
    }

    private void OnDataChange(MonitoredItem item, MonitoredItemNotificationEventArgs e, OPCUAInputConfiguration config)
    {
        try
        {
            foreach (var value in item.DequeueValues())
            {
                var dataPoint = CreateDataPoint(item, value, config);
                if (dataPoint != null)
                {
                    if (_connectionConfig?.EnableDetailedLogging == true)
                    {
                        Logger.LogDebug("Received data change for node {NodeId}: {Value}", 
                            item.StartNodeId, value.Value);
                    }

                    OnDataReceived(dataPoint, config.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing data change for node {NodeId}", item.StartNodeId);
        }
    }

    private DataPoint? CreateDataPoint(MonitoredItem item, DataValue value, OPCUAInputConfiguration config)
    {
        try
        {
            // Create topic from node ID and configuration
            var topic = BuildTopicFromNodeId(item.StartNodeId.ToString(), config);

            // Extract timestamps
            var timestamp = config.IncludeServerTimestamp && value.ServerTimestamp != DateTime.MinValue
                ? value.ServerTimestamp
                : config.IncludeSourceTimestamp && value.SourceTimestamp != DateTime.MinValue
                    ? value.SourceTimestamp
                    : DateTime.UtcNow;

            // Create data point
            var dataPoint = new DataPoint
            {
                Topic = topic,
                Value = value.Value,
                Timestamp = timestamp,
                Quality = value.StatusCode.ToString(),
                ConnectionId = ConnectionId,
                SourceSystem = "OPC UA",
                Metadata = new Dictionary<string, object>
                {
                    ["InputId"] = config.Id,
                    ["NodeId"] = item.StartNodeId.ToString(),
                    ["ServerUrl"] = _connectionConfig?.EndpointUrl ?? "",
                    ["StatusCode"] = value.StatusCode.ToString(),
                    ["Protocol"] = "OPC UA"
                }
            };

            if (config.IncludeServerTimestamp && value.ServerTimestamp != DateTime.MinValue)
                dataPoint.Metadata["ServerTimestamp"] = value.ServerTimestamp;

            if (config.IncludeSourceTimestamp && value.SourceTimestamp != DateTime.MinValue)
                dataPoint.Metadata["SourceTimestamp"] = value.SourceTimestamp;

            return dataPoint;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating data point for node {NodeId}", item.StartNodeId);
            return null;
        }
    }

    private string BuildTopicFromNodeId(string nodeId, OPCUAInputConfiguration config)
    {
        if (config.AutoMapToUNS && !string.IsNullOrEmpty(config.DefaultNamespace))
        {
            return $"{config.DefaultNamespace}/{nodeId}";
        }

        return $"{Name}/{nodeId}";
    }

    private async Task WriteToOutput(OPCUAOutputConfiguration output, DataPoint dataPoint, CancellationToken cancellationToken)
    {
        if (_session == null) return;

        try
        {
            var nodeId = new NodeId(output.NodeId);

            // Read current value to determine data type if validation is enabled
            if (output.ValidateDataTypes)
            {
                var readResult = await _session.ReadAsync(null, 0, TimestampsToReturn.Neither, 
                    new ReadValueIdCollection { new() { NodeId = nodeId, AttributeId = Attributes.Value } }, cancellationToken);

                if (!StatusCode.IsGood(readResult.Results?[0].StatusCode ?? StatusCodes.Bad))
                {
                    Logger.LogWarning("Cannot read current value for validation from node {NodeId}", output.NodeId);
                }
            }

            // Write value
            var writeValue = new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(dataPoint.Value))
            };

            var writeResult = await _session.WriteAsync(null, new WriteValueCollection { writeValue }, cancellationToken);
            
            if (StatusCode.IsGood(writeResult.Results?[0] ?? StatusCodes.Bad))
            {
                Logger.LogDebug("Successfully wrote value to OPC UA node {NodeId}: {Value}", 
                    output.NodeId, dataPoint.Value);
                IncrementDataSent();
            }
            else
            {
                Logger.LogError("Failed to write value to OPC UA node {NodeId}: {StatusCode}", 
                    output.NodeId, writeResult.Results?[0]);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error writing to OPC UA output {OutputId}", output.Id);
            throw;
        }
    }

    private List<OPCUAOutputConfiguration> GetApplicableOutputs(DataPoint dataPoint, string? outputId)
    {
        lock (_lockObject)
        {
            if (!string.IsNullOrEmpty(outputId))
            {
                return _outputs.TryGetValue(outputId, out var output) ? new List<OPCUAOutputConfiguration> { output } : new();
            }

            return _outputs.Values
                .Where(o => o.IsEnabled && MatchesTopicFilters(dataPoint.Topic, o.TopicFilters))
                .ToList();
        }
    }

    private bool MatchesTopicFilters(string topic, List<string> filters)
    {
        if (!filters.Any())
            return true;

        return filters.Any(filter => 
            string.IsNullOrWhiteSpace(filter) || 
            topic.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private void OnSessionKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsNotGood(e.Status))
        {
            Logger.LogWarning("OPC UA session keep alive failed: {Status}", e.Status);
            UpdateStatus(ConnectionStatus.Error, $"Keep alive failed: {e.Status}");
        }
    }

    private void OnSessionClosing(object? sender, EventArgs e)
    {
        _isConnected = false;
        UpdateStatus(ConnectionStatus.Disconnected, "OPC UA session closed");
        Logger.LogWarning("OPC UA session closed unexpectedly");
    }
}