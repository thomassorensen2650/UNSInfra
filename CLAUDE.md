# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UNSInfra is a Unified Namespace (UNS) Infrastructure system for manufacturing and industrial IoT data management. It implements ISA-S95 hierarchical data structures and provides support for Sparkplug B protocol over MQTT. The system enables unified data ingestion, processing, and visualization across manufacturing systems.

## Common Development Commands

### Build & Run
```bash
# Build entire solution
dotnet build UNSInfra.sln

# Run the UI application (Blazor Server)
dotnet run --project src/UNSInfra.UI

# Run example application
dotnet run --project src/UNSInfra.Examples

# Run with Docker Compose
docker compose up
```

### Project Management
```bash
# Restore NuGet packages
dotnet restore

# Clean build artifacts
dotnet clean

# Add package reference
dotnet add package <PackageName> --project <ProjectPath>

# Add project reference
dotnet add reference <ProjectPath>
```

## Architecture Overview

### Core Components

**UNSInfra.Core** - Foundation library containing:
- `HierarchicalPath` - ISA-S95 hierarchy implementation (Enterprise/Site/Area/WorkCenter/WorkUnit/Property)
- Data models (`DataPoint`, `DataSchema`)
- Storage abstractions (`IRealtimeStorage`, `IHistoricalStorage`)
- Service interfaces for data ingestion and topic management

**UNSInfra.Services.V1** - Production services including:
- MQTT data service with MQTTnet integration
- Sparkplug B protocol support with protobuf serialization
- Configuration management for MQTT brokers
- Dependency injection extensions

**UNSInfra.Services.SocketIO** - Generic Socket.IO data service:
- Socket.IO client for real-time data ingestion
- JSON data parsing and topic creation
- Configurable for any Socket.IO server
- Auto-discovery of topics with ISA-S95 mapping

**UNSInfra.UI** - Blazor Server web application:
- Real-time topic visualization with `TopicTree` components
- Multiple views: LiveView, Settings, TestData
- Uses .NET 9.0

**UNSInfra.Storage.InMemory** - In-memory storage implementations for development/testing

**UNSInfra.Services.Mock/Processing** - Mock services and data processing orchestration

### Data Flow Architecture

1. **Data Ingestion**: Multiple data sources implement `IDataIngestionService` (MQTT, SocketIO, future sources)
2. **Generic Event Handling**: `DataIngestionBackgroundService` handles events from all data sources without source-specific knowledge
3. **Hierarchical Organization**: Data mapped to ISA-S95 paths via `HierarchicalPath`
4. **Storage**: Abstracted through `IRealtimeStorage` and `IHistoricalStorage`
5. **Visualization**: Blazor UI components display hierarchical data trees
6. **Sparkplug B**: Industrial IoT protocol support for standardized messaging

### Adding New Data Sources

To add a new data ingestion source:
1. Implement `IDataIngestionService` interface
2. Register both the specific interface and `IDataIngestionService` in DI container
3. The `DataIngestionBackgroundService` will automatically discover and manage it
4. No changes needed to the background service or storage layers

### Key Design Patterns

- **Dependency Injection**: Extensive use of Microsoft.Extensions.DependencyInjection
- **Repository Pattern**: Schema and topic configuration repositories
- **Event-Driven**: Data ingestion services use events for loose coupling
- **Abstraction Layers**: Storage and service interfaces enable testability
- **Hierarchical Data**: ISA-S95 compliant path structures throughout

## Framework Versions

- **Core Libraries**: .NET 8.0
- **UI Application**: .NET 9.0
- **Key Dependencies**: MQTTnet 4.3.7, Google.Protobuf 3.25.2, SocketIOClient 3.1.2

## Configuration

- MQTT configuration uses `MqttConfiguration` class with sample settings in `src/UNSInfra.Services.V1/Configuration/sample-appsettings.json`
- SocketIO configuration uses `SocketIOConfiguration` class with settings for server URL, connection parameters, and event handling

## Development Notes

- All projects use nullable reference types enabled
- Documentation XML generation enabled for core libraries
- Root namespace follows `UNSInfra[.SubNamespace]` pattern
- Sparkplug B implementation is simplified for common use cases