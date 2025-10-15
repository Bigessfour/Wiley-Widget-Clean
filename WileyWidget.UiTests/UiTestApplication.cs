using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;
using WileyWidget.Tests;

namespace WileyWidget.UiTests;

/// <summary>
/// Base class for UI tests that require dependency injection.
/// Initializes the DI container and WPF Application context.
/// Provides robust app launch and cleanup methods to eliminate duplication.
/// </summary>
public abstract class UiTestApplication : TestApplication
{
    private static readonly IConfiguration Configuration;
    private static readonly string ErrorLogPath;
    
    static UiTestApplication()
    {
        // Initialize DI container once for all UI tests
        TestDiSetup.Initialize();
        
        // Load configuration
        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.UiTests.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
            
        // Setup error logging
        ErrorLogPath = Environment.GetEnvironmentVariable("TEST_ERROR_LOG_PATH") ?? "./test-logs/ui-test-errors.log";
        Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath) ?? "./test-logs");
    }

    /// <summary>
    /// Initializes a new instance of the UiTestApplication class.
    /// </summary>
    protected UiTestApplication()
    {
        // DI container is already initialized in static constructor
    }
    
    /// <summary>
    /// Gets the executable path using robust resolution with configuration fallbacks.
    /// Priority: Configuration > Relative paths > Environment variables
    /// </summary>
    protected virtual string GetExecutablePath()
    {
        // Try configuration first
        var configPath = Configuration?["UiTests:ExecutablePath:RelativePath"];
        if (!string.IsNullOrEmpty(configPath))
        {
            var fullPath = Path.Combine(GetProjectRoot(), configPath);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        
        // Try fallback paths from configuration
        var fallbackPaths = Configuration?.GetSection("UiTests:ExecutablePath:FallbackPaths")?.Get<string[]>();
        if (fallbackPaths != null)
        {
            foreach (var fallback in fallbackPaths)
            {
                var fullPath = Path.Combine(GetProjectRoot(), fallback);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }
        
        // Default resolution: navigate from test assembly to project root
        var projectRoot = GetProjectRoot();
        var defaultPaths = new[]
        {
            Path.Combine(projectRoot, "bin", "Debug", "net9.0-windows", "WileyWidget.exe"),
            Path.Combine(projectRoot, "bin", "Release", "net9.0-windows", "WileyWidget.exe"),
            Path.Combine(projectRoot, "bin", "Debug", "net8.0-windows", "WileyWidget.exe"),
            Path.Combine(projectRoot, "bin", "Release", "net8.0-windows", "WileyWidget.exe")
        };
        
        foreach (var path in defaultPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        
        throw new FileNotFoundException(
            $"WileyWidget.exe not found. Searched paths:\n" +
            $"  - {string.Join("\n  - ", defaultPaths)}\n" +
            "Please build the WileyWidget project before running UI tests.");
    }
    
    /// <summary>
    /// Gets the project root directory.
    /// </summary>
    private string GetProjectRoot()
    {
        // Start from test assembly location and navigate up to project root
        var testAssemblyPath = AppDomain.CurrentDomain.BaseDirectory;
        var directory = Directory.GetParent(testAssemblyPath);
        
        // Navigate up: bin/Debug/net9.0-windows -> bin/Debug -> bin -> project root
        while (directory != null && directory.Parent != null)
        {
            if (File.Exists(Path.Combine(directory.Parent.FullName, "WileyWidget.sln")))
            {
                return directory.Parent.FullName;
            }
            directory = directory.Parent;
        }
        
        throw new DirectoryNotFoundException("Could not locate WileyWidget project root");
    }
    
    /// <summary>
    /// Logs detailed error information to both console and error log file.
    /// </summary>
    protected void LogError(string message, Exception? ex = null, string? testName = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var threadInfo = $"Thread: {Thread.CurrentThread.ManagedThreadId} ({Thread.CurrentThread.Name})";
        var testInfo = testName != null ? $"Test: {testName}" : "";
        
        var logMessage = $"[{timestamp}] ERROR - {message}";
        if (!string.IsNullOrEmpty(testInfo)) logMessage += $" | {testInfo}";
        logMessage += $" | {threadInfo}";
        
        if (ex != null)
        {
            logMessage += $"\nException: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}";
            if (ex.InnerException != null)
            {
                logMessage += $"\nInner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            }
        }
        
        // Log to console
        Console.Error.WriteLine(logMessage);
        
        // Log to file
        try
        {
            File.AppendAllText(ErrorLogPath, logMessage + "\n\n");
        }
        catch (Exception logEx)
        {
            Console.Error.WriteLine($"Failed to write to error log: {logEx.Message}");
        }
        
        // Log to debug output
        System.Diagnostics.Debug.WriteLine(logMessage);
    }
    
    /// <summary>
    /// Logs diagnostic information for UI test debugging.
    /// </summary>
    protected void LogDiagnostic(string message, string? testName = null)
    {
        if (Environment.GetEnvironmentVariable("UI_TEST_DIAGNOSTICS") != "true")
            return;
            
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] DIAGNOSTIC - {message}";
        if (testName != null) logMessage += $" | Test: {testName}";
        
        Console.WriteLine(logMessage);
        System.Diagnostics.Debug.WriteLine(logMessage);
    }
    
    /// <summary>
    /// Enhanced RunOnUIThread with error logging and diagnostics.
    /// </summary>
    protected new void RunOnUIThread(Action action)
    {
        try
        {
            LogDiagnostic("Starting UI thread operation");
            base.RunOnUIThread(action);
            LogDiagnostic("UI thread operation completed successfully");
        }
        catch (Exception ex)
        {
            LogError("UI thread operation failed", ex);
            throw;
        }
    }
    
    /// <summary>
    /// Enhanced RunOnUIThreadAsync with error logging and diagnostics.
    /// </summary>
    protected new Task RunOnUIThreadAsync(Func<Task> action)
    {
        return RunOnUIThreadAsync(action, null);
    }
    
    /// <summary>
    /// Enhanced RunOnUIThreadAsync with error logging and diagnostics.
    /// </summary>
    protected Task RunOnUIThreadAsync(Func<Task> action, string? testName)
    {
        try
        {
            LogDiagnostic("Starting async UI thread operation", testName);
            var task = base.RunOnUIThreadAsync(async () =>
            {
                try
                {
                    await action();
                    LogDiagnostic("Async UI thread operation completed successfully", testName);
                }
                catch (Exception ex)
                {
                    LogError("Async UI thread operation failed", ex, testName);
                    throw;
                }
            });
            return task;
        }
        catch (Exception ex)
        {
            LogError("Failed to start async UI thread operation", ex, testName);
            throw;
        }
    }
    
    /// <summary>
    /// Launches the WileyWidget application with robust window detection and error handling.
    /// This method eliminates duplication across test files and provides consistent app initialization.
    /// </summary>
    /// <param name="output">Test output helper for logging</param>
    /// <param name="customTimeout">Optional custom timeout (uses config default if not specified)</param>
    /// <returns>Tuple of (Application, MainWindow, Automation) for test usage</returns>
    protected async Task<(FlaUI.Core.Application app, FlaUI.Core.AutomationElements.Window mainWindow, AutomationBase automation)> LaunchAppAsync(
        ITestOutputHelper output,
        TimeSpan? customTimeout = null)
    {
        var exePath = GetExecutablePath();
        output.WriteLine($"Launching app from: {exePath}");
        
        if (!File.Exists(exePath))
        {
            Assert.Fail($"Executable not found at {exePath}");
        }
        
        // Get timeout from configuration or use custom
        var timeout = customTimeout ?? UiTestConstants.Timeouts.AdjustForCi(UiTestConstants.Timeouts.AppLaunch);
        
        // Initialize automation
        var automation = new UIA3Automation();
        output.WriteLine("✓ Initialized UIA3 automation");
        
        // Launch application
        FlaUI.Core.Application app = null;
        try
        {
            app = FlaUI.Core.Application.Launch(exePath);
            output.WriteLine($"✓ Launched app (PID: {app.ProcessId})");
        }
        catch (Exception ex)
        {
            output.WriteLine($"✗ Failed to launch app: {ex.Message}");
            automation?.Dispose();
            throw;
        }
        
        // Wait for main window with retry logic
        FlaUI.Core.AutomationElements.Window? mainWindow = null;
        var cts = new CancellationTokenSource(timeout);
        var sw = Stopwatch.StartNew();
        
        output.WriteLine($"Waiting for main window (timeout: {timeout.TotalSeconds}s)...");
        
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                if (mainWindow != null && mainWindow.IsAvailable)
                {
                    output.WriteLine($"✓ Main window found ({sw.Elapsed.TotalSeconds:F2}s)");
                    
                    // Verify window is responsive using helper
                    if (UiTestHelpers.WaitForElementResponsive(mainWindow, TimeSpan.FromSeconds(5)))
                    {
                        output.WriteLine("✓ Main window is responsive");
                        return (app, mainWindow, automation);
                    }
                    
                    output.WriteLine("⚠ Main window found but not responsive, retrying...");
                }
            }
            catch (Exception ex)
            {
                output.WriteLine($"⚠ Window detection attempt failed: {ex.Message}");
            }
            
            // Check for error dialogs
            try
            {
                var allWindows = app.GetAllTopLevelWindows(automation);
                foreach (var window in allWindows)
                {
                    var title = window.Title ?? "";
                    if (UiTestConstants.ErrorDialogKeywords.Keywords.Any(keyword => 
                        title.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        output.WriteLine($"✗ Error dialog detected: {title}");
                        await CleanupAsync(app, mainWindow, automation, output);
                        Assert.Fail($"Application crashed with error dialog: {title}");
                    }
                }
            }
            catch
            {
                // Ignore errors during error dialog detection
            }
            
            await Task.Delay(UiTestConstants.Timeouts.RetryInterval, cts.Token);
        }
        
        // Timeout reached
        output.WriteLine($"✗ Main window not found after {timeout.TotalSeconds}s");
        await CleanupAsync(app, mainWindow, automation, output);
        Assert.Fail($"Main window not found within {timeout.TotalSeconds}s. App may have crashed or failed to initialize.");
        
        // Unreachable, but required for compiler
        return (null, null, null);
    }
    
    /// <summary>
    /// Cleans up application resources with robust error handling.
    /// Should be called in test Dispose or finally blocks.
    /// </summary>
    protected async Task CleanupAsync(
        FlaUI.Core.Application app, 
        FlaUI.Core.AutomationElements.Window mainWindow, 
        AutomationBase automation,
        ITestOutputHelper output)
    {
        output?.WriteLine("Cleaning up test resources...");
        
        // Close main window first
        if (mainWindow != null && !mainWindow.IsOffscreen)
        {
            try
            {
                mainWindow.Close();
                output?.WriteLine("✓ Main window closed");
            }
            catch (Exception ex)
            {
                output?.WriteLine($"⚠ Failed to close main window: {ex.Message}");
            }
        }
        
        // Close application
        if (app != null)
        {
            try
            {
                if (!app.HasExited)
                {
                    app.Close();
                    output?.WriteLine("✓ Application closed gracefully");
                    
                    // Wait for graceful exit
                    await Task.Delay(1000);
                }
                
                // Force kill if still running
                if (!app.HasExited)
                {
                    app.Kill();
                    output?.WriteLine("⚠ Application force-killed");
                }
            }
            catch (Exception ex)
            {
                output?.WriteLine($"⚠ Failed to close application: {ex.Message}");
                
                // Last resort: kill by process ID
                try
                {
                    var process = Process.GetProcessById(app.ProcessId);
                    process.Kill();
                    output?.WriteLine("✓ Process killed by ID");
                }
                catch
                {
                    // Process already gone
                }
            }
            finally
            {
                app?.Dispose();
            }
        }
        
        // Dispose automation
        automation?.Dispose();
        output?.WriteLine("✓ Automation disposed");
    }
}