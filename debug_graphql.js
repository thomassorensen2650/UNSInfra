// Debug script to examine GraphQL namespaceStructure vs topics
const { GraphQLClient } = require('graphql-request');
const fs = require('fs');

const client = new GraphQLClient('https://localhost:5001/graphql', {
    agent: new (require('https')).Agent({
        rejectUnauthorized: false // Ignore SSL certificate for localhost
    })
});

async function debugGraphQL() {
    try {
        console.log('🔍 Debugging GraphQL interface...\n');

        // Query all the data the MCP server queries
        const fullQuery = `
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
                        children {
                            name
                            fullPath
                            nodeType
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

        const data = await client.request(fullQuery);
        
        console.log('📊 System Status:');
        console.log(`- Total Topics: ${data.systemStatus.totalTopics}`);
        console.log(`- Assigned Topics: ${data.systemStatus.assignedTopics}`);
        console.log(`- Active Topics: ${data.systemStatus.activeTopics}`);
        console.log(`- Namespaces: ${data.systemStatus.namespaces}\n`);

        console.log('📁 Namespaces Array:');
        console.log(`- Length: ${data.namespaces.length}`);
        if (data.namespaces.length > 0) {
            data.namespaces.forEach((ns, i) => console.log(`  ${i+1}. ${ns}`));
        }
        console.log();

        console.log('🌳 Namespace Structure:');
        console.log(`- Length: ${data.namespaceStructure.length}`);
        if (data.namespaceStructure.length > 0) {
            data.namespaceStructure.forEach((ns, i) => {
                console.log(`  ${i+1}. ${ns.name} (${ns.fullPath}) - ${ns.nodeType}`);
                if (ns.children && ns.children.length > 0) {
                    ns.children.forEach((child, j) => {
                        console.log(`     ${i+1}.${j+1}. ${child.name} (${child.fullPath}) - ${child.nodeType}`);
                        if (child.children && child.children.length > 0) {
                            child.children.forEach((grandchild, k) => {
                                console.log(`        ${i+1}.${j+1}.${k+1}. ${grandchild.name} (${grandchild.fullPath}) - ${grandchild.nodeType}`);
                            });
                        }
                    });
                }
            });
        }
        console.log();

        console.log('📋 Topics with NSPath:');
        const topicsWithPath = data.topics.filter(t => t.nsPath);
        console.log(`- Count: ${topicsWithPath.length}`);
        
        // Group topics by NSPath to see the hierarchy
        const pathGroups = {};
        topicsWithPath.forEach(topic => {
            if (!pathGroups[topic.nsPath]) {
                pathGroups[topic.nsPath] = [];
            }
            pathGroups[topic.nsPath].push(topic);
        });

        Object.keys(pathGroups).sort().forEach(path => {
            console.log(`  ${path}:`);
            pathGroups[path].forEach(topic => {
                console.log(`    - ${topic.unsName || topic.topic} (${topic.topic})`);
            });
        });

        // Write detailed output to files
        fs.writeFileSync('/tmp/graphql_debug.json', JSON.stringify(data, null, 2));
        console.log('\n📁 Full GraphQL response written to /tmp/graphql_debug.json');
        
        // Create a simplified hierarchy view
        const hierarchy = {
            systemStatus: data.systemStatus,
            namespaceStructure: data.namespaceStructure,
            topicsByPath: pathGroups
        };
        
        fs.writeFileSync('/tmp/hierarchy_analysis.json', JSON.stringify(hierarchy, null, 2));
        console.log('📁 Hierarchy analysis written to /tmp/hierarchy_analysis.json');
        
    } catch (error) {
        console.error('❌ Error:', error.message);
        
        // Try HTTPS if HTTP fails
        if (error.message.includes('ECONNREFUSED') && !client.url.includes('https')) {
            console.log('🔄 Trying HTTPS...');
            const httpsClient = new GraphQLClient('https://localhost:5001/graphql', {
                agent: new (require('https')).Agent({
                    rejectUnauthorized: false
                })
            });
            
            // Retry with HTTPS (recursive call with HTTPS client)
            client.url = 'https://localhost:5001/graphql';
            client.options = {
                agent: new (require('https')).Agent({
                    rejectUnauthorized: false
                })
            };
            await debugGraphQL();
        }
    }
}

debugGraphQL();