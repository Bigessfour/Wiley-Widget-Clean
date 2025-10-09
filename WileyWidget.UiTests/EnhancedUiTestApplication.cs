using System;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using WileyWidget.UiTests;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.UiTests;

/// <summary>
/// Enhanced base class for UI tests that properly initializes DataContext and provides visual verification.
/// This addresses the critical rendering disconnect issues identified in the repository analysis.
/// </summary>
public abstract class EnhancedUiTestApplication : UiTestApplication
{
    /// <summary>
    /// Creates a view with proper DataContext initialization and full WPF lifecycle simulation.
    /// This is the RECOMMENDED method for creating views in UI tests.
    /// </summary>
    protected async Task<T> CreateViewWithFullLifecycleAsync<T>() where T : Window, new()
    {
        return await TestDiSetup.CreateViewWithFullLifecycleAsync<T>();
    }

    /// <summary>
    /// Enhanced RunOnUIThread that includes proper async/await patterns and error handling.
    /// </summary>
    protected async Task RunOnUIThreadAsync(Func<Task> testAction)
    {
        await RunOnUIThread(async () =>
        {
            try
            {
                await testAction();
            }
            catch (Exception ex)
            {
                // Log detailed WPF threading information
                System.Diagnostics.Debug.WriteLine($"UI Test Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId} ({System.Threading.Thread.CurrentThread.Name})");
                System.Diagnostics.Debug.WriteLine($"Apartment State: {System.Threading.Thread.CurrentThread.GetApartmentState()}");
                throw;
            }
        });
    }

    /// <summary>
    /// Standard test pattern for verifying Syncfusion controls render correctly.
    /// Includes DataContext verification, visual rendering checks, and FlaUI validation.
    /// </summary>
    protected async Task VerifySyncfusionControlRenderingAsync<T>(
        T control,
        string controlDescription,
        Func<T, Task> additionalAssertions = null) where T : FrameworkElement
    {
        // 1. Verify DataContext is set (catches the main disconnect)
        Assert.NotNull(control.DataContext, $"DataContext should be set for {controlDescription}");

        // 2. Verify basic WPF properties
        Assert.True(control.IsVisible, $"Control should be visible for {controlDescription}");
        Assert.True(control.ActualWidth > 0, $"Control should have width for {controlDescription}");
        Assert.True(control.ActualHeight > 0, $"Control should have height for {controlDescription}");

        // 3. Wait for data binding to complete
        if (control.DataContext != null)
        {
            // Wait for key ViewModel properties to be populated
            await UiTestHelpers.WaitForDataBindingAsync(control, "IsLoading", 2000);
        }

        // 4. Force layout updates
        control.UpdateLayout();
        UiTestHelpers.DoEvents();

        // 5. Verify visual rendering with FlaUI (catches rendering disconnects)
        var window = Window.GetWindow(control);
        if (window != null)
        {
            await UiTestHelpers.VerifyVisualRenderingAsync(window, control, controlDescription);
        }

        // 6. Run additional control-specific assertions
        if (additionalAssertions != null)
        {
            await additionalAssertions(control);
        }
    }

    /// <summary>
    /// Helper for testing SfDataGrid rendering with data verification.
    /// </summary>
    protected async Task VerifySfDataGridRenderingAsync(Syncfusion.UI.Xaml.Grid.SfDataGrid grid, string description)
    {
        await VerifySyncfusionControlRenderingAsync(grid, description, async (g) =>
        {
            // Verify grid has data source
            Assert.NotNull(g.ItemsSource, $"SfDataGrid should have ItemsSource for {description}");

            // Wait for grid to populate
            await Task.Delay(200);
            UiTestHelpers.DoEvents();

            // Verify visual children (rows/cells) exist
            var rows = UiTestHelpers.FindVisualChildren<Syncfusion.UI.Xaml.Grid.GridRow>(g);
            Assert.True(rows.Any(), $"SfDataGrid should have visible rows for {description}");

            // Verify at least one row has content
            var firstRow = rows.FirstOrDefault();
            if (firstRow != null)
            {
                var cells = UiTestHelpers.FindVisualChildren<Syncfusion.UI.Xaml.Grid.GridCell>(firstRow);
                Assert.True(cells.Any(c => !string.IsNullOrEmpty(c.Content?.ToString())),
                           $"SfDataGrid rows should have content for {description}");
            }
        });
    }

    /// <summary>
    /// Helper for testing SfChart rendering.
    /// </summary>
    protected async Task VerifySfChartRenderingAsync(Syncfusion.UI.Xaml.Charts.SfChart chart, string description)
    {
        await VerifySyncfusionControlRenderingAsync(chart, description, async (c) =>
        {
            // Verify chart has series
            Assert.True(c.Series.Count > 0, $"SfChart should have series for {description}");

            // Verify series have data
            foreach (var series in c.Series)
            {
                Assert.NotNull(series.ItemsSource, $"Chart series should have ItemsSource for {description}");
            }

            // Wait for chart rendering
            await Task.Delay(300);
            UiTestHelpers.DoEvents();

            // Verify visual elements exist
            var plotArea = UiTestHelpers.FindVisualChildren<Syncfusion.UI.Xaml.Charts.ChartPlotArea>(c).FirstOrDefault();
            Assert.NotNull(plotArea, $"SfChart should have plot area for {description}");
        });
    }
}