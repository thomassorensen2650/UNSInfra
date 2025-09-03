using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services;
using UNSInfra.Services.TopicDiscovery;
using UNSInfra.Services.TopicBrowser;
using UNSInfra.Services.Events;

/// <summary>
/// Test to replicate the issue where VirtualFactory topics are mapped but don't appear in UNS tree
/// </summary>
class TopicMappingTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Topic Mapping Test ===");
        Console.WriteLine("Replicating issue: VirtualFactory topic mapped to namespace but not appearing in UNS tree");
        Console.WriteLine();

        // The issue:
        // 1. SimplifiedAutoMapperService maps "VirtualFactory/update/Enterprise/Dallas/Press/Line1/Dashboard" to "Enterprise/Dallas/Press/Line1"
        // 2. This logs: "Mapped topic ... to namespace ..."
        // 3. But the topic doesn't appear in UNS tree because NSPath is not set on TopicInfo
        // 4. NSPath only gets set when TopicAutoMappedEvent is processed
        // 5. The question is: Is the event being fired and processed correctly?

        Console.WriteLine("Key insight: Topics need BOTH mapping AND NSPath assignment to appear in UNS tree");
        Console.WriteLine();
        
        Console.WriteLine("Flow should be:");
        Console.WriteLine("1. Topic data received -> SimplifiedAutoMapperService.TryMapTopic()");
        Console.WriteLine("2. If mapped -> SimplifiedAutoMappingBackgroundService publishes TopicAutoMappedEvent");
        Console.WriteLine("3. CachedTopicBrowserService processes event -> sets NSPath on TopicInfo");  
        Console.WriteLine("4. Topic appears in UNS tree via GetTopicsForNamespaceAsync()");
        Console.WriteLine();

        Console.WriteLine("The missing piece: AllowTopics validation should happen in step 2");
        Console.WriteLine("TopicAutoMappedEvent should only be published if AllowTopics=true for target hierarchy level");
        Console.WriteLine();

        Console.WriteLine("Test scenario:");
        Console.WriteLine("- Topic: 'VirtualFactory/update/Enterprise/Dallas/BU'");  
        Console.WriteLine("- Expected mapping: 'Enterprise/Dallas'");
        Console.WriteLine("- Site level AllowTopics: true");
        Console.WriteLine("- Expected result: Topic should appear in UNS tree under Enterprise/Dallas");
        Console.WriteLine();

        Console.WriteLine("If topic is not appearing, possible causes:");
        Console.WriteLine("1. TopicAutoMappedEvent not being published");
        Console.WriteLine("2. AllowTopics validation failing");
        Console.WriteLine("3. Event not being processed by CachedTopicBrowserService");
        Console.WriteLine("4. NSPath not being set correctly");
        Console.WriteLine("5. Namespace 'Enterprise/Dallas' doesn't exist in namespace structure");
        
        Console.WriteLine();
        Console.WriteLine("To verify: Check logs for 'TopicAutoMappedEvent', 'Processing TopicAutoMappedEvent', and AllowTopics validation");
    }
}