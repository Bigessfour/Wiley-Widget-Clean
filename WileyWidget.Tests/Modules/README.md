# DashboardModule Unit Tests

This directory contains comprehensive xUnit unit tests for the `DashboardModule` Prism WPF module, designed to run in a Docker container environment.

## Test Coverage

The `DashboardModuleTests.cs` file provides complete test coverage for:

### ✅ Successful Initialization
- **OnInitialized with mocked IContainerProvider**: Verifies proper dependency resolution and view registration
- **IRegionManager resolution**: Ensures the region manager is resolved correctly
- **View registration verification**: Confirms `DashboardView` is registered with `MainRegion`
- **Type registration**: Validates `DashboardViewModel` and navigation registration

### ❌ Failure Cases
- **Missing dependencies**: Tests behavior when `IRegionManager` cannot be resolved
- **Null container provider**: Handles `ArgumentNullException` for null inputs
- **Region registration failures**: Tests error handling when regions don't exist
- **Container registry failures**: Validates null checks for type registration

### 🔍 View Registration Verification
- **Correct region name**: Ensures views are registered with "MainRegion"
- **Correct view type**: Verifies `DashboardView` type is registered
- **Registration method calls**: Confirms `RegisterViewWithRegion` is called appropriately

### ⚡ Async Initialization Edge Cases
- **Concurrent initialization**: Tests thread safety with parallel calls
- **Multiple initialization calls**: Verifies behavior with repeated initialization
- **Different container providers**: Tests independence across container instances
- **Async execution**: Ensures initialization works in asynchronous contexts

## Running Tests

### Prerequisites
- Docker Desktop with Windows containers enabled
- PowerShell 7.0 or later
- .NET 9.0 SDK (for local development)

### Quick Start

#### Option 1: PowerShell Script (Recommended)
```powershell
# Run complete test cycle (build + test)
.\run-dashboard-tests.ps1 -Action Test

# Or run individual steps
.\run-dashboard-tests.ps1 -Action Build
.\run-dashboard-tests.ps1 -Action Run
```

#### Option 2: Direct Docker Commands
```powershell
# Build the test image
docker build -f Dockerfile.test -t wiley-widget-tests .

# Run tests in container
docker run --rm -v ${PWD}/TestResults:/test-results wiley-widget-tests
```

#### Option 3: Docker Compose
```powershell
# Run tests using docker-compose
docker-compose -f docker-compose.test.yml up --abort-on-container-exit
```

### Test Results

Test results are saved to the `TestResults` directory:
- `.trx` files: Visual Studio test results format
- `.xml` files: Coverage reports (when coverage is enabled)
- Console output: Real-time test execution feedback

## Docker Configuration

### Base Image
- **mcr.microsoft.com/dotnet/sdk:9.0-windowsservercore-ltsc2022**
- Windows Server Core LTSC 2022
- .NET 9.0 SDK pre-installed

### Container Features
- **Multi-stage build**: Optimized for test execution
- **Volume mounting**: Test results persisted to host
- **Environment variables**: Telemetry disabled for cleaner output
- **TRX logging**: Structured test results for CI/CD integration

## Test Architecture

### Mocking Strategy
- **Moq framework**: Industry-standard mocking library
- **Prism interfaces**: `IContainerProvider`, `IRegionManager`, `IContainerRegistry`
- **Fluent assertions**: Readable assertion syntax
- **Dependency isolation**: Each test is completely isolated

### Test Organization
```csharp
public class DashboardModuleTests
{
    // Setup: Mocked dependencies
    // Exercise: Module initialization
    // Verify: Expected behavior
    // Teardown: Automatic (xUnit handles disposal)
}
```

### Naming Convention
- **Method_Scenario_ExpectedBehavior**: Clear, descriptive test names
- **Arrange-Act-Assert**: Standard unit testing pattern
- **One assertion per test**: Focused, single-responsibility tests

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Run Dashboard Module Tests
  run: .\run-dashboard-tests.ps1 -Action Test

- name: Upload Test Results
  uses: actions/upload-artifact@v3
  with:
    name: dashboard-test-results
    path: TestResults/
```

### Local Development
```powershell
# Run tests locally (without Docker)
dotnet test WileyWidget.Tests/WileyWidget.Tests.csproj --filter "DashboardModuleTests"

# Run with coverage
dotnet test WileyWidget.Tests/WileyWidget.Tests.csproj --collect:"XPlat Code Coverage"
```

## Troubleshooting

### Common Issues

#### Docker Build Failures
```powershell
# Clear Docker cache
docker system prune -f

# Rebuild without cache
docker build --no-cache -f Dockerfile.test -t wiley-widget-tests .
```

#### Test Execution Issues
```powershell
# Check container logs
docker logs wiley-widget-test-runner

# Run interactively for debugging
docker run -it --rm wiley-widget-tests pwsh
```

#### Permission Issues
```powershell
# Ensure TestResults directory is writable
icacls TestResults /grant "Everyone:(OI)(CI)F"
```

## Dependencies

### NuGet Packages
- **xunit**: Testing framework
- **Moq**: Mocking library
- **FluentAssertions**: Assertion library
- **Microsoft.NET.Test.Sdk**: Test SDK
- **Prism.Wpf**: Module framework

### Docker Requirements
- **Windows containers**: Required for WPF/.NET Windows apps
- **4GB+ RAM**: Recommended for .NET compilation
- **SSD storage**: Faster build times

## Contributing

When adding new tests:
1. Follow existing naming conventions
2. Include both positive and negative test cases
3. Add documentation comments
4. Update this README if needed
5. Test locally before committing

## Performance Notes

- **Container startup**: ~30-60 seconds initial build
- **Incremental builds**: ~10-20 seconds for code changes
- **Test execution**: ~5-15 seconds depending on test count
- **Resource usage**: ~2-4GB RAM during builds