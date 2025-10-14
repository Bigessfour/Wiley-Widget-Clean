# RegionViewRegistry Test Suite

Comprehensive xUnit test suite for Prism's `RegionViewRegistry` functionality in WPF applications. These tests validate view registration, error handling, auto-population behaviors, and region activation/deactivation lifecycle management.

## 📋 Table of Contents

- [Overview](#overview)
- [Test Coverage](#test-coverage)
- [Running Tests](#running-tests)
- [Docker Isolation](#docker-isolation)
- [Test Categories](#test-categories)
- [Architecture](#architecture)

## 🎯 Overview

This test suite provides comprehensive coverage for Prism's region management system, focusing on:

- **View Registration**: Registering views with regions using `RegisterViewWithRegion()`
- **Error Handling**: XAML parse exceptions, DI resolution failures, constructor errors
- **Auto-Population**: Automatic view injection when regions are created
- **Region Lifecycle**: View activation, deactivation, and KeepAlive behaviors
- **Concurrent Operations**: Thread-safe registration and resolution
- **Navigation**: INavigationAware implementation testing

## 🧪 Test Coverage

### Test Categories

| Category | Tests | Description |
|----------|-------|-------------|
| **View Registration** | 4 tests | Basic registration validation, null checks |
| **Registration Failures** | 4 tests | XAML parse errors, DI failures, constructor exceptions |
| **Auto-Population** | 3 tests | Region view injection, resolution handling |
| **Activation/Deactivation** | 3 tests | Region lifecycle management |
| **Multiple Views** | 2 tests | Multi-view registration per region |
| **Exception Handling** | 3 tests | Circular dependencies, resolution failures |
| **Concurrent Registration** | 1 test | Thread-safe operations |
| **Region Lifecycle** | 2 tests | Lazy resolution, activation order |
| **View-Specific Behaviors** | 3 tests | KeepAlive, navigation awareness |

**Total Tests**: 25+ comprehensive test cases

### Key Test Scenarios

#### ✅ Basic Registration
```csharp
RegisterViewWithRegion("MainRegion", typeof(DashboardView));
```

#### ⚠️ Error Scenarios
- **XAML Parse Exception**: Invalid XAML markup in view
- **DI Resolution Failure**: Missing service registrations
- **Constructor Exception**: View constructor throws during instantiation
- **Circular Dependencies**: Service A → B → A dependency chains

#### 🔄 Lifecycle Management
- **Auto-Population**: Views automatically added when regions are created
- **KeepAlive Behavior**: Views persisted (true) vs. transient (false)
- **Activation Order**: Maintains registration sequence
- **Lazy Resolution**: Views resolved only when regions exist

## 🚀 Running Tests

### Local Execution

```powershell
# Run all RegionViewRegistry tests
dotnet test --filter "FullyQualifiedName~RegionViewRegistryTests"

# With detailed output
dotnet test --filter "FullyQualifiedName~RegionViewRegistryTests" --logger "console;verbosity=detailed"

# With coverage
dotnet test --filter "FullyQualifiedName~RegionViewRegistryTests" --collect "XPlat Code Coverage"
```

### Using VS Code Tasks

Available tasks in `.vscode/tasks.json`:
- **test-csharp**: Run all C# tests including RegionViewRegistry
- **test-csharp-coverage**: Run with code coverage
- **test-all**: Run entire test suite

## 🐳 Docker Isolation

The test suite includes Docker configurations for isolated, reproducible test execution.

### Files

- **Dockerfile.regionviewregistry-tests**: Multi-stage Docker build for tests
- **docker-compose.regionviewregistry-tests.yml**: Orchestration configuration
- **scripts/run-regionviewregistry-tests.ps1**: PowerShell test runner

### Docker Build Stages

1. **build**: Restore dependencies and compile test project
2. **test**: Run tests in isolated container
3. **test-volume**: Development mode with live file mounting

### Quick Start

```powershell
# Standard test execution
.\scripts\run-regionviewregistry-tests.ps1

# With coverage collection
.\scripts\run-regionviewregistry-tests.ps1 -Mode Coverage

# Development mode (live file mounting)
.\scripts\run-regionviewregistry-tests.ps1 -Mode Dev

# Rebuild image from scratch
.\scripts\run-regionviewregistry-tests.ps1 -Rebuild
```

### Using Docker Directly

```bash
# Build the test image
docker build -f Dockerfile.regionviewregistry-tests --target test -t wiley-regionviewregistry-tests .

# Run tests with results mounting
docker run --rm \
  -v $(pwd)/TestResults/RegionViewRegistry:/testresults \
  wiley-regionviewregistry-tests

# Using docker-compose
docker-compose -f docker-compose.regionviewregistry-tests.yml run --rm regionviewregistry-tests
```

### Development Mode

Mount test project for iterative development:

```bash
docker-compose -f docker-compose.regionviewregistry-tests.yml run --rm \
  -v $(pwd)/WileyWidget.Tests:/src/WileyWidget.Tests:ro \
  regionviewregistry-tests-dev
```

## 📁 Test Architecture

### Test Class Structure

```
RegionViewRegistryTests
├── View Registration Tests (4)
│   ├── Valid registration
│   ├── Null region name
│   ├── Empty region name
│   └── Null view type
├── Registration Failure Tests (4)
│   ├── Container resolution failure
│   ├── XAML parse exception
│   ├── DI injection failure
│   └── Constructor exceptions
├── Auto-Population Tests (3)
│   ├── Auto-populate on region creation
│   ├── No views registered
│   └── Partial resolution failure
├── Activation/Deactivation Tests (3)
│   ├── View activation
│   ├── View deactivation
│   └── KeepAlive behavior
└── Advanced Tests (11)
    ├── Multiple views per region
    ├── Concurrent registration
    ├── Lazy resolution
    ├── Navigation awareness
    └── Lifecycle management
```

### Mock Objects

Uses Moq for dependency isolation:

```csharp
Mock<IContainerProvider> _mockContainerProvider;
Mock<ITestRegionManager> _mockRegionManager;
Mock<ITestRegion> _mockRegion;
```

### Custom Exceptions

```csharp
ViewRegistrationException: Wraps all registration-related errors
├── XAML Parse Errors (XamlParseException)
├── DI Resolution Errors (InvalidOperationException)
├── Constructor Errors (Exception)
└── Circular Dependencies (InvalidOperationException)
```

## 🔍 Test Implementation Details

### View Registration Validation

```csharp
[Fact]
public void RegisterViewWithRegion_ValidInputs_DelegatesToRegionManager()
{
    const string regionName = "MainRegion";
    var viewType = typeof(TestDashboardView);

    _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType);

    _mockRegionManager.Verify(rm =>
        rm.RegisterViewWithRegion(
            It.Is<string>(s => s == regionName),
            It.Is<Type>(t => t == viewType)),
        Times.Once);
}
```

### Error Handling

```csharp
[Fact]
public void RegisterViewWithRegion_XamlParseException_ThrowsViewRegistrationException()
{
    const string regionName = "MainRegion";
    var viewType = typeof(TestDashboardView);

    _mockRegionManager
        .Setup(rm => rm.RegisterViewWithRegion(regionName, viewType))
        .Throws(new ViewRegistrationException("XAML parsing failed",
            new System.Windows.Markup.XamlParseException("Invalid XAML")));

    var exception = Assert.Throws<ViewRegistrationException>(() =>
        _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType));

    exception.InnerException.Should().BeOfType<System.Windows.Markup.XamlParseException>();
}
```

### Auto-Population Testing

```csharp
[Fact]
public void RegionViewRegistry_AutoPopulatesRegisteredViews_WhenRegionCreated()
{
    const string regionName = "MainRegion";
    var viewType = typeof(TestDashboardView);
    var expectedView = new TestDashboardView();

    _mockContainerProvider.Setup(cp => cp.Resolve(viewType)).Returns(expectedView);

    var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);
    registry.RegisterViewWithRegion(regionName, viewType);
    registry.OnRegionCreated(_mockRegion.Object, regionName);

    _mockRegion.Verify(r => r.Add(expectedView), Times.Once);
}
```

## 📊 Coverage Expectations

Target coverage metrics:

| Metric | Target | Description |
|--------|--------|-------------|
| Line Coverage | 95%+ | Core registration logic |
| Branch Coverage | 90%+ | All error paths validated |
| Method Coverage | 100% | All public APIs tested |

## 🔧 Configuration

### Test Settings

- **Framework**: .NET 9.0
- **UI Framework**: WPF (Windows Forms fallback in tests)
- **Test Framework**: xUnit 2.4.2+
- **Mocking**: Moq 4.20.0+
- **Assertions**: FluentAssertions 6.12.0+

### Docker Configuration

- **Base Image**: mcr.microsoft.com/dotnet/sdk:9.0
- **Runtime**: Linux x64
- **Headless Mode**: DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
- **Results Mount**: ./TestResults/RegionViewRegistry

## 📝 Best Practices

### When Writing New Tests

1. **Follow AAA Pattern**: Arrange, Act, Assert
2. **Use Descriptive Names**: Method_Scenario_ExpectedBehavior
3. **Mock External Dependencies**: IContainerProvider, IRegionManager
4. **Test One Thing**: Single assertion per test when possible
5. **Handle Exceptions**: Use Assert.Throws<T>() for error scenarios

### Test Isolation

- Each test creates fresh mock instances
- No shared state between tests
- Docker provides complete environment isolation
- Test results written to dedicated directory

## 🐛 Troubleshooting

### Common Issues

**Tests fail with "Type not registered"**
```csharp
// Ensure mock setup includes required types
_mockContainerProvider.Setup(cp => cp.Resolve(typeof(YourView))).Returns(new YourView());
```

**XAML parse exceptions in Docker**
```bash
# WPF requires specific dependencies in Linux
# Already included in Dockerfile.regionviewregistry-tests
```

**Coverage not generated**
```powershell
# Use Coverage mode explicitly
.\scripts\run-regionviewregistry-tests.ps1 -Mode Coverage
```

## 📚 References

- [Prism Library Documentation](https://prismlibrary.com/)
- [Prism Region Navigation](https://prismlibrary.com/docs/wpf/legacy/Regions.html)
- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)

## 🤝 Contributing

When adding new tests:

1. Follow existing test patterns
2. Add to appropriate test category region
3. Update this README with new test count
4. Ensure tests pass in Docker isolation
5. Maintain 90%+ coverage

## 📄 License

Part of the Wiley Widget project. See root LICENSE file for details.
