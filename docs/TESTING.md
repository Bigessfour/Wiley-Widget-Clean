# WileyWidget Testing Guide

## Overview

This document provides comprehensive information about the xUnit testing environment configured for the WileyWidget project, including setup details, package dependencies, and execution instructions.
# WileyWidget Testing Guide (Python-first)

We have standardized on Python (pytest) for all unit, integration, and UI automation testing. .NET/xUnit projects were retired to reduce redundancy and simplify CI.

## What’s in use

- Test runner: pytest
- Config: pytest.ini at repo root
- Entry tasks: VS Code task “test-fast” (runs unit or smoke markers)

## Quick start

```powershell
# (Optional) Ensure Python deps are installed
python -m pip install -r requirements-test.txt

# Run fast suite (unit + smoke markers)
python -m pytest -m "unit or smoke" --tb=short --maxfail=5

# Run everything
python -m pytest
```

## Markers

- unit – fast logic tests
- smoke – minimal UI or integration checks
- integration – slower end-to-end tests (opt in)

Examples:

```powershell
# Run tests in a specific class
dotnet test --filter "ClassName=WileyWidget.Tests.WidgetTests"
```

## Test Organization


## UI automation options

If/when we need Windows UI automation for the WPF app from Python:
- pywinauto – simple Windows UI automation, good for WPF
- WinAppDriver + Appium (Python client) – more structured, CI-friendly

Keep UI tests small and tagged as smoke to avoid slowing the pipeline.

## CI integration

- The approved CI workflow runs pytest via VS Code tasks and Trunk integration.
- See docs/cicd-quick-reference.md for the pipeline sequence and how to monitor.

## Removed legacy

- .NET test projects (WileyWidget.Tests, WileyWidget.UiTests)
- xUnit, coverlet, and FlaUI dependencies

If you need to reference an old .NET test for logic, see the archived files under WileyWidget.Tests/ and WileyWidget.UiTests/ folders; their csproj files are disabled and excluded from the solution.

### Unit Test Structure

```
WileyWidget.Tests/
├── WidgetTests.cs          # Widget entity tests
├── DatabaseIntegrationTests.cs  # Database operations
├── SettingsServiceTests.cs     # Configuration services
├── MainWindowTests.cs         # Main window logic
└── WileyWidget.Tests.csproj
```

### UI Test Structure

```
WileyWidget.UiTests/
├── MainWindowUITests.cs     # Main window UI interactions
├── AboutWindowUITests.cs    # About dialog tests
└── WileyWidget.UiTests.csproj
```

## Writing Tests

### Unit Test Example

```csharp
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests
{
    public class WidgetTests
    {
        [Fact]
        public void Widget_Creation_SetsProperties()
        {
            // Arrange
            var widget = new Widget
            {
                Name = "Test Widget",
                Description = "A test widget"
            };

            // Act & Assert
            Assert.Equal("Test Widget", widget.Name);
            Assert.Equal("A test widget", widget.Description);
        }

        [Theory]
        [InlineData("Valid Name", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Widget_Name_Validation(string name, bool expected)
        {
            // Arrange
            var widget = new Widget { Name = name };

            // Act
            var isValid = !string.IsNullOrEmpty(widget.Name);

            // Assert
            Assert.Equal(expected, isValid);
        }
    }
}
```

### UI Test Example (FlaUI)

```csharp
using Xunit;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace WileyWidget.UiTests
{
    public class MainWindowUITests : IDisposable
    {
        private readonly Application _app;
        private readonly Window _mainWindow;

        public MainWindowUITests()
        {
            // Start the application
            _app = Application.Launch("WileyWidget.exe");

            // Get the main window
            var automation = new UIA3Automation();
            _mainWindow = _app.GetMainWindow(automation);
        }

        [Fact]
        public void MainWindow_Loads_Correctly()
        {
            // Assert
            Assert.NotNull(_mainWindow);
            Assert.True(_mainWindow.IsAvailable);
            Assert.Contains("Wiley Widget", _mainWindow.Title);
        }

        [Fact]
        public void MainWindow_HasExpectedControls()
        {
            // Arrange
            var automation = new UIA3Automation();

            // Act
            var buttons = _mainWindow.FindAllDescendants(cf => cf.ByControlType(automation.ControlType.Button));
            var textBoxes = _mainWindow.FindAllDescendants(cf => cf.ByControlType(automation.ControlType.Edit));

            // Assert
            Assert.NotEmpty(buttons);
            Assert.NotEmpty(textBoxes);
        }

        public void Dispose()
        {
            _app?.Close();
            _app?.Dispose();
        }
    }
}
```

## Best Practices

### Test Naming Conventions

- Use descriptive names that explain what the test verifies
- Follow the pattern: `MethodName_Condition_ExpectedResult`
- Example: `CalculateTotal_ValidItems_ReturnsCorrectSum`

### Test Organization

- Group related tests in the same class
- Use `[Trait]` attributes for categorization
- Keep test methods focused on a single behavior

### Database Testing

- Use in-memory database for isolated testing
- Reset database state between tests
- Avoid dependencies on external databases

### UI Testing

- Use appropriate waits for UI elements to load
- Clean up application instances after tests
- Test both positive and negative scenarios
- Consider using Page Object pattern for complex UIs

## Troubleshooting

### Common Issues

#### Tests Not Discovered

- Ensure `xunit.runner.visualstudio` package is installed
- Check that test methods are public and have `[Fact]` or `[Theory]` attributes
- Verify target framework compatibility

#### FlaUI Tests Failing

- Ensure application can be launched from test directory
- Check that UI elements have proper automation IDs
- Verify Windows UI Automation is enabled

#### Code Coverage Not Working

- Install `coverlet.collector` package
- Use `--collect:"XPlat Code Coverage"` parameter
- Check that test assemblies are being instrumented

### Debug Tips

- Use `Debugger.Launch()` in test methods for debugging
- Add logging to understand test execution flow
- Use conditional breakpoints for specific test scenarios

## Integration with CI/CD

The test environment is designed to work seamlessly with CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
- name: Run Tests
  run: dotnet test WileyWidget.sln --collect:"XPlat Code Coverage" --results-directory ./test-results

- name: Upload Coverage
  uses: codecov/codecov-action@v3
  with:
    file: ./test-results/*/coverage.cobertura.xml
```

## Maintenance

### Updating Packages

```powershell
# Update xUnit packages
dotnet add package xunit --version 2.9.2
dotnet add package xunit.runner.visualstudio --version 2.8.2

# Update FlaUI packages
dotnet add package FlaUI.Core --version 5.0.0
dotnet add package FlaUI.UIA3 --version 5.0.0
```

### Adding New Test Projects

1. Create new test project using template
2. Add necessary package references
3. Configure project settings (RID, warnings, etc.)
4. Add project reference to main application
5. Update solution file

## Resources

- [xUnit Documentation](https://xunit.net/)
- [FlaUI Documentation](https://github.com/FlaUI/FlaUI)
- [Microsoft Testing Documentation](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [Code Coverage with Coverlet](https://github.com/coverlet-coverage/coverlet)

---

**Last Updated:** August 28, 2025
**Test Environment Status:** ✅ Configured and Ready
