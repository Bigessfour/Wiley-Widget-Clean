# RegionViewRegistry Test Suite - Quick Reference

## 🚀 Quick Start

```powershell
# Local execution
dotnet test --filter "FullyQualifiedName~RegionViewRegistryTests"

# Docker isolation
.\scripts\run-regionviewregistry-tests.ps1

# With coverage
.\scripts\run-regionviewregistry-tests.ps1 -Mode Coverage
```

## 📁 Files Created

| File | Purpose |
|------|---------|
| `WileyWidget.Tests/Regions/RegionViewRegistryTests.cs` | Enhanced test suite (25+ tests) |
| `Dockerfile.regionviewregistry-tests` | Multi-stage Docker build |
| `docker-compose.regionviewregistry-tests.yml` | Container orchestration |
| `scripts/run-regionviewregistry-tests.ps1` | PowerShell test runner |
| `scripts/test-regionviewregistry.sh` | Bash test runner |
| `.github/workflows/regionviewregistry-tests.yml` | CI/CD workflow |
| `docs/REGIONVIEWREGISTRY_TESTS.md` | Complete documentation |

## 🧪 Test Categories (25+ Tests)

### ✅ View Registration (4 tests)
- Valid registration
- Null/empty region name validation
- Null view type validation

### ⚠️ Error Handling (7 tests)
- XAML parse exceptions
- DI resolution failures
- Constructor exceptions
- Circular dependencies
- All views fail resolution

### 🔄 Auto-Population (3 tests)
- Views auto-added on region creation
- No views registered
- Partial resolution failure handling

### 🎯 Lifecycle Management (5 tests)
- View activation/deactivation
- KeepAlive behavior
- Lazy resolution
- Activation order preservation

### 🔀 Multi-View Scenarios (3 tests)
- Multiple views per region
- Same view type registered twice
- Concurrent registrations

### 🧭 Navigation & Advanced (3 tests)
- INavigationAware implementation
- KeepAlive true/false behaviors
- Navigation state management

## 🐳 Docker Commands

```bash
# Build image
docker build -f Dockerfile.regionviewregistry-tests --target test -t wiley-regionviewregistry-tests .

# Run tests
docker run --rm -v $(pwd)/TestResults/RegionViewRegistry:/testresults wiley-regionviewregistry-tests

# Using docker-compose
docker-compose -f docker-compose.regionviewregistry-tests.yml run --rm regionviewregistry-tests

# Development mode (live mounting)
docker-compose -f docker-compose.regionviewregistry-tests.yml run --rm regionviewregistry-tests-dev

# Clean up
docker-compose -f docker-compose.regionviewregistry-tests.yml down --rmi all
```

## 📊 Coverage Expectations

| Metric | Target |
|--------|--------|
| Line Coverage | 95%+ |
| Branch Coverage | 90%+ |
| Method Coverage | 100% |

## 🔍 Key Test Examples

### Basic Registration
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
    _mockRegionManager
        .Setup(rm => rm.RegisterViewWithRegion(regionName, viewType))
        .Throws(new ViewRegistrationException("XAML parsing failed",
            new XamlParseException("Invalid XAML")));
    
    var exception = Assert.Throws<ViewRegistrationException>(() =>
        _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType));
    
    exception.InnerException.Should().BeOfType<XamlParseException>();
}
```

### Auto-Population
```csharp
[Fact]
public void RegionViewRegistry_AutoPopulatesRegisteredViews_WhenRegionCreated()
{
    _mockContainerProvider.Setup(cp => cp.Resolve(viewType)).Returns(expectedView);
    
    var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);
    registry.RegisterViewWithRegion(regionName, viewType);
    registry.OnRegionCreated(_mockRegion.Object, regionName);
    
    _mockRegion.Verify(r => r.Add(expectedView), Times.Once);
}
```

## 🛠️ Mock Objects

All tests use Moq for dependency isolation:

```csharp
Mock<IContainerProvider> _mockContainerProvider;
Mock<ITestRegionManager> _mockRegionManager;
Mock<ITestRegion> _mockRegion;
```

## 🎯 Custom Test Types

### Exceptions
```csharp
ViewRegistrationException: Base exception for all registration errors
├── XAML parse errors (XamlParseException inner)
├── DI failures (InvalidOperationException inner)
├── Constructor errors (Exception inner)
└── Circular dependencies (InvalidOperationException inner)
```

### Test Views
```csharp
TestDashboardView: UserControl + ITestRegionMemberLifetime (KeepAlive = true)
TestSettingsView: UserControl + ITestRegionMemberLifetime (KeepAlive = false)
TestNavigationAwareView: UserControl + ITestNavigationAware
```

### Test Interfaces
```csharp
ITestRegionManager: Simulates Prism's IRegionManager
ITestRegion: Simulates Prism's IRegion
ITestRegionMemberLifetime: Simulates Prism's IRegionMemberLifetime
ITestNavigationAware: Simulates Prism's INavigationAware
```

## 🔄 CI/CD Integration

The GitHub Actions workflow automatically:
1. Runs tests on push/PR to main/develop
2. Collects code coverage
3. Generates HTML coverage reports
4. Publishes test results as artifacts
5. Comments coverage on PRs
6. Validates 90% coverage threshold

## 📝 Adding New Tests

1. Add test to appropriate `#region` block
2. Follow naming: `MethodName_Scenario_ExpectedBehavior`
3. Use AAA pattern: Arrange, Act, Assert
4. Ensure test passes locally
5. Run in Docker to validate isolation
6. Update test count in documentation

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| Tests fail locally but pass in Docker | Check WPF dependencies, use Docker as source of truth |
| Coverage not generated | Use `-Mode Coverage` or `--collect "XPlat Code Coverage"` |
| Mock setup not working | Verify `Setup()` before calling `Object` property |
| Container resolution fails | Ensure `_mockContainerProvider.Setup(...)` configured |

## 📚 References

- **Main Docs**: `docs/REGIONVIEWREGISTRY_TESTS.md`
- **Test File**: `WileyWidget.Tests/Regions/RegionViewRegistryTests.cs`
- **Dockerfile**: `Dockerfile.regionviewregistry-tests`
- **Prism Docs**: https://prismlibrary.com/docs/wpf/legacy/Regions.html

## ✅ Validation Checklist

Before committing:
- [ ] All tests pass locally
- [ ] Tests pass in Docker
- [ ] Coverage meets 90% threshold
- [ ] No compiler warnings
- [ ] Documentation updated
- [ ] CI workflow validates successfully

---
**Created**: 2025-10-14  
**Last Updated**: 2025-10-14  
**Test Count**: 25+  
**Target Coverage**: 90%+
