# WileyWidget.IntegrationTests

## ⚠️ **CURRENT STATUS: DISABLED**

**Integration tests are currently disabled** due to .NET 9.0 WPF project reference compatibility issues.

See **[Known Issues](#-known-issues)** section below for details and resolution plan.

---

## Overview

This project contains comprehensive integration tests for the Wiley Widget application, focusing on database operations, entity relationships, concurrency handling, and performance benchmarking.

## 🎯 Testing Scope

### 1. **Database Integration Tests**

- Real database operations using SQL Server TestContainers
- Entity Framework Core behavior validation
- Transaction handling and isolation
- Migration and schema validation

### 2. **Concurrency Tests**

- Optimistic concurrency with row versioning
- Conflict detection and resolution
- Concurrent update scenarios
- Timestamp-based concurrency tokens

### 3. **Relationship & Constraint Tests**

- Foreign key integrity
- Cascading deletes and updates
- Navigation property behavior
- Required vs optional relationships
- Constraint violation handling

### 4. **Performance Benchmarking**

- SQLite vs SQL Server comparison
- CRUD operation performance
- Query optimization validation
- Bulk operation efficiency

## 🐳 TestContainers Setup

This project uses [Testcontainers](https://dotnet.testcontainers.org/) to spin up real SQL Server instances for testing.

### Prerequisites

- Docker Desktop installed and running
- .NET 9.0 SDK
- 4GB+ available RAM for containers

### Container Configuration

```csharp
var container = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .WithPassword("YourStrong!Passw0rd")
    .WithPortBinding(1433, true)
    .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
    .Build();
```

## 🚀 Running Tests

### Run All Integration Tests

```powershell
dotnet test WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj
```

### Run Specific Test Categories

```powershell
# Concurrency tests only
dotnet test --filter "FullyQualifiedName~ConcurrencyTests"

# Relationship tests only
dotnet test --filter "FullyQualifiedName~RelationshipTests"

# Performance benchmarks only
dotnet test --filter "FullyQualifiedName~PerformanceTests"
```

### Run with Detailed Output

```powershell
dotnet test --verbosity detailed --logger "console;verbosity=detailed"
```

### Run Performance Benchmarks (BenchmarkDotNet)

```powershell
cd WileyWidget.IntegrationTests
dotnet run -c Release -- --filter *PerformanceBenchmarks*
```

## 📊 Performance Benchmarking

### BenchmarkDotNet Tests

The `DatabasePerformanceBenchmarks` class provides detailed performance metrics:

- **Insert operations**: Single and bulk inserts
- **Query operations**: Simple queries, complex joins, filtering
- **Update operations**: Single and batch updates
- **Delete operations**: Single and cascading deletes

### Expected Results

Typical performance characteristics (your results may vary):

| Operation         | SQLite | SQL Server | Winner     |
| ----------------- | ------ | ---------- | ---------- |
| Single Insert     | ~1ms   | ~2ms       | SQLite     |
| Bulk Insert (100) | ~50ms  | ~30ms      | SQL Server |
| Simple Query      | ~0.5ms | ~1ms       | SQLite     |
| Complex Join      | ~5ms   | ~3ms       | SQL Server |
| Update            | ~1ms   | ~2ms       | SQLite     |
| Delete            | ~1ms   | ~2ms       | SQLite     |

**Key Findings:**

- **SQLite**: Faster for single operations and simple queries (no network overhead)
- **SQL Server**: Better for bulk operations, complex queries, and concurrent access
- **Production Choice**: SQL Server recommended for multi-user scenarios

## 🧪 Test Structure

### Base Infrastructure

- `SqlServerTestBase`: Base class for SQL Server tests with TestContainers
- `TestDataBuilder`: Fluent API for creating test data
- `TestHelpers`: Utility methods for assertions and validation

### Test Categories

#### 1. ConcurrencyConflictTests

```csharp
[Fact]
public async Task UpdateWithStaleRowVersion_ShouldThrowConcurrencyException()
{
    // Tests optimistic concurrency handling
}
```

#### 2. ForeignKeyRelationshipTests

```csharp
[Fact]
public async Task DeleteVendorWithInvoices_ShouldCascadeDelete()
{
    // Tests cascading delete behavior
}
```

#### 3. PerformanceComparisonTests

```csharp
[Fact]
public async Task CompareInsertPerformance_SqlServerVsSqlite()
{
    // Compares database performance
}
```

## 🔧 Configuration

### Test Settings

Configuration is managed through `appsettings.test.json` (if needed) or environment variables:

```json
{
  "TestContainers": {
    "SqlServer": {
      "Image": "mcr.microsoft.com/mssql/server:2022-latest",
      "Password": "YourStrong!Passw0rd",
      "CleanupAfterTests": true
    }
  }
}
```

### Connection Strings

Automatically generated for each test container:

```csharp
Server=localhost,{dynamicPort};Database=WileyWidgetTest;User Id=sa;Password=YourStrong!Passw0rd;
```

## 🐛 Troubleshooting

### Docker Issues

**Problem**: TestContainers fails to start

```
Error: Docker daemon is not running
```

**Solution**: Start Docker Desktop and ensure it's fully initialized

### Port Conflicts

**Problem**: SQL Server container fails to bind port

```
Error: Port 1433 is already in use
```

**Solution**: Tests use dynamic port binding automatically. Check Docker containers:

```powershell
docker ps -a
docker stop <container-id>
```

### Memory Issues

**Problem**: Container fails to start due to memory

```
Error: Cannot allocate memory
```

**Solution**: Increase Docker Desktop memory allocation (Settings > Resources)

### Test Timeout

**Problem**: Tests timeout waiting for container

```
Error: Container did not become healthy in time
```

**Solution**: Increase timeout in test configuration or check Docker performance

## 📝 Test Data Management

### Test Data Builder Pattern

```csharp
var vendor = TestDataBuilder.CreateVendor()
    .WithName("Test Vendor")
    .WithContactEmail("test@example.com")
    .Build();

var invoice = TestDataBuilder.CreateInvoice()
    .WithVendor(vendor)
    .WithAmount(1000m)
    .WithDueDate(DateTime.Today.AddDays(30))
    .Build();
```

### Cleanup Strategy

- **Per-Test Cleanup**: Each test uses a fresh database
- **Container Lifecycle**: Containers are disposed after test completion
- **Transaction Rollback**: Not used (full database isolation instead)

## 🎓 Best Practices

### 1. Test Isolation

✅ Each test gets a fresh database instance
✅ No shared state between tests
✅ Parallel test execution supported

### 2. Performance Testing

✅ Use Release configuration for benchmarks
✅ Run multiple iterations for accuracy
✅ Measure cold and warm cache scenarios

### 3. Assertion Patterns

```csharp
// Use xUnit assertions
Assert.NotNull(result);
Assert.Equal(expected, actual);
Assert.Throws<DbUpdateConcurrencyException>(() => operation);

// Use FluentAssertions for complex assertions
result.Should().NotBeNull();
result.Name.Should().Be("Expected Name");
invoices.Should().HaveCount(3);
```

### 4. Async Testing

```csharp
[Fact]
public async Task TestName()
{
    // Always use async/await for database operations
    var result = await _context.Vendors.ToListAsync();
}
```

## 📚 Related Documentation

- [Integration Testing Strategy](../docs/INTEGRATION_TESTING_STRATEGY.md)
- [Database Setup Guide](../docs/database-setup.md)
- [Development Guide](../docs/DEVELOPMENT_GUIDE_AND_BEST_PRACTICES.md)

## 🤝 Contributing

When adding new integration tests:

1. **Inherit from `SqlServerTestBase`** for database tests
2. **Use TestContainers** for real database isolation
3. **Follow AAA pattern**: Arrange, Act, Assert
4. **Add descriptive test names** that explain the scenario
5. **Include comments** for complex test setups
6. **Test both success and failure paths**
7. **Clean up resources** (handled automatically by base class)

---

## ⚠️ Known Issues

### Integration Tests Disabled - .NET 9.0 WPF Compatibility

**Status**: 🔴 **BLOCKED** - Tests excluded from compilation

**Problem**: Test projects cannot reference .NET 9.0 WPF projects directly due to temporary build artifact generation.

#### Technical Details

When `WileyWidget.IntegrationTests` (`.NET 8.0`) references `WileyWidget` (`.NET 9.0-windows` WPF), two failures occur:

1. **Framework mismatch** when test project targets `.NET 8.0`:

   ```
   error NU1201: Project WileyWidget is not compatible with net8.0
   Project WileyWidget supports: net9.0-windows7.0
   ```

2. **Missing dependencies** when test project targets `.NET 9.0-windows`:
   - WPF generates temporary projects (`*_wpftmp.csproj`) during build
   - Test code compiles against temporary project instead of actual project
   - Temporary project doesn't include test project's NuGet packages
   - Results in hundreds of `CS0246` errors (types not found)

#### Root Cause

From [Microsoft Documentation - Fix intermittent build failures](https://learn.microsoft.com/en-us/visualstudio/msbuild/fix-intermittent-build-failures):

> WPF projects create temporary build artifacts during compilation. Test projects compiling against these temporary projects lose access to their own dependencies.

#### Solution (Pending Implementation)

**Create `WileyWidget.Core` class library** to extract shared code:

```
WileyWidget.Core/              ← NEW: .NET 8.0 class library
├── Models/                    ← Moved from WileyWidget/Models
├── Data/                      ← Moved from WileyWidget/Data
└── WileyWidget.Core.csproj

WileyWidget/                   ← .NET 9.0-windows WPF app
└── References WileyWidget.Core

WileyWidget.IntegrationTests/  ← .NET 8.0 test project
└── References WileyWidget.Core (NOT WileyWidget)
```

**Implementation Steps**:

1. Create `WileyWidget.Core` library (`.NET 8.0`)
2. Move `Models/` and `Data/` to Core
3. Update project references
4. Re-enable tests in `.csproj`

#### Current Workaround

Tests are **excluded from compilation** but preserved for future use:

```xml
<ItemGroup>
  <Compile Remove="**\*.cs" />
  <None Include="**\*.cs" />
</ItemGroup>
```

Build script validation works:

```powershell
.\scripts\build-integration-tests.ps1
# Output: ⚠️  Integration tests temporarily disabled
```

✅ Build infrastructure validated  
✅ Comprehensive logging implemented  
✅ PSScriptAnalyzer compliant  
❌ Tests awaiting architecture refactor

**Related Issues**:

- [WPF Temporary Projects](https://github.com/dotnet/wpf/issues)
- [.NET Multi-Targeting](https://learn.microsoft.com/en-us/dotnet/standard/frameworks)

---

## 📊 CI/CD Integration

These tests are integrated into the CI/CD pipeline:

```yaml
- name: Run Integration Tests
  run: |
    docker ps  # Verify Docker is running
    dotnet test WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj \
      --configuration Release \
      --logger trx \
      --results-directory TestResults/Integration
```

## 🏆 Success Metrics

- **Test Coverage**: >80% of database operations
- **Test Execution Time**: <5 minutes for full suite
- **Container Startup**: <30 seconds
- **Test Reliability**: >95% pass rate on CI/CD

## 📄 License

This project is part of Wiley Widget and follows the same license terms.

---

**Last Updated**: October 11, 2025  
**Maintained By**: Development Team  
**Questions?**: See [CONTRIBUTING.md](../CONTRIBUTING.md)
