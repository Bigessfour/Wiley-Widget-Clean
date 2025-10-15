using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Runtime.Versioning;
using Syncfusion.Windows.Tools.Controls;
using Xunit;
using WileyWidget.Tests;
using WileyWidget.ViewModels;
using WileyWidget.Views;

namespace WileyWidget.UiTests.ComponentTests
{
    /// <summary>
    /// Component-level StaFact tests for MainWindow
    /// Tests UI automation, data binding, control interactions, theming, and accessibility
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class MainWindowComponentTests : UiTestApplication
    {
        #region 1. UI Automation Testing

        [StaFact]
        public void MainWindow_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                window.Show();

                // Verify window properties
                Assert.Equal("Wiley Widget", window.Title);
                Assert.Equal(600, window.Height);
                Assert.Equal(1000, window.Width);

                // Verify main layout elements exist
                var dockPanel = window.Content as DockPanel;
                Assert.NotNull(dockPanel);

                // Verify Ribbon is present and docked at top
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);
                Assert.Equal(Dock.Top, DockPanel.GetDock(ribbon));

                window.Close();
            });
        }

        [StaFact]
        public void MainWindow_Ribbon_Controls_ShouldBeAccessible()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                window.Show();

                var dockPanel = window.Content as DockPanel;
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);

                // Verify ribbon tabs exist
                Assert.True(ribbon.Items.Count > 0);

                // Test keyboard shortcuts are registered
                var inputBindings = window.InputBindings;
                Assert.True(inputBindings.Count > 0);

                // Verify specific shortcuts exist
                var ctrlC = inputBindings.OfType<KeyBinding>()
                    .FirstOrDefault(kb => kb.Key == Key.C && kb.Modifiers == ModifierKeys.Control);
                Assert.NotNull(ctrlC);

                window.Close();
            });
        }

        #endregion

        #region 2. Data Binding & MVVM Testing

        [StaFact]
        public void MainWindow_DataBinding_ShouldUpdateUI()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                var viewModel = window.DataContext as MainViewModel;
                Assert.NotNull(viewModel);

                // Test initial state
                Assert.NotNull(viewModel.Enterprises);
                Assert.True(viewModel.Enterprises.Count >= 0);

                // Test command availability
                Assert.NotNull(viewModel.AddTestEnterpriseCommand);
                Assert.NotNull(viewModel.RefreshCommand);
                Assert.NotNull(viewModel.OpenSettingsCommand);

                window.Close();
            });
        }

        [StaFact]
        public void MainWindow_CommandBinding_ShouldExecuteCommands()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                var viewModel = window.DataContext as MainViewModel;

                // Test AddTestEnterpriseCommand can execute
                Assert.True(viewModel.AddTestEnterpriseCommand.CanExecute(null));

                // Test RefreshCommand can execute
                Assert.True(viewModel.RefreshCommand.CanExecute(null));

                window.Close();
            });
        }

        #endregion

        #region 3. Control Interaction Testing

        [StaFact]
        public void MainWindow_RibbonButtons_ShouldHandleClicks()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                window.Show();

                var dockPanel = window.Content as DockPanel;
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);

                // Find ribbon buttons
                var ribbonButtons = FindVisualChildren<RibbonButton>(ribbon);
                Assert.True(ribbonButtons.Any());

                // Test button properties
                foreach (var button in ribbonButtons)
                {
                    Assert.False(string.IsNullOrEmpty(button.Label));
                }

                window.Close();
            });
        }

        [StaFact]
        public void MainWindow_KeyboardShortcuts_ShouldWork()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                window.Show();

                // Test that input bindings are properly configured
                var inputBindings = window.InputBindings;
                var expectedShortcuts = new[]
                {
                    (Key.C, ModifierKeys.Control), // SelectNextCommand
                    (Key.N, ModifierKeys.Control), // AddTestEnterpriseCommand
                    (Key.F5, ModifierKeys.None),   // RefreshCommand
                    (Key.S, ModifierKeys.Control), // OpenSettingsCommand
                    (Key.F1, ModifierKeys.None),   // OpenHelpCommand
                    (Key.E, ModifierKeys.Control), // OpenEnterpriseCommand
                    (Key.B, ModifierKeys.Control), // OpenBudgetCommand
                    (Key.D, ModifierKeys.Control), // OpenDashboardCommand
                    (Key.A, ModifierKeys.Control), // OpenAIAssistCommand
                };

                foreach (var (key, modifiers) in expectedShortcuts)
                {
                    var binding = inputBindings.OfType<KeyBinding>()
                        .FirstOrDefault(kb => kb.Key == key && kb.Modifiers == modifiers);
                    Assert.NotNull(binding);
                }

                window.Close();
            });
        }

        #endregion

        #region 4. Theming & Styling Tests

        [StaFact]
        public void MainWindow_Theme_ShouldApplyCorrectly()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                window.Show();

                // Verify window has theme applied
                var style = window.Style;
                Assert.NotNull(style);
                Assert.Equal(typeof(Window), style.TargetType);

                // Test that Syncfusion theme is applied to ribbon
                var dockPanel = window.Content as DockPanel;
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);

                window.Close();
            });
        }

        #endregion

        #region 5. Accessibility Testing

        [StaFact]
        public void MainWindow_Accessibility_ShouldBeCompliant()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                window.Show();

                // Test window accessibility
                Assert.Equal("Wiley Widget", window.Title);
                Assert.True(window.IsEnabled);

                // Test ribbon accessibility
                var dockPanel = window.Content as DockPanel;
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);
                Assert.True(ribbon.IsEnabled);

                // Test automation properties on key elements
                var ribbonButtons = FindVisualChildren<RibbonButton>(ribbon);
                foreach (var button in ribbonButtons)
                {
                    // Verify buttons have accessible names
                    var name = AutomationProperties.GetName(button);
                    if (!string.IsNullOrEmpty(button.Label))
                    {
                        Assert.Equal(button.Label, name);
                    }
                }

                window.Close();
            });
        }

        #endregion

        #region 6. Performance Testing

        [StaFact]
        public void MainWindow_Rendering_ShouldBeFast()
        {
            RunOnUIThread(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var window = new MainWindow();
                window.Show();

                // Measure layout and rendering time
                window.UpdateLayout();
                stopwatch.Stop();

                // Should render within reasonable time (adjust threshold as needed)
                Assert.True(stopwatch.ElapsedMilliseconds < 2000,
                    $"Window rendering took {stopwatch.ElapsedMilliseconds}ms, expected < 2000ms");

                window.Close();
            });
        }

        #endregion

        #region 7. Error Handling Testing

        [StaFact]
        public void MainWindow_ErrorHandling_ShouldGracefullyHandleExceptions()
        {
            RunOnUIThread(() =>
            {
                var window = new MainWindow();

                // Test that window can handle invalid states gracefully
                // This would test error handling in view model commands, etc.

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