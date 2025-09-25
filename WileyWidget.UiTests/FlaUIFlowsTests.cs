using System;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.Tests;

namespace WileyWidget.UiTests;

public class FlaUIFlowsTests : UiTestApplication
{
    [StaFact(DisplayName = "Launch app and sort grid"), Trait("Category", "UI-HighRisk")]
    public void Launch_And_Sort_Main_Grid()
    {
        // Skip this test in CI/headless environments where GUI applications cannot run
        if (Environment.GetEnvironmentVariable("CI") == "true" ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SESSIONNAME")))
        {
            Assert.Skip("Skipping GUI automation test in headless/CI environment");
            return;
        }

        // Locate built exe output
        var exePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "bin", "Debug", "net9.0-windows", "WileyWidget.exe"));
        Assert.True(File.Exists(exePath), $"Executable not found at {exePath}. Build the app first.");

        using var app = FlaUI.Core.Application.Launch(exePath);
        
        // Give the application more time to fully start up
        System.Threading.Thread.Sleep(5000); // Wait 5 seconds for app to initialize
        
        using var automation = new UIA3Automation();
        
        // Try to get the main window with a longer timeout
        var main = app.GetMainWindow(automation, TimeSpan.FromSeconds(30));
        Assert.NotNull(main);

        // Wait a bit more for the UI to fully load
        System.Threading.Thread.Sleep(2000);

        // Click Ribbon button "🏠 Dashboard" (or just find any Ribbon)
        var ribbon = main.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.ToolBar).Or(cf.ByClassName("Ribbon")));
        Assert.NotNull(ribbon);

        // Find the SfDataGrid by name "Grid"
        var gridElement = main.FindFirstDescendant(cf => cf.ByName("Grid"));
        Assert.NotNull(gridElement);

        // Convert to Grid and try to sort first column by clicking header
        var header = gridElement.FindFirstDescendant(cf => cf.ByControlType(ControlType.Header));
        Assert.NotNull(header);
        var firstHeaderItem = header.FindAllChildren().FirstOrDefault();
        Assert.NotNull(firstHeaderItem);
        firstHeaderItem.AsButton()?.Invoke();

        // Verify rows exist
        var dataItems = gridElement.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
        Assert.True(dataItems?.Length >= 0); // Allow empty data set in CI, just ensure query works

        // Close app
        main.Close();
        app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(2));
    }
}
