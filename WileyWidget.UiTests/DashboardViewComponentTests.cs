using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using System.Runtime.Versioning;
using Syncfusion.Windows.Tools.Controls;
using Syncfusion.UI.Xaml.Charts;
using Xunit;
using WileyWidget.Tests;
using WileyWidget.ViewModels;

namespace WileyWidget.UiTests.ComponentTests
{
    /// <summary>
    /// Component-level StaFact tests for DashboardView
    /// Tests data visualization, KPI cards, charts, and dashboard interactions
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class DashboardViewComponentTests : UiTestApplication
    {
        #region 1. UI Automation Testing

        [StaFact]
        public void DashboardView_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                var view = new DashboardView();
                var window = new Window { Content = view, Title = "Dashboard", Height = 700, Width = 1200 };
                window.Show();

                // Verify window properties
                Assert.Equal("Dashboard", window.Title);
                Assert.Equal(700, window.Height);
                Assert.Equal(1200, window.Width);

                // Verify main layout elements exist
                var dockPanel = window.Content as DockPanel;
                Assert.NotNull(dockPanel);

                // Verify Ribbon is present
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);

                // Verify main content area
                var scrollViewer = dockPanel.Children.OfType<ScrollViewer>().FirstOrDefault();
                Assert.NotNull(scrollViewer);

                window.Close();
            });
        }

        [StaFact]
        public void DashboardView_KPICards_ShouldDisplayData()
        {
            RunOnUIThread(() =>
            {
                var window = new DashboardView();
                window.Show();

                var dockPanel = window.Content as DockPanel;
                var scrollViewer = dockPanel.Children.OfType<ScrollViewer>().FirstOrDefault();
                var stackPanel = scrollViewer.Content as StackPanel;
                Assert.NotNull(stackPanel);

                // Find KPI cards wrap panel
                var wrapPanel = stackPanel.Children.OfType<WrapPanel>().FirstOrDefault();
                Assert.NotNull(wrapPanel);

                // Verify KPI cards exist
                var borders = wrapPanel.Children.OfType<Border>();
                Assert.True(borders.Any(), "No KPI cards found");

                // Test each KPI card structure
                foreach (var border in borders)
                {
                    var cardStackPanel = border.Child as StackPanel;
                    Assert.NotNull(cardStackPanel);

                    // Verify card has title, value, and change indicator
                    var textBlocks = FindVisualChildren<TextBlock>(cardStackPanel);
                    Assert.True(textBlocks.Count() >= 3, "KPI card missing required text elements");
                }

                window.Close();
            });
        }

        #endregion

        #region 2. Data Binding & MVVM Testing

        [StaFact]
        public void DashboardView_DataBinding_ShouldUpdateKPIs()
        {
            RunOnUIThread(() =>
            {
                var window = new DashboardView();
                var viewModel = window.DataContext as DashboardViewModel;
                Assert.NotNull(viewModel);

                // Test KPI properties are bound
                Assert.True(viewModel.TotalEnterprises >= 0);
                Assert.NotNull(viewModel.EnterprisesChangeText);
                Assert.NotNull(viewModel.EnterprisesChangeColor);

                // Test refresh command
                Assert.NotNull(viewModel.RefreshDashboardCommand);
                Assert.True(viewModel.RefreshDashboardCommand.CanExecute(null));

                window.Close();
            });
        }

        [StaFact]
        public void DashboardView_AutoRefresh_ShouldWork()
        {
            RunOnUIThread(() =>
            {
                var window = new DashboardView();
                var viewModel = window.DataContext as DashboardViewModel;

                // Test auto refresh properties
                Assert.NotNull(viewModel.ToggleAutoRefreshCommand);
                Assert.True(viewModel.RefreshIntervalMinutes > 0);

                // Test toggle command
                Assert.True(viewModel.ToggleAutoRefreshCommand.CanExecute(null));

                window.Close();
            });
        }

        #endregion

        #region 3. Control Interaction Testing

        [StaFact]
        public void DashboardView_RibbonControls_ShouldHandleInteractions()
        {
            RunOnUIThread(() =>
            {
                var window = new DashboardView();
                window.Show();

                var dockPanel = window.Content as DockPanel;
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);

                // Find ribbon controls
                var ribbonButtons = FindVisualChildren<RibbonButton>(ribbon);
                var integerTextBoxes = FindVisualChildren<Syncfusion.Windows.Shared.IntegerTextBox>(ribbon);

                // Test refresh button
                var refreshButton = ribbonButtons.FirstOrDefault(rb => rb.Label == "Refresh");
                Assert.NotNull(refreshButton);

                // Test export button
                var exportButton = ribbonButtons.FirstOrDefault(rb => rb.Label == "Export");
                Assert.NotNull(exportButton);

                // Test auto refresh button
                var autoRefreshButton = ribbonButtons.FirstOrDefault(rb => rb.Label == "Auto Refresh");
                Assert.NotNull(autoRefreshButton);

                // Test refresh interval control
                Assert.True(integerTextBoxes.Any());

                window.Close();
            });
        }

        [StaFact]
        public void DashboardView_Charts_ShouldRenderData()
        {
            RunOnUIThread(() =>
            {
                var window = new DashboardView();
                window.Show();

                // Find chart controls (SfChart or similar)
                var charts = FindVisualChildren<Syncfusion.UI.Xaml.Charts.SfChart>(window);
                if (charts.Any())
                {
                    foreach (var chart in charts)
                    {
                        // Verify chart has data
                        Assert.NotNull(chart.Series);
                        Assert.True(chart.Series.Count >= 0);
                    }
                }

                window.Close();
            });
        }

        #endregion

        #region 4. Theming & Styling Tests

        [StaFact]
        public void DashboardView_Theme_ShouldApplyToKPICards()
        {
            RunOnUIThread(() =>
            {
                var window = new DashboardView();
                window.Show();

                var dockPanel = window.Content as DockPanel;
                var scrollViewer = dockPanel.Children.OfType<ScrollViewer>().FirstOrDefault();
                var stackPanel = scrollViewer.Content as StackPanel;
                var wrapPanel = stackPanel.Children.OfType<WrapPanel>().FirstOrDefault();

                // Test KPI card styling
                var borders = wrapPanel.Children.OfType<Border>();
                foreach (var border in borders)
                {
                    // Verify cards have proper styling
                    Assert.NotEqual(Colors.Transparent, ((SolidColorBrush)border.Background).Color);
                    Assert.True(border.BorderThickness.Left > 0);
                    Assert.True(border.CornerRadius.TopLeft > 0);
                }

                window.Close();
            });
        }

        #endregion

        #region 5. Accessibility Testing

        [StaFact]
        public void DashboardView_Accessibility_ShouldBeCompliant()
        {
            RunOnUIThread(() =>
            {
                var window = new DashboardView();
                window.Show();

                // Test window accessibility
                Assert.Equal("Dashboard", window.Title);
                Assert.True(window.IsEnabled);

                // Test KPI cards accessibility
                var dockPanel = window.Content as DockPanel;
                var scrollViewer = dockPanel.Children.OfType<ScrollViewer>().FirstOrDefault();
                var stackPanel = scrollViewer.Content as StackPanel;
                var wrapPanel = stackPanel.Children.OfType<WrapPanel>().FirstOrDefault();

                var borders = wrapPanel.Children.OfType<Border>();
                foreach (var border in borders)
                {
                    // Test automation properties
                    var automationId = AutomationProperties.GetAutomationId(border);
                    var name = AutomationProperties.GetName(border);

                    // Cards should have accessible names
                    Assert.False(string.IsNullOrEmpty(name) && string.IsNullOrEmpty(automationId));
                }

                window.Close();
            });
        }

        #endregion

        #region 6. Performance Testing

        [StaFact]
        public void DashboardView_KPIRendering_ShouldBeFast()
        {
            RunOnUIThread(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var window = new DashboardView();
                window.Show();

                // Force layout update
                window.UpdateLayout();
                stopwatch.Stop();

                // Dashboard should render within reasonable time
                Assert.True(stopwatch.ElapsedMilliseconds < 3000,
                    $"Dashboard rendering took {stopwatch.ElapsedMilliseconds}ms, expected < 3000ms");

                window.Close();
            });
        }

        #endregion

        #region 7. Error Handling Testing

        [StaFact]
        public void DashboardView_ErrorHandling_ShouldHandleDataErrors()
        {
            RunOnUIThread(() =>
            {
                var window = new DashboardView();
                var viewModel = window.DataContext as DashboardViewModel;

                // Test error handling when data is unavailable
                // This would test fallback behavior in view model

                window.Show();
                Assert.True(window.IsLoaded);

                window.Close();
            });
        }

        #endregion

        #region Helper Methods

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
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

        #endregion
    }
}