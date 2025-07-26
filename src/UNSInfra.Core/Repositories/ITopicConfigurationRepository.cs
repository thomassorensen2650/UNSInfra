using UNSInfra.Models.Hierarchy;

namespace UNSInfra.Repositories;

    /// <summary>
    /// Interface for managing topic configuration persistence and retrieval.
    /// Provides CRUD operations for topic mappings and discovery rules.
    /// </summary>
    public interface ITopicConfigurationRepository
    {
        /// <summary>
        /// Retrieves the topic configuration for a specific topic.
        /// </summary>
        /// <param name="topic">The topic to get configuration for</param>
        /// <returns>The topic configuration, or null if not found</returns>
        Task<TopicConfiguration?> GetTopicConfigurationAsync(string topic);

        /// <summary>
        /// Saves or updates a topic configuration.
        /// </summary>
        /// <param name="configuration">The configuration to save</param>
        /// <returns>A task representing the asynchronous save operation</returns>
        Task SaveTopicConfigurationAsync(TopicConfiguration configuration);

        /// <summary>
        /// Retrieves all topic configurations, optionally filtered by verification status.
        /// </summary>
        /// <param name="verifiedOnly">If true, returns only verified configurations</param>
        /// <returns>A collection of topic configurations</returns>
        Task<IEnumerable<TopicConfiguration>> GetAllTopicConfigurationsAsync(bool verifiedOnly = false);

        /// <summary>
        /// Retrieves all unverified topic configurations that need administrator review.
        /// </summary>
        /// <returns>A collection of unverified topic configurations</returns>
        Task<IEnumerable<TopicConfiguration>> GetUnverifiedTopicConfigurationsAsync();

        /// <summary>
        /// Deletes a topic configuration by its identifier.
        /// </summary>
        /// <param name="configurationId">The unique identifier of the configuration to delete</param>
        /// <returns>A task representing the asynchronous delete operation</returns>
        Task DeleteTopicConfigurationAsync(string configurationId);

        /// <summary>
        /// Retrieves all topic mapping rules ordered by priority.
        /// </summary>
        /// <returns>A collection of mapping rules ordered by priority (highest first)</returns>
        Task<IOrderedEnumerable<TopicMappingRule>> GetTopicMappingRulesAsync();

        /// <summary>
        /// Saves or updates a topic mapping rule.
        /// </summary>
        /// <param name="rule">The mapping rule to save</param>
        /// <returns>A task representing the asynchronous save operation</returns>
        Task SaveTopicMappingRuleAsync(TopicMappingRule rule);

        /// <summary>
        /// Marks a topic configuration as verified by an administrator.
        /// </summary>
        /// <param name="configurationId">The identifier of the configuration to verify</param>
        /// <param name="verifiedBy">The user who verified the configuration</param>
        /// <returns>A task representing the asynchronous verification operation</returns>
        Task VerifyTopicConfigurationAsync(string configurationId, string verifiedBy);
    }