# UNS Infrastructure UI Tests

This project contains comprehensive unit tests for the Blazor UI components in the UNS Infrastructure system using bUnit (Blazor Unit Testing framework).

## Test Coverage

### Components Tested

1. **LogViewer Component** (`LogViewerTests.cs`)
   - Log display and filtering functionality
   - Search capabilities
   - Date range filtering
   - Log level filtering
   - Auto-refresh toggle
   - Empty state handling
   - Log entry expansion

2. **Settings Component** (`SettingsTests.cs`)
   - Tab navigation (Storage, Hierarchy, Connections, Schemas, System)
   - Schema validation functionality
   - Settings configuration forms
   - Search and filtering in schema management
   - Empty state handling

3. **ModernLayout Component** (`ModernLayoutTests.cs`)
   - Navigation bar rendering
   - Mobile menu functionality
   - Theme toggle
   - Responsive design
   - Brand logo and navigation links
   - Icon display and functionality

4. **TopicTree Component** (`TopicTreeTests.cs`)
   - Tab switching between UNS and Data Browser
   - Topic display and organization
   - Source type grouping
   - Empty state handling
   - Parameter passing and callbacks

## Framework and Dependencies

- **bUnit 1.30.3** - Blazor Unit Testing framework
- **xUnit** - Test runner and assertions
- **Moq** - Mocking framework for dependencies
- **AngleSharp** - HTML parsing for component testing

## Test Structure

### Base Test Context (`UITestContext`)

All UI tests inherit from `UITestContext` which provides:
- Mock services for dependencies
- Configured DI container
- JSInterop mocking
- Common test setup

### Mock Services

The test setup includes mocks for:
- `IInMemoryLogService` - For log viewer functionality
- `ISchemaRepository` - For schema management
- `ITopicBrowserService` - For topic data
- `ISchemaValidator` - For validation functionality
- `ILogger<T>` - For logging

## Running Tests

```bash
# Run all UI tests
dotnet test test/UNSInfra.UI.Tests/UNSInfra.UI.Tests.csproj

# Run with verbose output
dotnet test test/UNSInfra.UI.Tests/UNSInfra.UI.Tests.csproj --verbosity normal

# Run specific test class
dotnet test test/UNSInfra.UI.Tests/UNSInfra.UI.Tests.csproj --filter "ClassName=LogViewerTests"
```

## Current Status

**58 total tests** - Comprehensive coverage of major UI components

The test suite is set up with proper infrastructure and covers:
- Component rendering
- User interactions
- State management
- Event handling
- Responsive behavior
- Error scenarios

## Notes

- Tests use bUnit's `RenderComponent<T>()` method to render components in isolation
- Mock data is used to test component behavior without dependencies
- Tests verify both HTML output and component behavior
- Component interactions are tested through simulated user actions (clicks, input changes)

## Future Enhancements

Additional test scenarios could include:
- Integration tests with real services
- Visual regression testing
- Accessibility testing
- Performance testing for large datasets
- Cross-browser compatibility testing