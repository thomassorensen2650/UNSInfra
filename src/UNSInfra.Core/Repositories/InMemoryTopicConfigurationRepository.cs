using System.Collections.Concurrent;
using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Repositories;

/// <summary>
/// In-memory implementation of topic configuration repository for development and testing.
/// Stores configurations and rules in thread-safe dictionary structures.
/// </summary>
public class InMemoryTopicConfigurationRepository : ITopicConfigurationRepository
{
    private readonly ConcurrentDictionary<string, TopicConfiguration> _configurations = new();
    private readonly ConcurrentDictionary<string, TopicMappingRule> _mappingRules = new();

    /// <summary>
    /// Retrieves the topic configuration for a specific topic from memory.
    /// </summary>
    /// <param name="topic">The topic to get configuration for</param>
    /// <returns>The topic configuration, or null if not found</returns>
    public Task<TopicConfiguration?> GetTopicConfigurationAsync(string topic)
    {
        _configurations.TryGetValue(topic, out var configuration);
        return Task.FromResult(configuration);
    }

    /// <summary>
    /// Saves or updates a topic configuration in memory.
    /// </summary>
    /// <param name="configuration">The configuration to save</param>
    /// <returns>A completed task</returns>
    public Task SaveTopicConfigurationAsync(TopicConfiguration configuration)
    {
        configuration.ModifiedAt = DateTime.UtcNow;
        _configurations.AddOrUpdate(configuration.Topic, configuration, (key, existing) => configuration);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves all topic configurations from memory, optionally filtered by verification status.
    /// </summary>
    /// <param name="verifiedOnly">If true, returns only verified configurations</param>
    /// <returns>A collection of topic configurations</returns>
    public Task<IEnumerable<TopicConfiguration>> GetAllTopicConfigurationsAsync(bool verifiedOnly = false)
    {
        // Create a snapshot to avoid collection modified exceptions
        var configurations = _configurations.Values.ToList().AsEnumerable();
        if (verifiedOnly)
        {
            configurations = configurations.Where(c => c.IsVerified);
        }
        return Task.FromResult(configurations);
    }

    /// <summary>
    /// Retrieves all unverified topic configurations from memory.
    /// </summary>
    /// <returns>A collection of unverified topic configurations</returns>
    public Task<IEnumerable<TopicConfiguration>> GetUnverifiedTopicConfigurationsAsync()
    {
        // Create a snapshot to avoid collection modified exceptions
        var unverified = _configurations.Values.ToList().Where(c => !c.IsVerified);
        return Task.FromResult(unverified);
    }

    /// <summary>
    /// Deletes a topic configuration by its identifier from memory.
    /// </summary>
    /// <param name="configurationId">The unique identifier of the configuration to delete</param>
    /// <returns>A completed task</returns>
    public Task DeleteTopicConfigurationAsync(string configurationId)
    {
        var config = _configurations.Values.ToList().FirstOrDefault(c => c.Id == configurationId);
        if (config != null)
        {
            _configurations.TryRemove(config.Topic, out _);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves all topic mapping rules from memory ordered by priority.
    /// </summary>
    /// <returns>A collection of mapping rules ordered by priority (highest first)</returns>
    public Task<IOrderedEnumerable<TopicMappingRule>> GetTopicMappingRulesAsync()
    {
        // Create a snapshot to avoid collection modified exceptions
        var rules = _mappingRules.Values.ToList().OrderByDescending(r => r.Priority);
        return Task.FromResult(rules);
    }

    /// <summary>
    /// Saves or updates a topic mapping rule in memory.
    /// </summary>
    /// <param name="rule">The mapping rule to save</param>
    /// <returns>A completed task</returns>
    public Task SaveTopicMappingRuleAsync(TopicMappingRule rule)
    {
        _mappingRules.AddOrUpdate(rule.Id, rule, (key, existing) => rule);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Marks a topic configuration as verified by an administrator.
    /// </summary>
    /// <param name="configurationId">The identifier of the configuration to verify</param>
    /// <param name="verifiedBy">The user who verified the configuration</param>
    /// <returns>A completed task</returns>
    public Task VerifyTopicConfigurationAsync(string configurationId, string verifiedBy)
    {
        var config = _configurations.Values.ToList().FirstOrDefault(c => c.Id == configurationId);
        if (config != null)
        {
            config.IsVerified = true;
            config.ModifiedAt = DateTime.UtcNow;
            config.Metadata["VerifiedBy"] = verifiedBy;
            config.Metadata["VerifiedAt"] = DateTime.UtcNow;
            // Update the configuration in the dictionary
            _configurations.AddOrUpdate(config.Topic, config, (key, existing) => config);
        }
        return Task.CompletedTask;
    }
}