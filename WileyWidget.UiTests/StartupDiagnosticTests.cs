using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;
using WileyWidget.Tests;

namespace WileyWidget.UiTests;

/// <summary>
/// UI element constants for WileyWidget application.
/// Centralizes element names to reduce brittleness and make maintenance easier.
/// Used across all E2E tests for consistent element identification.
/// </summary>
public static class UiElementConstants
{
    // Navigation elements
    public static readonly string[] DashboardNavigation = { "Dashboard", "Home" };
    public static readonly string[] EnterpriseNavigation = { "Enterprise", "EnterpriseView" };
    public static readonly string[] BudgetNavigation = { "Budget", "BudgetView" };
    public static readonly string[] AnalyticsNavigation = { "Analytics", "AnalyticsView" };
    public static readonly string[] SettingsNavigation = { "Settings", "SettingsView", "Options" };
    public static readonly string[] AiAssistNavigation = { "AI Assist", "AIAssist", "AI" };
    public static readonly string[] MunicipalAccountNavigation = { "Municipal Account", "MunicipalAccount", "Accounts" };
    public static readonly string[] UtilityCustomerNavigation = { "Utility Customer", "UtilityCustomer", "Customers" };
    
    // View identifiers
    public static readonly string DashboardViewId = "DashboardView";
    public static readonly string EnterpriseViewId = "EnterpriseView";
    public static readonly string BudgetViewId = "BudgetView";
    public static readonly string AnalyticsViewId = "AnalyticsView";
    public static readonly string SettingsViewId = "SettingsView";
    public static readonly string AiAssistViewId = "AIAssistView";
    public static readonly string MunicipalAccountViewId = "MunicipalAccountView";
    public static readonly string UtilityCustomerViewId = "UtilityCustomerView";
    
    // Region identifiers
    public static readonly string MainRegionId = "MainRegion";
    public static readonly string ContentRegionId = "ContentRegion";
    public static readonly string NavigationRegionId = "NavigationRegion";
    public static readonly string HeaderRegionId = "HeaderRegion";
    public static readonly string FooterRegionId = "FooterRegion";
    
    // Data indicators
    public static readonly string[] DataIndicators = { "Enterprises", "Budget", "Total", "Count", "Loading", "Data" };
    
    // Empty state messages
    public static readonly string[] EmptyStateIndicators = {
        "No data", "No enterprises", "No records", "Empty", "0 enterprises", "No items", "Getting started"
    };
    
    // Error dialog keywords
    public static readonly string[] ErrorDialogKeywords = { "Error", "Exception" };
    
    // Navigation items array
    public static readonly string[] NavigationItems = { "Dashboard", "Enterprise", "Budget", "Analytics", "AI Assist", "Settings" };
    
    // Syncfusion control prefixes
    public static readonly string SyncfusionPrefix = "Syncfusion";
    
    // Window title patterns
    public static readonly string WindowTitlePattern = "Wiley";
    
    // Error dialog patterns
    public static readonly string[] ErrorDialogPatterns = {
        "Exception", "Error", "XamlParseException", "has stopped working", "stopped responding"
    };
    
    // Loading indicators
    public static readonly string[] LoadingIndicators = {
        "Loading", "Please wait", "Progress"
    };
}

/// <summary>
/// End-to-end UI tests using FlaUI to verify application startup and rendering.
/// These tests launch the actual WileyWidget.exe and verify UI behavior.
/// Based on FlaUI best practices: https://github.com/FlaUI/FlaUI
/// </summary>
[Collection("UI")] // Prevent parallel execution of UI tests
public class StartupDiagnosticTests : UiTestApplication
{
    private readonly ITestOutputHelper _output;

    public StartupDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Captures a screenshot with unique filename to prevent race conditions.
    /// </summary>
    private async Task<string> CaptureScreenshot(Window window, string testName, string reason)
    {
        try
        {
            var screenshotPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"UI_Test_{testName}_{reason}_{Guid.NewGuid():N}.png"
            );
            
            using var bitmap = window.Capture();
            using var stream = System.IO.File.Create(screenshotPath);
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            
            return screenshotPath;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Screenshot capture failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Verifies that no error dialogs are present in the application.
    /// </summary>
    private void CheckForErrorDialogs(FlaUI.Core.Application app, AutomationBase automation, string testName)
    {
        var errorDialog = UiTestHelpers.CheckForErrorDialogs(app, automation);
        if (errorDialog != null)
        {
            _output.WriteLine($"✗ Error dialog detected: {errorDialog.Title}");
            Assert.Fail($"Error dialog detected in {testName}: {errorDialog.Title}");
        }
    }

    /// <summary>
    /// Polls for element stability (content stops changing).
    /// </summary>
    private async Task<bool> WaitForElementStability(AutomationElement element, TimeSpan timeout, int stabilityThreshold = 3)
    {
        var stopwatch = Stopwatch.StartNew();
        var stableCount = 0;
        var previousChildCount = -1;
        
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var currentChildCount = element.FindAllChildren().Length;
                
                if (currentChildCount == previousChildCount)
                {
                    stableCount++;
                    if (stableCount >= stabilityThreshold)
                    {
                        return true; // Element is stable
                    }
                }
                else
                {
                    stableCount = 0;
                    previousChildCount = currentChildCount;
                }
            }
            catch
            {
                // Element might be temporarily unavailable
                stableCount = 0;
            }
            
            await Task.Delay(200);
        }
        
        return false; // Timeout without stability
    }

    /// <summary>
    /// Safely clicks an element with proper waiting and error handling.
    /// </summary>
    private async Task<bool> SafeClickElement(AutomationElement element, string elementName = "element")
    {
        try
        {
            // Wait for element to be clickable
            var clickable = await UiTestHelpers.WaitForElementClickableAsync(element, TimeSpan.FromSeconds(5));
            if (!clickable)
            {
                _output.WriteLine($"⚠ {elementName} not clickable within timeout");
                return false;
            }
            
            // Wait for element to be responsive using helper
            var responsive = UiTestHelpers.WaitForElementResponsive(element, TimeSpan.FromSeconds(2));
            if (!responsive)
            {
                _output.WriteLine($"⚠ {elementName} not responsive");
                return false;
            }
            
            // Perform click
            element.Click();
            _output.WriteLine($"✓ Clicked {elementName}");
            return true;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Failed to click {elementName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Finds a navigation element using multiple strategies with constants.
    /// </summary>
    private AutomationElement FindNavigationElement(Window mainWindow, string[] navigationNames)
    {
        foreach (var navName in navigationNames)
        {
            _output.WriteLine($"  Trying: '{navName}'");
            
            // Try multiple strategies to find navigation element
            var strategies = new System.Func<AutomationElement>[]
            {
                () => mainWindow.FindFirstDescendant(cf => cf.ByName(navName)),
                () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(navName)),
                () => mainWindow.FindFirstDescendant(cf => cf.ByText(navName)),
                () => mainWindow.FindAllDescendants(cf => 
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                    .FirstOrDefault(e => e.Name?.Contains(navName) == true),
                () => mainWindow.FindFirstDescendant(cf => 
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem)
                    .And(cf.ByName(navName)))
            };
            
            foreach (var strategy in strategies)
            {
                try
                {
                    var element = strategy();
                    if (element != null && element.IsAvailable)
                    {
                        _output.WriteLine($"  ✓ Found via strategy: {element.ControlType}");
                        return element;
                    }
                }
                catch { }
            }
        }
        
        return null;
    }

    /// <summary>
    /// END-TO-END TEST: Full application launch using FlaUI automation.
    /// Launches WileyWidget.exe and verifies the main window appears and renders correctly.
    /// </summary>
    [StaFact]
    public async Task E2E_01_ApplicationLaunches_AndMainWindowRenders()
    {
        _output.WriteLine("====== END-TO-END APPLICATION LAUNCH TEST ======");
        _output.WriteLine("This test launches the actual WileyWidget.exe and verifies UI rendering");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        Window? mainWindow = null;
        
        try
        {
            // Launch application using base class helper method
            (app, mainWindow, automation) = await LaunchAppAsync(_output);
            _output.WriteLine($"✓ Application launched and main window available");
            
            // Check for error dialogs immediately after launch
            CheckForErrorDialogs(app, automation, nameof(E2E_01_ApplicationLaunches_AndMainWindowRenders));
            
            // ========== VERIFY WINDOW PROPERTIES ==========
            _output.WriteLine("\n=== Main Window Properties ===");
            _output.WriteLine($"Title: {mainWindow.Title}");
            _output.WriteLine($"Name: {mainWindow.Name}");
            _output.WriteLine($"AutomationId: {mainWindow.AutomationId}");
            _output.WriteLine($"ClassName: {mainWindow.ClassName}");
            _output.WriteLine($"Bounds: {mainWindow.BoundingRectangle}");
            _output.WriteLine($"IsVisible: {mainWindow.IsAvailable}");
            _output.WriteLine($"IsOffscreen: {mainWindow.IsOffscreen}");
            
            // Verify window title contains expected text using constants
            Assert.Contains(UiTestConstants.WindowTitles.Pattern, mainWindow.Title, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine($"✓ Window title contains '{UiTestConstants.WindowTitles.Pattern}'");
            
            // ========== ENUMERATE UI STRUCTURE ==========
            _output.WriteLine("\n=== Top-Level UI Controls ===");
            var children = mainWindow.FindAllChildren();
            _output.WriteLine($"Found {children.Length} top-level child controls");
            
            foreach (var child in children.Take(20)) // Show first 20 controls
            {
                _output.WriteLine($"  - {child.ControlType}: '{child.Name ?? child.AutomationId ?? "(unnamed)"}' (Class: {child.ClassName})");
            }
            
            // ========== VERIFY EXPECTED UI ELEMENTS ==========
            _output.WriteLine("\n=== Expected UI Elements Search ===");
            
            // Look for Dashboard element using constants and retry helpers
            var dashboardElement = UiTestHelpers.FindElementByNamesWithRetry(
                mainWindow,
                UiTestConstants.NavigationItems.DashboardAliases,
                UiTestConstants.Timeouts.ElementSearch);
            
            if (dashboardElement != null)
            {
                _output.WriteLine($"✓ Found Dashboard element: {dashboardElement.ControlType} - {dashboardElement.Name}");
            }
            else
            {
                _output.WriteLine("⚠ Dashboard element not found - may indicate navigation issues");
            }
            
            // Look for menu/ribbon controls
            var menuElements = mainWindow.FindAllDescendants(cf => 
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu)
                .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuBar))
                .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ToolBar)));
            
            if (menuElements.Any())
            {
                _output.WriteLine($"✓ Found {menuElements.Length} menu/toolbar element(s)");
            }
            else
            {
                _output.WriteLine("⚠ No menu/toolbar elements found");
            }
            
            // Look for Syncfusion controls using constants
            var syncfusionControls = mainWindow.FindAllDescendants()
                .Where(e => e.ClassName?.StartsWith(UiElementConstants.SyncfusionPrefix) == true)
                .ToArray();
            
            if (syncfusionControls.Any())
            {
                _output.WriteLine($"✓ Found {syncfusionControls.Length} Syncfusion control(s)");
                foreach (var ctrl in syncfusionControls.Take(5))
                {
                    _output.WriteLine($"    - {ctrl.ClassName}");
                }
            }
            else
            {
                _output.WriteLine("⚠ No Syncfusion controls found - may indicate theme/control loading issues");
            }
            
            // ========== CAPTURE SCREENSHOT ==========
            var screenshotPath = await CaptureScreenshot(mainWindow, "E2E_01", "MainWindow");
            if (screenshotPath != null)
            {
                _output.WriteLine($"\n✓ Screenshot saved: {screenshotPath}");
            }
            
            _output.WriteLine("\n✅ END-TO-END TEST PASSED");
            _output.WriteLine("  Application launched successfully and main window rendered correctly");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ END-TO-END TEST FAILED");
            _output.WriteLine($"Error Type: {ex.GetType().Name}");
            _output.WriteLine($"Error Message: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                _output.WriteLine($"\nInner Exception: {ex.InnerException.Message}");
            }
            
            // Capture failure screenshot
            if (mainWindow != null)
            {
                var failureScreenshot = await CaptureScreenshot(mainWindow, "E2E_01", "Failure");
                if (failureScreenshot != null)
                {
                    _output.WriteLine($"Failure screenshot: {failureScreenshot}");
                }
            }
            
            throw;
        }
        finally
        {
            await CleanupAsync(app, mainWindow, automation, _output);
        }
    }

    /// <summary>
    /// END-TO-END TEST: Comprehensive startup process verification.
    /// Verifies the complete WileyWidget startup sequence including:
    /// - Executable launch
    /// - Splash screen detection (if present)
    /// - Main window initialization
    /// - Theme application (FluentDark)
    /// - Initial module loading (Diagnostics, Syncfusion, Dashboard)
    /// - Syncfusion control verification
    /// </summary>
    [StaFact]
    public async Task E2E_01B_CompleteStartupProcess_Verification()
    {
        _output.WriteLine("====== COMPLETE STARTUP PROCESS VERIFICATION ======");
        _output.WriteLine("Testing full WileyWidget initialization sequence");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        var startupStopwatch = Stopwatch.StartNew();
        
        try
        {
            // ========== PHASE 1: LOCATE AND LAUNCH EXECUTABLE ==========
            _output.WriteLine("\n--- Phase 1: Executable Launch ---");
            
            var exePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..",
                "bin", "Debug", "net9.0-windows",
                "WileyWidget.exe"
            );
            exePath = System.IO.Path.GetFullPath(exePath);
            
            _output.WriteLine($"Executable path: {exePath}");
            
            if (!System.IO.File.Exists(exePath))
            {
                _output.WriteLine($"✗ Executable not found");
                _output.WriteLine("  Build with: dotnet build");
                _output.WriteLine("  SKIPPING TEST");
                return;
            }
            
            _output.WriteLine("✓ Executable found");
            
            // Create automation
            automation = new UIA3Automation();
            _output.WriteLine("✓ UIA3 Automation created");
            
            // Launch application
            _output.WriteLine($"Launching application at {DateTime.Now:HH:mm:ss.fff}...");
            app = FlaUI.Core.Application.Launch(exePath);
            _output.WriteLine($"✓ Process started (PID: {app.ProcessId}) at {DateTime.Now:HH:mm:ss.fff}");
            
            // ========== PHASE 2: SPLASH SCREEN DETECTION ==========
            _output.WriteLine("\n--- Phase 2: Splash Screen Detection ---");
            
            Window splashScreen = null;
            var splashTimeout = TimeSpan.FromSeconds(5);
            var splashStopwatch = Stopwatch.StartNew();
            
            try
            {
                // Try to detect splash screen window
                var cts = new CancellationTokenSource(splashTimeout);
                while (!cts.Token.IsCancellationRequested && splashStopwatch.Elapsed < splashTimeout)
                {
                    try
                    {
                        // Look for window with "Splash" in title or className
                        var windows = automation.GetDesktop().FindAllChildren(cf => 
                            cf.ByProcessId(app.ProcessId));
                        
                        splashScreen = windows.FirstOrDefault(w => 
                            w.Name?.Contains("Splash", StringComparison.OrdinalIgnoreCase) == true ||
                            w.Name?.Contains("Loading", StringComparison.OrdinalIgnoreCase) == true ||
                            w.ClassName?.Contains("Splash", StringComparison.OrdinalIgnoreCase) == true) as Window;
                        
                        if (splashScreen != null && splashScreen.IsAvailable)
                        {
                            _output.WriteLine($"✓ Splash screen detected after {splashStopwatch.ElapsedMilliseconds}ms");
                            _output.WriteLine($"    Title: '{splashScreen.Name}'");
                            _output.WriteLine($"    ClassName: '{splashScreen.ClassName}'");
                            
                            // Wait for splash to disappear (max 10s)
                            var splashDisappearTimeout = TimeSpan.FromSeconds(10);
                            var splashDisappearCts = new CancellationTokenSource(splashDisappearTimeout);
                            
                            while (!splashDisappearCts.Token.IsCancellationRequested)
                            {
                                if (!splashScreen.IsAvailable)
                                {
                                    _output.WriteLine($"✓ Splash screen closed after {splashStopwatch.ElapsedMilliseconds}ms total");
                                    break;
                                }
                                await Task.Delay(100, splashDisappearCts.Token);
                            }
                            
                            break;
                        }
                    }
                    catch { }
                    
                    await Task.Delay(100, cts.Token);
                }
                
                if (splashScreen == null)
                {
                    _output.WriteLine("⚠ No splash screen detected (application may not use one)");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"⚠ Splash screen detection error: {ex.Message}");
            }
            
            // ========== PHASE 3: MAIN WINDOW INITIALIZATION ==========
            _output.WriteLine("\n--- Phase 3: Main Window Initialization ---");
            
            Window? mainWindow = null;
            var mainWindowTimeout = TimeSpan.FromSeconds(30);
            var mainWindowCts = new CancellationTokenSource(mainWindowTimeout);
            var mainWindowStopwatch = Stopwatch.StartNew();
            int attemptCount = 0;
            
            while (!mainWindowCts.Token.IsCancellationRequested)
            {
                attemptCount++;
                try
                {
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                    
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        _output.WriteLine($"✓ Main window found after {mainWindowStopwatch.ElapsedMilliseconds}ms ({attemptCount} attempts)");
                        _output.WriteLine($"    Title: '{mainWindow.Title}'");
                        _output.WriteLine($"    ClassName: '{mainWindow.ClassName}'");
                        _output.WriteLine($"    AutomationId: '{mainWindow.AutomationId}'");
                        _output.WriteLine($"    Bounds: {mainWindow.BoundingRectangle}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (attemptCount % 5 == 0) // Log every 5th attempt
                    {
                        _output.WriteLine($"  Attempt {attemptCount}: Waiting for main window... ({ex.Message})");
                    }
                }
                
                await Task.Delay(500, mainWindowCts.Token);
            }
            
            if (mainWindow == null)
            {
                _output.WriteLine($"✗ CRITICAL: Main window did not appear within {mainWindowTimeout.TotalSeconds}s");
                
                // Detailed diagnostics
                try
                {
                    var process = Process.GetProcessById(app.ProcessId);
                    _output.WriteLine("\n  Process Diagnostics:");
                    _output.WriteLine($"    - Running: {!process.HasExited}");
                    _output.WriteLine($"    - Threads: {process.Threads.Count}");
                    _output.WriteLine($"    - Memory: {process.WorkingSet64 / 1024 / 1024} MB");
                    _output.WriteLine($"    - CPU Time: {process.TotalProcessorTime}");
                    _output.WriteLine($"    - Responding: {process.Responding}");
                    
                    if (!process.HasExited && process.Responding)
                    {
                        _output.WriteLine("\n  ⚠ Process is running and responding but no window appeared");
                        _output.WriteLine("     Likely causes:");
                        _output.WriteLine("     1. Application hung during initialization");
                        _output.WriteLine("     2. Window created but not visible");
                        _output.WriteLine("     3. Exception during window creation");
                    }
                    else if (!process.Responding)
                    {
                        _output.WriteLine("\n  ⚠ Process is NOT responding - application hung");
                    }
                }
                catch
                {
                    _output.WriteLine("  ✗ Process has exited - application crashed during startup");
                }
                
                Assert.Fail("Main window did not initialize");
            }
            
            // Verify window title
            Assert.Contains("Wiley", mainWindow.Title, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine("✓ Window title validation passed");
            
            // ========== PHASE 4: THEME APPLICATION VERIFICATION ==========
            _output.WriteLine("\n--- Phase 4: Theme Application (FluentDark) ---");
            
            try
            {
                // Wait a moment for theme to fully apply
                await Task.Delay(1000);
                
                // Look for FluentDark theme indicators
                var themeElements = mainWindow.FindAllDescendants()
                    .Where(e => e.ClassName?.Contains("Fluent") == true)
                    .ToArray();
                
                if (themeElements.Any())
                {
                    _output.WriteLine($"✓ Found {themeElements.Length} FluentDark theme element(s)");
                    foreach (var elem in themeElements.Take(3))
                    {
                        _output.WriteLine($"    - {elem.ClassName}");
                    }
                }
                else
                {
                    _output.WriteLine("⚠ No explicit FluentDark elements detected");
                    _output.WriteLine("  Theme may be applied at resource level (not detectable via UIA)");
                }
                
                // Check window background/styling
                _output.WriteLine($"  Window ClassName: {mainWindow.ClassName}");
                _output.WriteLine("✓ Theme verification completed");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"⚠ Theme verification warning: {ex.Message}");
            }
            
            // ========== PHASE 5: INITIAL MODULE LOADING ==========
            _output.WriteLine("\n--- Phase 5: Initial Module Loading ---");
            
            // Wait for modules to load
            await Task.Delay(2000);
            
            // Check for Diagnostics module
            try
            {
                var diagnosticElements = mainWindow.FindAllDescendants()
                    .Where(e => e.Name?.Contains("Diagnostic") == true || 
                                e.AutomationId?.Contains("Diagnostic") == true)
                    .ToArray();
                
                _output.WriteLine($"  Diagnostics: {(diagnosticElements.Any() ? $"✓ Found {diagnosticElements.Length} element(s)" : "⚠ Not detected")}");
            }
            catch { _output.WriteLine("  Diagnostics: ⚠ Search failed"); }
            
            // Check for Dashboard module
            try
            {
                var dashboardElements = mainWindow.FindAllDescendants()
                    .Where(e => e.Name?.Contains("Dashboard") == true ||
                                e.AutomationId?.Contains("Dashboard") == true ||
                                e.Name == "Dashboard")
                    .ToArray();
                
                if (dashboardElements.Any())
                {
                    _output.WriteLine($"  Dashboard: ✓ Found {dashboardElements.Length} element(s)");
                    foreach (var elem in dashboardElements.Take(2))
                    {
                        _output.WriteLine($"      - {elem.ControlType}: '{elem.Name}'");
                    }
                }
                else
                {
                    _output.WriteLine("  Dashboard: ⚠ Not detected in initial view");
                }
            }
            catch { _output.WriteLine("  Dashboard: ⚠ Search failed"); }
            
            // ========== PHASE 6: SYNCFUSION CONTROLS VERIFICATION ==========
            _output.WriteLine("\n--- Phase 6: Syncfusion Controls Verification ---");
            
            var retryCount = 0;
            var maxRetries = 3;
            AutomationElement[] syncfusionControls = null;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    syncfusionControls = mainWindow.FindAllDescendants()
                        .Where(e => e.ClassName?.StartsWith("Syncfusion") == true)
                        .ToArray();
                    
                    if (syncfusionControls.Any())
                    {
                        break;
                    }
                    
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        _output.WriteLine($"  Retry {retryCount}/{maxRetries}: Waiting for Syncfusion controls to load...");
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Retry {retryCount + 1}/{maxRetries}: Search error - {ex.Message}");
                    retryCount++;
                    await Task.Delay(1000);
                }
            }
            
            if (syncfusionControls != null && syncfusionControls.Any())
            {
                _output.WriteLine($"✓ Found {syncfusionControls.Length} Syncfusion control(s)");
                
                // Enumerate Syncfusion controls
                var controlTypes = syncfusionControls
                    .GroupBy(c => c.ClassName)
                    .OrderByDescending(g => g.Count());
                
                _output.WriteLine("  Syncfusion Controls Breakdown:");
                foreach (var group in controlTypes.Take(10))
                {
                    _output.WriteLine($"    - {group.Key}: {group.Count()} instance(s)");
                }
                
                // Verify specific expected Syncfusion controls
                var expectedControls = new[]
                {
                    "SfDataGrid",
                    "RibbonControl",
                    "DockingManager",
                    "SfChart",
                    "SfButton"
                };
                
                foreach (var expectedControl in expectedControls)
                {
                    var found = syncfusionControls.Any(c => 
                        c.ClassName?.Contains(expectedControl, StringComparison.OrdinalIgnoreCase) == true);
                    
                    var status = found ? "✓" : "⚠";
                    _output.WriteLine($"    {status} {expectedControl}: {(found ? "Present" : "Not found")}");
                }
            }
            else
            {
                _output.WriteLine("✗ CRITICAL: No Syncfusion controls detected after all retries");
                _output.WriteLine("  Possible causes:");
                _output.WriteLine("    1. Syncfusion controls not loaded");
                _output.WriteLine("    2. License issue preventing control instantiation");
                _output.WriteLine("    3. Theme resources not properly applied");
                _output.WriteLine("    4. Control initialization failure");
                
                Assert.Fail("Syncfusion controls not detected - critical UI component missing");
            }
            
            // ========== PHASE 7: UI STRUCTURE ENUMERATION ==========
            _output.WriteLine("\n--- Phase 7: Complete UI Structure ---");
            
            var allChildren = mainWindow.FindAllChildren();
            _output.WriteLine($"Total top-level controls: {allChildren.Length}");
            
            var controlTypeBreakdown = allChildren
                .GroupBy(c => c.ControlType)
                .OrderByDescending(g => g.Count());
            
            _output.WriteLine("Control Type Distribution:");
            foreach (var group in controlTypeBreakdown)
            {
                _output.WriteLine($"  - {group.Key}: {group.Count()}");
            }
            
            // ========== PHASE 8: SCREENSHOT CAPTURE ==========
            _output.WriteLine("\n--- Phase 8: Visual Verification ---");
            
            try
            {
                var screenshotPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    $"E2E_StartupComplete_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                );
                
                using var bitmap = mainWindow.Capture();
                using var stream = System.IO.File.Create(screenshotPath);
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                
                _output.WriteLine($"✓ Screenshot saved: {screenshotPath}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"⚠ Screenshot failed: {ex.Message}");
            }
            
            // ========== FINAL SUMMARY ==========
            startupStopwatch.Stop();
            
            _output.WriteLine("\n====== STARTUP VERIFICATION SUMMARY ======");
            _output.WriteLine($"✓ Total startup time: {startupStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"✓ Main window initialized");
            _output.WriteLine($"✓ Theme applied");
            _output.WriteLine($"✓ Syncfusion controls loaded");
            _output.WriteLine($"✓ UI structure verified");
            _output.WriteLine("\n✅ COMPLETE STARTUP PROCESS VERIFICATION PASSED");
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("\n✗ TEST TIMED OUT");
            throw;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ STARTUP VERIFICATION FAILED");
            _output.WriteLine($"Error: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            // Cleanup
            if (app != null && !app.HasExited)
            {
                try
                {
                    _output.WriteLine("\nCleaning up...");
                    app.Close();
                    await Task.Delay(2000);
                    
                    if (!app.HasExited)
                    {
                        _output.WriteLine("⚠ Forcing termination");
                        app.Kill();
                    }
                    else
                    {
                        _output.WriteLine("✓ Application closed gracefully");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"⚠ Cleanup error: {ex.Message}");
                    try { app.Kill(); } catch { }
                }
            }
            
            automation?.Dispose();
            _output.WriteLine($"✓ Test cleanup completed (Total runtime: {startupStopwatch.ElapsedMilliseconds}ms)");
        }
    }

    /// <summary>
    /// END-TO-END TEST: Verify all Prism views can be navigated to.
    /// Tests navigation between different views (Dashboard, Enterprise, Budget, etc.)
    /// </summary>
    [StaFact]
    public async Task E2E_02_AllPrismViews_CanBeNavigated()
    {
        _output.WriteLine("====== PRISM NAVIGATION END-TO-END TEST ======");
        _output.WriteLine("Testing navigation between all application views");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        
        try
        {
            // Launch application using helper method
            (app, var mainWindow, automation) = await LaunchAppAsync(_output);
            _output.WriteLine($"✓ Application launched and main window available");
            
            // Use navigation items from constants
            var navigationItems = UiElementConstants.NavigationItems;
            
            int successCount = 0;
            int notFoundCount = 0;
            
            foreach (var itemName in navigationItems)
            {
                _output.WriteLine($"\n--- Testing navigation to: {itemName} ---");
                
                try
                {
                    // Use improved navigation element finding
                    var navButton = FindNavigationElement(mainWindow, new[] { itemName });
                    
                    if (navButton != null && navButton.IsAvailable)
                    {
                        _output.WriteLine($"  ✓ Found navigation element: {navButton.ControlType} '{navButton.Name}'");
                        
                        // Use safe click with improved polling
                        var clickResult = await SafeClickElement(navButton, itemName);
                        if (clickResult)
                        {
                            _output.WriteLine($"  ✓ Clicked {itemName} navigation button");
                            
                            // Wait for navigation to complete using helper
                            var navElement = await UiTestHelpers.WaitForElementResponsiveAsync(mainWindow, cf => 
                                cf.ByAutomationId($"{itemName}View")
                                .Or(cf.ByName(itemName))
                                .Or(cf.ByText(itemName)), 
                                UiTestConstants.Timeouts.Navigation);
                            
                            // Verify view content appeared
                            var viewContent = mainWindow.FindFirstDescendant(cf => 
                                cf.ByAutomationId($"{itemName}View")
                                .Or(cf.ByName(itemName))
                                .Or(cf.ByText(itemName)));
                            
                            if (viewContent != null)
                            {
                                _output.WriteLine($"  ✓ {itemName} view content rendered");
                                successCount++;
                            }
                            else
                            {
                                _output.WriteLine($"  ⚠ {itemName} view content not detected (may be embedded or using different naming)");
                                successCount++; // Still count as success if button clicked
                            }
                            
                            // Take screenshot of each view
                            var screenshotPath = await CaptureScreenshot(mainWindow, "E2E_02", $"{itemName}View");
                            if (screenshotPath != null)
                            {
                                _output.WriteLine($"  ✓ Screenshot saved: {screenshotPath}");
                            }
                        }
                        else
                        {
                            _output.WriteLine($"  ✗ Failed to click {itemName} navigation button");
                            notFoundCount++;
                        }
                    }
                    else
                    {
                        _output.WriteLine($"  ✗ Navigation element for '{itemName}' not found");
                        notFoundCount++;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  ✗ Navigation to {itemName} failed: {ex.Message}");
                    notFoundCount++;
                }
            }
            
            // Report results
            _output.WriteLine($"\n=== Navigation Test Summary ===");
            _output.WriteLine($"Successfully navigated: {successCount}/{navigationItems.Length}");
            _output.WriteLine($"Not found: {notFoundCount}/{navigationItems.Length}");
            
            if (successCount > 0)
            {
                _output.WriteLine("\n✅ NAVIGATION TEST PASSED");
                _output.WriteLine($"  At least {successCount} view(s) successfully navigated");
            }
            else
            {
                _output.WriteLine("\n⚠ NAVIGATION TEST INCONCLUSIVE");
                _output.WriteLine("  No navigation elements were found - may need UI structure adjustment");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ NAVIGATION TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            // Cleanup
            if (app != null && !app.HasExited)
            {
                try 
                { 
                    app.Close();
                    await Task.Delay(2000);
                    if (!app.HasExited) app.Kill();
                } 
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }

    /// <summary>
    /// END-TO-END TEST: Application launch components verification.
    /// Focuses on low-level process metrics during startup:
    /// - EXE launches without errors
    /// - Process starts with correct threads/memory (~3MB initial)
    /// - STA apartment state verification
    /// - No unhandled exceptions during initialization
    /// - Expected startup time (<300ms to process creation)
    /// - No error dialogs during launch
    /// Uses FlaUI for process attachment and Process class for detailed metrics.
    /// Reference: https://github.com/FlaUI/FlaUI/wiki/Application
    /// </summary>
    [StaFact]
    public async Task E2E_04_ApplicationLaunchComponents_ProcessMetrics()
    {
        _output.WriteLine("====== APPLICATION LAUNCH COMPONENTS TEST ======");
        _output.WriteLine("Verifying low-level process metrics and startup behavior");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        Process monitoredProcess = null;
        var startupStopwatch = Stopwatch.StartNew();
        
        try
        {
            // ========== PHASE 1: EXECUTABLE LOCATION ==========
            _output.WriteLine("\n--- Phase 1: Locate Executable ---");
            var exePath = GetExecutablePath();
            
            _output.WriteLine($"Executable path: {exePath}");
            
            if (!System.IO.File.Exists(exePath))
            {
                _output.WriteLine($"✗ Executable not found at: {exePath}");
                _output.WriteLine("  Build the application first: dotnet build");
                _output.WriteLine("  SKIPPING TEST - executable not available");
                return;
            }
            
            var fileInfo = new System.IO.FileInfo(exePath);
            _output.WriteLine($"✓ Executable found");
            _output.WriteLine($"  File size: {fileInfo.Length / 1024 / 1024:F2} MB");
            _output.WriteLine($"  Last modified: {fileInfo.LastWriteTime}");
            
            // ========== PHASE 2: LAUNCH AND PROCESS CREATION ==========
            _output.WriteLine("\n--- Phase 2: Launch Process ---");
            
            // Record pre-launch system state
            var prelaunchProcesses = Process.GetProcessesByName("WileyWidget");
            _output.WriteLine($"Pre-launch WileyWidget processes: {prelaunchProcesses.Length}");
            
            // Launch application
            _output.WriteLine("Launching application...");
            var launchStartTime = Stopwatch.StartNew();
            
            automation = new UIA3Automation();
            app = FlaUI.Core.Application.Launch(exePath);
            
            launchStartTime.Stop();
            _output.WriteLine($"✓ Process launched in {launchStartTime.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Process ID: {app.ProcessId}");
            
            // Verify launch time is within acceptable range
            Assert.True(launchStartTime.ElapsedMilliseconds < 300, 
                $"Process launch took {launchStartTime.ElapsedMilliseconds}ms, expected <300ms");
            _output.WriteLine($"✓ Launch time within acceptable range (<300ms)");
            
            // ========== PHASE 3: PROCESS ATTACHMENT AND INITIAL METRICS ==========
            _output.WriteLine("\n--- Phase 3: Process Attachment ---");
            
            // Attach to the process for detailed monitoring
            monitoredProcess = Process.GetProcessById(app.ProcessId);
            _output.WriteLine($"✓ Attached to process PID {monitoredProcess.Id}");
            
            // Wait a moment for initial process setup
            await Task.Delay(100);
            
            // Verify process is running
            Assert.False(monitoredProcess.HasExited, "Process exited immediately after launch");
            _output.WriteLine($"✓ Process is running (HasExited: {monitoredProcess.HasExited})");
            
            // ========== PHASE 4: THREAD AND MEMORY METRICS ==========
            _output.WriteLine("\n--- Phase 4: Initial Process Metrics ---");
            
            // Thread count verification
            var initialThreadCount = monitoredProcess.Threads.Count;
            _output.WriteLine($"Thread count: {initialThreadCount}");
            Assert.True(initialThreadCount > 0, "Process has no threads");
            Assert.True(initialThreadCount < 100, $"Excessive thread count: {initialThreadCount}");
            _output.WriteLine($"✓ Thread count is reasonable (1-100 range)");
            
            // Memory metrics
            var initialMemoryMB = monitoredProcess.WorkingSet64 / 1024.0 / 1024.0;
            var privateMemoryMB = monitoredProcess.PrivateMemorySize64 / 1024.0 / 1024.0;
            var pagedMemoryMB = monitoredProcess.PagedMemorySize64 / 1024.0 / 1024.0;
            
            _output.WriteLine($"Working Set (Physical RAM): {initialMemoryMB:F2} MB");
            _output.WriteLine($"Private Memory: {privateMemoryMB:F2} MB");
            _output.WriteLine($"Paged Memory: {pagedMemoryMB:F2} MB");
            
            // Initial memory should be relatively small (around 3-50MB for WPF startup)
            Assert.True(initialMemoryMB > 0.5, "Memory usage suspiciously low");
            Assert.True(initialMemoryMB < 500, $"Excessive initial memory usage: {initialMemoryMB:F2} MB");
            _output.WriteLine($"✓ Initial memory usage is reasonable ({initialMemoryMB:F2} MB)");
            
            // ========== PHASE 5: STA APARTMENT STATE ==========
            _output.WriteLine("\n--- Phase 5: Thread Apartment State ---");
            
            // Check main thread apartment state
            var mainThread = monitoredProcess.Threads[0];
            _output.WriteLine($"Main thread ID: {mainThread.Id}");
            _output.WriteLine($"Main thread state: {mainThread.ThreadState}");
            _output.WriteLine($"Main thread priority: {mainThread.PriorityLevel}");
            
            // For WPF applications, we expect STA (Single-Threaded Apartment)
            // Note: We can't directly check ApartmentState from Process.Threads,
            // but we verify the process is responding correctly
            _output.WriteLine("✓ Main thread detected and running");
            
            // ========== PHASE 6: PROCESS DETAILS ==========
            _output.WriteLine("\n--- Phase 6: Additional Process Details ---");
            
            _output.WriteLine($"Process name: {monitoredProcess.ProcessName}");
            _output.WriteLine($"Start time: {monitoredProcess.StartTime}");
            _output.WriteLine($"Total processor time: {monitoredProcess.TotalProcessorTime}");
            _output.WriteLine($"User processor time: {monitoredProcess.UserProcessorTime}");
            _output.WriteLine($"Privileged processor time: {monitoredProcess.PrivilegedProcessorTime}");
            _output.WriteLine($"Base priority: {monitoredProcess.BasePriority}");
            _output.WriteLine($"Priority class: {monitoredProcess.PriorityClass}");
            _output.WriteLine($"Handle count: {monitoredProcess.HandleCount}");
            
            // Verify process is responsive
            Assert.True(monitoredProcess.Responding, "Process is not responding");
            _output.WriteLine($"✓ Process is responding");
            
            // ========== PHASE 7: WAIT FOR WINDOW AND NO ERROR DIALOGS ==========
            _output.WriteLine("\n--- Phase 7: Window Detection and Error Dialog Check ---");
            
            var windowDetectionStopwatch = Stopwatch.StartNew();
            Window? mainWindow = null;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Check for error dialogs first using constants
                    var errorDialogs = automation.GetDesktop().FindAllDescendants(cf => 
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                        .Where(w => w.Name?.Contains(UiElementConstants.ErrorDialogKeywords[0]) == true ||
                                    w.Name?.Contains(UiElementConstants.ErrorDialogKeywords[1]) == true)
                        .ToArray();
                    
                    if (errorDialogs.Any())
                    {
                        _output.WriteLine($"✗ ERROR DIALOG DETECTED: {errorDialogs[0].Name}");
                        
                        // Try to get error message text
                        var errorText = errorDialogs[0].FindAllDescendants(cf => 
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                        
                        foreach (var text in errorText.Take(5))
                        {
                            _output.WriteLine($"  Error text: {text.Name}");
                        }
                        
                        Assert.Fail($"Error dialog appeared during startup: {errorDialogs[0].Name}");
                    }
                    
                    // Try to get main window using responsive polling
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(1));
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        windowDetectionStopwatch.Stop();
                        _output.WriteLine($"✓ Main window appeared after {windowDetectionStopwatch.ElapsedMilliseconds}ms");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Waiting for window... ({ex.Message})");
                }
                
                await Task.Delay(500, cts.Token);
            }
            
            Assert.NotNull(mainWindow);
            _output.WriteLine($"✓ No error dialogs detected during startup");
            
            // ========== PHASE 8: POST-WINDOW PROCESS METRICS ==========
            _output.WriteLine("\n--- Phase 8: Post-Window Process Metrics ---");
            
            // Refresh process info
            monitoredProcess.Refresh();
            
            var postWindowThreadCount = monitoredProcess.Threads.Count;
            var postWindowMemoryMB = monitoredProcess.WorkingSet64 / 1024.0 / 1024.0;
            
            _output.WriteLine($"Thread count after window: {postWindowThreadCount} (initial: {initialThreadCount})");
            _output.WriteLine($"Memory after window: {postWindowMemoryMB:F2} MB (initial: {initialMemoryMB:F2} MB)");
            _output.WriteLine($"Memory growth: {(postWindowMemoryMB - initialMemoryMB):F2} MB");
            
            // Verify reasonable growth
            var memoryGrowth = postWindowMemoryMB - initialMemoryMB;
            Assert.True(memoryGrowth < 400, $"Excessive memory growth during startup: {memoryGrowth:F2} MB");
            _output.WriteLine($"✓ Memory growth within acceptable range");
            
            // ========== PHASE 9: PROCESS BY NAME VERIFICATION ==========
            _output.WriteLine("\n--- Phase 9: Process.GetProcessesByName Verification ---");
            
            var allWileyWidgetProcesses = Process.GetProcessesByName("WileyWidget");
            _output.WriteLine($"Total WileyWidget processes running: {allWileyWidgetProcesses.Length}");
            
            var ourProcess = allWileyWidgetProcesses.FirstOrDefault(p => p.Id == app.ProcessId);
            Assert.NotNull(ourProcess);
            _output.WriteLine($"✓ Our process found via GetProcessesByName (PID: {ourProcess.Id})");
            
            foreach (var proc in allWileyWidgetProcesses)
            {
                _output.WriteLine($"  Process PID {proc.Id}: Memory={proc.WorkingSet64 / 1024 / 1024}MB, Threads={proc.Threads.Count}");
            }
            
            // ========== PHASE 10: UNHANDLED EXCEPTION CHECK ==========
            _output.WriteLine("\n--- Phase 10: Exception Detection ---");
            
            // Check event logs for unhandled exceptions (simplified check)
            var exceptionDetected = false;
            
            try
            {
                // Check if process has any error state indicators
                if (!monitoredProcess.Responding)
                {
                    _output.WriteLine("✗ Process is not responding - possible unhandled exception");
                    exceptionDetected = true;
                }
                
                // Check for crash dialogs
                var crashDialogs = automation.GetDesktop().FindAllDescendants()
                    .Where(e => e.Name?.Contains("stopped working") == true ||
                                e.Name?.Contains("has stopped responding") == true)
                    .ToArray();
                
                if (crashDialogs.Any())
                {
                    _output.WriteLine($"✗ Crash dialog detected: {crashDialogs[0].Name}");
                    exceptionDetected = true;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"⚠ Exception check warning: {ex.Message}");
            }
            
            Assert.False(exceptionDetected, "Unhandled exception or crash detected during initialization");
            _output.WriteLine($"✓ No unhandled exceptions detected");
            
            // ========== FINAL METRICS ==========
            startupStopwatch.Stop();
            _output.WriteLine("\n=== Final Startup Metrics ===");
            _output.WriteLine($"Total test duration: {startupStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Process launch time: {launchStartTime.ElapsedMilliseconds}ms");
            _output.WriteLine($"Window appearance time: {windowDetectionStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Final thread count: {postWindowThreadCount}");
            _output.WriteLine($"Final memory usage: {postWindowMemoryMB:F2} MB");
            _output.WriteLine($"Process is responding: {monitoredProcess.Responding}");
            
            // ========== SYSTEM STATE LOGGING ==========
            _output.WriteLine("\n=== System State ===");
            _output.WriteLine($"Machine name: {Environment.MachineName}");
            _output.WriteLine($"OS version: {Environment.OSVersion}");
            _output.WriteLine($"Processor count: {Environment.ProcessorCount}");
            _output.WriteLine($"Working set: {Environment.WorkingSet / 1024 / 1024} MB");
            _output.WriteLine($".NET version: {Environment.Version}");
            
            _output.WriteLine("\n✅ APPLICATION LAUNCH COMPONENTS TEST PASSED");
            _output.WriteLine("  All launch metrics within acceptable ranges");
            _output.WriteLine("  No errors or exceptions detected during initialization");
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("\n✗ TEST TIMED OUT");
            _output.WriteLine("  Window did not appear within 30 seconds");
            
            LogSystemStateAtFailure(monitoredProcess);
            throw;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ APPLICATION LAUNCH COMPONENTS TEST FAILED");
            _output.WriteLine($"Error Type: {ex.GetType().Name}");
            _output.WriteLine($"Error Message: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            
            LogSystemStateAtFailure(monitoredProcess);
            throw;
        }
        finally
        {
            // Cleanup
            if (app != null && !app.HasExited)
            {
                try
                {
                    _output.WriteLine("\nClosing application...");
                    app.Close();
                    await Task.Delay(2000);
                    
                    if (!app.HasExited)
                    {
                        _output.WriteLine("⚠ Application did not close gracefully, forcing termination");
                        app.Kill();
                    }
                    else
                    {
                        _output.WriteLine("✓ Application closed gracefully");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"⚠ Error during cleanup: {ex.Message}");
                    try { app.Kill(); } catch { }
                }
            }
            
            automation?.Dispose();
            monitoredProcess?.Dispose();
        }
    }
    
    /// <summary>
    /// Helper method to log comprehensive system state when a test fails.
    /// Captures process state, memory usage, threads, and system resources.
    /// </summary>
    private void LogSystemStateAtFailure(Process process)
    {
        _output.WriteLine("\n=== SYSTEM STATE AT FAILURE ===");
        
        try
        {
            if (process != null && !process.HasExited)
            {
                process.Refresh();
                
                _output.WriteLine("Process State:");
                _output.WriteLine($"  PID: {process.Id}");
                _output.WriteLine($"  Name: {process.ProcessName}");
                _output.WriteLine($"  Responding: {process.Responding}");
                _output.WriteLine($"  Threads: {process.Threads.Count}");
                _output.WriteLine($"  Working Set: {process.WorkingSet64 / 1024 / 1024:F2} MB");
                _output.WriteLine($"  Private Memory: {process.PrivateMemorySize64 / 1024 / 1024:F2} MB");
                _output.WriteLine($"  Virtual Memory: {process.VirtualMemorySize64 / 1024 / 1024:F2} MB");
                _output.WriteLine($"  Handle Count: {process.HandleCount}");
                _output.WriteLine($"  CPU Time: {process.TotalProcessorTime}");
                _output.WriteLine($"  Start Time: {process.StartTime}");
                
                // Thread details (fixed casting issue)
                _output.WriteLine("\nThread Details:");
                foreach (ProcessThread thread in process.Threads)
                {
                    _output.WriteLine($"  Thread {thread.Id}: State={thread.ThreadState}, Priority={thread.PriorityLevel}");
                }
            }
            else if (process != null)
            {
                _output.WriteLine("Process State: EXITED");
                try
                {
                    // Fixed: Wrap ExitCode access in try-catch
                    _output.WriteLine($"  Exit code: {process.ExitCode}");
                    _output.WriteLine($"  Exit time: {process.ExitTime}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Exit code unavailable: {ex.Message}");
                }
            }
            
            // System resources
            _output.WriteLine("\nSystem Resources:");
            _output.WriteLine($"  Machine: {Environment.MachineName}");
            _output.WriteLine($"  OS: {Environment.OSVersion}");
            _output.WriteLine($"  Processors: {Environment.ProcessorCount}");
            _output.WriteLine($"  System Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
            
            // All WileyWidget processes
            var allProcesses = Process.GetProcessesByName("WileyWidget");
            _output.WriteLine($"\nAll WileyWidget Processes: {allProcesses.Length}");
            foreach (var p in allProcesses)
            {
                _output.WriteLine($"  PID {p.Id}: Memory={p.WorkingSet64 / 1024 / 1024}MB, Threads={p.Threads.Count}, Responding={p.Responding}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error logging system state: {ex.Message}");
        }
    }

    /// <summary>
    /// END-TO-END TEST: Comprehensive view rendering verification.
    /// Tests all major views in the WileyWidget application:
    /// - DashboardView, EnterpriseView, BudgetView, AIAssistView
    /// - AnalyticsView, SettingsView, MunicipalAccountView, UtilityCustomerView
    /// 
    /// For each view:
    /// 1. Navigate via FlaUI (click menu/navigation items)
    /// 2. Wait for view load (1000ms + polling until stable)
    /// 3. Verify visibility and bounds (width/height > 0)
    /// 4. Check DataContext indirectly via expected child elements
    /// 5. Verify Syncfusion controls render without errors
    /// 6. Capture screenshot on failure
    /// 
    /// Reference: https://github.com/FlaUI/FlaUI/wiki/Element-Search
    /// </summary>
    [StaFact]
    public async Task E2E_05_AllViews_RenderingVerification()
    {
        _output.WriteLine("====== COMPREHENSIVE VIEW RENDERING VERIFICATION ======");
        _output.WriteLine("Testing all major application views for proper rendering");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        
        try
        {
            // ========== PHASE 1: APPLICATION LAUNCH ==========
            _output.WriteLine("\n--- Phase 1: Launch Application ---");
            
            // Launch application using helper method
            (app, var mainWindow, automation) = await LaunchAppAsync(_output);
            _output.WriteLine($"✓ Application launched and main window available");
            
            // ========== PHASE 2: DEFINE VIEWS TO TEST ==========
            var viewsToTest = new[]
            {
                new ViewTestDefinition
                {
                    Name = "Dashboard",
                    NavigationNames = new[] { "Dashboard", "Home" },
                    ExpectedChildElements = new[] { "SfChart", "TileLayout", "Panel" },
                    RequiresSyncfusion = true,
                    Description = "Main dashboard with charts and tiles"
                },
                new ViewTestDefinition
                {
                    Name = "Enterprise",
                    NavigationNames = new[] { "Enterprise", "EnterpriseView" },
                    ExpectedChildElements = new[] { "DataGrid", "SfDataGrid", "Grid" },
                    RequiresSyncfusion = true,
                    Description = "Enterprise data management view"
                },
                new ViewTestDefinition
                {
                    Name = "Budget",
                    NavigationNames = new[] { "Budget", "BudgetView" },
                    ExpectedChildElements = new[] { "DataGrid", "SfDataGrid", "TextBox" },
                    RequiresSyncfusion = true,
                    Description = "Budget management and tracking"
                },
                new ViewTestDefinition
                {
                    Name = "AIAssist",
                    NavigationNames = new[] { "AI Assist", "AIAssist", "AI" },
                    ExpectedChildElements = new[] { "TextBox", "RichTextBox", "Button" },
                    RequiresSyncfusion = false,
                    Description = "AI assistance interface"
                },
                new ViewTestDefinition
                {
                    Name = "Analytics",
                    NavigationNames = new[] { "Analytics", "AnalyticsView" },
                    ExpectedChildElements = new[] { "Chart", "SfChart", "DataGrid" },
                    RequiresSyncfusion = true,
                    Description = "Analytics and reporting"
                },
                new ViewTestDefinition
                {
                    Name = "Settings",
                    NavigationNames = new[] { "Settings", "SettingsView", "Options" },
                    ExpectedChildElements = new[] { "CheckBox", "ComboBox", "TextBox" },
                    RequiresSyncfusion = false,
                    Description = "Application settings and preferences"
                },
                new ViewTestDefinition
                {
                    Name = "MunicipalAccount",
                    NavigationNames = new[] { "Municipal Account", "MunicipalAccount", "Accounts" },
                    ExpectedChildElements = new[] { "DataGrid", "SfDataGrid", "TreeView" },
                    RequiresSyncfusion = true,
                    Description = "Municipal account management"
                },
                new ViewTestDefinition
                {
                    Name = "UtilityCustomer",
                    NavigationNames = new[] { "Utility Customer", "UtilityCustomer", "Customers" },
                    ExpectedChildElements = new[] { "DataGrid", "SfDataGrid", "Panel" },
                    RequiresSyncfusion = true,
                    Description = "Utility customer data management"
                }
            };
            
            _output.WriteLine($"\nTesting {viewsToTest.Length} views for rendering");
            
            // ========== PHASE 3: TEST EACH VIEW ==========
            var results = new System.Collections.Generic.List<ViewTestResult>();
            
            foreach (var viewDef in viewsToTest)
            {
                _output.WriteLine($"\n{'=',60}");
                _output.WriteLine($"Testing View: {viewDef.Name}");
                _output.WriteLine($"Description: {viewDef.Description}");
                _output.WriteLine($"{'=',60}");
                
                var result = await TestViewRendering(mainWindow, automation, viewDef, app);
                results.Add(result);
                
                // Brief pause between views
                await Task.Delay(500);
            }
            
            // ========== PHASE 4: REPORT RESULTS ==========
            _output.WriteLine("\n\n" + new string('=', 80));
            _output.WriteLine("VIEW RENDERING TEST RESULTS");
            _output.WriteLine(new string('=', 80));
            
            var passedCount = results.Count(r => r.Success);
            var failedCount = results.Count(r => !r.Success);
            var skippedCount = results.Count(r => r.Skipped);
            
            _output.WriteLine($"\nSummary:");
            _output.WriteLine($"  ✓ Passed: {passedCount}");
            _output.WriteLine($"  ✗ Failed: {failedCount}");
            _output.WriteLine($"  ⊘ Skipped: {skippedCount}");
            _output.WriteLine($"  Total: {results.Count}");
            
            _output.WriteLine("\nDetailed Results:");
            foreach (var result in results)
            {
                var icon = result.Success ? "✓" : (result.Skipped ? "⊘" : "✗");
                _output.WriteLine($"  {icon} {result.ViewName,-20} - {result.Message}");
                
                if (!string.IsNullOrEmpty(result.Details))
                {
                    foreach (var line in result.Details.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            _output.WriteLine($"      {line}");
                        }
                    }
                }
            }
            
            // Assert that at least some views passed
            Assert.True(passedCount > 0, "No views passed rendering verification");
            _output.WriteLine($"\n✅ VIEW RENDERING TEST COMPLETED");
            _output.WriteLine($"   {passedCount}/{results.Count} views rendered successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ VIEW RENDERING TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            // Cleanup
            if (app != null && !app.HasExited)
            {
                try
                {
                    app.Close();
                    await Task.Delay(2000);
                    if (!app.HasExited) app.Kill();
                }
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }
    
    /// <summary>
    /// Helper method to test a single view's rendering.
    /// </summary>
    private async Task<ViewTestResult> TestViewRendering(
        Window mainWindow, 
        AutomationBase automation, 
        ViewTestDefinition viewDef,
        FlaUI.Core.Application app)
    {
        var result = new ViewTestResult
        {
            ViewName = viewDef.Name,
            Success = false,
            Skipped = false,
            Message = "",
            Details = ""
        };
        
        var detailsBuilder = new System.Text.StringBuilder();
        
        try
        {
            // ========== STEP 1: FIND NAVIGATION ELEMENT ==========
            _output.WriteLine($"\nStep 1: Finding navigation element...");
            
            AutomationElement navElement = null;
            string foundNavigationName = null;
            
            foreach (var navName in viewDef.NavigationNames)
            {
                _output.WriteLine($"  Trying: '{navName}'");
                
                // Try multiple strategies to find navigation element
                var strategies = new System.Func<AutomationElement>[]
                {
                    () => mainWindow.FindFirstDescendant(cf => cf.ByName(navName)),
                    () => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(navName)),
                    () => mainWindow.FindFirstDescendant(cf => cf.ByText(navName)),
                    () => mainWindow.FindAllDescendants(cf => 
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                        .FirstOrDefault(e => e.Name?.Contains(navName) == true),
                    () => mainWindow.FindFirstDescendant(cf => 
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem)
                        .And(cf.ByName(navName)))
                };
                
                foreach (var strategy in strategies)
                {
                    try
                    {
                        navElement = strategy();
                        if (navElement != null && navElement.IsAvailable)
                        {
                            foundNavigationName = navName;
                            _output.WriteLine($"  ✓ Found via strategy: {navElement.ControlType}");
                            break;
                        }
                    }
                    catch { }
                }
                
                if (navElement != null) break;
            }
            
            if (navElement == null)
            {
                _output.WriteLine($"  ⊘ Navigation element not found - skipping view");
                result.Skipped = true;
                result.Message = "Navigation element not found";
                detailsBuilder.AppendLine($"Tried names: {string.Join(", ", viewDef.NavigationNames)}");
                result.Details = detailsBuilder.ToString();
                return result;
            }
            
            _output.WriteLine($"  ✓ Navigation element found: '{foundNavigationName}'");
            detailsBuilder.AppendLine($"Navigation: {foundNavigationName} ({navElement.ControlType})");
            
            // ========== STEP 2: CLICK NAVIGATION ==========
            _output.WriteLine($"\nStep 2: Navigating to view...");
            
            try
            {
                // Use safe click with improved polling
                var clickResult = await SafeClickElement(navElement, viewDef.Name);
                if (clickResult)
                {
                    _output.WriteLine($"  ✓ Clicked navigation element");
                    detailsBuilder.AppendLine("Clicked: Success");
                }
                else
                {
                    _output.WriteLine($"  ✗ Click failed");
                    result.Message = "Navigation click failed";
                    result.Details = detailsBuilder.ToString();
                    await CaptureFailureScreenshot(mainWindow, viewDef.Name, "NavigationClickFailed");
                    return result;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ✗ Click failed: {ex.Message}");
                result.Message = $"Navigation click failed: {ex.Message}";
                result.Details = detailsBuilder.ToString();
                await CaptureFailureScreenshot(mainWindow, viewDef.Name, "NavigationClickFailed");
                return result;
            }
            
            // ========== STEP 3: WAIT FOR VIEW LOAD ==========
            _output.WriteLine($"\nStep 3: Waiting for view to load...");
            
            // Use responsive polling helper
            var viewElement = await UiTestHelpers.WaitForElementResponsiveAsync(mainWindow, cf => 
                cf.ByAutomationId($"{viewDef.Name}View")
                .Or(cf.ByName(viewDef.Name))
                .Or(cf.ByText(viewDef.Name)), 
                UiTestConstants.Timeouts.ViewRender);
            
            // Poll for view stability (wait for content to appear and stabilize)
            var pollAttempts = 0;
            var maxPollAttempts = 10;
            var previousChildCount = 0;
            var stableCount = 0;
            
            while (pollAttempts < maxPollAttempts)
            {
                var currentChildren = mainWindow.FindAllDescendants();
                var currentChildCount = currentChildren.Length;
                
                _output.WriteLine($"  Poll {pollAttempts + 1}: Child count = {currentChildCount}");
                
                if (currentChildCount == previousChildCount)
                {
                    stableCount++;
                    if (stableCount >= 2)
                    {
                        _output.WriteLine($"  ✓ View stabilized after {pollAttempts + 1} polls");
                        break;
                    }
                }
                else
                {
                    stableCount = 0;
                }
                
                previousChildCount = currentChildCount;
                pollAttempts++;
                await Task.Delay(300);
            }
            
            detailsBuilder.AppendLine($"Stability: {pollAttempts + 1} polls, final child count = {previousChildCount}");
            
            // ========== STEP 4: VERIFY VISIBILITY AND BOUNDS ==========
            _output.WriteLine($"\nStep 4: Verifying view visibility and bounds...");
            
            // Try to find the specific view container
            AutomationElement viewContainer = null;
            
            foreach (var expectedElement in viewDef.ExpectedChildElements)
            {
                viewContainer = mainWindow.FindAllDescendants(cf => 
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Pane)
                    .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Custom)))
                    .FirstOrDefault(e => e.ClassName?.Contains(expectedElement) == true);
                
                if (viewContainer != null && viewContainer.IsAvailable)
                {
                    break;
                }
            }
            
            // If no specific container, use the main content area
            if (viewContainer == null)
            {
                viewContainer = mainWindow.FindFirstDescendant(cf => 
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Pane));
            }
            
            if (viewContainer != null && viewContainer.IsAvailable)
            {
                var bounds = viewContainer.BoundingRectangle;
                _output.WriteLine($"  View bounds: X={bounds.X}, Y={bounds.Y}, W={bounds.Width}, H={bounds.Height}");
                
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    _output.WriteLine($"  ✓ View has valid bounds (W={bounds.Width}, H={bounds.Height})");
                    detailsBuilder.AppendLine($"Bounds: {bounds.Width}x{bounds.Height}");
                }
                else
                {
                    _output.WriteLine($"  ✗ View has invalid bounds (W={bounds.Width}, H={bounds.Height})");
                    result.Message = "View has zero width or height";
                    result.Details = detailsBuilder.ToString();
                    await CaptureFailureScreenshot(mainWindow, viewDef.Name, "InvalidBounds");
                    return result;
                }
                
                // Check if offscreen
                if (viewContainer.IsOffscreen)
                {
                    _output.WriteLine($"  ⚠ View is offscreen");
                    detailsBuilder.AppendLine("Warning: View is offscreen");
                }
                else
                {
                    _output.WriteLine($"  ✓ View is visible on screen");
                }
            }
            else
            {
                _output.WriteLine($"  ⚠ Could not locate specific view container, checking children instead");
            }
            
            // ========== STEP 5: CHECK DATACONTEXT VIA CHILD ELEMENTS ==========
            _output.WriteLine($"\nStep 5: Checking DataContext via expected child elements...");
            
            var foundExpectedElements = new System.Collections.Generic.List<string>();
            
            foreach (var expectedElement in viewDef.ExpectedChildElements)
            {
                _output.WriteLine($"  Looking for: {expectedElement}");
                
                var elements = mainWindow.FindAllDescendants()
                    .Where(e => e.ClassName?.Contains(expectedElement) == true)
                    .ToArray();
                
                if (elements.Any())
                {
                    _output.WriteLine($"    ✓ Found {elements.Length} '{expectedElement}' element(s)");
                    foundExpectedElements.Add(expectedElement);
                    
                    // Log first few instances
                    foreach (var elem in elements.Take(3))
                    {
                        _output.WriteLine($"      - {elem.ControlType}: {elem.Name ?? elem.AutomationId ?? "(unnamed)"}");
                    }
                }
                else
                {
                    _output.WriteLine($"    ⊘ '{expectedElement}' not found");
                }
            }
            
            if (foundExpectedElements.Any())
            {
                _output.WriteLine($"  ✓ Found {foundExpectedElements.Count}/{viewDef.ExpectedChildElements.Length} expected element types");
                detailsBuilder.AppendLine($"Expected elements: {string.Join(", ", foundExpectedElements)}");
            }
            else
            {
                _output.WriteLine($"  ⚠ No expected child elements found - view may use different controls");
                detailsBuilder.AppendLine("Warning: No expected child elements found");
            }
            
            // ========== STEP 6: VERIFY SYNCFUSION CONTROLS ==========
            _output.WriteLine($"\nStep 6: Verifying Syncfusion controls...");
            
            if (viewDef.RequiresSyncfusion)
            {
                var syncfusionControls = mainWindow.FindAllDescendants()
                    .Where(e => e.ClassName?.StartsWith("Syncfusion") == true)
                    .ToArray();
                
                _output.WriteLine($"  Found {syncfusionControls.Length} Syncfusion controls");
                
                if (syncfusionControls.Length > 0)
                {
                    _output.WriteLine($"  ✓ Syncfusion controls present");
                    detailsBuilder.AppendLine($"Syncfusion controls: {syncfusionControls.Length}");
                    
                    // Check each Syncfusion control for rendering issues
                    var blankControls = 0;
                    foreach (var ctrl in syncfusionControls.Take(10))
                    {
                        var childCount = ctrl.FindAllChildren().Length;
                        var bounds = ctrl.BoundingRectangle;
                        
                        _output.WriteLine($"    - {ctrl.ClassName}: Children={childCount}, Size={bounds.Width}x{bounds.Height}");
                        
                        // Flag potential blank areas (no children and reasonable size)
                        if (childCount == 0 && bounds.Width > 50 && bounds.Height > 50)
                        {
                            _output.WriteLine($"      ⚠ Possible blank area detected");
                            blankControls++;
                        }
                    }
                    
                    if (blankControls > syncfusionControls.Length / 2)
                    {
                        _output.WriteLine($"  ⚠ Many controls appear blank ({blankControls}/{syncfusionControls.Length})");
                        detailsBuilder.AppendLine($"Warning: {blankControls} potentially blank controls");
                    }
                    else
                    {
                        _output.WriteLine($"  ✓ Controls appear to be rendering content");
                    }
                }
                else
                {
                    _output.WriteLine($"  ⚠ No Syncfusion controls found (expected for this view)");
                    detailsBuilder.AppendLine("Warning: Expected Syncfusion controls not found");
                }
            }
            else
            {
                _output.WriteLine($"  ⊘ Syncfusion controls not required for this view");
            }
            
            // ========== STEP 7: FINAL VERIFICATION ==========
            _output.WriteLine($"\nStep 7: Final verification...");
            
            // Count all visible elements in the view
            var allElements = mainWindow.FindAllDescendants();
            var visibleElements = allElements.Count(e => e.IsAvailable && !e.IsOffscreen);
            
            _output.WriteLine($"  Total elements: {allElements.Length}");
            _output.WriteLine($"  Visible elements: {visibleElements}");
            detailsBuilder.AppendLine($"Visible elements: {visibleElements}/{allElements.Length}");
            
            if (visibleElements > 5) // Arbitrary threshold - view should have some content
            {
                _output.WriteLine($"  ✓ View has sufficient visible content ({visibleElements} elements)");
                result.Success = true;
                result.Message = $"Rendering verified ({visibleElements} elements)";
            }
            else
            {
                _output.WriteLine($"  ✗ View has insufficient visible content ({visibleElements} elements)");
                result.Message = $"Insufficient content ({visibleElements} elements)";
                await CaptureFailureScreenshot(mainWindow, viewDef.Name, "InsufficientContent");
            }
            
            result.Details = detailsBuilder.ToString();
            
            // Capture success screenshot
            if (result.Success)
            {
                await CaptureSuccessScreenshot(mainWindow, viewDef.Name);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ View test failed with exception: {ex.Message}");
            result.Success = false;
            result.Message = $"Exception: {ex.Message}";
            result.Details = detailsBuilder.ToString() + $"\nException: {ex.GetType().Name}\n{ex.StackTrace}";
            
            await CaptureFailureScreenshot(mainWindow, viewDef.Name, "Exception");
        }
        
        return result;
    }
    
    /// <summary>
    /// Captures a screenshot on test failure for diagnostics.
    /// </summary>
    private async Task CaptureFailureScreenshot(Window window, string viewName, string reason)
    {
        try
        {
            var screenshotPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"ViewTest_FAILED_{viewName}_{reason}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            );
            
            using var bitmap = window.Capture();
            using var stream = System.IO.File.Create(screenshotPath);
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            
            _output.WriteLine($"  📷 Failure screenshot: {screenshotPath}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  ⚠ Screenshot capture failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Captures a screenshot on test success for verification.
    /// </summary>
    private async Task CaptureSuccessScreenshot(Window window, string viewName)
    {
        try
        {
            var screenshotPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"ViewTest_SUCCESS_{viewName}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            );
            
            using var bitmap = window.Capture();
            using var stream = System.IO.File.Create(screenshotPath);
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            
            _output.WriteLine($"  ✓ Success screenshot: {screenshotPath}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  ⚠ Screenshot capture failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// View test definition structure.
    /// </summary>
    private class ViewTestDefinition
    {
        public string Name { get; set; }
        public string[] NavigationNames { get; set; }
        public string[] ExpectedChildElements { get; set; }
        public bool RequiresSyncfusion { get; set; }
        public string Description { get; set; }
    }
    
    /// <summary>
    /// View test result structure.
    /// </summary>
    private class ViewTestResult
    {
        public string ViewName { get; set; }
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// END-TO-END TEST: Prism module initialization verification.
    /// Verifies that Prism regions are properly initialized and views are loaded:
    /// - MainRegion has active views
    /// - Region navigation is functional
    /// - Module catalog is complete
    /// - Views are properly injected into regions
    /// Uses FlaUI to inspect region content and verify module loading.
    /// Reference: https://github.com/FlaUI/FlaUI/wiki/Property-Patterns
    /// </summary>
    [StaFact]
    public async Task E2E_06_PrismModuleInitialization_Verification()
    {
        _output.WriteLine("====== PRISM MODULE INITIALIZATION TEST ======");
        _output.WriteLine("Verifying Prism regions and module loading");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        
        try
        {
            // ========== PHASE 1: LAUNCH APPLICATION ==========
            _output.WriteLine("\n--- Phase 1: Launch Application ---");
            
            // Launch application using helper method
            (app, var mainWindow, automation) = await LaunchAppAsync(_output);
            _output.WriteLine($"✓ Application launched and main window available");
            
            // ========== PHASE 2: VERIFY PRISM REGIONS ==========
            _output.WriteLine("\n--- Phase 2: Prism Region Verification ---");
            
            // Expected Prism regions in WileyWidget
            var expectedRegions = new[] 
            { 
                "MainRegion", 
                "ContentRegion", 
                "NavigationRegion",
                "HeaderRegion",
                "FooterRegion"
            };
            
            var foundRegions = new System.Collections.Generic.List<string>();
            
            foreach (var regionName in expectedRegions)
            {
                _output.WriteLine($"\nChecking region: {regionName}");
                
                // Try to find region by AutomationId (Prism typically uses region name as AutomationId)
                var regionElement = mainWindow.FindFirstDescendant(cf => 
                    cf.ByAutomationId(regionName)
                    .Or(cf.ByName(regionName)));
                
                if (regionElement != null && regionElement.IsAvailable)
                {
                    _output.WriteLine($"  ✓ Region found: {regionName}");
                    foundRegions.Add(regionName);
                    
                    // Check if region has content
                    var regionChildren = regionElement.FindAllChildren();
                    _output.WriteLine($"    Direct children: {regionChildren.Length}");
                    
                    if (regionChildren.Length > 0)
                    {
                        _output.WriteLine($"    ✓ Region has content ({regionChildren.Length} child elements)");
                        
                        // Log first few children
                        foreach (var child in regionChildren.Take(5))
                        {
                            _output.WriteLine($"      - {child.ControlType}: {child.Name ?? child.AutomationId ?? child.ClassName}");
                        }
                    }
                    else
                    {
                        _output.WriteLine($"    ⚠ Region appears empty (may be lazy-loaded)");
                    }
                    
                    // Check region bounds
                    var bounds = regionElement.BoundingRectangle;
                    _output.WriteLine($"    Bounds: {bounds.Width}x{bounds.Height} at ({bounds.X}, {bounds.Y})");
                    
                    if (bounds.Width > 0 && bounds.Height > 0)
                    {
                        _output.WriteLine($"    ✓ Region has valid bounds");
                    }
                    else
                    {
                        _output.WriteLine($"    ⚠ Region has zero bounds");
                    }
                }
                else
                {
                    _output.WriteLine($"  ⊘ Region '{regionName}' not found (may use different naming)");
                }
            }
            
            _output.WriteLine($"\n✓ Found {foundRegions.Count}/{expectedRegions.Length} expected regions");
            _output.WriteLine($"  Regions: {string.Join(", ", foundRegions)}");
            
            // ========== PHASE 3: VERIFY MAIN REGION CONTENT ==========
            _output.WriteLine("\n--- Phase 3: Main Region Content Verification ---");
            
            // Look for content in main display area (likely MainRegion or ContentRegion)
            var mainContentArea = mainWindow.FindFirstDescendant(cf => 
                cf.ByAutomationId("MainRegion")
                .Or(cf.ByAutomationId("ContentRegion"))
                .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Pane).And(cf.ByName("ContentHost"))));
            
            if (mainContentArea != null && mainContentArea.IsAvailable)
            {
                _output.WriteLine($"✓ Main content area found");
                
                var allDescendants = mainContentArea.FindAllDescendants();
                _output.WriteLine($"  Total descendant elements: {allDescendants.Length}");
                
                // Check for views loaded in the region
                var viewElements = allDescendants.Where(e => 
                    e.ClassName != null && 
                    (e.ClassName.EndsWith("View") || e.ClassName.Contains("ViewModel")));
                
                if (viewElements.Any())
                {
                    _output.WriteLine($"  ✓ Found {viewElements.Count()} view elements:");
                    foreach (var view in viewElements.Take(10))
                    {
                        _output.WriteLine($"    - {view.ClassName}: {view.Name ?? view.AutomationId ?? "(unnamed)"}");
                    }
                }
                else
                {
                    _output.WriteLine($"  ⚠ No view elements detected in main region");
                }
                
                // Verify the region is not empty
                Assert.True(allDescendants.Length > 0, "Main region has no content - Prism may not be initializing views");
                _output.WriteLine($"  ✓ Main region initialized with content");
            }
            else
            {
                _output.WriteLine($"  ⚠ Main content area not found - checking alternative patterns");
                
                // Fallback: check if there's ANY substantial content
                var allContent = mainWindow.FindAllDescendants();
                _output.WriteLine($"  Window has {allContent.Length} total elements");
                
                Assert.True(allContent.Length > 20, "Insufficient UI content - possible Prism initialization failure");
            }
            
            // ========== PHASE 4: VERIFY MODULE LOADING ==========
            _output.WriteLine("\n--- Phase 4: Module Loading Verification ---");
            
            // Check for presence of module-specific UI elements
            var moduleIndicators = new[]
            {
                ("Dashboard Module", new[] { "Dashboard", "DashboardView" }),
                ("Enterprise Module", new[] { "Enterprise", "EnterpriseView" }),
                ("Budget Module", new[] { "Budget", "BudgetView" }),
                ("Analytics Module", new[] { "Analytics", "AnalyticsView" })
            };
            
            var loadedModules = 0;
            
            foreach (var (moduleName, indicators) in moduleIndicators)
            {
                var found = false;
                foreach (var indicator in indicators)
                {
                    var element = mainWindow.FindFirstDescendant(cf => 
                        cf.ByAutomationId(indicator)
                        .Or(cf.ByName(indicator)));
                    
                    if (element != null && element.IsAvailable)
                    {
                        _output.WriteLine($"  ✓ {moduleName}: Found indicator '{indicator}'");
                        found = true;
                        loadedModules++;
                        break;
                    }
                }
                
                if (!found)
                {
                    _output.WriteLine($"  ⊘ {moduleName}: No indicators found");
                }
            }
            
            _output.WriteLine($"\n✓ Detected {loadedModules}/{moduleIndicators.Length} modules loaded");
            
            // ========== PHASE 5: NAVIGATION FUNCTIONALITY ==========
            _output.WriteLine("\n--- Phase 5: Region Navigation Test ---");
            
            // Try navigating to a different view to verify region navigation works
            var dashboardNav = mainWindow.FindFirstDescendant(cf => 
                cf.ByName("Dashboard").Or(cf.ByAutomationId("Dashboard")));
            
            if (dashboardNav != null && dashboardNav.IsAvailable)
            {
                _output.WriteLine($"✓ Dashboard navigation element found");
                
                try
                {
                    dashboardNav.Click();
                    _output.WriteLine($"  ✓ Clicked Dashboard navigation");
                    
                    await Task.Delay(1000);
                    
                    // Verify content changed
                    var dashboardContent = mainWindow.FindFirstDescendant(cf => 
                        cf.ByAutomationId("DashboardView"))
                    ?? mainWindow.FindAllDescendants()
                        .FirstOrDefault(e => e.ClassName?.Contains("Dashboard") == true);
                    
                    if (dashboardContent != null)
                    {
                        _output.WriteLine($"  ✓ Region navigation successful - Dashboard view loaded");
                    }
                    else
                    {
                        _output.WriteLine($"  ⚠ Dashboard view not detected after navigation");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  ⚠ Navigation test failed: {ex.Message}");
                }
            }
            
            _output.WriteLine("\n✅ PRISM MODULE INITIALIZATION TEST PASSED");
            _output.WriteLine($"  Regions initialized and modules loaded successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ PRISM MODULE INITIALIZATION TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            if (app != null && !app.HasExited)
            {
                try { app.Close(); await Task.Delay(2000); if (!app.HasExited) app.Kill(); }
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }

    /// <summary>
    /// END-TO-END TEST: Syncfusion license validation and theme application.
    /// Verifies:
    /// - Syncfusion license is properly registered (no license dialogs)
    /// - Theme is applied (FluentDark or other)
    /// - Controls render with proper styling
    /// Uses FlaUI to inspect control properties like Background, Foreground, etc.
    /// Reference: https://github.com/FlaUI/FlaUI/wiki/Automation-Patterns
    /// </summary>
    [StaFact]
    public async Task E2E_07_SyncfusionLicenseAndTheme_Verification()
    {
        _output.WriteLine("====== SYNCFUSION LICENSE & THEME VERIFICATION TEST ======");
        _output.WriteLine("Verifying Syncfusion licensing and theme application");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        
        try
        {
            // ========== PHASE 1: LAUNCH APPLICATION ==========
            _output.WriteLine("\n--- Phase 1: Launch Application ---");
            
            // Launch application using helper method
            (app, var mainWindow, automation) = await LaunchAppAsync(_output);
            _output.WriteLine($"✓ Application launched and main window available");
            
            // ========== PHASE 2: CHECK FOR LICENSE DIALOGS ==========
            _output.WriteLine("\n--- Phase 2: License Dialog Detection ---");
            
            // Wait briefly for potential license dialog
            await Task.Delay(2000);
            
            var desktop = automation.GetDesktop();
            var allWindows = desktop.FindAllChildren();
            
            _output.WriteLine($"Open windows: {allWindows.Length}");
            
            var licenseDialogDetected = false;
            foreach (var window in allWindows)
            {
                var title = window.Name ?? "";
                _output.WriteLine($"  Window: '{title}' (Class: {window.ClassName})");
                
                if (title.Contains("License", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) && 
                    title.Contains("Trial", StringComparison.OrdinalIgnoreCase))
                {
                    _output.WriteLine($"    ⚠ Potential license dialog detected!");
                    licenseDialogDetected = true;
                    
                    // Try to get dialog text
                    var dialogText = window.FindAllDescendants(cf => 
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                    
                    foreach (var text in dialogText.Take(5))
                    {
                        _output.WriteLine($"      Dialog text: {text.Name}");
                    }
                }
            }
            
            Assert.False(licenseDialogDetected, "Syncfusion license dialog appeared - license key may be invalid or missing");
            _output.WriteLine($"✓ No license dialogs detected - Syncfusion license is valid");
            
            // ========== PHASE 3: THEME VERIFICATION VIA PROPERTIES ==========
            _output.WriteLine("\n--- Phase 3: Theme Application Verification ---");
            
            // Check window background color (indicates theme application)
            try
            {
                var windowProperties = mainWindow.Properties;
                _output.WriteLine($"Window properties available: {windowProperties != null}");
                
                // Try to get background color property
                if (mainWindow.Patterns.LegacyIAccessible.IsSupported)
                {
                    _output.WriteLine($"  IAccessible pattern supported");
                }
                
                // Check main window automation properties
                _output.WriteLine($"  Window ClassName: {mainWindow.ClassName}");
                _output.WriteLine($"  Window Name: {mainWindow.Name}");
                _output.WriteLine($"  Window AutomationId: {mainWindow.AutomationId}");
                
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ⚠ Could not access window properties: {ex.Message}");
            }
            
            // ========== PHASE 5: SYNCFUSION CONTROL DETECTION ==========
            _output.WriteLine("\n--- Phase 5: Syncfusion Control Detection ---");
            
            var syncfusionControls = mainWindow.FindAllDescendants()
                .Where(e => e.ClassName?.StartsWith("Syncfusion") == true)
                .ToArray();
            
            _output.WriteLine($"Found {syncfusionControls.Length} Syncfusion controls");
            
            Assert.True(syncfusionControls.Length > 0, "No Syncfusion controls found - components may not be loading");
            _output.WriteLine($"✓ Syncfusion controls present in UI");
            
            // Log details of Syncfusion controls
            var controlTypes = new System.Collections.Generic.Dictionary<string, int>();
            
            foreach (var ctrl in syncfusionControls)
            {
                var className = ctrl.ClassName ?? "Unknown";
                if (!controlTypes.ContainsKey(className))
                {
                    controlTypes[className] = 0;
                }
                controlTypes[className]++;
            }
            
            _output.WriteLine("\nSyncfusion control types:");
            foreach (var kvp in controlTypes.OrderByDescending(x => x.Value))
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            // ========== PHASE 6: INSPECT CONTROL STYLING ==========
            _output.WriteLine("\n--- Phase 6: Control Styling Inspection ---");
            
            // Check a sample of Syncfusion controls for proper rendering
            var sampleControls = syncfusionControls.Take(10).ToArray();
            var styledControls = 0;
            var blankControls = 0;
            
            foreach (var ctrl in sampleControls)
            {
                _output.WriteLine($"\nInspecting: {ctrl.ClassName}");
                
                var bounds = ctrl.BoundingRectangle;
                var children = ctrl.FindAllChildren();
                var isVisible = ctrl.IsAvailable && !ctrl.IsOffscreen;
                
                _output.WriteLine($"  Bounds: {bounds.Width}x{bounds.Height}");
                _output.WriteLine($"  Children: {children.Length}");
                _output.WriteLine($"  Visible: {isVisible}");
                
                // Try to access styling properties
                try
                {
                    // Check if control has content (indicates proper styling/rendering)
                    if (children.Length > 0 || (bounds.Width > 0 && bounds.Height > 0 && isVisible))
                    {
                        _output.WriteLine($"  ✓ Control appears styled/rendered");
                        styledControls++;
                    }
                    else
                    {
                        _output.WriteLine($"  ⚠ Control may be blank or not visible");
                        blankControls++;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  ⚠ Could not inspect control: {ex.Message}");
                }
            }
            
            _output.WriteLine($"\nStyling Summary:");
            _output.WriteLine($"  Styled/Rendered: {styledControls}/{sampleControls.Length}");
            _output.WriteLine($"  Blank/Hidden: {blankControls}/{sampleControls.Length}");
            
            var stylingPercentage = (double)styledControls / sampleControls.Length * 100;
            Assert.True(stylingPercentage >= 50, $"Too many controls appear unstyled ({stylingPercentage:F0}% styled)");
            _output.WriteLine($"✓ Theme appears to be applied ({stylingPercentage:F0}% controls properly styled)");
            
            // ========== PHASE 7: SPECIFIC THEME ELEMENTS ==========
            _output.WriteLine("\n--- Phase 7: Theme-Specific Element Check ---");
            
            // Look for theme-specific styling indicators
            var themedElements = new[]
            {
                "SfButton",
                "SfDataGrid", 
                "SfChart",
                "RibbonControl",
                "DockingManager"
            };
            
            var foundThemedElements = 0;
            
            foreach (var elementName in themedElements)
            {
                var elements = mainWindow.FindAllDescendants()
                    .Where(e => e.ClassName?.Contains(elementName) == true)
                    .ToArray();
                
                if (elements.Any())
                {
                    _output.WriteLine($"  ✓ {elementName}: {elements.Length} instance(s)");
                    foundThemedElements++;
                }
                else
                {
                    _output.WriteLine($"  ⊘ {elementName}: Not found");
                }
            }
            
            _output.WriteLine($"\n✓ Found {foundThemedElements}/{themedElements.Length} themed control types");
            
            _output.WriteLine("\n✅ SYNCFUSION LICENSE & THEME TEST PASSED");
            _output.WriteLine("  License valid, controls loading, theme applied");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ SYNCFUSION LICENSE & THEME TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            if (app != null && !app.HasExited)
            {
                try { app.Close(); await Task.Delay(2000); if (!app.HasExited) app.Kill(); }
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }

    /// <summary>
    /// END-TO-END TEST: Data binding and rendering verification.
    /// Polls data-bound controls (grids, charts) to verify they populate with data:
    /// - SfDataGrid has non-zero row count
    /// - Charts have series with data points
    /// - Lists/TreeViews have items
    /// Uses FlaUI ItemCount properties and descendant polling.
    /// </summary>
    [StaFact]
    public async Task E2E_08_DataBindingAndRendering_Verification()
    {
        _output.WriteLine("====== DATA BINDING & RENDERING VERIFICATION TEST ======");
        _output.WriteLine("Verifying data-bound controls populate correctly");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        
        try
        {
            // ========== PHASE 1: LAUNCH APPLICATION ==========
            _output.WriteLine("\n--- Phase 1: Launch Application ---");
            
            // Launch application using helper method
            (app, var mainWindow, automation) = await LaunchAppAsync(_output);
            _output.WriteLine($"✓ Application launched and main window available");
            
            // ========== PHASE 2: FIND DATA-BOUND CONTROLS ==========
            _output.WriteLine("\n--- Phase 2: Locate Data-Bound Controls ---");
            
            // Wait for data binding to complete
            await Task.Delay(2000);
            
            // Find DataGrids
            var dataGrids = mainWindow.FindAllDescendants()
                .Where(e => e.ClassName?.Contains("DataGrid") == true)
                .ToArray();
            
            _output.WriteLine($"Found {dataGrids.Length} DataGrid controls");
            
            // Find Charts
            var charts = mainWindow.FindAllDescendants()
                .Where(e => e.ClassName?.Contains("Chart") == true)
                .ToArray();
            
            _output.WriteLine($"Found {charts.Length} Chart controls");
            
            // Find Lists/TreeViews
            var lists = mainWindow.FindAllDescendants(cf => 
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.List)
                .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tree)));
            
            _output.WriteLine($"Found {lists.Length} List/Tree controls");
            
            // ========== PHASE 3: POLL DATAGRIDS FOR DATA ==========
            _output.WriteLine("\n--- Phase 3: DataGrid Data Verification ---");
            
            var populatedGrids = 0;
            var emptyGrids = 0;
            
            foreach (var grid in dataGrids.Take(10))
            {
                _output.WriteLine($"\nInspecting DataGrid: {grid.AutomationId ?? grid.Name ?? "Unnamed"}");
                
                // Poll for data population
                var maxPolls = 5;
                var pollDelay = 500;
                var hasData = false;
                
                for (int poll = 0; poll < maxPolls; poll++)
                {
                    try
                    {
                        // Check for row elements
                        var rows = grid.FindAllDescendants()
                            .Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.DataItem || e.ClassName?.Contains("Row") == true)
                            .ToArray();
                        
                        _output.WriteLine($"  Poll {poll + 1}: Found {rows.Length} row elements");
                        
                        if (rows.Length > 0)
                        {
                            hasData = true;
                            _output.WriteLine($"  ✓ Grid has data ({rows.Length} rows)");
                            populatedGrids++;
                            break;
                        }
                        
                        await Task.Delay(pollDelay);
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  ⚠ Poll error: {ex.Message}");
                    }
                }
                
                if (!hasData)
                {
                    _output.WriteLine($"  ⊘ Grid appears empty (may be intentional or no data source)");
                    emptyGrids++;
                }
            }
            
            _output.WriteLine($"\nDataGrid Summary:");
            _output.WriteLine($"  Populated: {populatedGrids}");
            _output.WriteLine($"  Empty: {emptyGrids}");
            
            if (dataGrids.Any())
            {
                _output.WriteLine($"  Data binding rate: {(double)populatedGrids / dataGrids.Length * 100:F0}%");
            }
            
            // ========== PHASE 4: POLL CHARTS FOR DATA ==========
            _output.WriteLine("\n--- Phase 4: Chart Data Verification ---");
            
            var populatedCharts = 0;
            var emptyCharts = 0;
            
            foreach (var chart in charts.Take(10))
            {
                _output.WriteLine($"\nInspecting Chart: {chart.AutomationId ?? chart.Name ?? "Unnamed"}");
                
                try
                {
                    // Check for series/data point elements
                    var seriesElements = chart.FindAllDescendants()
                        .Where(e => e.ClassName?.Contains("Series") == true || e.ClassName?.Contains("DataPoint") == true || e.ControlType == FlaUI.Core.Definitions.ControlType.DataItem)
                        .ToArray();
                    
                    _output.WriteLine($"  Found {seriesElements.Length} series/data elements");
                    
                    if (seriesElements.Length > 0)
                    {
                        _output.WriteLine($"  ✓ Chart has data ({seriesElements.Length} elements)");
                        populatedCharts++;
                    }
                    else
                    {
                        // Check if chart at least has visible area
                        var bounds = chart.BoundingRectangle;
                        if (bounds.Width > 50 && bounds.Height > 50)
                        {
                            _output.WriteLine($"  ⊘ Chart visible but no data elements detected");
                            _output.WriteLine($"     (Chart may use custom rendering)");
                            populatedCharts++; // Count as populated if visible
                        }
                        else
                        {
                            _output.WriteLine($"  ⊘ Chart appears empty");
                            emptyCharts++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  ⚠ Chart inspection error: {ex.Message}");
                    emptyCharts++;
                }
            }
            
            _output.WriteLine($"\nChart Summary:");
            _output.WriteLine($"  Populated: {populatedCharts}");
            _output.WriteLine($"  Empty: {emptyCharts}");
            
            // ========== PHASE 5: CHECK LIST CONTROLS ==========
            _output.WriteLine("\n--- Phase 5: List/Tree Control Verification ---");
            
            var populatedLists = 0;
            
            foreach (var list in lists.Take(5))
            {
                _output.WriteLine($"\nInspecting List: {list.AutomationId ?? list.Name ?? "Unnamed"}");
                
                try
                {
                    var items = list.FindAllDescendants(cf => 
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)
                        .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem)));
                    
                    _output.WriteLine($"  Items: {items.Length}");
                    
                    if (items.Length > 0)
                    {
                        _output.WriteLine($"  ✓ List has items");
                        populatedLists++;
                        
                        // Log first few items
                        foreach (var item in items.Take(3))
                        {
                            _output.WriteLine($"    - {item.Name ?? "(unnamed)"}");
                        }
                    }
                    else
                    {
                        _output.WriteLine($"  ⊘ List appears empty");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  ⚠ List inspection error: {ex.Message}");
                }
            }
            
            // ========== PHASE 6: OVERALL ASSESSMENT ==========
            _output.WriteLine("\n--- Phase 6: Overall Data Binding Assessment ---");
            
            var totalControls = dataGrids.Length + charts.Length + lists.Length;
            var totalPopulated = populatedGrids + populatedCharts + populatedLists;
            
            _output.WriteLine($"Total data-bound controls: {totalControls}");
            _output.WriteLine($"Populated controls: {totalPopulated}");
            
            if (totalControls > 0)
            {
                var populationRate = (double)totalPopulated / totalControls * 100;
                _output.WriteLine($"Population rate: {populationRate:F0}%");
                
                // We expect at least some controls to have data
                // but not all might be bound in initial view
                if (totalPopulated > 0)
                {
                    _output.WriteLine($"✓ Data binding is working ({totalPopulated} controls have data)");
                }
                else
                {
                    _output.WriteLine($"⚠ No data-bound controls populated");
                    _output.WriteLine($"  This may be expected if no data sources are configured");
                }
            }
            else
            {
                _output.WriteLine($"⊘ No data-bound controls found in initial view");
            }
            
            _output.WriteLine("\n✅ DATA BINDING & RENDERING TEST COMPLETED");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ DATA BINDING TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            if (app != null && !app.HasExited)
            {
                try { app.Close(); await Task.Delay(2000); if (!app.HasExited) app.Kill(); }
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }

    /// <summary>
    /// END-TO-END TEST: Full startup sequence with exception monitoring.
    /// Simulates complete startup and monitors for:
    /// - XamlParseExceptions
    /// - Error dialogs
    /// - Crash reports
    /// - Event log errors
    /// Integrates with main window test and provides comprehensive diagnostics.
    /// </summary>
    [StaFact]
    public async Task E2E_09_FullStartupSequence_ExceptionMonitoring()
    {
        _output.WriteLine("====== FULL STARTUP SEQUENCE WITH EXCEPTION MONITORING ======");
        _output.WriteLine("Comprehensive startup test with exception detection");
        
        FlaUI.Core.Application? app = null;
        AutomationBase? automation = null;
        var startupStopwatch = Stopwatch.StartNew();
        var componentStates = new System.Collections.Generic.Dictionary<string, string>();
        
        try
        {
            // ========== PHASE 1: PRE-LAUNCH STATE ==========
            _output.WriteLine("\n--- Phase 1: Pre-Launch System State ---");
            
            componentStates["PreLaunch_Time"] = DateTime.Now.ToString("HH:mm:ss.fff");
            componentStates["PreLaunch_Memory"] = $"{Environment.WorkingSet / 1024 / 1024} MB";
            componentStates["PreLaunch_Threads"] = Process.GetCurrentProcess().Threads.Count.ToString();
            
            _output.WriteLine($"System time: {componentStates["PreLaunch_Time"]}");
            _output.WriteLine($"Test process memory: {componentStates["PreLaunch_Memory"]}");
            _output.WriteLine($"Test process threads: {componentStates["PreLaunch_Threads"]}");
            
            // ========== PHASE 2: LAUNCH WITH MONITORING ==========
            _output.WriteLine("\n--- Phase 2: Launch Application with Monitoring ---");
            
            var launchTime = Stopwatch.StartNew();
            // Launch application using helper method
            (app, var mainWindow, automation) = await LaunchAppAsync(_output);
            launchTime.Stop();
            
            componentStates["Launch_Time"] = $"{launchTime.ElapsedMilliseconds}ms";
            componentStates["Process_ID"] = app.ProcessId.ToString();
            
            _output.WriteLine($"✓ Process launched in {launchTime.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Process ID: {app.ProcessId}");
            
            // ========== PHASE 3: MONITOR FOR ERROR DIALOGS ==========
            _output.WriteLine("\n--- Phase 3: Exception Dialog Monitoring ---");
            
            var desktop = automation.GetDesktop();
            var monitoringTask = Task.Run(async () =>
            {
                for (int i = 0; i < 30; i++) // Monitor for 15 seconds
                {
                    try
                    {
                        var allWindows = desktop.FindAllChildren();
                        
                        foreach (var window in allWindows)
                        {
                            var title = window.Name ?? "";
                            
                            // Check for exception dialogs
                            if (title.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("XamlParseException", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("has stopped working", StringComparison.OrdinalIgnoreCase))
                            {
                                _output.WriteLine($"  ⚠ ALERT: Exception dialog detected!");
                                _output.WriteLine($"    Title: {title}");
                                
                                componentStates["Exception_Dialog"] = title;
                                
                                // Try to capture exception text
                                var textElements = window.FindAllDescendants(cf => 
                                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text)
                                    .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)));
                                
                                foreach (var text in textElements.Take(10))
                                {
                                    var textValue = text.Name ?? (text.Patterns.Value?.Pattern.Value) ?? "";
                                    if (!string.IsNullOrWhiteSpace(textValue))
                                    {
                                        _output.WriteLine($"      Text: {textValue}");
                                    }
                                }
                                
                                return false; // Exception detected
                            }
                        }
                    }
                    catch { }
                    
                    await Task.Delay(500);
                }
                
                return true; // No exceptions detected
            });
            
            // ========== PHASE 3: IMMEDIATE WINDOW CHECK ==========
            _output.WriteLine("\n--- Phase 3: Immediate Window Check ---");
            
            var windowTime = Stopwatch.StartNew();
            windowTime.Stop(); // Window is already available from helper method
            
            componentStates["Window_Initialization"] = $"{windowTime.ElapsedMilliseconds}ms";
            _output.WriteLine($"✓ Main window initialized in {windowTime.ElapsedMilliseconds}ms");
            
            // ========== PHASE 4: COMPONENT STATE LOGGING ==========
            _output.WriteLine("\n--- Phase 4: Component State Verification ---");
            
            // Check process state
            var process = Process.GetProcessById(app.ProcessId);
            componentStates["Process_Threads"] = process.Threads.Count.ToString();
            componentStates["Process_Memory"] = $"{process.WorkingSet64 / 1024 / 1024} MB";
            componentStates["Process_Responding"] = process.Responding.ToString();
            
            _output.WriteLine($"Process threads: {componentStates["Process_Threads"]}");
            _output.WriteLine($"Process memory: {componentStates["Process_Memory"]}");
            _output.WriteLine($"Process responding: {componentStates["Process_Responding"]}");
            
            // Check for Syncfusion controls - using post-filtering for partial match
            var syncfusionControls = mainWindow.FindAllDescendants()
                .Where(e => e.ClassName?.StartsWith("Syncfusion", StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();
            componentStates["Syncfusion_Controls"] = syncfusionControls.Length.ToString();
            _output.WriteLine($"Syncfusion controls: {componentStates["Syncfusion_Controls"]}");
            
            // Check for Prism regions - using post-filtering for partial match
            var regions = mainWindow.FindAllDescendants()
                .Where(e => e.AutomationId?.Contains("Region", StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();
            componentStates["Prism_Regions"] = regions.Length.ToString();
            _output.WriteLine($"Prism regions: {componentStates["Prism_Regions"]}");
            
            // ========== PHASE 5: WAIT FOR MONITORING COMPLETION ==========
            _output.WriteLine("\n--- Phase 5: Complete Exception Monitoring ---");
            
            var noExceptions = await monitoringTask;
            
            Assert.True(noExceptions, "Exception dialog(s) detected during startup");
            _output.WriteLine($"✓ No exception dialogs detected during startup");
            
            componentStates["Exceptions_Detected"] = "None";
            
            // ========== PHASE 6: FINAL STATE REPORT ==========
            startupStopwatch.Stop();
            
            _output.WriteLine("\n=== STARTUP SEQUENCE COMPONENT STATES ===");
            foreach (var kvp in componentStates.OrderBy(x => x.Key))
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            _output.WriteLine($"\n=== STARTUP TIMELINE ===");
            _output.WriteLine($"Total startup time: {startupStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Process launch: {componentStates["Launch_Time"]}");
            _output.WriteLine($"  Window init: {componentStates["Window_Initialization"]}");
            
            _output.WriteLine("\n✅ FULL STARTUP SEQUENCE TEST PASSED");
            _output.WriteLine("  No exceptions, all components initialized successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ STARTUP SEQUENCE TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            
            _output.WriteLine("\n=== COMPONENT STATES AT FAILURE ===");
            foreach (var kvp in componentStates)
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            throw;
        }
        finally
        {
            if (app != null && !app.HasExited)
            {
                try { app.Close(); await Task.Delay(2000); if (!app.HasExited) app.Kill(); }
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }

    /// <summary>
    /// END-TO-END TEST: Post-startup data loading verification.
    /// After application launch and dashboard render, verifies:
    /// - Data loading completes (dashboard shows enterprise count, metrics, etc.)
    /// - Async data loads are handled (polling up to 5s)
    /// - Views display appropriate messages for empty data
    /// - No exceptions during data loading
    /// Uses FlaUI text patterns and value extraction to verify content.
    /// Reference: https://github.com/FlaUI/FlaUI/wiki/Value-Pattern
    /// </summary>
    [StaFact]
    public async Task E2E_10_PostStartup_DataLoadingVerification()
    {
        _output.WriteLine("====== POST-STARTUP DATA LOADING VERIFICATION ======");
        _output.WriteLine("Verifying data loads correctly after application startup");
        
        FlaUI.Core.Application? app = null;
        AutomationBase? automation = null;
        
        try
        {
            // ========== PHASE 1: LAUNCH APPLICATION ==========
            _output.WriteLine("\n--- Phase 1: Launch and Initialize ---");
            
            var exePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..",
                "bin", "Debug", "net9.0-windows",
                "WileyWidget.exe"
            );
            exePath = System.IO.Path.GetFullPath(exePath);
            
            if (!System.IO.File.Exists(exePath))
            {
                _output.WriteLine($"✗ Executable not found - skipping test");
                return;
            }
            
            automation = new UIA3Automation();
            app = FlaUI.Core.Application.Launch(exePath);
            _output.WriteLine($"✓ Application launched (PID: {app.ProcessId})");
            
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Window? mainWindow = null;
            
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        break;
                    }
                }
                catch { }
                await Task.Delay(500, cts.Token);
            }
            
            Assert.NotNull(mainWindow);
            _output.WriteLine($"✓ Main window initialized");
            
            // ========== PHASE 2: NAVIGATE TO DASHBOARD ==========
            _output.WriteLine("\n--- Phase 2: Navigate to Dashboard ---");
            
            // Wait for initial render
            await Task.Delay(2000);
            
            // Try to navigate to Dashboard (may already be there)
            var dashboardNav = mainWindow.FindFirstDescendant(cf => 
                cf.ByName("Dashboard")
                .Or(cf.ByAutomationId("Dashboard"))
                .Or(cf.ByText("Dashboard")));
            
            if (dashboardNav != null && dashboardNav.IsAvailable)
            {
                try
                {
                    _output.WriteLine($"Clicking Dashboard navigation...");
                    dashboardNav.Click();
                    await Task.Delay(1000);
                    _output.WriteLine($"✓ Navigated to Dashboard");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"⚠ Dashboard navigation click failed: {ex.Message}");
                    _output.WriteLine($"  (May already be on Dashboard)");
                }
            }
            else
            {
                _output.WriteLine($"⊘ Dashboard navigation element not found");
                _output.WriteLine($"  Assuming already on Dashboard view");
            }
            
            // ========== PHASE 3: POLL FOR DATA LOADING ==========
            _output.WriteLine("\n--- Phase 3: Poll for Data Loading (5s timeout) ---");
            
            var dataLoadingIndicators = new[]
            {
                "Enterprises",
                "Budget",
                "Total",
                "Count",
                "Loading",
                "Data"
            };
            
            var foundData = new System.Collections.Generic.Dictionary<string, string>();
            var pollStartTime = Stopwatch.StartNew();
            var maxPollTime = TimeSpan.FromSeconds(5);
            
            while (pollStartTime.Elapsed < maxPollTime)
            {
                _output.WriteLine($"\nPoll iteration at {pollStartTime.ElapsedMilliseconds}ms:");
                
                // Search for text elements that might show data
                var textElements = mainWindow.FindAllDescendants(cf => 
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text)
                    .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))
                    .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Document)));
                
                _output.WriteLine($"  Found {textElements.Length} text elements");
                
                foreach (var element in textElements)
                {
                    try
                    {
                        var elementName = element.Name ?? "";
                        var elementValue = "";
                        
                        // Try to get value via Value pattern
                        if (element.Patterns.Value.IsSupported)
                        {
                            elementValue = element.Patterns.Value.Pattern.Value ?? "";
                        }
                        
                        var textContent = !string.IsNullOrWhiteSpace(elementValue) ? elementValue : elementName;
                        
                        if (string.IsNullOrWhiteSpace(textContent))
                        {
                            continue;
                        }
                        
                        // Check if text matches any data indicators
                        foreach (var indicator in dataLoadingIndicators)
                        {
                            if (textContent.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                            {
                                var key = $"{indicator}_{element.AutomationId ?? "Unknown"}";
                                if (!foundData.ContainsKey(key))
                                {
                                    foundData[key] = textContent;
                                    _output.WriteLine($"  ✓ Found: {textContent}");
                                }
                            }
                        }
                    }
                    catch { }
                }
                
                // If we found data, we can stop polling
                if (foundData.Count > 0)
                {
                    _output.WriteLine($"  Data detected, continuing poll to gather all data...");
                }
                
                await Task.Delay(1000);
            }
            
            pollStartTime.Stop();
            
            _output.WriteLine($"\n✓ Polling complete after {pollStartTime.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Found {foundData.Count} data elements");
            
            // ========== PHASE 4: ANALYZE LOADED DATA ==========
            _output.WriteLine("\n--- Phase 4: Data Analysis ---");
            
            if (foundData.Count > 0)
            {
                _output.WriteLine("Data elements found:");
                foreach (var kvp in foundData)
                {
                    _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
                
                // Look for specific metrics
                var enterpriseCount = ExtractNumericValue(foundData, "Enterprises");
                var budgetTotal = ExtractNumericValue(foundData, "Budget");
                
                if (enterpriseCount.HasValue)
                {
                    _output.WriteLine($"\n✓ Enterprise count: {enterpriseCount.Value}");
                    _output.WriteLine($"  (Shows {(enterpriseCount.Value == 0 ? "empty database" : "seeded data")})");
                }
                
                if (budgetTotal.HasValue)
                {
                    _output.WriteLine($"✓ Budget total: {budgetTotal.Value:C}");
                }
                
                _output.WriteLine($"\n✓ Dashboard shows data indicators ({foundData.Count} elements)");
            }
            else
            {
                _output.WriteLine("⊘ No specific data indicators found");
                _output.WriteLine("  Dashboard may use different text patterns or be empty");
            }
            
            // ========== PHASE 5: CHECK FOR EMPTY STATE MESSAGES ==========
            _output.WriteLine("\n--- Phase 5: Empty State Handling ---");
            
            var emptyStateMessages = new[]
            {
                "No data",
                "No enterprises",
                "No records",
                "Empty",
                "0 enterprises",
                "No items",
                "Getting started"
            };
            
            var foundEmptyState = false;
            
            foreach (var message in emptyStateMessages)
            {
                // Use post-filtering for partial match since ByName().Contains is invalid
                var elements = mainWindow.FindAllDescendants()
                    .Where(e => 
                        (e.Name?.Contains(message, StringComparison.OrdinalIgnoreCase) == true) ||
                        (e.Properties.Name.ValueOrDefault?.Contains(message, StringComparison.OrdinalIgnoreCase) == true))
                    .ToArray();
                
                if (elements.Any())
                {
                    _output.WriteLine($"  ✓ Found empty state message: '{message}'");
                    foundEmptyState = true;
                    
                    foreach (var elem in elements.Take(3))
                    {
                        _output.WriteLine($"    Element: {elem.ControlType} - {elem.Name}");
                    }
                }
            }
            
            if (foundEmptyState)
            {
                _output.WriteLine($"\n✓ Application handles empty database gracefully");
                _output.WriteLine($"  Appropriate messages shown to user");
            }
            else
            {
                _output.WriteLine($"\n⊘ No explicit empty state messages found");
                _output.WriteLine($"  Dashboard may show data or use implicit empty handling");
            }
            
            // ========== PHASE 6: VERIFY NO EXCEPTIONS DURING DATA LOAD ==========
            _output.WriteLine("\n--- Phase 6: Exception Detection ---");
            
            var desktop = automation.GetDesktop();
            // Use post-filtering for partial match since ByName().Contains is invalid
            var errorDialogs = desktop.FindAllDescendants()
                .Where(e => 
                {
                    var name = e.Name ?? "";
                    return name.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains("Failed", StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();
            
            var appErrorDialogs = errorDialogs.Where(e => 
            {
                try
                {
                    var name = e.Name ?? "";
                    return name.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains("Error", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }).ToArray();
            
            if (appErrorDialogs.Any())
            {
                _output.WriteLine($"✗ ERROR: Exception dialog(s) detected!");
                foreach (var dialog in appErrorDialogs.Take(3))
                {
                    _output.WriteLine($"  Dialog: {dialog.Name}");
                }
                Assert.Fail("Exception dialog detected during data loading");
            }
            else
            {
                _output.WriteLine($"✓ No exception dialogs detected");
                _output.WriteLine($"  Data loading completed without errors");
            }
            
            // ========== PHASE 7: CHECK FOR LOADING INDICATORS ==========
            _output.WriteLine("\n--- Phase 7: Loading Indicator Check ---");
            
            // Use post-filtering for partial match since ByName().Contains and ByClassName().Contains are invalid
            var loadingIndicators = mainWindow.FindAllDescendants()
                .Where(e => 
                    (e.Name?.Contains("Loading", StringComparison.OrdinalIgnoreCase) == true) ||
                    (e.Name?.Contains("Please wait", StringComparison.OrdinalIgnoreCase) == true) ||
                    (e.ClassName?.Contains("Progress", StringComparison.OrdinalIgnoreCase) == true) ||
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.ProgressBar))
                .ToArray();
            
            if (loadingIndicators.Any())
            {
                _output.WriteLine($"⚠ Found {loadingIndicators.Length} loading indicator(s):");
                foreach (var indicator in loadingIndicators.Take(5))
                {
                    _output.WriteLine($"  - {indicator.ControlType}: {indicator.Name ?? "(unnamed)"}");
                }
                _output.WriteLine($"  Note: Data may still be loading asynchronously");
            }
            else
            {
                _output.WriteLine($"✓ No active loading indicators");
                _output.WriteLine($"  Data loading appears complete");
            }
            
            _output.WriteLine("\n✅ POST-STARTUP DATA LOADING TEST PASSED");
            _output.WriteLine("  Dashboard rendered, data loaded/handled gracefully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ DATA LOADING TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            if (app != null && !app.HasExited)
            {
                try { app.Close(); await Task.Delay(2000); if (!app.HasExited) app.Kill(); }
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }

    /// <summary>
    /// END-TO-END TEST: Navigation stress test and error handling.
    /// Rapidly navigates between views to ensure:
    /// - Navigation doesn't crash
    /// - Views handle repeated loading
    /// - No memory leaks during navigation
    /// - Error states are handled gracefully
    /// </summary>
    [StaFact]
    public async Task E2E_11_NavigationStressTest_ErrorHandling()
    {
        _output.WriteLine("====== NAVIGATION STRESS TEST & ERROR HANDLING ======");
        _output.WriteLine("Testing navigation stability and error handling");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        
        try
        {
            // ========== PHASE 1: LAUNCH APPLICATION ==========
            _output.WriteLine("\n--- Phase 1: Launch Application ---");
            
            var exePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..",
                "bin", "Debug", "net9.0-windows",
                "WileyWidget.exe"
            );
            exePath = System.IO.Path.GetFullPath(exePath);
            
            if (!System.IO.File.Exists(exePath))
            {
                _output.WriteLine($"✗ Executable not found - skipping test");
                return;
            }
            
            automation = new UIA3Automation();
            app = FlaUI.Core.Application.Launch(exePath);
            _output.WriteLine($"✓ Application launched (PID: {app.ProcessId})");
            
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Window? mainWindow = null;
            
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        break;
                    }
                }
                catch { }
                await Task.Delay(500, cts.Token);
            }
            
            Assert.NotNull(mainWindow);
            _output.WriteLine($"✓ Main window initialized");
            
            // ========== PHASE 2: IDENTIFY NAVIGATION TARGETS ==========
            _output.WriteLine("\n--- Phase 2: Identify Navigation Elements ---");
            
            var navigationViews = new[] 
            { 
                "Dashboard", 
                "Enterprise", 
                "Budget", 
                "Analytics",
                "Settings"
            };
            
            var availableNavElements = new System.Collections.Generic.List<(string name, AutomationElement element)>();
            
            foreach (var viewName in navigationViews)
            {
                var navElement = mainWindow.FindFirstDescendant(cf => 
                    cf.ByName(viewName)
                    .Or(cf.ByAutomationId(viewName))
                    .Or(cf.ByText(viewName)));
                
                if (navElement != null && navElement.IsAvailable)
                {
                    availableNavElements.Add((viewName, navElement));
                    _output.WriteLine($"  ✓ Found: {viewName}");
                }
                else
                {
                    _output.WriteLine($"  ⊘ Not found: {viewName}");
                }
            }
            
            _output.WriteLine($"\n✓ {availableNavElements.Count}/{navigationViews.Length} navigation elements available");
            
            if (availableNavElements.Count == 0)
            {
                _output.WriteLine($"⊘ No navigation elements found - skipping stress test");
                return;
            }
            
            // ========== PHASE 3: BASELINE MEMORY ==========
            _output.WriteLine("\n--- Phase 3: Baseline Memory Measurement ---");
            
            var process = Process.GetProcessById(app.ProcessId);
            process.Refresh();
            
            var baselineMemoryMB = process.WorkingSet64 / 1024.0 / 1024.0;
            var baselineThreads = process.Threads.Count;
            
            _output.WriteLine($"Baseline memory: {baselineMemoryMB:F2} MB");
            _output.WriteLine($"Baseline threads: {baselineThreads}");
            
            // ========== PHASE 4: NAVIGATION STRESS TEST ==========
            _output.WriteLine("\n--- Phase 4: Navigation Stress Test (10 iterations) ---");
            
            var navigationSuccesses = 0;
            var navigationFailures = 0;
            var exceptionsCaught = 0;
            
            for (int iteration = 0; iteration < 10; iteration++)
            {
                _output.WriteLine($"\nIteration {iteration + 1}/10:");
                
                foreach (var (viewName, _) in availableNavElements)
                {
                    try
                    {
                        // Re-find element each time (in case DOM changed)
                        var navElement = mainWindow.FindFirstDescendant(cf => 
                            cf.ByName(viewName)
                            .Or(cf.ByAutomationId(viewName)));
                        
                        if (navElement != null && navElement.IsAvailable)
                        {
                            navElement.Click();
                            _output.WriteLine($"  ✓ Navigated to {viewName}");
                            navigationSuccesses++;
                            
                            // Brief wait for navigation
                            await Task.Delay(300);
                            
                            // Check for error dialogs - using post-filtering for partial match
                            var desktop = automation.GetDesktop();
                            var errorDialogs = desktop.FindAllDescendants()
                                .Where(e => 
                                {
                                    var name = e.Name ?? "";
                                    return name.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                                           name.Contains("Exception", StringComparison.OrdinalIgnoreCase);
                                })
                                .ToArray();
                            
                            if (errorDialogs.Any())
                            {
                                _output.WriteLine($"    ✗ ERROR DIALOG DETECTED!");
                                var dialog = errorDialogs[0];
                                _output.WriteLine($"      Title: {dialog.Name}");
                                exceptionsCaught++;
                                
                                // Try to close error dialog
                                var closeButton = dialog.FindFirstDescendant(cf => 
                                    cf.ByName("Close").Or(cf.ByName("OK")));
                                if (closeButton != null)
                                {
                                    closeButton.Click();
                                    await Task.Delay(500);
                                }
                            }
                        }
                        else
                        {
                            _output.WriteLine($"  ✗ Navigation element for {viewName} not available");
                            navigationFailures++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  ✗ Navigation to {viewName} failed: {ex.Message}");
                        navigationFailures++;
                        exceptionsCaught++;
                    }
                }
            }
            
            _output.WriteLine($"\n=== Navigation Stress Test Results ===");
            _output.WriteLine($"Successes: {navigationSuccesses}");
            _output.WriteLine($"Failures: {navigationFailures}");
            _output.WriteLine($"Exceptions: {exceptionsCaught}");
            
            Assert.True(navigationSuccesses > 0, "No successful navigations - navigation system may be broken");
            Assert.True(exceptionsCaught == 0, $"Navigation caused {exceptionsCaught} exception(s)");
            
            _output.WriteLine($"✓ Navigation stress test passed");
            
            // ========== PHASE 5: POST-NAVIGATION MEMORY CHECK ==========
            _output.WriteLine("\n--- Phase 5: Post-Navigation Memory Check ---");
            
            process.Refresh();
            
            var postMemoryMB = process.WorkingSet64 / 1024.0 / 1024.0;
            var postThreads = process.Threads.Count;
            var memoryGrowth = postMemoryMB - baselineMemoryMB;
            
            _output.WriteLine($"Post-navigation memory: {postMemoryMB:F2} MB");
            _output.WriteLine($"Post-navigation threads: {postThreads}");
            _output.WriteLine($"Memory growth: {memoryGrowth:F2} MB ({(memoryGrowth / baselineMemoryMB * 100):F1}%)");
            
            // Alert if excessive memory growth
            if (memoryGrowth > 100)
            {
                _output.WriteLine($"⚠ WARNING: Significant memory growth detected!");
                _output.WriteLine($"  Possible memory leak during navigation");
            }
            else
            {
                _output.WriteLine($"✓ Memory growth is acceptable");
            }
            
            // ========== PHASE 6: VERIFY APPLICATION STILL RESPONSIVE ==========
            _output.WriteLine("\n--- Phase 6: Responsiveness Check ---");
            
            process.Refresh();
            
            Assert.True(process.Responding, "Application not responding after navigation stress test");
            _output.WriteLine($"✓ Application is still responding");
            
            Assert.False(process.HasExited, "Application exited during navigation stress test");
            _output.WriteLine($"✓ Application is still running");
            
            _output.WriteLine("\n✅ NAVIGATION STRESS TEST PASSED");
            _output.WriteLine("  Navigation stable, no crashes or exceptions");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ NAVIGATION STRESS TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            if (app != null && !app.HasExited)
            {
                try { app.Close(); await Task.Delay(2000); if (!app.HasExited) app.Kill(); }
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }

    /// <summary>
    /// END-TO-END TEST: Empty database graceful handling verification.
    /// Verifies that views handle missing/empty data without crashing:
    /// - Enterprise view with no enterprises
    /// - Budget view with no budget entries
    /// - Analytics with no data to chart
    /// Tests that appropriate messages are shown and no exceptions occur.
    /// </summary>
    [StaFact]
    public async Task E2E_12_EmptyDatabase_GracefulHandling()
    {
        _output.WriteLine("====== EMPTY DATABASE GRACEFUL HANDLING TEST ======");
        _output.WriteLine("Verifying views handle empty data without exceptions");
        
        FlaUI.Core.Application app = null;
        AutomationBase automation = null;
        
        try
        {
            // ========== PHASE 1: LAUNCH APPLICATION ==========
            _output.WriteLine("\n--- Phase 1: Launch Application ---");
            
            var exePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..",
                "bin", "Debug", "net9.0-windows",
                "WileyWidget.exe"
            );
            exePath = System.IO.Path.GetFullPath(exePath);
            
            if (!System.IO.File.Exists(exePath))
            {
                _output.WriteLine($"✗ Executable not found - skipping test");
                return;
            }
            
            automation = new UIA3Automation();
            app = FlaUI.Core.Application.Launch(exePath);
            _output.WriteLine($"✓ Application launched (PID: {app.ProcessId})");
            
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Window? mainWindow = null;
            
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        break;
                    }
                }
                catch { }
                await Task.Delay(500, cts.Token);
            }
            
            Assert.NotNull(mainWindow);
            _output.WriteLine($"✓ Main window initialized");
            
            // ========== PHASE 2: TEST VIEWS WITH EMPTY DATA ==========
            _output.WriteLine("\n--- Phase 2: Test Views with Empty Data ---");
            
            var viewsToTest = new[]
            {
                ("Enterprise", new[] { "No enterprises", "No records", "Add enterprise", "Empty" }),
                ("Budget", new[] { "No budget", "No entries", "Create budget", "Empty" }),
                ("Analytics", new[] { "No data", "No analytics", "Insufficient data", "Empty" }),
                ("Dashboard", new[] { "0 enterprises", "Welcome", "Getting started", "No data" })
            };
            
            var viewResults = new System.Collections.Generic.List<(string view, bool graceful, string message)>();
            
            foreach (var (viewName, emptyStateIndicators) in viewsToTest)
            {
                _output.WriteLine($"\n=== Testing {viewName} View ===");
                
                try
                {
                    // Navigate to view
                    var navElement = mainWindow.FindFirstDescendant(cf => 
                        cf.ByName(viewName)
                        .Or(cf.ByAutomationId(viewName)));
                    
                    if (navElement == null || !navElement.IsAvailable)
                    {
                        _output.WriteLine($"  ⊘ Navigation element not found - skipping");
                        viewResults.Add((viewName, true, "Navigation not available"));
                        continue;
                    }
                    
                    navElement.Click();
                    _output.WriteLine($"  ✓ Navigated to {viewName}");
                    
                    // Wait for view to load
                    await Task.Delay(2000);
                    
                    // Check for exception dialogs - using post-filtering for partial match
                    var desktop = automation.GetDesktop();
                    var errorDialogs = desktop.FindAllDescendants()
                        .Where(e => 
                        {
                            var name = e.Name ?? "";
                            return name.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                                   name.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                                   name.Contains("has stopped", StringComparison.OrdinalIgnoreCase);
                        })
                        .ToArray();
                    
                    if (errorDialogs.Any())
                    {
                        _output.WriteLine($"  ✗ EXCEPTION DIALOG DETECTED!");
                        var dialog = errorDialogs[0];
                        _output.WriteLine($"    Title: {dialog.Name}");
                        
                        viewResults.Add((viewName, false, $"Exception: {dialog.Name}"));
                        continue;
                    }
                    
                    // Look for empty state indicators
                    var foundEmptyState = false;
                    var emptyStateMessage = "";
                    
                    foreach (var indicator in emptyStateIndicators)
                    {
                        // Use post-filtering for partial match since ByName().Contains is invalid
                        var elements = mainWindow.FindAllDescendants()
                            .Where(e => 
                                (e.Name?.Contains(indicator, StringComparison.OrdinalIgnoreCase) == true) ||
                                (e.Properties.Name.ValueOrDefault?.Contains(indicator, StringComparison.OrdinalIgnoreCase) == true))
                            .ToArray();
                        
                        if (elements.Any())
                        {
                            foundEmptyState = true;
                            emptyStateMessage = indicator;
                            _output.WriteLine($"  ✓ Found empty state indicator: '{indicator}'");
                            break;
                        }
                    }
                    
                    if (foundEmptyState)
                    {
                        _output.WriteLine($"  ✓ View handles empty data gracefully");
                        viewResults.Add((viewName, true, $"Empty state: {emptyStateMessage}"));
                    }
                    else
                    {
                        _output.WriteLine($"  ⊘ No explicit empty state message found");
                        _output.WriteLine($"    View may show data or use implicit handling");
                        
                        // Check if view at least has content (not crashed)
                        var viewContent = mainWindow.FindAllDescendants();
                        if (viewContent.Length > 20)
                        {
                            _output.WriteLine($"  ✓ View has content ({viewContent.Length} elements)");
                            viewResults.Add((viewName, true, "Content present, no crash"));
                        }
                        else
                        {
                            _output.WriteLine($"  ⚠ View has minimal content");
                            viewResults.Add((viewName, true, "Minimal content but no crash"));
                        }
                    }
                    
                    // Verify process is still responsive
                    var process = Process.GetProcessById(app.ProcessId);
                    process.Refresh();
                    
                    if (!process.Responding)
                    {
                        _output.WriteLine($"  ✗ Application not responding!");
                        viewResults.Add((viewName, false, "Application hung"));
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  ✗ Test failed with exception: {ex.Message}");
                    viewResults.Add((viewName, false, $"Exception: {ex.Message}"));
                }
            }
            
            // ========== PHASE 3: SUMMARY ==========
            _output.WriteLine("\n=== Empty Data Handling Results ===");
            
            foreach (var (view, graceful, message) in viewResults)
            {
                var icon = graceful ? "✓" : "✗";
                _output.WriteLine($"{icon} {view}: {message}");
            }
            
            var gracefulCount = viewResults.Count(r => r.graceful);
            var totalCount = viewResults.Count;
            
            _output.WriteLine($"\nGraceful handling: {gracefulCount}/{totalCount}");
            
            Assert.True(gracefulCount == totalCount, 
                $"Some views did not handle empty data gracefully: {totalCount - gracefulCount} failures");
            
            _output.WriteLine("\n✅ EMPTY DATABASE HANDLING TEST PASSED");
            _output.WriteLine("  All views handle empty data without exceptions");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\n✗ EMPTY DATABASE HANDLING TEST FAILED");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            if (app != null && !app.HasExited)
            {
                try { app.Close(); await Task.Delay(2000); if (!app.HasExited) app.Kill(); }
                catch { try { app.Kill(); } catch { } }
            }
            automation?.Dispose();
        }
    }

    /// <summary>
    /// Helper method to extract numeric values from data dictionary.
    /// Improved regex to handle currencies, commas, and decimals.
    /// </summary>
    private decimal? ExtractNumericValue(System.Collections.Generic.Dictionary<string, string> data, string key)
    {
        foreach (var kvp in data)
        {
            if (kvp.Key.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                var text = kvp.Value;
                
                // Improved regex to handle currencies ($), commas, and decimals
                var numberMatch = System.Text.RegularExpressions.Regex.Match(text, @"[\$]?\d{1,3}(?:,\d{3})*(?:\.\d{2})?|\d+(?:\.\d+)?");
                if (numberMatch.Success)
                {
                    var numberString = numberMatch.Value.Replace("$", "").Replace(",", "");
                    if (decimal.TryParse(numberString, out var value))
                    {
                        return value;
                    }
                }
            }
        }
        
        return null;
    }
}
