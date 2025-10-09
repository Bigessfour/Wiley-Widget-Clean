# WileyWidget Dependency Injection Tests

Comprehensive test suite for validating dependency injection configuration, service lifetimes, and constructor resolution patterns.

## Test Categories

### 1. ServiceContainerValidationTests

Validates the entire DI container configuration:

- **Container Build Success**: Ensures host builds without errors
- **Service Resolvability**: All registered services can be resolved
- **Critical Services**: Key infrastructure services are present
- **Singleton Consistency**: Singleton services return same instance
- **Transient Behavior**: Transient services return new instances
- **ViewModel Registration**: All view models are properly registered
- **Configuration Loading**: appsettings.json is loaded correctly
- **Hosted Services**: Background services are registered
- **No Duplicate Singletons**: Critical services aren't double-registered

### 2. ConstructorResolutionTests

Tests constructor selection and ambiguity resolution:

- **ActivatorUtilitiesConstructor Attribute**: ShellViewModel uses the attribute
- **No Ambiguous Constructors**: DI can resolve ShellViewModel unambiguously
- **Multiple Constructor Handling**: ViewModels with multiple constructors are marked
- **Preferred Constructor Selection**: Marked constructor is chosen by ActivatorUtilities
- **Optional Parameters**: Constructors with optional dependencies work

### 3. ServiceLifetimeTests

Validates service lifetime behaviors and disposal:

- **Expected Lifetimes**: Services have correct Singleton/Transient/Scoped lifetimes
- **Scoped Isolation**: Scoped services are same within scope, different across scopes
- **Disposal on Scope End**: Scoped services dispose when scope disposes
- **Scope Validation**: Singletons can't depend on scoped services
- **Transient Disposal**: Transient IDisposable services are tracked and disposed
- **Independent Scopes**: Multiple scopes have independent lifetimes
- **Root Disposal**: Singleton services dispose with root ServiceProvider

## Running Tests

```powershell
# Run all DI tests
dotnet test WileyWidget.DependencyInjection.Tests.csproj

# Run with detailed output
dotnet test WileyWidget.DependencyInjection.Tests.csproj --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ServiceContainerValidationTests"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Expected Results

All tests should **PASS** in a correctly configured DI container. Failures indicate:

- **Container Build Failure**: Missing registrations or ambiguous constructors
- **Service Resolution Failure**: Dependencies not registered or circular dependencies
- **Constructor Ambiguity**: Multiple constructors without [ActivatorUtilitiesConstructor]
- **Lifetime Mismatch**: Services registered with wrong lifetime
- **Disposal Issues**: Resources not cleaned up properly

## Integration with CI/CD

These tests are designed to run in CI/CD pipelines to catch DI issues early:

1. **Pre-commit**: Run locally before committing DI changes
2. **PR Validation**: Automated on pull requests
3. **Build Pipeline**: Part of continuous integration
4. **Release Gates**: Must pass before deployment

## Fixing Common Issues

### Ambiguous Constructor Error

```csharp
// ❌ Bad: Multiple constructors, no attribute
public class MyViewModel
{
    public MyViewModel(ServiceA a) { }
    public MyViewModel(ServiceA a, ServiceB b) { }
}

// ✅ Good: Mark preferred constructor
public class MyViewModel
{
    [ActivatorUtilitiesConstructor]
    public MyViewModel(ServiceA a, ServiceB b) { }

    public MyViewModel(ServiceA a) : this(a, null!) { }
}
```

### Missing Service Registration

```csharp
// ❌ Bad: Service not registered
services.AddTransient<MyViewModel>();

// ✅ Good: Register all dependencies
services.AddTransient<ServiceA>();
services.AddTransient<ServiceB>();
services.AddTransient<MyViewModel>();
```

### Wrong Lifetime

```csharp
// ❌ Bad: Stateful service as transient
services.AddTransient<CacheService>(); // New cache each time!

// ✅ Good: Stateful services as singleton
services.AddSingleton<CacheService>(); // Shared cache
```

## References

- [Microsoft DI Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [ActivatorUtilitiesConstructorAttribute](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilitiesconstructorattribute)
- [Service Lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes)
- [DI Guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
