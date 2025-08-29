// Test script to compare MCP server vs UI data from the actual running server
const GraphQL = require('graphql-request');
const fs = require('fs');

const client = new GraphQL.GraphQLClient('https://localhost:5001/graphql', {
    agent: new (require('https')).Agent({
        rejectUnauthorized: false // Ignore SSL certificate for localhost
    })
});

async function testMcpVsUI() {
    try {
        console.log('Testing MCP vs UI hierarchy data...\n');

        // Query UI data (same as integration test)
        const uiQuery = `
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
            }
        `;

        const uiData = await client.request(uiQuery);
        
        console.log('UI Data:');
        console.log('- Total Topics:', uiData.systemStatus.totalTopics);
        console.log('- Namespace Structure entries:', uiData.namespaceStructure.length);
        console.log('- Topics with NSPath:', uiData.topics.filter(t => t.nsPath).length);
        
        if (uiData.namespaceStructure.length > 0) {
            console.log('\nUI Namespace Structure:');
            uiData.namespaceStructure.forEach(ns => {
                console.log(`  - ${ns.fullPath} (${ns.nodeType})`);
            });
        }

        if (uiData.topics.length > 0) {
            console.log('\nUI Topics with NSPath:');
            uiData.topics.filter(t => t.nsPath).forEach(topic => {
                console.log(`  - ${topic.topic} -> ${topic.nsPath}`);
            });
        }

        // Write results to files for analysis
        fs.writeFileSync('/tmp/ui_data.json', JSON.stringify(uiData, null, 2));
        console.log('\nUI data written to /tmp/ui_data.json');
        
    } catch (error) {
        console.error('Error:', error);
    }
}

testMcpVsUI();