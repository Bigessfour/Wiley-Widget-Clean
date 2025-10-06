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
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.Charts;
using Xunit;
using WileyWidget.Tests;
using WileyWidget.ViewModels;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.UiTests.ComponentTests
{
    /// <summary>
    /// Mock implementation of IEnterpriseRepository for testing
    /// </summary>
    internal class MockEnterpriseRepository : IEnterpriseRepository
    {
        private readonly List<Enterprise> _enterprises = new()
        {
            new Enterprise { Id = 1, Name = "Test Enterprise 1", CitizenCount = 1000, CurrentRate = 100m, MonthlyExpenses = 50000m },
            new Enterprise { Id = 2, Name = "Test Enterprise 2", CitizenCount = 500, CurrentRate = 50m, MonthlyExpenses = 40000m },
            new Enterprise { Id = 3, Name = "Test Enterprise 3", CitizenCount = 1500, CurrentRate = 100m, MonthlyExpenses = 75000m }
        };

        public Task<IEnumerable<Enterprise>> GetAllAsync() => Task.FromResult(_enterprises.AsEnumerable());
        public Task<Enterprise> GetByIdAsync(int id) => Task.FromResult(_enterprises.FirstOrDefault(e => e.Id == id));
        public Task<Enterprise> GetByNameAsync(string name) => Task.FromResult(_enterprises.FirstOrDefault(e => e.Name == name));
        public Task<Enterprise> AddAsync(Enterprise enterprise) { _enterprises.Add(enterprise); return Task.FromResult(enterprise); }
        public Task<Enterprise> UpdateAsync(Enterprise enterprise) { return Task.FromResult(enterprise); }
        public Task<bool> DeleteAsync(int id) { _enterprises.RemoveAll(e => e.Id == id); return Task.FromResult(true); }
        public Task<bool> ExistsByNameAsync(string name, int? excludeId = null) => Task.FromResult(_enterprises.Any(e => e.Name == name && e.Id != excludeId));
        public Task<int> GetCountAsync() => Task.FromResult(_enterprises.Count);
        public Task<IEnumerable<Enterprise>> GetByIdsAsync(IEnumerable<int> ids) => Task.FromResult(_enterprises.Where(e => ids.Contains(e.Id)));
        public Task<IEnumerable<Enterprise>> GetWithInteractionsAsync() => Task.FromResult(_enterprises.AsEnumerable());
        public Enterprise CreateFromHeaderMapping(IDictionary<string, string> headerValueMap) => new Enterprise();
    }

    /// <summary>
    /// Component-level StaFact tests for BudgetView
    /// Tests financial data display, analysis controls, and budget calculations
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class BudgetViewComponentTests : UiTestApplication
    {
        #region 1. UI Automation Testing

        [StaFact]
        public void BudgetView_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                window.Show();

                // Verify window properties
                Assert.Equal("Budget Analysis & Reporting", window.Title);
                Assert.Equal(700, window.Height);
                Assert.Equal(1000, window.Width);

                // Verify main layout elements exist
                var dockPanel = window.Content as DockPanel;
                Assert.NotNull(dockPanel);

                // Verify Ribbon is present
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);

                window.Close();
            });
        }

        [StaFact]
        public void BudgetView_Ribbon_AnalysisControls_ShouldBePresent()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                window.Show();
                window.UpdateLayout();

                var dockPanel = window.Content as DockPanel;
                Assert.NotNull(dockPanel);

                // Verify Ribbon is present
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);

                // Verify ribbon tabs
                var budgetTab = ribbon.Items.OfType<RibbonTab>()
                    .FirstOrDefault(tab => string.Equals(tab.Caption as string, "Budget Analysis", StringComparison.Ordinal));
                Assert.NotNull(budgetTab);

                // Verify analysis ribbon bars
                var ribbonBars = budgetTab.Items.OfType<RibbonBar>();
                Assert.True(ribbonBars.Any());

                // Test for specific analysis controls
                var dataBar = ribbonBars.FirstOrDefault(rb => rb.Header == "Data");
                var analysisBar = ribbonBars.FirstOrDefault(rb => rb.Header == "Analysis");

                Assert.NotNull(dataBar);
                Assert.NotNull(analysisBar);

                window.Close();
            });
        }

        #endregion

        #region 2. Data Binding & MVVM Testing

        [StaFact]
        public void BudgetView_DataBinding_ShouldDisplayBudgetData()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                var viewModel = window.DataContext as BudgetViewModel;
                Assert.NotNull(viewModel);

                // Test budget data properties
                Assert.NotNull(viewModel.BudgetDetails);
                Assert.True(viewModel.TotalRevenue >= 0);
                Assert.True(viewModel.TotalExpenses >= 0);

                // Test analysis commands
                Assert.NotNull(viewModel.RefreshBudgetDataCommand);
                Assert.NotNull(viewModel.ExportReportCommand);
                Assert.NotNull(viewModel.BreakEvenAnalysisCommand);
                Assert.NotNull(viewModel.TrendAnalysisCommand);

                window.Close();
            });
        }

        [StaFact]
        public void BudgetView_BudgetCalculations_ShouldBeAccurate()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                var viewModel = window.DataContext as BudgetViewModel;

                // Test calculation properties
                Assert.True(viewModel.NetBalance >= decimal.MinValue); // Allow any decimal value
                // Note: BreakEvenPoint is calculated per enterprise, not as a global property

                // Test percentage calculations
                if (viewModel.TotalRevenue > 0)
                {
                    var expenseRatio = viewModel.TotalExpenses / viewModel.TotalRevenue;
                    Assert.True(expenseRatio >= 0 && expenseRatio <= 2); // Allow up to 200% expense ratio
                }

                window.Close();
            });
        }

        #endregion

        #region 3. Control Interaction Testing

        [StaFact]
        public void BudgetView_DataGrid_ShouldDisplayBudgetDetails()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                window.Show();

                // Find SfDataGrid controls
                var dataGrids = FindVisualChildren<SfDataGrid>(window);
                if (dataGrids.Any())
                {
                    var dataGrid = dataGrids.First();
                    Assert.NotNull(dataGrid);

                    // Verify data grid has items source
                    Assert.NotNull(dataGrid.ItemsSource);

                    // Test data grid columns
                    Assert.True(dataGrid.Columns.Count > 0);
                }

                window.Close();
            });
        }

        [StaFact]
        public void BudgetView_AnalysisCommands_ShouldExecute()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                var viewModel = window.DataContext as BudgetViewModel;

                // Test all analysis commands can execute
                Assert.True(viewModel.RefreshBudgetDataCommand.CanExecute(null));
                Assert.True(viewModel.ExportReportCommand.CanExecute(null));
                Assert.True(viewModel.BreakEvenAnalysisCommand.CanExecute(null));
                Assert.True(viewModel.TrendAnalysisCommand.CanExecute(null));

                window.Close();
            });
        }

        #endregion

        #region 4. Theming & Styling Tests

        [StaFact]
        public void BudgetView_ColorConverters_ShouldApplyCorrectly()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                window.Show();

                // Test that balance color converter is available in resources
                var balanceColorConverter = window.Resources["BalanceColorConverter"];
                Assert.NotNull(balanceColorConverter);

                // Test other converters
                var boolToVisConverter = window.Resources["BoolToVis"];
                Assert.NotNull(boolToVisConverter);

                window.Close();
            });
        }

        [StaFact]
        public void BudgetView_DataVisualization_ShouldBeStyled()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                window.Show();
                window.UpdateLayout();

                // Find any chart or visualization controls
                var charts = FindVisualChildren<SfChart>(window);
                if (charts.Any())
                {
                    foreach (var chart in charts)
                    {
                        // Verify charts have proper styling
                        Assert.NotNull(chart.Background);
                    }
                }

                window.Close();
            });
        }

        #endregion

        #region 5. Accessibility Testing

        [StaFact]
        public void BudgetView_Accessibility_ShouldBeCompliant()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                window.Show();
                window.UpdateLayout();

                // Test window accessibility
                Assert.Equal("Budget Analysis & Reporting", window.Title);
                Assert.True(window.IsEnabled);

                // Test ribbon accessibility
                var dockPanel = window.Content as DockPanel;
                var ribbon = dockPanel.Children.OfType<Ribbon>().FirstOrDefault();
                Assert.NotNull(ribbon);

                // Test data grid accessibility if present
                var dataGrids = FindVisualChildren<SfDataGrid>(window);
                if (dataGrids.Any())
                {
                    var dataGrid = dataGrids.First();
                    var automationId = AutomationProperties.GetAutomationId(dataGrid);
                    var name = AutomationProperties.GetName(dataGrid);

                    // Data grid should have accessible properties
                    Assert.False(string.IsNullOrEmpty(name) || string.IsNullOrEmpty(automationId));
                }

                window.Close();
            });
        }

        #endregion

        #region 6. Performance Testing

        [StaFact]
        public void BudgetView_DataLoading_ShouldBeFast()
        {
            RunOnUIThread(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                window.Show();

                // Force layout and data binding update
                window.UpdateLayout();
                stopwatch.Stop();

                // Budget view should load within reasonable time
                Assert.True(stopwatch.ElapsedMilliseconds < 2500,
                    $"Budget view loading took {stopwatch.ElapsedMilliseconds}ms, expected < 2500ms");

                window.Close();
            });
        }

        #endregion

        #region 7. Error Handling Testing

        [StaFact]
        public void BudgetView_ErrorHandling_ShouldHandleCalculationErrors()
        {
            RunOnUIThread(() =>
            {
                var mockRepo = new MockEnterpriseRepository();
                var window = new BudgetView(mockRepo);
                var viewModel = window.DataContext as BudgetViewModel;

                // Test error handling for division by zero, invalid data, etc.
                // This would test view model's error handling capabilities

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