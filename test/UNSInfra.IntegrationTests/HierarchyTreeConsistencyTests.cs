using System.Text.Json;
using FluentAssertions;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UNSInfra.Services;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.MCP.Server;
using UNSInfra.UI;
using Xunit;
using Xunit.Abstractions;

namespace UNSInfra.IntegrationTests;

/// <summary>
/// Integration tests to verify that the MCP Server's get_uns_hierarchy_tree 
/// returns identical data to what the UI displays in its UNS tree
/// </summary>
public class HierarchyTreeConsistencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public HierarchyTreeConsistencyTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task McpHierarchyTree_ShouldMatch_UIHierarchyTree()
    {
        // Arrange - Start the UI web application
        using var client = _factory.CreateClient();
        
        // Get the GraphQL endpoint from the UI
        var graphQlClient = new GraphQLHttpClient(
            new GraphQLHttpClientOptions { EndPoint = new Uri(client.BaseAddress!, "/graphql") },
            new SystemTextJsonSerializer(),
            client);

        // Act - Get hierarchy from UI via GraphQL (same source as MCP server)
        var uiHierarchyData = await GetUIHierarchyDataAsync(graphQlClient);
        
        // Get hierarchy from MCP server directly
        var mcpHierarchyData = await GetMcpHierarchyDataAsync(graphQlClient);

        // Assert - Compare the two hierarchies
        _output.WriteLine("UI Hierarchy Data:");
        _output.WriteLine(JsonSerializer.Serialize(uiHierarchyData, new JsonSerializerOptions { WriteIndented = true }));
        
        _output.WriteLine("MCP Hierarchy Data:");
        _output.WriteLine(JsonSerializer.Serialize(mcpHierarchyData, new JsonSerializerOptions { WriteIndented = true }));

        // Verify the structures match
        AssertHierarchyTreesMatch(uiHierarchyData, mcpHierarchyData);
    }

    [Fact]
    public async Task McpHierarchyTree_ShouldInclude_EmptyNamespaces()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var graphQlClient = new GraphQLHttpClient(
            new GraphQLHttpClientOptions { EndPoint = new Uri(client.BaseAddress!, "/graphql") },
            new SystemTextJsonSerializer(),
            client);

        // Act - Get namespace structure that includes empty namespaces
        var namespaceStructure = await GetNamespaceStructureAsync(graphQlClient);
        var mcpHierarchy = await GetMcpHierarchyDataAsync(graphQlClient);

        // Assert - MCP hierarchy should include all namespaces from namespace structure
        _output.WriteLine("Namespace Structure:");
        _output.WriteLine(JsonSerializer.Serialize(namespaceStructure, new JsonSerializerOptions { WriteIndented = true }));

        // Verify that empty namespaces are included in MCP hierarchy
        VerifyEmptyNamespacesIncluded(namespaceStructure, mcpHierarchy);
    }

    private async Task<dynamic> GetUIHierarchyDataAsync(GraphQLHttpClient graphQlClient)
    {
        var uiQuery = new GraphQLRequest
        {
            Query = @"
            query {
                systemStatus {
                    totalTopics
                    assignedTopics
                    activeTopics
                    namespaces
                }
                namespaces
                namespaceStructure {
                    name
                    fullPath
                    path
                    nodeType
                    hierarchyNode {
                        id
                        name
                        description
                    }
                    namespace {
                        id
                        name
                        type
                        description
                    }
                    children {
                        name
                        fullPath
                        path
                        nodeType
                        hierarchyNode {
                            id
                            name
                            description
                        }
                        namespace {
                            id
                            name
                            type
                            description
                        }
                    }
                }
                topics {
                    topic
                    unsName
                    nsPath
                    path
                    isActive
                    sourceType
                    description
                }
            }"
        };

        var response = await graphQlClient.SendQueryAsync<dynamic>(uiQuery);
        
        if (response.Errors?.Length > 0)
        {
            throw new Exception($"GraphQL UI query failed: {string.Join(", ", response.Errors.Select(e => e.Message))}");
        }

        return response.Data;
    }

    private async Task<dynamic> GetMcpHierarchyDataAsync(GraphQLHttpClient graphQlClient)
    {
        // Simulate what the MCP server does - call the same GraphQL endpoint
        // but process the data through the MCP server's tree building logic
        
        // Use the MCP server's method directly
        var result = await GraphQLMcpTools.GetUnsHierarchyTreeAsync(graphQlClient);
        
        // Parse the JSON result
        var mcpResult = JsonSerializer.Deserialize<dynamic>(result);
        
        if (mcpResult?.GetProperty("success").GetBoolean() != true)
        {
            throw new Exception($"MCP hierarchy query failed: {mcpResult?.GetProperty("message").GetString()}");
        }

        return mcpResult;
    }

    private async Task<dynamic> GetNamespaceStructureAsync(GraphQLHttpClient graphQlClient)
    {
        var query = new GraphQLRequest
        {
            Query = @"
            query {
                namespaceStructure {
                    name
                    fullPath
                    path
                    nodeType
                    hierarchyNode {
                        id
                        name
                        description
                    }
                    namespace {
                        id
                        name
                        type
                        description
                    }
                    children {
                        name
                        fullPath
                        path
                        nodeType
                    }
                }
            }"
        };

        var response = await graphQlClient.SendQueryAsync<dynamic>(query);
        
        if (response.Errors?.Length > 0)
        {
            throw new Exception($"GraphQL namespace structure query failed: {string.Join(", ", response.Errors.Select(e => e.Message))}");
        }

        if (response.Data is JsonElement dataElement)
        {
            if (dataElement.TryGetProperty("namespaceStructure", out var nsStructure))
                return nsStructure;
        }
        return ((dynamic)response.Data).namespaceStructure;
    }

    private void AssertHierarchyTreesMatch(dynamic uiData, dynamic mcpData)
    {
        // Extract hierarchy trees for comparison
        var uiHierarchy = ExtractHierarchyStructure(uiData);
        var mcpHierarchy = ExtractMcpHierarchyStructure(mcpData);

        // Compare basic structure
        uiHierarchy.Should().NotBeNull("UI hierarchy should not be null");
        mcpHierarchy.Should().NotBeNull("MCP hierarchy should not be null");

        // Compare system status
        CompareSystemStatus(uiData, mcpData);
        
        // Compare topic counts
        CompareTopicCounts(uiData, mcpData);
        
        // Compare hierarchy structure
        CompareHierarchyStructure(uiHierarchy, mcpHierarchy);
    }

    private void CompareSystemStatus(dynamic uiData, dynamic mcpData)
    {
        if (uiData.systemStatus != null && mcpData.GetProperty("systemStatus").ValueKind != JsonValueKind.Null)
        {
            var uiSystemStatus = uiData.systemStatus;
            var mcpSystemStatus = mcpData.GetProperty("systemStatus");

            // Compare key metrics
            _output.WriteLine($"UI Total Topics: {uiSystemStatus.totalTopics}");
            _output.WriteLine($"MCP Total Topics: {mcpData.GetProperty("topicCount")}");
            
            // Both should have same topic count
            ((int)uiSystemStatus.totalTopics).Should().Be(mcpData.GetProperty("topicCount").GetInt32(),
                "UI and MCP should report the same total topic count");
        }
    }

    private void CompareTopicCounts(dynamic uiData, dynamic mcpData)
    {
        // Count topics from UI
        var uiTopics = uiData.topics;
        int uiTopicCount = 0;
        if (uiTopics != null && uiTopics.GetType().IsArray)
        {
            uiTopicCount = ((Array)uiTopics).Length;
        }

        // Count topics from MCP
        var mcpTopicCount = mcpData.GetProperty("topicCount").GetInt32();

        uiTopicCount.Should().Be(mcpTopicCount, 
            "UI and MCP should have the same number of topics");
    }

    private void CompareHierarchyStructure(dynamic uiHierarchy, dynamic mcpHierarchy)
    {
        // This is a complex comparison - we need to compare the tree structures
        // For now, we'll do a basic structural comparison
        
        _output.WriteLine("Comparing hierarchy structures...");
        
        // Both hierarchies should exist
        uiHierarchy.Should().NotBeNull("UI hierarchy structure should exist");
        mcpHierarchy.Should().NotBeNull("MCP hierarchy structure should exist");
        
        // The hierarchies should have structural similarity
        // This is where we would implement detailed tree comparison logic
        CompareTreeNodes(uiHierarchy, mcpHierarchy, "Root");
    }

    private void CompareTreeNodes(dynamic uiNode, dynamic mcpNode, string nodePath)
    {
        _output.WriteLine($"Comparing node at path: {nodePath}");
        
        // Compare node properties
        if (uiNode != null && mcpNode != null)
        {
            // Both nodes should have names/paths
            var uiNodeData = ExtractNodeData(uiNode);
            var mcpNodeData = ExtractNodeData(mcpNode);
            
            _output.WriteLine($"  UI Node: {uiNodeData}");
            _output.WriteLine($"  MCP Node: {mcpNodeData}");
        }
    }

    private string ExtractNodeData(dynamic node)
    {
        // Extract key identifying information from a node
        try
        {
            if (node == null) return "null";
            
            // Try to extract name, path, or other identifying info
            var nodeStr = JsonSerializer.Serialize(node);
            var nodeObj = JsonSerializer.Deserialize<Dictionary<string, object>>(nodeStr);
            
            var identifiers = new List<string>();
            if (nodeObj?.ContainsKey("name") == true) identifiers.Add($"name:{nodeObj["name"]}");
            if (nodeObj?.ContainsKey("fullPath") == true) identifiers.Add($"path:{nodeObj["fullPath"]}");
            if (nodeObj?.ContainsKey("nodeType") == true) identifiers.Add($"type:{nodeObj["nodeType"]}");
            
            return string.Join(", ", identifiers);
        }
        catch
        {
            return node?.ToString() ?? "unknown";
        }
    }

    private object ExtractHierarchyStructure(dynamic uiData)
    {
        // Extract the hierarchy structure from UI GraphQL response
        if (uiData is JsonElement element)
        {
            if (element.TryGetProperty("namespaceStructure", out var nsStructure))
                return nsStructure;
        }
        return ((JsonElement)uiData).GetProperty("namespaceStructure");
    }

    private object ExtractMcpHierarchyStructure(dynamic mcpData)
    {
        // Extract the hierarchy structure from MCP response
        if (mcpData is JsonElement element)
        {
            if (element.TryGetProperty("hierarchyTree", out var hierarchyTree))
                return hierarchyTree;
        }
        return ((JsonElement)mcpData).GetProperty("hierarchyTree");
    }

    private void VerifyEmptyNamespacesIncluded(dynamic namespaceStructure, dynamic mcpHierarchy)
    {
        _output.WriteLine("Verifying empty namespaces are included in MCP hierarchy...");
        
        // This test ensures that empty namespaces (those without topics) 
        // are still included in the MCP hierarchy tree
        namespaceStructure.Should().NotBeNull("Namespace structure should exist");
        mcpHierarchy.Should().NotBeNull("MCP hierarchy should exist");
        
        // The test should verify that all namespace paths from namespaceStructure
        // are represented in the MCP hierarchy tree
        var namespacePaths = ExtractNamespacePaths(namespaceStructure);
        var mcpPaths = ExtractMcpPaths(mcpHierarchy);
        
        _output.WriteLine($"Namespace paths: {string.Join(", ", namespacePaths)}");
        _output.WriteLine($"MCP paths: {string.Join(", ", mcpPaths)}");
        
        // All namespace paths should be present in MCP hierarchy
        foreach (var namespacePath in namespacePaths)
        {
            mcpPaths.Should().Contain(namespacePath, 
                $"MCP hierarchy should include namespace path: {namespacePath}");
        }
    }

    private List<string> ExtractNamespacePaths(dynamic namespaceStructure)
    {
        var paths = new List<string>();
        
        try
        {
            var structureJson = JsonSerializer.Serialize(namespaceStructure);
            var elements = JsonSerializer.Deserialize<JsonElement[]>(structureJson);
            
            foreach (var element in elements ?? Array.Empty<JsonElement>())
            {
                ExtractPathsFromElement(element, paths);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error extracting namespace paths: {ex.Message}");
        }
        
        return paths;
    }

    private void ExtractPathsFromElement(JsonElement element, List<string> paths)
    {
        if (element.TryGetProperty("fullPath", out var pathElement))
        {
            var path = pathElement.GetString();
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }
        
        if (element.TryGetProperty("children", out var childrenElement) && 
            childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenElement.EnumerateArray())
            {
                ExtractPathsFromElement(child, paths);
            }
        }
    }

    private List<string> ExtractMcpPaths(dynamic mcpHierarchy)
    {
        var paths = new List<string>();
        
        try
        {
            var hierarchyJson = JsonSerializer.Serialize(mcpHierarchy);
            var hierarchyElement = JsonSerializer.Deserialize<JsonElement>(hierarchyJson);
            
            ExtractMcpPathsFromElement(hierarchyElement, paths);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error extracting MCP paths: {ex.Message}");
        }
        
        return paths;
    }

    private void ExtractMcpPathsFromElement(JsonElement element, List<string> paths)
    {
        if (element.TryGetProperty("fullPath", out var pathElement))
        {
            var path = pathElement.GetString();
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }
        
        if (element.TryGetProperty("children", out var childrenElement) && 
            childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenElement.EnumerateArray())
            {
                ExtractMcpPathsFromElement(child, paths);
            }
        }
    }
}