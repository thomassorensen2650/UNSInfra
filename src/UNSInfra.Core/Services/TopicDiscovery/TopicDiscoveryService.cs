using System.Text.RegularExpressions;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;

namespace UNSInfra.Services.TopicDiscovery;

/// <summary>
/// Service for discovering and automatically mapping unknown topics to hierarchical paths.
/// Uses pattern matching and configurable rules to generate topic configurations.
/// </summary>
public class TopicDiscoveryService : ITopicDiscoveryService
{
    private readonly ITopicConfigurationRepository _configurationRepository;

    /// <summary>
    /// Initializes a new instance of the TopicDiscoveryService.
    /// </summary>
    /// <param name="configurationRepository">Repository for managing topic configurations</param>
    public TopicDiscoveryService(ITopicConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository;
    }

    /// <summary>
    /// Attempts to resolve a topic to a hierarchical path using existing configuration or discovery rules.
    /// </summary>
    /// <param name="topic">The topic to resolve</param>
    /// <param name="sourceType">The source system type (MQTT, Kafka, etc.)</param>
    /// <returns>A topic configuration with the resolved path, or null if no mapping could be determined</returns>
    public async Task<TopicConfiguration?> ResolveTopicAsync(string topic, string sourceType)
    {
        // First, check if we have an existing configuration
        var existingConfig = await _configurationRepository.GetTopicConfigurationAsync(topic);
        if (existingConfig != null && existingConfig.IsActive)
        {
            return existingConfig;
        }

        // Try to generate a path using mapping rules
        var generatedPath = await GeneratePathFromTopicAsync(topic);
        if (generatedPath != null)
        {
            // Create and save a new configuration
            var newConfig = await CreateUnverifiedTopicAsync(topic, sourceType, generatedPath);
            await _configurationRepository.SaveTopicConfigurationAsync(newConfig);
            return newConfig;
        }

        return null;
    }

    /// <summary>
    /// Creates a new unverified topic configuration for an unknown topic.
    /// </summary>
    /// <param name="topic">The unknown topic</param>
    /// <param name="sourceType">The source system type</param>
    /// <param name="suggestedPath">Optional suggested hierarchical path</param>
    /// <returns>The newly created topic configuration</returns>
    public async Task<TopicConfiguration> CreateUnverifiedTopicAsync(string topic, string sourceType, HierarchicalPath? suggestedPath = null)
    {
        var configuration = new TopicConfiguration
        {
            Topic = topic,
            Path = suggestedPath ?? GenerateDefaultPath(topic),
            IsVerified = false,
            SourceType = sourceType,
            CreatedBy = "AutoDiscovery",
            Description = $"Auto-discovered topic from {sourceType}",
            Metadata = new Dictionary<string, object>
            {
                { "AutoDiscovered", true },
                { "DiscoveredAt", DateTime.UtcNow }
            }
        };

        await _configurationRepository.SaveTopicConfigurationAsync(configuration);
        return configuration;
    }

    /// <summary>
    /// Attempts to generate a hierarchical path from a topic using mapping rules.
    /// </summary>
    /// <param name="topic">The topic to generate a path for</param>
    /// <returns>A generated hierarchical path, or null if no rules match</returns>
    public async Task<HierarchicalPath?> GeneratePathFromTopicAsync(string topic)
    {
        var rules = await _configurationRepository.GetTopicMappingRulesAsync();
        
        foreach (var rule in rules.Where(r => r.IsActive))
        {
            try
            {
                var regex = new Regex(rule.TopicPattern, RegexOptions.IgnoreCase);
                var match = regex.Match(topic);
                
                if (match.Success)
                {
                    var pathString = rule.PathTemplate;
                    
                    // Replace numbered placeholders {0}, {1}, etc. with regex groups
                    for (int i = 0; i < match.Groups.Count; i++)
                    {
                        pathString = pathString.Replace($"{{{i}}}", match.Groups[i].Value);
                    }
                    
                    // Replace named placeholders with named groups
                    foreach (var groupName in regex.GetGroupNames().Skip(1)) // Skip group 0 (entire match)
                    {
                        if (match.Groups[groupName].Success)
                        {
                            pathString = pathString.Replace($"{{{groupName}}}", match.Groups[groupName].Value);
                        }
                    }
                    
                    return HierarchicalPath.FromPath(pathString);
                }
            }
            catch (Exception ex)
            {
                // Log regex or template errors but continue with other rules
                Console.WriteLine($"Error processing mapping rule {rule.Id}: {ex.Message}");
            }
        }
        
        return null;
    }

    /// <summary>
    /// Generates a default hierarchical path when no rules match.
    /// Uses the topic structure to create a basic path mapping.
    /// </summary>
    /// <param name="topic">The topic to generate a default path for</param>
    /// <returns>A default hierarchical path based on topic structure</returns>
    private HierarchicalPath GenerateDefaultPath(string topic)
    {
        // Simple default: treat the topic as the property and create placeholder hierarchy
        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        return new HierarchicalPath
        {
            Enterprise = "Unknown",
            Site = "Unknown", 
            Area = "Unknown",
            WorkCenter = parts.Length > 1 ? parts[^2] : "Unknown", // Second to last part
            WorkUnit = "Unknown",
            Property = parts[^1] // Last part as property
        };
    }
}