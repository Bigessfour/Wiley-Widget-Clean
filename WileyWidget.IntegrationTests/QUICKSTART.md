# Integration Tests - Quick Start Guide

## 🚀 Prerequisites Checklist

- [ ] Docker Desktop installed and running
- [ ] .NET 9.0 SDK installed
- [ ] 4GB+ RAM available for containers
- [ ] Port 1433 available (or using dynamic ports)

## ⚡ Quick Start Commands

### 1. Verify Docker is Running
```powershell
docker --version
docker ps
```

### 2. Build the Integration Tests Project
```powershell
# From workspace root
dotnet build WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj
```

### 3. Run All Integration Tests
```powershell
dotnet test WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj --verbosity normal
```

### 4. Run Specific Test Categories
```powershell
# Concurrency tests
dotnet test --filter "FullyQualifiedName~ConcurrencyTests"

# Relationship tests
dotnet test --filter "FullyQualifiedName~RelationshipTests"

# Performance tests
dotnet test --filter "FullyQualifiedName~PerformanceTests"
```

## 🎯 Expected First Run Experience

### Container Initialization (First Time)
```
Starting SQL Server container...
Pulling image mcr.microsoft.com/mssql/server:2022-latest (this takes 2-5 minutes)
Container started successfully
Running migrations...
Tests executing...
```

### Subsequent Runs
```
Starting SQL Server container... (5-10 seconds)
Container started successfully
Tests executing...
```

## 📊 Sample Test Output

```
Starting test execution, please wait...
A total of 12 test files matched the specified pattern.

Test Run Successful.
Total tests: 15
     Passed: 15
 Total time: 45.3214 Seconds

Tests Summary:
✅ ConcurrencyConflictTests (5 tests, 5 passed)
✅ ForeignKeyRelationshipTests (7 tests, 7 passed)
✅ PerformanceComparisonTests (3 tests, 3 passed)
```

## 🐛 Common First-Time Issues

### Issue 1: Docker Not Running
```
Error: Cannot connect to the Docker daemon
```
**Fix**: Start Docker Desktop and wait for it to fully initialize

### Issue 2: Image Pull Timeout
```
Error: Failed to pull image
```
**Fix**: Check internet connection. Pull manually:
```powershell
docker pull mcr.microsoft.com/mssql/server:2022-latest
```

### Issue 3: Port Already in Use
```
Error: Bind for 0.0.0.0:1433 failed: port is already allocated
```
**Fix**: The tests use dynamic port binding, but if you see this:
```powershell
docker ps -a
docker stop $(docker ps -q --filter ancestor=mcr.microsoft.com/mssql/server:2022-latest)
```

### Issue 4: Memory Issues
```
Error: Container exited with code 137
```
**Fix**: Increase Docker memory limit:
1. Open Docker Desktop
2. Settings > Resources > Memory
3. Increase to at least 4GB
4. Apply & Restart

## 🔍 Debugging Tests

### Run with Detailed Logging
```powershell
dotnet test --verbosity detailed --logger "console;verbosity=detailed"
```

### Run Single Test
```powershell
dotnet test --filter "FullyQualifiedName=WileyWidget.IntegrationTests.ConcurrencyConflictTests.UpdateWithStaleRowVersion_ShouldThrowConcurrencyException"
```

### View Container Logs
```powershell
# List running containers
docker ps

# View logs for specific container
docker logs <container-id>

# Follow logs in real-time
docker logs -f <container-id>
```

## 🏃 Performance Benchmarking

### Run BenchmarkDotNet Tests
```powershell
cd WileyWidget.IntegrationTests
dotnet run -c Release -- --filter *PerformanceBenchmarks*
```

### View Benchmark Results
Results are saved to:
- `BenchmarkDotNet.Artifacts/results/`
- HTML reports: `*-report.html`
- CSV data: `*-report.csv`

## 📈 CI/CD Integration

### GitHub Actions Workflow
```yaml
- name: Start Docker
  run: |
    systemctl start docker
    docker ps

- name: Run Integration Tests
  run: |
    dotnet test WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj \
      --configuration Release \
      --logger trx \
      --results-directory TestResults/Integration
```

## 🎓 Next Steps

1. ✅ Run all tests to verify setup
2. 📖 Read [INTEGRATION_TESTING_STRATEGY.md](../docs/INTEGRATION_TESTING_STRATEGY.md)
3. 🔍 Explore test code in `WileyWidget.IntegrationTests/Tests/`
4. 🛠️ Add your own tests using the patterns shown
5. 📊 Run performance benchmarks and compare results

## 📚 Additional Resources

- [Full Integration Tests README](README.md)
- [TestContainers Documentation](https://dotnet.testcontainers.org/)
- [xUnit Documentation](https://xunit.net/)
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)

## ✅ Success Checklist

After running tests, you should see:
- [ ] All tests passed
- [ ] Container started and stopped cleanly
- [ ] No Docker containers left running (`docker ps -a`)
- [ ] Test results generated
- [ ] No errors in console output

## 🆘 Need Help?

- **Docker Issues**: Check Docker Desktop status and logs
- **Test Failures**: Run with `--verbosity detailed` for more info
- **Performance Issues**: Check Docker resource allocation
- **Build Errors**: Run `dotnet restore` and `dotnet clean`

---

**Ready to test?** Start with: `dotnet test WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj`

**Last Updated**: October 11, 2025
