using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUI.Core.Definitions;
using Xunit;

namespace WileyWidget.UiTests;

public static class UiTestHelpers
{
    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    public static IReadOnlyList<T> FindVisualChildrenWithRetry<T>(DependencyObject parent, int expectedMin = 1, int timeoutMs = 3000, int pollIntervalMs = 100) where T : DependencyObject
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        List<T> last = new();
        while (DateTime.UtcNow < deadline)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                last = System.Windows.Application.Current.Dispatcher.Invoke(() => FindVisualChildren<T>(parent).ToList());
            }
            else
            {
                last = FindVisualChildren<T>(parent).ToList();
            }
            if (last.Count >= expectedMin)
            {
                return last;
            }
            Thread.Sleep(pollIntervalMs);
            DoEvents();
        }
        return last;
    }

    // Simple message pump spin for WPF UI tests
    public static void DoEvents()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// Waits for a ViewModel property to be set, indicating data binding has completed.
    /// This catches the critical DataContext initialization gap.
    /// </summary>
    public static async Task WaitForDataBindingAsync(FrameworkElement element, string propertyName, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var dataContext = element.DataContext;

        while (DateTime.UtcNow < deadline)
        {
            if (dataContext != null)
            {
                var property = dataContext.GetType().GetProperty(propertyName);
                if (property != null)
                {
                    var value = property.GetValue(dataContext);
                    if (value != null)
                    {
                        return; // Data binding completed
                    }
                }
            }

            await Task.Delay(100);
            DoEvents();
            dataContext = element.DataContext; // Re-check in case DataContext was set
        }

        throw new TimeoutException($"Data binding for property '{propertyName}' did not complete within {timeoutMs}ms");
    }

    /// <summary>
    /// Verifies that a Syncfusion control is actually rendered and visible (not just in visual tree).
    /// Uses FlaUI for pixel-level verification to catch rendering disconnects.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task VerifyVisualRenderingAsync(System.Windows.Window window, FrameworkElement control, string controlDescription = "")
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on non-Windows platforms
        }

        using var automation = new UIA3Automation();
        var app = FlaUI.Core.Application.Attach(Process.GetCurrentProcess());
        var mainWindow = app.GetMainWindow(automation);

        // Verify window is actually visible
        Assert.True(mainWindow.IsEnabled, $"Window should be enabled for {controlDescription}");
        Assert.True(mainWindow.BoundingRectangle.Width > 0, $"Window should have width for {controlDescription}");
        Assert.True(mainWindow.BoundingRectangle.Height > 0, $"Window should have height for {controlDescription}");

        // Find the control using FlaUI
        var controlElement = FindControlByName(mainWindow, control.Name);
        if (controlElement != null)
        {
            Assert.True(controlElement.IsEnabled, $"Control '{control.Name}' should be enabled for {controlDescription}");
            Assert.True(controlElement.BoundingRectangle.Width > 0, $"Control '{control.Name}' should have width for {controlDescription}");
            Assert.True(controlElement.BoundingRectangle.Height > 0, $"Control '{control.Name}' should have height for {controlDescription}");

            // For data grids, verify they have content
            if (control is Syncfusion.UI.Xaml.Grid.SfDataGrid)
            {
                await VerifyDataGridContentAsync(controlElement.AsGrid(), controlDescription);
            }
        }
        else
        {
            Assert.Fail($"Control '{control.Name}' not found in automation tree for {controlDescription}");
        }
    }

    /// <summary>
    /// Verifies that a SfDataGrid actually displays data rows (catches blank grid issue).
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static async Task VerifyDataGridContentAsync(Grid grid, string description)
    {
        var rows = grid.Rows;
        Assert.True(rows.Length > 0, $"DataGrid should display data rows for {description}");

        // Verify at least one cell has content
        if (rows.Length > 0)
        {
            var firstRow = rows[0];
            var cells = firstRow.Cells;
            Assert.True(cells.Any(c => !string.IsNullOrEmpty(c.Name)),
                       $"At least one cell should have content for {description}");
        }
    }

    /// <summary>
    /// Finds a control by name in the FlaUI automation tree.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static AutomationElement FindControlByName(AutomationElement parent, string name)
    {
        return parent.FindFirstDescendant(cf => cf.ByName(name).Or(cf.ByAutomationId(name)));
    }

    /// <summary>
    /// Enhanced version of FindVisualChildrenWithRetry that also verifies rendering.
    /// </summary>
    public static async Task<IReadOnlyList<T>> FindAndVerifyVisualChildrenAsync<T>(
        DependencyObject parent,
        int expectedMin = 1,
        int timeoutMs = 3000,
        string description = "") where T : FrameworkElement
    {
        var elements = FindVisualChildrenWithRetry<T>(parent, expectedMin, timeoutMs);

        // Verify each element is actually rendered
        foreach (var element in elements)
        {
            Assert.NotNull(element.DataContext);
            Assert.True(element.IsVisible, $"Element {element.Name} should be visible for {description}");
            Assert.True(element.ActualWidth > 0 && element.ActualHeight > 0,
                       $"Element {element.Name} should have dimensions for {description}");
        }

        return elements;
    }
    
    #region FlaUI Retry Helpers
    
    /// <summary>
    /// Finds an element with built-in retry logic for robust element search.
    /// Polls for the element until found or timeout expires.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static AutomationElement FindElementWithRetry(
        AutomationElement scope,
        Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        var actualTimeout = timeout ?? UiTestConstants.Timeouts.ElementSearch;
        var actualInterval = interval ?? UiTestConstants.Timeouts.RetryInterval;
        var deadline = DateTime.UtcNow + actualTimeout;
        
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var element = scope.FindFirstDescendant(condition);
                if (element != null && element.IsAvailable)
                {
                    return element;
                }
            }
            catch
            {
                // Element not found or not accessible yet
            }
            
            Thread.Sleep(actualInterval);
        }
        
        return null;
    }
    
    /// <summary>
    /// Finds an element by name with retry logic.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static AutomationElement FindElementByNameWithRetry(
        AutomationElement scope,
        string name,
        TimeSpan? timeout = null)
    {
        return FindElementWithRetry(
            scope,
            cf => cf.ByName(name),
            timeout);
    }
    
    /// <summary>
    /// Finds an element by automation ID with retry logic.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static AutomationElement FindElementByIdWithRetry(
        AutomationElement scope,
        string automationId,
        TimeSpan? timeout = null)
    {
        return FindElementWithRetry(
            scope,
            cf => cf.ByAutomationId(automationId),
            timeout);
    }
    
    /// <summary>
    /// Finds an element by class name with retry logic.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static AutomationElement FindElementByClassNameWithRetry(
        AutomationElement scope,
        string className,
        TimeSpan? timeout = null)
    {
        return FindElementWithRetry(
            scope,
            cf => cf.ByClassName(className),
            timeout);
    }
    
    /// <summary>
    /// Waits for an element to become responsive.
    /// Returns true if element becomes responsive within timeout.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static bool WaitForElementResponsive(
        AutomationElement element,
        TimeSpan? timeout = null)
    {
        if (element == null) return false;
        
        var actualTimeout = timeout ?? UiTestConstants.Timeouts.ElementResponsive;
        var deadline = DateTime.UtcNow + actualTimeout;
        
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                // Try to access properties to verify element is responsive
                if (element.IsAvailable && element.IsEnabled)
                {
                    _ = element.Name; // Force property access
                    return true;
                }
            }
            catch
            {
                // Element not responsive yet
            }
            
            Thread.Sleep(100);
        }
        
        return false;
    }
    
    /// <summary>
    /// Waits for an element matching the condition to be found and responsive.
    /// Combines finding with responsiveness check for robust element access.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task<AutomationElement> WaitForElementResponsiveAsync(
        AutomationElement scope,
        Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase> condition,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? UiTestConstants.Timeouts.ElementSearch;
        var deadline = DateTime.UtcNow + actualTimeout;
        
        while (DateTime.UtcNow < deadline)
        {
            var element = FindElementWithRetry(scope, condition, TimeSpan.FromSeconds(2));
            
            if (element != null && element.IsAvailable)
            {
                if (WaitForElementResponsive(element, TimeSpan.FromSeconds(2)))
                {
                    return element;
                }
            }
            
            await Task.Delay(UiTestConstants.Timeouts.RetryInterval);
        }
        
        return null;
    }
    
    /// <summary>
    /// Waits for an element to be clickable (enabled, on-screen, responsive).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task<bool> WaitForElementClickableAsync(
        AutomationElement element,
        TimeSpan? timeout = null)
    {
        if (element == null) return false;
        
        var actualTimeout = timeout ?? UiTestConstants.Timeouts.ElementClickable;
        var deadline = DateTime.UtcNow + actualTimeout;
        
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (element.IsEnabled && !element.IsOffscreen && element.IsAvailable)
                {
                    // Verify we can get clickable point
                    var clickablePoint = element.GetClickablePoint();
                    if (WaitForElementResponsive(element, TimeSpan.FromSeconds(1)))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Not clickable yet
            }
            
            await Task.Delay(UiTestConstants.Timeouts.RetryInterval);
        }
        
        return false;
    }
    
    /// <summary>
    /// Finds multiple elements by name with fallback aliases.
    /// Useful for navigation items that may have different names across versions.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static AutomationElement FindElementByNamesWithRetry(
        AutomationElement scope,
        string[] names,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? UiTestConstants.Timeouts.ElementSearch;
        
        foreach (var name in names)
        {
            var element = FindElementByNameWithRetry(scope, name, actualTimeout);
            if (element != null && element.IsAvailable)
            {
                return element;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Checks if any error dialogs are present in the application.
    /// Returns the error dialog element if found, null otherwise.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static FlaUI.Core.AutomationElements.Window CheckForErrorDialogs(FlaUI.Core.Application app, AutomationBase automation)
    {
        try
        {
            var windows = app.GetAllTopLevelWindows(automation);
            foreach (var window in windows)
            {
                var title = window.Title ?? "";
                if (UiTestConstants.ErrorDialogKeywords.Keywords.Any(keyword =>
                    title.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return window;
                }
            }
        }
        catch
        {
            // Ignore errors during error detection
        }
        
        return null;
    }
    
    /// <summary>
    /// Dumps the automation element tree for debugging purposes.
    /// Useful when elements are not found as expected.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static string DumpElementTree(AutomationElement root, int maxDepth = 3)
    {
        var sb = new System.Text.StringBuilder();
        DumpElementTreeRecursive(root, 0, maxDepth, sb);
        return sb.ToString();
    }
    
    private static void DumpElementTreeRecursive(
        AutomationElement element, 
        int depth, 
        int maxDepth, 
        System.Text.StringBuilder sb)
    {
        if (element == null || depth > maxDepth) return;
        
        var indent = new string(' ', depth * 2);
        var name = element.Name ?? "(no name)";
        var automationId = element.Properties.AutomationId.ValueOrDefault ?? "(no ID)";
        var className = element.Properties.ClassName.ValueOrDefault ?? "(no class)";
        var controlType = element.Properties.ControlType.ValueOrDefault;
        
        sb.AppendLine($"{indent}[{controlType}] Name: {name}, ID: {automationId}, Class: {className}");
        
        try
        {
            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                DumpElementTreeRecursive(child, depth + 1, maxDepth, sb);
            }
        }
        catch
        {
            // Ignore errors when traversing tree
        }
    }
    
    #endregion
}

