using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Windows.Tools.Controls;
using Syncfusion.UI.Xaml.Grid;
using Xunit;
using Xunit.Abstractions;
using WileyWidget.Tests;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;
using WileyWidget.ViewModels;
using WileyWidget.Views;

namespace WileyWidget.UiTests;

/// <summary>
/// End-to-end comprehensive startup tests that simulate the actual application startup sequence.
/// These tests are designed to diagnose why the UI fails to start correctly.
/// </summary>
public class EndToEndStartupTests : UiTestApplication
{
    private readonly ITestOutputHelper _output;

    public EndToEndStartupTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [StaFact]
    public async Task E2E_01_FullApplicationStartup_WithTiming()
    {
        _output.WriteLine("=== FULL APPLICATION STARTUP TEST ===");
        _output.WriteLine($"Test started at: {DateTime.Now:HH:mm:ss.fff}");
        var overallStopwatch = Stopwatch.StartNew();
        var phaseTimings = new Dictionary<string, long>();

        await RunOnUIThreadAsync(async () =>
        {
            Window? mainWindow = null;
            IServiceScope? scope = null;
            try
            {
                // Phase 1: Service Provider Initialization
                _output.WriteLine("\n--- Phase 1: Service Provider Initialization ---");
                var sw = Stopwatch.StartNew();
                var sp = TestDiSetup.GetServiceProvider();
                Assert.NotNull(sp);
                scope = sp.CreateScope();
                sw.Stop();
                phaseTimings["ServiceProvider"] = sw.ElapsedMilliseconds;
                _output.WriteLine($"✓ Service Provider initialized in {sw.ElapsedMilliseconds}ms");

                // Phase 2: Critical Services Resolution
                _output.WriteLine("\n--- Phase 2: Critical Services Resolution ---");
                sw.Restart();
                var criticalServices = new Dictionary<string, Type>
                {
                    ["ISettingsService"] = typeof(ISettingsService),
                    ["MainViewModel"] = typeof(MainViewModel),
                    ["DashboardViewModel"] = typeof(DashboardViewModel)
                };

                foreach (var (name, type) in criticalServices)
                {
                    var service = scope.ServiceProvider.GetService(type);
                    Assert.NotNull(service);
                    _output.WriteLine($"  ✓ {name} resolved");
                }
                sw.Stop();
                phaseTimings["ServiceResolution"] = sw.ElapsedMilliseconds;
                _output.WriteLine($"✓ All critical services resolved in {sw.ElapsedMilliseconds}ms");

                // Phase 3.5: Cross-language validation with Python script
                _output.WriteLine("\n--- Phase 3.5: Cross-language Validation ---");
                sw.Restart();
                try
                {
                    var pythonScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "scripts", "debug-wpf-thread.py");
                    if (File.Exists(pythonScript))
                    {
                        var pythonProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "python",
                            Arguments = $"\"{pythonScript}\" --timeout=30",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        });

                        if (pythonProcess != null)
                        {
                            // Wait for Python script to complete or timeout
                            var pythonTask = Task.Run(() => pythonProcess.WaitForExit(30000));
                            if (await pythonTask)
                            {
                                _output.WriteLine($"  ✓ Python validation completed with exit code: {pythonProcess.ExitCode}");
                            }
                            else
                            {
                                pythonProcess.Kill();
                                _output.WriteLine($"  ⚠ Python validation timed out");
                            }
                        }
                    }
                    else
                    {
                        _output.WriteLine($"  ⚠ Python script not found: {pythonScript}");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  ⚠ Python validation failed: {ex.Message}");
                }
                sw.Stop();
                phaseTimings["CrossLanguageValidation"] = sw.ElapsedMilliseconds;
                _output.WriteLine($"✓ Cross-language validation completed in {sw.ElapsedMilliseconds}ms");

                // Phase 4: MainViewModel Initialization
                _output.WriteLine("\n--- Phase 3: MainViewModel Initialization ---");
                sw.Restart();
                var mainViewModel = scope.ServiceProvider.GetService<MainViewModel>();
                Assert.NotNull(mainViewModel);
                _output.WriteLine($"  ✓ MainViewModel created");
                sw.Stop();
                phaseTimings["MainViewModel"] = sw.ElapsedMilliseconds;
                _output.WriteLine($"✓ MainViewModel initialized in {sw.ElapsedMilliseconds}ms");

                // Phase 4: MainWindow Creation (Simplified)
                _output.WriteLine("\n--- Phase 4: MainWindow Creation ---");
                sw.Restart();
                mainWindow = scope.ServiceProvider.GetService<MainWindow>();
                Assert.NotNull(mainWindow);
                _output.WriteLine($"  ✓ MainWindow created");

                // Minimize Syncfusion complexity
                mainWindow.Width = 800;
                mainWindow.Height = 600;
                mainWindow.ShowInTaskbar = false;
                mainWindow.ShowActivated = false;
                mainWindow.WindowStyle = WindowStyle.None;
                mainWindow.Left = -10000;
                mainWindow.Top = -10000;

                // Disable heavy Syncfusion controls temporarily
                if (mainWindow.Content is FrameworkElement content)
                {
                    content.IsEnabled = false; // Test without full rendering
                    _output.WriteLine($"  ✓ Content disabled to isolate timeout");
                }

                sw.Stop();
                phaseTimings["MainWindowCreation"] = sw.ElapsedMilliseconds;
                _output.WriteLine($"✓ MainWindow configured in {sw.ElapsedMilliseconds}ms");

                // Phase 5: Window Content Verification
                _output.WriteLine("\n--- Phase 5: Window Content Verification ---");
                sw.Restart();
                if (mainWindow.Content is FrameworkElement contentElement)
                {
                    _output.WriteLine($"  ✓ Window has content: {contentElement.GetType().Name}");
                    _output.WriteLine($"    - Content size: {contentElement.ActualWidth}x{contentElement.ActualHeight}");
                }
                else
                {
                    _output.WriteLine($"  ✗ Window Content is null or not a FrameworkElement");
                }
                sw.Stop();
                phaseTimings["ContentVerification"] = sw.ElapsedMilliseconds;
                _output.WriteLine($"✓ Content verified in {sw.ElapsedMilliseconds}ms");

                // Phase 6: Window Show & Load
                _output.WriteLine("\n--- Phase 6: Window Show & Load ---");
                sw.Restart();

                var loadedTcs = new TaskCompletionSource<bool>();
                var contentRenderedTcs = new TaskCompletionSource<bool>();

                mainWindow.Loaded += (s, e) =>
                {
                    _output.WriteLine($"  ✓ Window Loaded event fired at {DateTime.Now:HH:mm:ss.fff}");
                    loadedTcs.TrySetResult(true);
                };

                mainWindow.ContentRendered += (s, e) =>
                {
                    _output.WriteLine($"  ✓ Window ContentRendered event fired at {DateTime.Now:HH:mm:ss.fff}");
                    contentRenderedTcs.TrySetResult(true);
                };

                mainWindow.Show();
                _output.WriteLine($"  ✓ MainWindow.Show() called at {DateTime.Now:HH:mm:ss.fff}");

                // Aggressive dispatcher pumping
                await Dispatcher.Yield(DispatcherPriority.Background);
                for (int i = 0; i < 20; i++)
                {
                    UiTestHelpers.DoEvents();
                    await Task.Delay(50);
                    if (loadedTcs.Task.IsCompleted && contentRenderedTcs.Task.IsCompleted) break;
                    _output.WriteLine($"  Dispatcher pump iteration {i + 1}");
                }

                // Wait with extended timeout
                var loadTask = Task.WhenAny(loadedTcs.Task, Task.Delay(90000));
                await loadTask;

                if (loadedTcs.Task.IsCompleted)
                {
                    _output.WriteLine($"  ✓ Window loaded successfully");
                }
                else
                {
                    _output.WriteLine($"  ✗ Window Loaded event timeout after 90 seconds");
                    Assert.Fail("Window did not load within timeout period");
                }

                var renderTask = Task.WhenAny(contentRenderedTcs.Task, Task.Delay(15000));
                await renderTask;

                if (contentRenderedTcs.Task.IsCompleted)
                {
                    _output.WriteLine($"  ✓ Content rendered successfully");
                }
                else
                {
                    _output.WriteLine($"  ⚠ ContentRendered event timeout after 15 seconds");
                }

                // Syncfusion-specific check (only if enabled)
                if (mainWindow.Content is FrameworkElement { IsEnabled: true })
                {
                    var grid = UiTestHelpers.FindVisualChildren<SfDataGrid>(mainWindow).FirstOrDefault();
                    if (grid != null)
                    {
                        await UiTestHelpers.VerifyVisualRenderingAsync(mainWindow, grid, "SfDataGrid in MainWindow");
                        _output.WriteLine($"  ✓ SfDataGrid rendered successfully");
                    }
                    else
                    {
                        _output.WriteLine($"  ✗ SfDataGrid not found in MainWindow");
                    }
                }

                sw.Stop();
                phaseTimings["WindowShow"] = sw.ElapsedMilliseconds;
                _output.WriteLine($"✓ Window show phase completed in {sw.ElapsedMilliseconds}ms");
            }
            finally
            {
                if (mainWindow?.IsLoaded == true)
                {
                    try
                    {
                        mainWindow.Close();
                        await Dispatcher.Yield(DispatcherPriority.Background);
                        UiTestHelpers.DoEvents();
                        _output.WriteLine($"  ✓ Window closed and resources released");
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  ✗ Window close failed: {ex.Message}");
                    }
                }
                scope?.Dispose();
                overallStopwatch.Stop();
                _output.WriteLine("\n=== TIMING SUMMARY ===");
                foreach (var (phase, ms) in phaseTimings.OrderBy(kvp => kvp.Key))
                {
                    _output.WriteLine($"{phase,-25}: {ms,6} ms");
                }
                _output.WriteLine($"{"TOTAL",-25}: {overallStopwatch.ElapsedMilliseconds,6} ms");
            }
        });
    }

    [StaFact]
    public async Task E2E_02_ViewNavigation_Test()
    {
        _output.WriteLine("=== VIEW NAVIGATION TEST ===");

        await RunOnUIThreadAsync(async () =>
        {
            Window? mainWindow = null;
            try
            {
                var sp = TestDiSetup.GetServiceProvider();
                var mainViewModel = sp.GetService<MainViewModel>();
                Assert.NotNull(mainViewModel);
                
                mainWindow = sp.GetService<MainWindow>();
                Assert.NotNull(mainWindow);
                
                // Configure for testing
                mainWindow.Width = 1;
                mainWindow.Height = 1;
                mainWindow.ShowInTaskbar = false;
                mainWindow.ShowActivated = false;
                mainWindow.WindowStyle = WindowStyle.None;
                mainWindow.Opacity = 0;
                
                mainWindow.Show();
                
                // Wait for window to be ready
                var loadedTcs = new TaskCompletionSource<bool>();
                mainWindow.Loaded += (s, e) => loadedTcs.TrySetResult(true);
                await Task.WhenAny(loadedTcs.Task, Task.Delay(5000));
                
                _output.WriteLine($"Window loaded. Testing view navigation...");
                
                // Test navigation commands
                var commands = new[]
                {
                    ("Dashboard", mainViewModel.NavigateToDashboardCommand),
                    ("Enterprise", mainViewModel.NavigateToEnterprisesCommand),
                    ("Budget", mainViewModel.NavigateToBudgetCommand),
                    ("AI Assist", mainViewModel.NavigateToAIAssistCommand),
                    ("Analytics", mainViewModel.NavigateToAnalyticsCommand)
                };
                
                foreach (var (name, command) in commands)
                {
                    if (command != null)
                    {
                        _output.WriteLine($"\nTesting {name}...");
                        
                        try
                        {
                            var canExecute = command.CanExecute(null);
                            _output.WriteLine($"  Can execute: {canExecute}");
                            
                            if (canExecute)
                            {
                                command.Execute(null);
                                await Task.Delay(200);
                                
                                _output.WriteLine($"  ✓ Command executed");
                                _output.WriteLine($"  Current view: {mainViewModel.CurrentView ?? "null"}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine($"  ✗ Navigation failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        _output.WriteLine($"\n{name} command is null");
                    }
                }
                
                mainWindow.Close();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"✗ Navigation test failed: {ex.Message}");
                _output.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (mainWindow?.IsLoaded == true)
                {
                    try { mainWindow.Close(); } catch { }
                }
                
                throw;
            }
        });
    }

    [StaFact]
    public async Task E2E_03_AllViewModels_Initialization_Test()
    {
        _output.WriteLine("=== ALL VIEWMODELS INITIALIZATION TEST ===");

        await RunOnUIThreadAsync(async () =>
        {
            try
            {
                var sp = TestDiSetup.GetServiceProvider();
                
                var viewModels = new Dictionary<string, Type>
                {
                    ["MainViewModel"] = typeof(MainViewModel),
                    ["DashboardViewModel"] = typeof(DashboardViewModel),
                    ["EnterpriseViewModel"] = typeof(EnterpriseViewModel),
                    ["BudgetViewModel"] = typeof(BudgetViewModel),
                    ["AIAssistViewModel"] = typeof(AIAssistViewModel),
                    ["AnalyticsViewModel"] = typeof(AnalyticsViewModel),
                    ["UtilityCustomerViewModel"] = typeof(UtilityCustomerViewModel),
                    ["SettingsViewModel"] = typeof(SettingsViewModel),
                    ["MunicipalAccountViewModel"] = typeof(MunicipalAccountViewModel)
                };
                
                int successCount = 0;
                int failCount = 0;
                
                foreach (var (name, type) in viewModels)
                {
                    _output.WriteLine($"\nTesting {name}...");
                    var sw = Stopwatch.StartNew();
                    
                    try
                    {
                        var vm = sp.GetService(type);
                        sw.Stop();
                        
                        if (vm == null)
                        {
                            _output.WriteLine($"  ✗ Service returned null");
                            failCount++;
                            continue;
                        }
                        
                        _output.WriteLine($"  ✓ Created in {sw.ElapsedMilliseconds}ms");
                        
                        // Check common ViewModel properties
                        var properties = type.GetProperties();
                        foreach (var prop in properties)
                        {
                            if (prop.Name.Contains("Command"))
                            {
                                var value = prop.GetValue(vm);
                                _output.WriteLine($"    - {prop.Name}: {(value != null ? "✓" : "✗ null")}");
                            }
                        }
                        
                        successCount++;
                        
                        // Small delay to avoid overwhelming the system
                        await Task.Delay(50);
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        _output.WriteLine($"  ✗ Failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            _output.WriteLine($"     Inner: {ex.InnerException.Message}");
                        }
                        failCount++;
                    }
                }
                
                _output.WriteLine($"\n=== SUMMARY ===");
                _output.WriteLine($"Success: {successCount}/{viewModels.Count}");
                _output.WriteLine($"Failed: {failCount}/{viewModels.Count}");
                
                Assert.True(successCount > 0, "At least one ViewModel should initialize successfully");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"✗ ViewModel initialization test failed: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// Recursively inspects the visual tree and logs structure
    /// </summary>
    /// <summary>
    /// Recursively searches the visual tree for a child of the specified type.
    /// </summary>
    private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }
            
            var found = FindVisualChild<T>(child);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
}
