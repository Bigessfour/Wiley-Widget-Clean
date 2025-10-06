# GitHub Copilot Custom Instructions for Wiley Widget Project

This file contains language-specific guidelines and best practices for GitHub Copilot to follow when generating code in this project.

## PowerShell Development Guidelines

## Applies to: \*.ps1

Target PowerShell version: 7.5.3

### Output Handling (CRITICAL)

- **NEVER use Write-Host** - breaks pipeline compatibility and testability
- ✅ **Use Write-Output** for pipeline-compatible output that can be captured
- ✅ **Use Write-Information** for user notifications with `-InformationAction Continue`
- ✅ **Use Write-Verbose** for debug/diagnostic information
- ✅ **Use Write-Warning** for non-terminating warnings
- ✅ **Use Write-Error** for terminating errors

### Code Structure

- Use proper function naming: Verb-Noun format (e.g., Get-UserData, Set-Configuration)
- Include comment-based help for all public functions
- Use splatting for commands with multiple parameters
- Prefer foreach loops over ForEach-Object for performance-critical code
- Use $PSCmdlet.ThrowTerminatingError() for proper error handling in advanced functions

### Best Practices

- Use strict mode: Set-StrictMode -Version Latest
- Validate parameters with [Validate*] attributes
- Use [CmdletBinding()] for advanced functions
- Handle pipeline input properly with ValueFromPipeline/ByPropertyName
- Use Write-Progress for long-running operations

## Python Development Guidelines

## Applies to: \*.py

Target Python version: 3.11.9

### Code Style

- Follow PEP 8 guidelines strictly
- Use 4 spaces for indentation (no tabs)
- Line length: 88 characters (Black formatter default)
- Use snake_case for variables and functions
- Use PascalCase for classes
- Use UPPER_CASE for constants

### Type Hints and Modern Python

- Use type hints for all function parameters and return values
- Leverage Python 3.11 features: match statements, exception groups
- Use dataclasses for simple data structures
- Prefer f-strings over .format() and % formatting
- Use pathlib for file path operations

### Best Practices

- Write comprehensive docstrings using Google/NumPy style
- Use list/dict/set comprehensions when appropriate
- Handle exceptions specifically (avoid bare except)
- Use context managers (with statements) for resource management
- Follow EAFP (Easier to Ask for Forgiveness than Permission) principle
- Use virtual environments and requirements.txt for dependencies

### Testing

- Write unit tests using pytest
- Use fixtures for test setup/teardown
- Aim for high test coverage (>80%)
- Use descriptive test names and assertions

## C# Development Guidelines

## Applies to: \*.cs

Target framework: .NET 9.0

### Nullable Reference Types (CRITICAL - Prevents Most Warnings)

**Project Setting**: `<Nullable>enable</Nullable>` is enabled globally

#### Core Principles

- **Non-nullable by default**: Reference types without `?` are guaranteed non-null
- **Explicit nullable types**: Use `Type?` only when null is a valid state
- **Initialize properly**: All non-nullable fields/properties must be initialized
- **Validate inputs**: Use `ArgumentNullException.ThrowIfNull()` for required parameters

#### Best Practices to Prevent Warnings

**1. Constructor Initialization**

```csharp
// ✅ GOOD: Required properties ensure non-null values
public class Customer
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? PhoneNumber { get; init; } // Optional
}

// ✅ GOOD: Field initialization
public class Service
{
    private readonly ILogger _logger;

    public Service(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

**2. Parameter Validation**

```csharp
// ✅ GOOD: Use ThrowIfNull for required parameters
public void ProcessOrder(Order order)
{
    ArgumentNullException.ThrowIfNull(order);
    // Now 'order' is guaranteed non-null
}

// ✅ GOOD: Nullable parameters when null is valid
public void UpdateCustomer(string? phoneNumber)
{
    if (phoneNumber is not null)
    {
        // Safe to use phoneNumber here
    }
}
```

**3. Null State Analysis**

```csharp
// ✅ GOOD: Compiler tracks null state
public string GetDisplayName(Customer customer)
{
    ArgumentNullException.ThrowIfNull(customer);

    // Compiler knows customer.Name is not null
    return customer.Name;
}

// ✅ GOOD: Null checks change null state
public void SendEmail(string email)
{
    if (string.IsNullOrEmpty(email))
        return;

    // Compiler knows email is not null here
    _emailService.Send(email);
}
```

**4. Null-Forgiving Operator (Use Sparingly)**

```csharp
// ⚠️ CAUTION: Only use ! when you know the value isn't null
public void ProcessKnownGoodData(string data)
{
    // If you know data is validated elsewhere
    var length = data!.Length; // Suppresses warning
}
```

**5. Attributes for API Contracts**

```csharp
// ✅ GOOD: Attributes provide null-state information
[return: NotNullIfNotNull(nameof(input))]
public string? Transform(string? input)
{
    return input?.ToUpper();
}

// ✅ GOOD: Null check methods
private bool IsValid([NotNullWhen(true)] string? value)
{
    return !string.IsNullOrWhiteSpace(value);
}
```

**6. Collection Handling**

```csharp
// ✅ GOOD: Non-nullable collections
public List<Order> Orders { get; } = new();

// ✅ GOOD: Nullable collections when appropriate
public List<Order>? OptionalOrders { get; set; }

// ❌ BAD: Uninitialized collections cause warnings
public List<Order> BadOrders; // CS8618 warning
```

**7. Async Methods**

```csharp
// ✅ GOOD: Return Task<T> for async operations
public async Task<string> GetDataAsync()
{
    var result = await _api.GetAsync();
    return result ?? string.Empty; // Handle null
}
```

#### Common Patterns to Avoid

**❌ DON'T: Unnecessary nullable types**

```csharp
// Bad - makes everything nullable unnecessarily
public string? GetName() => "John"; // Should be string
```

**❌ DON'T: Late initialization without required**

```csharp
// Bad - causes CS8618 warnings
public class BadClass
{
    public string Name { get; set; } // Not initialized
}
```

**❌ DON'T: Overuse null-forgiving operator**

```csharp
// Bad - hides real null issues
public void BadMethod(string? input)
{
    var length = input!.Length; // Dangerous
}
```

### Naming Conventions

- Use PascalCase for classes, methods, properties, and namespaces
- Use camelCase for local variables and method parameters
- Use UPPER_CASE for constants
- Prefix private fields with underscore: \_privateField
- Use meaningful, descriptive names

### Code Structure

- Use async/await for asynchronous operations
- Prefer LINQ for data manipulation
- Use dependency injection with interfaces
- Implement proper exception handling with custom exceptions when needed
- Use the using statement for IDisposable objects

### Best Practices

- Follow SOLID principles
- Use nullable reference types where appropriate
- Prefer immutable objects when possible
- Use records for simple data transfer objects
- Implement proper logging with Microsoft.Extensions.Logging
- Write XML documentation comments for public APIs

### Testing

- Use xUnit.net for unit testing
- Follow AAA pattern (Arrange, Act, Assert)
- Use Moq for mocking dependencies
- Write integration tests for critical paths
- Use TestData attributes for parameterized tests

### Performance

- Use StringBuilder for string concatenation in loops
- Prefer arrays over lists when size is known
- Use Span<T> for high-performance operations
- Avoid boxing/unboxing where possible
- Profile and optimize bottlenecks
