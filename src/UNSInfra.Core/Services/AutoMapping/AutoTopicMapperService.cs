using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Data;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Repositories;
using UNSInfra.Services;

namespace UNSInfra.Services.AutoMapping;

/// <summary>
/// Service for automatically mapping topics to existing UNS tree structures.
/// </summary>
public class AutoTopicMapperService : IAutoTopicMapper
{
    private readonly INamespaceStructureService _namespaceStructureService;
    private readonly ITopicConfigurationRepository _topicConfigurationRepository;
    private readonly IHierarchyService _hierarchyService;
    private readonly ILogger<AutoTopicMapperService> _logger;

    public AutoTopicMapperService(
        INamespaceStructureService namespaceStructureService,
        ITopicConfigurationRepository topicConfigurationRepository,
        IHierarchyService hierarchyService,
        ILogger<AutoTopicMapperService> logger)
    {
        _namespaceStructureService = namespaceStructureService;
        _topicConfigurationRepository = topicConfigurationRepository;
        _hierarchyService = hierarchyService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TopicConfiguration?> TryMapTopicAsync(string topic, string sourceType, AutoTopicMapperConfiguration autoMapperConfig)
    {
        if (!autoMapperConfig.Enabled)
        {
            _logger.LogDebug("Auto mapping is disabled for topic: {Topic}", topic);
            return null;
        }

        _logger.LogDebug("Attempting to auto-map topic: {Topic} from source: {SourceType}", topic, sourceType);

        var mappingResult = await ValidateAutoMappingAsync(topic, autoMapperConfig);
        
        if (!mappingResult.Success || mappingResult.Confidence < autoMapperConfig.MinimumConfidence)
        {
            _logger.LogDebug("Auto mapping failed for topic: {Topic}. Success: {Success}, Confidence: {Confidence}", 
                topic, mappingResult.Success, mappingResult.Confidence);
            return null;
        }

        // Create the topic configuration
        var topicConfiguration = new TopicConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Topic = topic,
            Path = mappingResult.HierarchicalPath ?? new HierarchicalPath(),
            NSPath = mappingResult.MappedPath ?? string.Empty,
            SourceType = sourceType,
            IsVerified = false,
            CreatedBy = "AutoMapper",
            Description = $"Auto-mapped with {mappingResult.Confidence:P0} confidence",
            Metadata = new Dictionary<string, object>
            {
                { "AutoMapped", true },
                { "AutoMappedAt", DateTime.UtcNow },
                { "MappingConfidence", mappingResult.Confidence },
                { "MappingRule", mappingResult.UsedRule?.Description ?? "Pattern matching" },
                { "MappingDetails", mappingResult.Details }
            }
        };

        _logger.LogInformation("Successfully auto-mapped topic: {Topic} to path: {MappedPath} with confidence: {Confidence:P0}", 
            topic, mappingResult.MappedPath, mappingResult.Confidence);

        return topicConfiguration;
    }

    /// <inheritdoc />
    public async Task<AutoMappingResult> ValidateAutoMappingAsync(string topic, AutoTopicMapperConfiguration autoMapperConfig)
    {
        try
        {
            // First try custom rules
            var customRuleResult = await TryCustomRulesAsync(topic, autoMapperConfig);
            if (customRuleResult.Success && customRuleResult.Confidence >= autoMapperConfig.MinimumConfidence)
            {
                return customRuleResult;
            }

            // Then try pattern-based matching
            var patternResult = await TryPatternMatchingAsync(topic, autoMapperConfig);
            if (patternResult.Success && patternResult.Confidence >= autoMapperConfig.MinimumConfidence)
            {
                return patternResult;
            }

            // Return the best result we found, even if it doesn't meet the confidence threshold
            var bestResult = customRuleResult.Confidence > patternResult.Confidence ? customRuleResult : patternResult;
            
            if (!bestResult.Success)
            {
                bestResult.ErrorMessage = "No matching UNS path found for topic";
            }

            return bestResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto mapping validation for topic: {Topic}", topic);
            return new AutoMappingResult
            {
                Success = false,
                Confidence = 0.0,
                ErrorMessage = $"Auto mapping failed: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<List<AutoMappingSuggestion>> GetAutoMappingSuggestionsAsync(string topic, AutoTopicMapperConfiguration autoMapperConfig)
    {
        var suggestions = new List<AutoMappingSuggestion>();

        try
        {
            // Get all UNS tree nodes
            var nsStructure = await _namespaceStructureService.GetNamespaceStructureAsync();
            var cleanedTopic = CleanTopic(topic, autoMapperConfig);

            // Find potential matches based on topic structure
            await FindPatternMatchesRecursive(nsStructure, cleanedTopic, suggestions, autoMapperConfig, "", 0);

            // Sort by confidence descending
            suggestions = suggestions.OrderByDescending(s => s.Confidence).Take(10).ToList();

            _logger.LogDebug("Found {Count} auto mapping suggestions for topic: {Topic}", suggestions.Count, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating auto mapping suggestions for topic: {Topic}", topic);
        }

        return suggestions;
    }

    private async Task<AutoMappingResult> TryCustomRulesAsync(string topic, AutoTopicMapperConfiguration autoMapperConfig)
    {
        foreach (var rule in autoMapperConfig.CustomRules.Where(r => r.IsActive))
        {
            try
            {
                var regex = new Regex(rule.TopicPattern, RegexOptions.IgnoreCase);
                var match = regex.Match(topic);

                if (match.Success)
                {
                    var unsPath = rule.UNSPathTemplate;
                    
                    // Replace numbered placeholders
                    for (int i = 0; i < match.Groups.Count; i++)
                    {
                        unsPath = unsPath.Replace($"{{{i}}}", match.Groups[i].Value);
                    }

                    // Replace named placeholders
                    foreach (var groupName in regex.GetGroupNames().Skip(1))
                    {
                        if (match.Groups[groupName].Success)
                        {
                            unsPath = unsPath.Replace($"{{{groupName}}}", match.Groups[groupName].Value);
                        }
                    }

                    // Validate that this UNS path exists
                    var hierarchicalPath = await _hierarchyService.CreatePathFromStringAsync(unsPath);
                    var pathExists = await ValidateUNSPathExists(unsPath);

                    if (pathExists)
                    {
                        return new AutoMappingResult
                        {
                            Success = true,
                            Confidence = rule.Confidence,
                            MappedPath = unsPath,
                            HierarchicalPath = hierarchicalPath,
                            UsedRule = rule,
                            Details = new Dictionary<string, object>
                            {
                                { "MatchedGroups", match.Groups.Cast<Group>().Select(g => g.Value).ToArray() },
                                { "RuleDescription", rule.Description ?? "Custom rule" }
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing custom rule with pattern: {Pattern}", rule.TopicPattern);
            }
        }

        return new AutoMappingResult { Success = false, Confidence = 0.0 };
    }

    private async Task<AutoMappingResult> TryPatternMatchingAsync(string topic, AutoTopicMapperConfiguration autoMapperConfig)
    {
        var cleanedTopic = CleanTopic(topic, autoMapperConfig);
        var topicParts = cleanedTopic.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (topicParts.Length == 0)
        {
            return new AutoMappingResult { Success = false, Confidence = 0.0 };
        }

        // Get all UNS structure
        var nsStructure = await _namespaceStructureService.GetNamespaceStructureAsync();
        
        // Try to find the best matching path
        var bestMatch = await FindBestPatternMatch(nsStructure, topicParts, autoMapperConfig);

        return bestMatch;
    }

    private async Task<AutoMappingResult> FindBestPatternMatch(IEnumerable<NSTreeNode> nodes, string[] topicParts, AutoTopicMapperConfiguration autoMapperConfig)
    {
        var bestResult = new AutoMappingResult { Success = false, Confidence = 0.0 };

        foreach (var node in nodes)
        {
            var result = await CheckNodeMatch(node, topicParts, autoMapperConfig, 0);
            if (result.Confidence > bestResult.Confidence)
            {
                bestResult = result;
            }

            // Recursively check children
            if (node.Children.Any())
            {
                var childResult = await FindBestPatternMatch(node.Children, topicParts, autoMapperConfig);
                if (childResult.Confidence > bestResult.Confidence)
                {
                    bestResult = childResult;
                }
            }
        }

        return bestResult;
    }

    private async Task<AutoMappingResult> CheckNodeMatch(NSTreeNode node, string[] topicParts, AutoTopicMapperConfiguration autoMapperConfig, int depth)
    {
        if (depth >= autoMapperConfig.MaxSearchDepth || depth >= topicParts.Length)
        {
            return new AutoMappingResult { Success = false, Confidence = 0.0 };
        }

        var topicPart = topicParts[depth];
        var nodeName = node.Name;

        // Check for exact match (case sensitive or insensitive)
        var isMatch = autoMapperConfig.CaseSensitive 
            ? string.Equals(nodeName, topicPart, StringComparison.Ordinal)
            : string.Equals(nodeName, topicPart, StringComparison.OrdinalIgnoreCase);

        if (!isMatch)
        {
            // Try partial matching for better suggestions
            var similarity = CalculateStringSimilarity(nodeName, topicPart);
            if (similarity < 0.5) // Minimum similarity threshold
            {
                return new AutoMappingResult { Success = false, Confidence = 0.0 };
            }
        }

        // If this is the last topic part or we've found a good match
        if (depth == topicParts.Length - 1 || isMatch)
        {
            var confidence = isMatch ? 1.0 : CalculateStringSimilarity(nodeName, topicPart);
            
            // Apply confidence penalty for depth and partial matches
            confidence *= Math.Pow(0.95, depth); // Slight penalty for deeper matches
            
            var hierarchicalPath = await _hierarchyService.CreatePathFromStringAsync(node.FullPath);

            return new AutoMappingResult
            {
                Success = true,
                Confidence = confidence,
                MappedPath = node.FullPath,
                HierarchicalPath = hierarchicalPath,
                Details = new Dictionary<string, object>
                {
                    { "MatchType", isMatch ? "Exact" : "Partial" },
                    { "MatchDepth", depth },
                    { "NodeType", node.NodeType.ToString() }
                }
            };
        }

        // Continue searching deeper if we have more topic parts
        if (node.Children.Any() && depth < topicParts.Length - 1)
        {
            return await FindBestPatternMatch(node.Children, topicParts, autoMapperConfig);
        }

        return new AutoMappingResult { Success = false, Confidence = 0.0 };
    }

    private async Task FindPatternMatchesRecursive(IEnumerable<NSTreeNode> nodes, string cleanedTopic, List<AutoMappingSuggestion> suggestions, AutoTopicMapperConfiguration autoMapperConfig, string currentPath, int depth)
    {
        if (depth >= autoMapperConfig.MaxSearchDepth)
            return;

        var topicParts = cleanedTopic.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var node in nodes)
        {
            var fullPath = string.IsNullOrEmpty(currentPath) ? node.Name : $"{currentPath}/{node.Name}";
            
            // Calculate similarity with the topic
            var similarity = CalculatePathSimilarity(fullPath, cleanedTopic, topicParts);
            
            if (similarity > 0.3) // Minimum threshold for suggestions
            {
                var hierarchicalPath = await _hierarchyService.CreatePathFromStringAsync(node.FullPath);
                
                suggestions.Add(new AutoMappingSuggestion
                {
                    UNSPath = node.FullPath,
                    Confidence = similarity,
                    Reason = $"Path similarity match (depth: {depth})",
                    HierarchicalPath = hierarchicalPath,
                    RequiresNewNodes = false
                });
            }

            // Recursively check children
            if (node.Children.Any())
            {
                await FindPatternMatchesRecursive(node.Children, cleanedTopic, suggestions, autoMapperConfig, fullPath, depth + 1);
            }
        }
    }

    private string CleanTopic(string topic, AutoTopicMapperConfiguration autoMapperConfig)
    {
        var cleaned = topic;

        // Strip prefixes
        foreach (var prefix in autoMapperConfig.StripPrefixes)
        {
            if (cleaned.StartsWith(prefix, autoMapperConfig.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(prefix.Length);
                break;
            }
        }


        return cleaned.Trim('/');
    }

    private double CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0.0;

        if (str1 == str2)
            return 1.0;

        // Simple Levenshtein distance-based similarity
        var distance = CalculateLevenshteinDistance(str1.ToLowerInvariant(), str2.ToLowerInvariant());
        var maxLength = Math.Max(str1.Length, str2.Length);
        
        return Math.Max(0.0, 1.0 - (double)distance / maxLength);
    }

    private double CalculatePathSimilarity(string path1, string path2, string[] path2Parts)
    {
        var path1Parts = path1.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (path1Parts.Length == 0 || path2Parts.Length == 0)
            return 0.0;

        // Calculate similarity based on matching path segments
        var matchCount = 0;
        var maxLength = Math.Max(path1Parts.Length, path2Parts.Length);

        for (int i = 0; i < Math.Min(path1Parts.Length, path2Parts.Length); i++)
        {
            var similarity = CalculateStringSimilarity(path1Parts[i], path2Parts[i]);
            if (similarity > 0.7) // Threshold for considering a match
            {
                matchCount++;
            }
        }

        return (double)matchCount / maxLength;
    }

    private int CalculateLevenshteinDistance(string str1, string str2)
    {
        var matrix = new int[str1.Length + 1, str2.Length + 1];

        for (int i = 0; i <= str1.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= str2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= str1.Length; i++)
        {
            for (int j = 1; j <= str2.Length; j++)
            {
                var cost = str1[i - 1] == str2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(Math.Min(
                    matrix[i - 1, j] + 1,      // deletion
                    matrix[i, j - 1] + 1),     // insertion
                    matrix[i - 1, j - 1] + cost); // substitution
            }
        }

        return matrix[str1.Length, str2.Length];
    }

    private async Task<bool> ValidateUNSPathExists(string unsPath)
    {
        try
        {
            var nsStructure = await _namespaceStructureService.GetNamespaceStructureAsync();
            return FindNodeByPath(nsStructure, unsPath) != null;
        }
        catch
        {
            return false;
        }
    }

    private NSTreeNode? FindNodeByPath(IEnumerable<NSTreeNode> nodes, string targetPath)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            if (node.Children.Any())
            {
                var found = FindNodeByPath(node.Children, targetPath);
                if (found != null)
                    return found;
            }
        }

        return null;
    }
}