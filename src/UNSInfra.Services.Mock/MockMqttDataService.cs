using UNSInfra.Services.TopicDiscovery;

namespace UNSInfra.Services.DataIngestion.Mock;

using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
    
    /// <summary>
    /// Enhanced MQTT data service that supports dynamic topic discovery and configuration.
    /// Automatically handles unknown topics by creating unverified configurations.
    /// </summary>
    public class MockMqttDataService : IMqttDataService
    {
        private readonly Dictionary<string, HierarchicalPath> _subscriptions = new();
        private readonly ITopicDiscoveryService _topicDiscoveryService;
        private bool _isRunning;

        /// <summary>
        /// Event raised when new data is received from the MQTT broker.
        /// </summary>
        public event EventHandler<DataPoint>? DataReceived;

        /// <summary>
        /// Initializes a new instance of the EnhancedMockMqttDataService.
        /// </summary>
        /// <param name="topicDiscoveryService">Service for discovering and mapping unknown topics</param>
        public MockMqttDataService(ITopicDiscoveryService topicDiscoveryService)
        {
            _topicDiscoveryService = topicDiscoveryService;
        }

        /// <summary>
        /// Starts the enhanced MQTT service with topic discovery capabilities.
        /// </summary>
        /// <returns>A completed task</returns>
        public Task StartAsync()
        {
            _isRunning = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the enhanced MQTT service and clears all subscriptions.
        /// </summary>
        /// <returns>A completed task</returns>
        public Task StopAsync()
        {
            _isRunning = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscribes to an MQTT topic with explicit path mapping.
        /// </summary>
        /// <param name="topic">The MQTT topic to subscribe to</param>
        /// <param name="path">The ISA-S95 hierarchical path for data from this topic</param>
        /// <returns>A completed task</returns>
        public Task SubscribeToTopicAsync(string topic, HierarchicalPath path)
        {
            _subscriptions[topic] = path;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unsubscribes from an MQTT topic.
        /// </summary>
        /// <param name="topic">The MQTT topic to unsubscribe from</param>
        /// <returns>A completed task</returns>
        public Task UnsubscribeFromTopicAsync(string topic)
        {
            _subscriptions.Remove(topic);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Simulates receiving data from an MQTT topic with automatic topic discovery.
        /// If the topic is unknown, it will be automatically configured as unverified.
        /// </summary>
        /// <param name="topic">The topic that received data</param>
        /// <param name="payload">The data payload to simulate</param>
        public async void SimulateDataReceived(string topic, object payload)
        {
            HierarchicalPath? path = null;

            // Check if we have explicit subscription
            if (_subscriptions.TryGetValue(topic, out var explicitPath))
            {
                path = explicitPath;
            }
            else
            {
                // Try to resolve using topic discovery
                var configuration = await _topicDiscoveryService.ResolveTopicAsync(topic, "MQTT");
                if (configuration != null)
                {
                    path = configuration.Path;
                    
                    // If it's unverified, log it for administrator attention
                    if (!configuration.IsVerified)
                    {
                        Console.WriteLine($"WARNING: Received data for unverified topic '{topic}'. Please review topic configuration.");
                    }
                }
                else
                {
                    // Create unverified configuration for completely unknown topic
                    configuration = await _topicDiscoveryService.CreateUnverifiedTopicAsync(topic, "MQTT");
                    path = configuration.Path;
                    Console.WriteLine($"INFO: Created unverified configuration for new topic '{topic}'");
                }
            }

            if (path != null)
            {
                var dataPoint = new DataPoint
                {
                    Topic = topic,
                    Path = path,
                    Value = payload,
                    Source = "MQTT"
                };
                DataReceived?.Invoke(this, dataPoint);
            }
        }
    }
