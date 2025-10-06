using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.Grid.Helpers;
using Syncfusion.Windows.Shared;
using Serilog;

namespace WileyWidget;


/// <summary>
/// Budget Analysis and Reporting Window
/// Provides comprehensive financial analysis and reporting capabilities
/// </summary>
public partial class BudgetView : Window
{
    public BudgetView()
    {
        InitializeComponent();

        EnsureNamedElementsAreDiscoverable();
        ConfigureDataGridEnhancements();

        // DataContext setup moved to Loaded event to avoid XAML load issues
        Loaded += async (s, e) =>
        {
            if (DataContext is BudgetViewModel vm && vm.BudgetDetails.Count == 0)
            {
                await vm.RefreshBudgetDataAsync();
            }
        };
    }

    /// <summary>
    /// Internal constructor for unit testing scenarios with a preconfigured view model.
    /// </summary>
    /// <param name="viewModel">Budget view model to bind to the view.</param>
    public BudgetView(BudgetViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        EnsureNamedElementsAreDiscoverable();

        DataContext = viewModel;

        ConfigureDataGridEnhancements();

        Loaded += async (s, e) =>
        {
            if (DataContext is BudgetViewModel vm && !vm.BudgetDetails.Any())
            {
                await vm.RefreshBudgetDataAsync();
            }
        };
    }

    private void ConfigureDataGridEnhancements()
    {
        if (BudgetDetailsGrid is null)
        {
            return;
        }

        BudgetDetailsGrid.ShowRowHeader = true;
        BudgetDetailsGrid.AllowRowHoverHighlighting = true;
    }

    private void EnsureNamedElementsAreDiscoverable()
    {
        EnsureNameRegistered(BudgetDetailsGrid, nameof(BudgetDetailsGrid));
        EnsureNameRegistered(BudgetRibbon, nameof(BudgetRibbon));
        EnsureNameRegistered(BudgetSpreadsheet, nameof(BudgetSpreadsheet));
    }

    private void EnsureNameRegistered(FrameworkElement? element, string name)
    {
        if (element is null)
        {
            return;
        }

        var scope = NameScope.GetNameScope(this);
        if (scope is null)
        {
            scope = new NameScope();
            NameScope.SetNameScope(this, scope);
        }

        if (scope.FindName(name) is null)
        {
            scope.RegisterName(name, element);
        }
    }

    private void BudgetDetailsGrid_CellToolTipOpening(object sender, GridCellToolTipOpeningEventArgs e)
    {
        if (e.Record is not BudgetDetailItem detail || e.Column is null)
        {
            return;
        }

        string? message = null;

        switch (e.Column.MappingName)
        {
            case nameof(BudgetDetailItem.MonthlyBalance):
                if (detail.MonthlyBalance < 0)
                {
                    message = "Deficit alert: Expenses are outpacing revenue for this enterprise.";
                }
                else if (detail.MonthlyBalance > 0)
                {
                    message = "Surplus: Revenue currently exceeds expenses.";
                }
                break;
            case nameof(BudgetDetailItem.BreakEvenRate):
                var delta = detail.BreakEvenRate - detail.CurrentRate;
                if (delta > 0)
                {
                    message = $"Needs a rate increase of {delta:C2} to reach break-even.";
                }
                else if (delta < 0)
                {
                    message = $"Current rate is {Math.Abs(delta):C2} above break-even.";
                }
                break;
            case nameof(BudgetDetailItem.Status):
                if (detail.Status.Equals("Deficit", StringComparison.OrdinalIgnoreCase))
                {
                    message = "Flagged for remediation: consider expense controls or rate adjustments.";
                }
                else if (detail.Status.Equals("Surplus", StringComparison.OrdinalIgnoreCase))
                {
                    message = "Healthy performance: monitor for reinvestment opportunities.";
                }
                break;
        }

        if (message is not null)
        {
            e.ToolTip.Content = message;
        }
    }

    /// <summary>
    /// Show the Budget Analysis window
    /// </summary>
    public static void ShowBudgetWindow()
    {
        var window = new BudgetView();
        window.Show();
    }

    /// <summary>
    /// Show the Budget Analysis window as dialog
    /// </summary>
    public static bool? ShowBudgetDialog()
    {
        var window = new BudgetView();
        return window.ShowDialog();
    }

    public new object? FindName(string name)
    {
        var result = base.FindName(name);
        if (result is not null)
        {
            return result;
        }

        return name switch
        {
            nameof(BudgetDetailsGrid) => BudgetDetailsGrid,
            nameof(BudgetRibbon) => BudgetRibbon,
            nameof(BudgetSpreadsheet) => BudgetSpreadsheet,
            _ => null
        };
    }
}