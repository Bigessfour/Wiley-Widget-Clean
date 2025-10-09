using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics;
using System.Runtime.Versioning;
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
            if (Application.Current?.Dispatcher != null)
            {
                last = Application.Current.Dispatcher.Invoke(() => FindVisualChildren<T>(parent).ToList());
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
    public static async Task VerifyVisualRenderingAsync(Window window, FrameworkElement control, string controlDescription = "")
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on non-Windows platforms
        }

        using var automation = new UIA3Automation();
        var app = Application.Attach(Process.GetCurrentProcess());
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
            Assert.NotNull(element.DataContext, $"Element {element.Name} should have DataContext for {description}");
            Assert.True(element.IsVisible, $"Element {element.Name} should be visible for {description}");
            Assert.True(element.ActualWidth > 0 && element.ActualHeight > 0,
                       $"Element {element.Name} should have dimensions for {description}");
        }

        return elements;
    }
}
