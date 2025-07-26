# UNSInfra Solution Structure

This solution contains the complete UNS Infrastructure system organized into multiple projects.

## Solution Structure:
```
UNSInfra/
├── UNSInfra.sln
├── src/
│   ├── UNSInfra.Core/
│   │   ├── UNSInfra.Core.csproj
│   │   ├── Models/
│   │   │   ├── Hierarchy/
│   │   │   │   └── HierarchicalPath.cs
│   │   │   ├── Data/
│   │   │   │   └── DataPoint.cs
│   │   │   └── Schema/
│   │   │       ├── DataSchema.cs
│   │   │       └── ValidationRule.cs
│   │   ├── Storage/
│   │   │   └── Abstractions/
│   │   │       ├── IRealtimeStorage.cs
│   │   │       └── IHistoricalStorage.cs
│   │   ├── Services/
│   │   │   └── DataIngestion/
│   │   │       ├── IDataIngestionService.cs
│   │   │       ├── IMqttDataService.cs
│   │   │       └── IKafkaDataService.cs
│   │   ├── Validation/
│   │   │   ├── ISchemaValidator.cs
│   │   │   ├── JsonSchemaValidator.cs
│   │   │   └── ValidationResult.cs
│   │   └── Repositories/
│   │       ├── ISchemaRepository.cs
│   │       └── InMemorySchemaRepository.cs
│   ├── UNSInfra.Storage.InMemory/
│   │   ├── UNSInfra.Storage.InMemory.csproj
│   │   ├── InMemoryRealtimeStorage.cs
│   │   └── InMemoryHistoricalStorage.cs
│   ├── UNSInfra.Services.Mock/
│   │   ├── UNSInfra.Services.Mock.csproj
│   │   ├── MockMqttDataService.cs
│   │   └── MockKafkaDataService.cs
│   ├── UNSInfra.Services.Processing/
│   │   ├── UNSInfra.Services.Processing.csproj
│   │   └── DataProcessingService.cs
│   └── UNSInfra.Examples/
│       ├── UNSInfra.Examples.csproj
│       └── Program.cs
└── README.md
```

## Projects Description:

- **UNSInfra.Core**: Core models, interfaces, and abstractions
- **UNSInfra.Storage.InMemory**: In-memory storage implementations
- **UNSInfra.Services.Mock**: Mock data ingestion services for testing
- **UNSInfra.Services.Processing**: Main data processing orchestration
- **UNSInfra.Examples**: Example application demonstrating usage

## Build Instructions:

1. Open the solution in JetBrains Rider
2. Restore NuGet packages
3. Build the solution
4. Run the UNSInfra.Examples project to see the system in action

## Dependencies:

All projects target .NET 8.0 and use System.Text.Json for JSON serialization.
