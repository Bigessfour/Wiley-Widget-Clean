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
using Xunit;
using WileyWidget.Tests;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.UiTests.ComponentTests
{
    /// <summary>
    /// Component-level StaFact tests for remaining views
    /// Tests SettingsView, AIAssistView, AboutWindow, SplashScreenWindow, UtilityCustomerView
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class RemainingViewsComponentTests : UiTestApplication
    {
        #region SettingsView Tests

        [StaFact]
        public void SettingsView_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                var window = new SettingsView();
                window.Show();

                // Verify window properties
                Assert.Equal("Settings", window.Title);
                Assert.True(window.Height > 0);
                Assert.True(window.Width > 0);

                // Verify main layout elements exist
                var dockPanel = window.Content as DockPanel;
                Assert.NotNull(dockPanel);

                window.Close();
            });
        }

        [StaFact]
        public void SettingsView_DataBinding_ShouldWork()
        {
            RunOnUIThread(() =>
            {
                var window = new SettingsView();
                var viewModel = window.DataContext as SettingsViewModel;
                Assert.NotNull(viewModel);

                // Test settings properties
                Assert.NotNull(viewModel.SaveSettingsCommand);
                Assert.NotNull(viewModel.ResetSettingsCommand);

                window.Close();
            });
        }

        #endregion

        #region AIAssistView Tests

        [StaFact]
        public void AIAssistView_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                var window = new AIAssistView();
                window.Show();

                // Verify window properties
                Assert.Equal("AI Assistant", window.Title);
                Assert.True(window.Height > 0);
                Assert.True(window.Width > 0);

                window.Close();
            });
        }

        [StaFact]
        public void AIAssistView_Conversation_ShouldWork()
        {
            RunOnUIThread(() =>
            {
                var window = new AIAssistView();
                var viewModel = window.DataContext as AIAssistViewModel;
                Assert.NotNull(viewModel);

                // Test AI conversation properties
                Assert.NotNull(viewModel.SendMessageCommand);
                Assert.NotNull(viewModel.ClearChatCommand);
                Assert.NotNull(viewModel.ChatMessages);

                window.Close();
            });
        }

        #endregion

        #region AboutWindow Tests

        [StaFact]
        public void AboutWindow_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                var window = new AboutWindow();
                window.Show();

                // Verify window properties
                Assert.Equal("About Wiley Widget", window.Title);
                Assert.True(window.Height > 0);
                Assert.True(window.Width > 0);

                // Verify content includes version, copyright, etc.
                var content = window.Content;
                Assert.NotNull(content);

                window.Close();
            });
        }

        [StaFact]
        public void AboutWindow_Information_ShouldBeDisplayed()
        {
            RunOnUIThread(() =>
            {
                var window = new AboutWindow();
                window.Show();

                // Find text elements displaying about information
                var textBlocks = FindVisualChildren<TextBlock>(window);
                Assert.True(textBlocks.Any());

                // Should contain version, copyright, or license information
                var textContent = string.Join(" ", textBlocks.Select(tb => tb.Text));
                Assert.False(string.IsNullOrEmpty(textContent));

                window.Close();
            });
        }

        #endregion

        #region SplashScreenWindow Tests

        [StaFact]
        public void SplashScreenWindow_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                var window = new SplashScreenWindow();
                window.Show();

                // Verify window properties - splash screens are typically borderless
                Assert.True(window.Height > 0);
                Assert.True(window.Width > 0);

                // Verify content includes logo, version, loading indicator
                var content = window.Content;
                Assert.NotNull(content);

                window.Close();
            });
        }

        [StaFact]
        public void SplashScreenWindow_VisualElements_ShouldBePresent()
        {
            RunOnUIThread(() =>
            {
                var window = new SplashScreenWindow();
                window.Show();

                // Find visual elements like images, progress bars, text
                var images = FindVisualChildren<Image>(window);
                var progressBars = FindVisualChildren<ProgressBar>(window);
                var textBlocks = FindVisualChildren<TextBlock>(window);

                // Splash screen should have some visual elements
                Assert.True(images.Any() || progressBars.Any() || textBlocks.Any());

                window.Close();
            });
        }

        #endregion

        #region UtilityCustomerView Tests

        [StaFact]
        public void UtilityCustomerView_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                var window = new UtilityCustomerView();
                window.Show();

                // Verify window properties
                Assert.Equal("Utility Customer Management", window.Title);
                Assert.True(window.Height > 0);
                Assert.True(window.Width > 0);

                window.Close();
            });
        }

        [StaFact]
        public void UtilityCustomerView_DataBinding_ShouldWork()
        {
            RunOnUIThread(() =>
            {
                var window = new UtilityCustomerView();
                var viewModel = window.DataContext as UtilityCustomerViewModel;
                Assert.NotNull(viewModel);

                // Test customer data properties
                Assert.NotNull(viewModel.Customers);
                Assert.NotNull(viewModel.AddCustomerCommand);
                Assert.NotNull(viewModel.SaveCustomerCommand);
                Assert.NotNull(viewModel.DeleteCustomerCommand);

                window.Close();
            });
        }

        #endregion

        #region Cross-View Integration Tests

        [StaFact]
        public void AllViews_ShouldFollowConsistentStyling()
        {
            RunOnUIThread(() =>
            {
                var serviceProvider = TestDiSetup.ServiceProvider;
                var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                // Create windows, handling scoped dependencies properly
                var windows = new List<Window>();

                // MainWindow doesn't have scoped dependencies
                windows.Add(new MainWindow());

                // DashboardView requires scoped services, so create it within a scope
                using (var scope = scopeFactory.CreateScope())
                {
                    var scopedProvider = scope.ServiceProvider;
                    
                    // Temporarily set App.ServiceProvider to the scoped provider for DashboardView creation
                    var appType = typeof(WileyWidget.App);
                    var serviceProviderProperty = appType.GetProperty("ServiceProvider");
                    var originalProvider = serviceProviderProperty?.GetValue(null);
                    
                    try
                    {
                        serviceProviderProperty?.SetValue(null, scopedProvider);
                        var dashboardView = new DashboardView();
                        windows.Add(dashboardView);
                    }
                    finally
                    {
                        // Restore the original provider
                        serviceProviderProperty?.SetValue(null, originalProvider);
                    }
                }

                // Other views don't have scoped dependencies
                windows.Add(new BudgetView());
                windows.Add(new EnterpriseView());
                windows.Add(new SettingsView());
                windows.Add(new AIAssistView());
                windows.Add(new AboutWindow());
                windows.Add(new SplashScreenWindow());
                windows.Add(new UtilityCustomerView());

                foreach (var window in windows)
                {
                    window.Show();

                    // All windows should have consistent theming
                    Assert.NotNull(window.Background);

                    // All windows should be enabled
                    Assert.True(window.IsEnabled);

                    window.Close();
                }
            });
        }

        [StaFact]
        public void AllViews_Accessibility_ShouldBeConsistent()
        {
            RunOnUIThread(() =>
            {
                var windows = new Window[]
                {
                    new MainWindow(),
                    new DashboardView(),
                    new BudgetView(),
                    new EnterpriseView(),
                    new SettingsView(),
                    new AIAssistView(),
                    new AboutWindow(),
                    new SplashScreenWindow(),
                    new UtilityCustomerView()
                };

                foreach (var window in windows)
                {
                    window.Show();

                    // All windows should have titles (except splash screen)
                    if (!(window is SplashScreenWindow))
                    {
                        Assert.False(string.IsNullOrEmpty(window.Title));
                    }

                    window.Close();
                }
            });
        }

        #endregion

        #region Performance Testing for All Views

        [StaFact]
        public void AllViews_Rendering_ShouldBeFast()
        {
            RunOnUIThread(() =>
            {
                var windows = new Window[]
                {
                    new MainWindow(),
                    new DashboardView(),
                    new BudgetView(),
                    new EnterpriseView(),
                    new SettingsView(),
                    new AIAssistView(),
                    new AboutWindow(),
                    new SplashScreenWindow(),
                    new UtilityCustomerView()
                };

                foreach (var window in windows)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    window.Show();
                    window.UpdateLayout();
                    stopwatch.Stop();

                    // Each view should render within reasonable time
                    Assert.True(stopwatch.ElapsedMilliseconds < 2000,
                        $"{window.GetType().Name} rendering took {stopwatch.ElapsedMilliseconds}ms, expected < 2000ms");

                    window.Close();
                }
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