using Microsoft.AspNetCore.Mvc;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.ConnectionSDK.Abstractions;
using UNSInfra.Core.Repositories;
using UNSInfra.Repositories;
using UNSInfra.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace UNSInfra.UI.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class ApiController : ControllerBase
{
    private readonly CachedTopicBrowserService _cachedTopicBrowserService;
    private readonly IConnectionManager _connectionManager;
    private readonly ITopicConfigurationRepository _topicConfigurationRepository;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        CachedTopicBrowserService cachedTopicBrowserService,
        IConnectionManager connectionManager,
        ITopicConfigurationRepository topicConfigurationRepository,
        ILogger<ApiController> logger)
    {
        _cachedTopicBrowserService = cachedTopicBrowserService;
        _connectionManager = connectionManager;
        _topicConfigurationRepository = topicConfigurationRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get the complete UNS hierarchy structure
    /// </summary>
    [HttpGet("hierarchy")]
    public async Task<IActionResult> GetHierarchy()
    {
        try
        {
            var topics = await _cachedTopicBrowserService.GetLatestTopicStructureAsync();
            var topicList = topics.ToList();
            
            // Group by namespace for better organization
            var namespacedTopics = topicList
                .GroupBy(t => !string.IsNullOrEmpty(t.NSPath) ? t.NSPath.Split('/')[0] : "unassigned")
                .ToDictionary(g => g.Key, g => g.ToList());

            return Ok(new
            {
                success = true,
                message = "UNS hierarchy retrieved successfully",
                totalTopics = topicList.Count,
                assignedTopics = topicList.Count(t => !string.IsNullOrEmpty(t.NSPath)),
                namespaces = namespacedTopics.Keys.ToList(),
                topics = namespacedTopics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving UNS hierarchy");
            return StatusCode(500, new
            {
                success = false,
                message = "Error retrieving UNS hierarchy",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get the current value of a specific topic
    /// </summary>
    [HttpGet("topics/{topicName}/current")]
    public async Task<IActionResult> GetTopicCurrentValue(
        [FromRoute, Required] string topicName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(topicName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Topic name is required"
                });
            }

            // URL decode the topic name
            topicName = Uri.UnescapeDataString(topicName);

            _logger.LogDebug("Getting current value for topic: {TopicName}", topicName);

            // Get topic configuration
            var allTopics = await _topicConfigurationRepository.GetAllTopicConfigurationsAsync();
            var topicConfig = allTopics.FirstOrDefault(t => 
                t.Topic.Equals(topicName, StringComparison.OrdinalIgnoreCase));

            if (topicConfig == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Topic '{topicName}' not found",
                    topicName = topicName
                });
            }

            // Get current data from cached topic browser service
            var topics = await _cachedTopicBrowserService.GetLatestTopicStructureAsync();
            var currentTopic = topics.FirstOrDefault(t => 
                t.Topic.Equals(topicName, StringComparison.OrdinalIgnoreCase));

            return Ok(new
            {
                success = true,
                message = currentTopic != null ? "Topic configuration retrieved successfully" : "Topic configured but no current data available",
                topicName = topicName,
                topic = new
                {
                    topic = topicConfig.Topic,
                    unsName = topicConfig.UNSName,
                    hierarchicalPath = topicConfig.Path?.ToString(),
                    lastDataTimestamp = currentTopic?.LastDataTimestamp,
                    lastUpdated = currentTopic?.ModifiedAt ?? topicConfig.ModifiedAt,
                    sourceType = topicConfig.SourceType,
                    isVerified = topicConfig.IsVerified,
                    isActive = currentTopic?.IsActive ?? false,
                    hasCurrentData = currentTopic != null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current value for topic: {TopicName}", topicName);
            return StatusCode(500, new
            {
                success = false,
                message = "Error retrieving topic value",
                topicName = topicName,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get topics by namespace
    /// </summary>
    [HttpGet("namespaces/{namespaceName}/topics")]
    public async Task<IActionResult> GetTopicsByNamespace(
        [FromRoute, Required] string namespaceName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Namespace name is required"
                });
            }

            // URL decode the namespace name
            namespaceName = Uri.UnescapeDataString(namespaceName);

            _logger.LogDebug("Getting topics for namespace: {NamespaceName}", namespaceName);

            var topics = await _cachedTopicBrowserService.GetLatestTopicStructureAsync();
            var namespacedTopics = topics
                .Where(t => !string.IsNullOrEmpty(t.NSPath) && 
                           t.NSPath.StartsWith(namespaceName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Ok(new
            {
                success = true,
                message = $"Topics for namespace '{namespaceName}' retrieved successfully",
                namespaceName = namespaceName,
                totalTopics = namespacedTopics.Count,
                topics = namespacedTopics.Select(t => new
                {
                    topic = t.Topic,
                    unsName = t.UNSName,
                    nsPath = t.NSPath,
                    lastDataTimestamp = t.LastDataTimestamp,
                    lastUpdated = t.ModifiedAt,
                    isActive = t.IsActive
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving topics for namespace: {NamespaceName}", namespaceName);
            return StatusCode(500, new
            {
                success = false,
                message = $"Error retrieving topics for namespace '{namespaceName}'",
                namespaceName = namespaceName,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get system status and statistics
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetSystemStatus()
    {
        try
        {
            var topics = await _cachedTopicBrowserService.GetLatestTopicStructureAsync();
            var topicList = topics.ToList();
            
            var connectionConfigurations = _connectionManager.GetAllConnectionConfigurations().ToList();
            var enabledConfigurations = connectionConfigurations.Where(c => c.IsEnabled).ToList();

            return Ok(new
            {
                success = true,
                message = "System status retrieved successfully",
                timestamp = DateTime.UtcNow,
                status = new
                {
                    totalTopics = topicList.Count,
                    assignedTopics = topicList.Count(t => !string.IsNullOrEmpty(t.NSPath)),
                    activeTopics = topicList.Count(t => t.IsActive),
                    totalConnections = connectionConfigurations.Count,
                    activeConnections = enabledConfigurations.Count,
                    namespaces = topicList
                        .Where(t => !string.IsNullOrEmpty(t.NSPath))
                        .Select(t => t.NSPath!.Split('/')[0])
                        .Distinct()
                        .Count()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system status");
            return StatusCode(500, new
            {
                success = false,
                message = "Error retrieving system status",
                error = ex.Message
            });
        }
    }
}