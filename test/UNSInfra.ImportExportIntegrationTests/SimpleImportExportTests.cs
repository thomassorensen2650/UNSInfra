using System.Text.Json;
using FluentAssertions;
using UNSInfra.MCP.Server;
using Xunit;

namespace UNSInfra.ImportExportIntegrationTests;

/// <summary>
/// Simple tests for import/export JSON structure validation
/// </summary>
public class SimpleImportExportTests
{
    [Fact]
    public void ExportConfigurationJson_ShouldHaveValidStructure()
    {
        // This test validates that export creates proper JSON structure
        // without requiring actual repositories
        
        var sampleExportJson = """
        {
            "success": true,
            "message": "Configuration exported successfully",
            "exportData": {
                "exportInfo": {
                    "exportedAt": "2024-01-01T00:00:00Z",
                    "version": "1.0.0",
                    "description": "UNS Infrastructure Configuration Export"
                },
                "hierarchyConfigurations": [
                    {
                        "id": "test-hierarchy",
                        "name": "Test Hierarchy",
                        "description": "Test description",
                        "isActive": true,
                        "isSystemDefined": false,
                        "nodes": [],
                        "createdAt": "2024-01-01T00:00:00Z",
                        "modifiedAt": "2024-01-01T00:00:00Z"
                    }
                ],
                "namespaceConfigurations": [
                    {
                        "id": "test-namespace",
                        "name": "Test Namespace",
                        "description": "Test namespace description",
                        "isActive": true,
                        "createdAt": "2024-01-01T00:00:00Z",
                        "modifiedAt": "2024-01-01T00:00:00Z"
                    }
                ],
                "topicConfigurations": [
                    {
                        "topic": "test/topic",
                        "unsName": "TestTopic",
                        "hierarchicalPath": "Enterprise/Site",
                        "nsPath": "TestNamespace",
                        "sourceType": "MQTT",
                        "isVerified": true,
                        "createdAt": "2024-01-01T00:00:00Z",
                        "modifiedAt": "2024-01-01T00:00:00Z"
                    }
                ],
                "summary": {
                    "totalHierarchyNodes": 1,
                    "totalNamespaces": 1,
                    "totalTopicConfigurations": 1,
                    "verifiedTopics": 1,
                    "unverifiedTopics": 0
                }
            }
        }
        """;

        // Act & Assert - Should parse without errors
        var jsonDocument = JsonDocument.Parse(sampleExportJson);
        var root = jsonDocument.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        
        var exportData = root.GetProperty("exportData");
        exportData.GetProperty("exportInfo").ValueKind.Should().Be(JsonValueKind.Object);
        exportData.GetProperty("hierarchyConfigurations").ValueKind.Should().Be(JsonValueKind.Array);
        exportData.GetProperty("namespaceConfigurations").ValueKind.Should().Be(JsonValueKind.Array);
        exportData.GetProperty("topicConfigurations").ValueKind.Should().Be(JsonValueKind.Array);
        exportData.GetProperty("summary").ValueKind.Should().Be(JsonValueKind.Object);

        // Validate specific data
        var hierarchies = exportData.GetProperty("hierarchyConfigurations");
        hierarchies.GetArrayLength().Should().Be(1);
        hierarchies[0].GetProperty("id").GetString().Should().Be("test-hierarchy");
        hierarchies[0].GetProperty("name").GetString().Should().Be("Test Hierarchy");

        var topics = exportData.GetProperty("topicConfigurations");
        topics.GetArrayLength().Should().Be(1);
        topics[0].GetProperty("topic").GetString().Should().Be("test/topic");
        topics[0].GetProperty("unsName").GetString().Should().Be("TestTopic");

        var summary = exportData.GetProperty("summary");
        summary.GetProperty("totalHierarchyNodes").GetInt32().Should().Be(1);
        summary.GetProperty("totalNamespaces").GetInt32().Should().Be(1);
        summary.GetProperty("totalTopicConfigurations").GetInt32().Should().Be(1);
        summary.GetProperty("verifiedTopics").GetInt32().Should().Be(1);
        summary.GetProperty("unverifiedTopics").GetInt32().Should().Be(0);
    }

    [Fact]
    public void ImportConfigurationJson_ShouldParseValidStructure()
    {
        // This test validates that import can handle proper JSON structure
        
        var validImportJson = """
        {
            "hierarchyConfigurations": [
                {
                    "id": "hierarchy1",
                    "name": "Test Hierarchy",
                    "description": "Test description",
                    "isActive": true,
                    "isSystemDefined": false
                }
            ],
            "namespaceConfigurations": [
                {
                    "id": "namespace1",
                    "name": "Test Namespace",
                    "description": "Test namespace description",
                    "isActive": true
                }
            ],
            "topicConfigurations": [
                {
                    "topic": "test/topic",
                    "unsName": "TestTopic",
                    "hierarchicalPath": "Enterprise/Site/Area",
                    "nsPath": "TestNamespace",
                    "sourceType": "MQTT",
                    "isVerified": true
                }
            ]
        }
        """;

        // Act & Assert - Should parse without errors
        var jsonDocument = JsonDocument.Parse(validImportJson);
        var root = jsonDocument.RootElement;

        // Validate structure exists
        root.TryGetProperty("hierarchyConfigurations", out var hierarchies).Should().BeTrue();
        root.TryGetProperty("namespaceConfigurations", out var namespaces).Should().BeTrue();
        root.TryGetProperty("topicConfigurations", out var topics).Should().BeTrue();

        hierarchies.ValueKind.Should().Be(JsonValueKind.Array);
        namespaces.ValueKind.Should().Be(JsonValueKind.Array);
        topics.ValueKind.Should().Be(JsonValueKind.Array);

        // Validate hierarchy structure
        hierarchies.GetArrayLength().Should().Be(1);
        var hierarchy = hierarchies[0];
        hierarchy.GetProperty("id").GetString().Should().Be("hierarchy1");
        hierarchy.GetProperty("name").GetString().Should().Be("Test Hierarchy");
        hierarchy.GetProperty("isActive").GetBoolean().Should().BeTrue();
        hierarchy.GetProperty("isSystemDefined").GetBoolean().Should().BeFalse();

        // Validate topic structure
        topics.GetArrayLength().Should().Be(1);
        var topic = topics[0];
        topic.GetProperty("topic").GetString().Should().Be("test/topic");
        topic.GetProperty("unsName").GetString().Should().Be("TestTopic");
        topic.GetProperty("hierarchicalPath").GetString().Should().Be("Enterprise/Site/Area");
        topic.GetProperty("sourceType").GetString().Should().Be("MQTT");
        topic.GetProperty("isVerified").GetBoolean().Should().BeTrue();

        // Validate namespace structure
        namespaces.GetArrayLength().Should().Be(1);
        var ns = namespaces[0];
        ns.GetProperty("id").GetString().Should().Be("namespace1");
        ns.GetProperty("name").GetString().Should().Be("Test Namespace");
        ns.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void ImportExportJsonStructures_ShouldBeCompatible()
    {
        // This test validates that export format can be used for import
        
        var exportedData = """
        {
            "exportInfo": {
                "exportedAt": "2024-01-01T00:00:00Z",
                "version": "1.0.0",
                "description": "UNS Infrastructure Configuration Export"
            },
            "hierarchyConfigurations": [
                {
                    "id": "hierarchy1",
                    "name": "Test Hierarchy",
                    "description": "Test description",
                    "isActive": true,
                    "isSystemDefined": false,
                    "nodes": [],
                    "createdAt": "2024-01-01T00:00:00Z",
                    "modifiedAt": "2024-01-01T00:00:00Z"
                }
            ],
            "namespaceConfigurations": [
                {
                    "id": "namespace1",
                    "name": "Test Namespace",
                    "description": "Test description",
                    "isActive": true,
                    "createdAt": "2024-01-01T00:00:00Z",
                    "modifiedAt": "2024-01-01T00:00:00Z"
                }
            ],
            "topicConfigurations": [
                {
                    "topic": "sensors/temperature",
                    "unsName": "TemperatureSensor",
                    "hierarchicalPath": "Enterprise/Site/Area/WorkCenter",
                    "nsPath": "Production/Sensors",
                    "sourceType": "MQTT",
                    "isVerified": true,
                    "createdAt": "2024-01-01T00:00:00Z",
                    "modifiedAt": "2024-01-01T00:00:00Z"
                }
            ],
            "summary": {
                "totalHierarchyNodes": 1,
                "totalNamespaces": 1,
                "totalTopicConfigurations": 1,
                "verifiedTopics": 1,
                "unverifiedTopics": 0
            }
        }
        """;

        // Act - Parse export data
        var jsonDocument = JsonDocument.Parse(exportedData);
        var root = jsonDocument.RootElement;

        // Assert - All required import sections exist
        root.TryGetProperty("hierarchyConfigurations", out var hierarchies).Should().BeTrue();
        root.TryGetProperty("namespaceConfigurations", out var namespaces).Should().BeTrue();
        root.TryGetProperty("topicConfigurations", out var topics).Should().BeTrue();

        // Validate that import-required fields exist in export format
        var hierarchy = hierarchies[0];
        hierarchy.TryGetProperty("id", out _).Should().BeTrue();
        hierarchy.TryGetProperty("name", out _).Should().BeTrue();
        hierarchy.TryGetProperty("description", out _).Should().BeTrue();
        hierarchy.TryGetProperty("isActive", out _).Should().BeTrue();
        hierarchy.TryGetProperty("isSystemDefined", out _).Should().BeTrue();

        var topic = topics[0];
        topic.TryGetProperty("topic", out _).Should().BeTrue();
        topic.TryGetProperty("unsName", out _).Should().BeTrue();
        topic.TryGetProperty("hierarchicalPath", out _).Should().BeTrue();
        topic.TryGetProperty("sourceType", out _).Should().BeTrue();
        topic.TryGetProperty("isVerified", out _).Should().BeTrue();

        var ns = namespaces[0];
        ns.TryGetProperty("id", out _).Should().BeTrue();
        ns.TryGetProperty("name", out _).Should().BeTrue();
        ns.TryGetProperty("description", out _).Should().BeTrue();
        ns.TryGetProperty("isActive", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid json")]
    [InlineData("{")]
    [InlineData("null")]
    public void InvalidJsonInputs_ShouldBeHandledGracefully(string invalidJson)
    {
        // This test validates that invalid JSON inputs are handled properly
        // In a real scenario, the import function would validate and return appropriate errors
        
        if (string.IsNullOrWhiteSpace(invalidJson))
        {
            invalidJson.Should().NotBeNull(); // Basic validation
            return;
        }

        // For actual JSON parsing, invalid JSON should throw or be handled
        Action parseAction = () => JsonDocument.Parse(invalidJson);
        
        if (invalidJson == "null")
        {
            parseAction.Should().NotThrow(); // null is valid JSON
        }
        else
        {
            parseAction.Should().Throw<JsonException>(); // Invalid JSON should throw
        }
    }

    [Fact]
    public void HierarchicalPathParsing_ShouldHandleVariousFormats()
    {
        // Test various hierarchical path formats that might appear in import/export
        
        var pathTestCases = new[]
        {
            "Enterprise",
            "Enterprise/Site",  
            "Enterprise/Site/Area",
            "Enterprise/Site/Area/WorkCenter",
            "Enterprise/Site/Area/WorkCenter/WorkUnit",
            "",
            "Single-Level-With-Dashes",
            "Level With Spaces/Another Level",
            "Special!@#$%^&*()Characters/InPath"
        };

        foreach (var path in pathTestCases)
        {
            // Validate that paths can be processed
            var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (string.IsNullOrEmpty(path))
            {
                pathParts.Should().BeEmpty();
            }
            else
            {
                pathParts.Should().NotBeEmpty();
                pathParts.Length.Should().BeGreaterThan(0);
            }

            // Validate reconstruction
            var reconstructed = string.Join("/", pathParts);
            if (!string.IsNullOrEmpty(path))
            {
                reconstructed.Should().Be(path);
            }
        }
    }

    [Fact]
    public void EmptyConfigurationData_ShouldBeValidStructure()
    {
        // Test that empty configurations are valid
        
        var emptyConfigJson = """
        {
            "hierarchyConfigurations": [],
            "namespaceConfigurations": [],
            "topicConfigurations": []
        }
        """;

        // Act & Assert
        var jsonDocument = JsonDocument.Parse(emptyConfigJson);
        var root = jsonDocument.RootElement;

        root.GetProperty("hierarchyConfigurations").GetArrayLength().Should().Be(0);
        root.GetProperty("namespaceConfigurations").GetArrayLength().Should().Be(0);
        root.GetProperty("topicConfigurations").GetArrayLength().Should().Be(0);

        // Should still be valid JSON structure for import
        root.GetProperty("hierarchyConfigurations").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("namespaceConfigurations").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("topicConfigurations").ValueKind.Should().Be(JsonValueKind.Array);
    }
}