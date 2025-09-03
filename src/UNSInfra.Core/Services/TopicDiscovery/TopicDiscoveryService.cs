using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services;

namespace UNSInfra.Services.TopicDiscovery;

/// <summary>
/// Service for discovering and automatically mapping unknown topics to hierarchical paths.
/// Uses pattern matching and configurable rules to generate topic configurations.
/// </summary>
public class TopicDiscoveryService : ITopicDiscoveryService
{
    private readonly ITopicConfigurationRepository _configurationRepository;
    private readonly IHierarchyService _hierarchyService;
    private readonly ILogger<TopicDiscoveryService> _logger;

    /// <summary>
    /// Initializes a new instance of the TopicDiscoveryService.
    /// </summary>
    /// <param name="configurationRepository">Repository for managing topic configurations</param>
    /// <param name="hierarchyService">Service for managing hierarchy configurations</param>
    /// <param name="logger">Logger instance</param>
    public TopicDiscoveryService(ITopicConfigurationRepository configurationRepository, IHierarchyService hierarchyService, ILogger<TopicDiscoveryService> logger)
    {
        _configurationRepository = configurationRepository;
        _hierarchyService = hierarchyService;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to resolve a topic to a hierarchical path using existing configuration or discovery rules.
    /// </summary>
    /// <param name="topic">The topic to resolve</param>
    /// <param name="sourceType">The source system type (MQTT, Kafka, etc.)</param>
    /// <returns>A topic configuration with the resolved path, or null if no mapping could be determined</returns>
    public async Task<TopicConfiguration?> ResolveTopicAsync(string topic, string sourceType)
    {
        _logger.LogDebug("Resolving topic: '{Topic}' from source: '{SourceType}'", topic, sourceType);
        
        // First, check if we have an existing configuration
        var existingConfig = await _configurationRepository.GetTopicConfigurationAsync(topic);
        if (existingConfig != null && existingConfig.IsActive)
        {
            _logger.LogDebug("Found existing configuration for topic: '{Topic}'", topic);
            return existingConfig;
        }

        // Try to generate a path using mapping rules
        var generatedPath = await GeneratePathFromTopicAsync(topic);
        if (generatedPath == null)
        {
            // Fall back to default path generation
            _logger.LogDebug("No mapping rules matched, using default path generation for: '{Topic}'", topic);
            generatedPath = await GenerateDefaultPathAsync(topic);
        }
        else
        {
            _logger.LogDebug("Generated path from mapping rules for topic: '{Topic}'", topic);
        }

        _logger.LogDebug("Generated path: {Path}", generatedPath.GetFullPath());

        // Create and save a new configuration
        var newConfig = await CreateUnverifiedTopicAsync(topic, sourceType, generatedPath);
        await _configurationRepository.SaveTopicConfigurationAsync(newConfig);
        _logger.LogDebug("Created and saved unverified configuration for topic: '{Topic}'", topic);
        return newConfig;
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
        var path = suggestedPath ?? await GenerateDefaultPathAsync(topic);
        
        // Validate that topics can be mapped to this hierarchical path
        var validation = await _hierarchyService.ValidateTopicMappingAsync(path);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Cannot create topic configuration for '{Topic}': {Errors}", 
                topic, string.Join(", ", validation.Errors));
            throw new InvalidOperationException($"Cannot map topic '{topic}' to hierarchical path: {string.Join(", ", validation.Errors)}");
        }

        var configuration = new TopicConfiguration
        {
            Topic = topic,
            Path = path,
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
                    
                    var generatedPath = await _hierarchyService.CreatePathFromStringAsync(pathString);
                    
                    // Validate that topics can be mapped to this path
                    var validation = await _hierarchyService.ValidateTopicMappingAsync(generatedPath);
                    if (validation.IsValid)
                    {
                        return generatedPath;
                    }
                    else
                    {
                        _logger.LogWarning("Generated path for topic '{Topic}' failed validation: {Errors}", 
                            topic, string.Join(", ", validation.Errors));
                        continue; // Try next rule
                    }
                }
            }
            catch (Exception ex)
            {
                // Log regex or template errors but continue with other rules
                _logger.LogWarning(ex, "Error processing mapping rule {RuleId}: {Message}", rule.Id, ex.Message);
            }
        }
        
        return null;
    }

    /// <summary>
    /// Generates a default hierarchical path when no rules match.
    /// Uses the topic structure to create a basic path mapping with dynamic hierarchy support.
    /// </summary>
    /// <param name="topic">The topic to generate a default path for</param>
    /// <returns>A default hierarchical path based on topic structure</returns>
    private async Task<HierarchicalPath> GenerateDefaultPathAsync(string topic)
    {
        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var hierarchyLevels = await _hierarchyService.GetHierarchyLevelsAsync();
        
        // Handle SocketIO topics which have explicit hierarchy format
        if (parts.Length > 2 && (parts[0] == "socketio" || parts[0] == "virtualfactory") && parts[1] == "update")
        {
            // Skip the first two parts (socketio/update) and map the rest to hierarchy levels
            var hierarchyParts = parts.Skip(2).ToArray();
            var pathString = string.Join("/", hierarchyParts.Take(hierarchyLevels.Count));
            return await _hierarchyService.CreatePathFromStringAsync(pathString);
        }
        
        // Default topic mapping - map topic parts to hierarchy levels in order
        var mappedParts = new List<string>();
        
        for (int i = 0; i < hierarchyLevels.Count; i++)
        {
            if (i < parts.Length)
            {
                mappedParts.Add(parts[i]);
            }
            else
            {
                // Fill missing levels with defaults based on level type
                var levelName = hierarchyLevels[i].ToLower();
                var defaultValue = levelName switch
                {
                    "enterprise" => "MQTT",
                    "site" => "Default",
                    "area" => "Area1",
                    "property" => parts.Length > 0 ? parts[^1] : "value",
                    _ => "Unknown"
                };
                mappedParts.Add(defaultValue);
            }
        }
        
        var defaultPathString = string.Join("/", mappedParts);
        return await _hierarchyService.CreatePathFromStringAsync(defaultPathString);
    }
}