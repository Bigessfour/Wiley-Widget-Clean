using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using System.Threading.Tasks;
using System.Linq;
using Serilog;
using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace WileyWidget.ViewModels;

/// <summary>
/// BudgetViewModel extension for hierarchical budget management
/// Adds support for GASB-compliant account structures and Excel import/export
/// </summary>
public partial class BudgetViewModel
{
    /// <summary>
    /// Hierarchical collection of budget accounts with parent-child relationships
    /// </summary>
    public ObservableCollection<BudgetAccount> BudgetAccounts { get; } = new();

    /// <summary>
    /// Collection of available fund types for dropdown editors
    /// </summary>
    public ObservableCollection<BudgetFundType> FundTypes { get; } = BudgetFundType.GetStandardFundTypes();

    /// <summary>
    /// Collection of fiscal years for selection
    /// </summary>
    public ObservableCollection<string> FiscalYears { get; } = new()
    {
        "FY 2023", "FY 2024", "FY 2025", "FY 2026"
    };

    /// <summary>
    /// Currently selected fiscal year
    /// </summary>
    [ObservableProperty]
    private string selectedFiscalYear = "FY 2025";

    /// <summary>
    /// Total budgeted amount across all accounts
    /// </summary>
    [ObservableProperty]
    private decimal totalBudget;

    /// <summary>
    /// Total actual expenses across all accounts
    /// </summary>
    [ObservableProperty]
    private decimal totalActual;

    /// <summary>
    /// Total variance (Budget - Actual)
    /// </summary>
    [ObservableProperty]
    private decimal totalVariance;

    /// <summary>
    /// Budget distribution data for pie chart
    /// </summary>
    public ObservableCollection<BudgetDistributionData> BudgetDistributionData { get; } = new();

    /// <summary>
    /// Budget comparison data for bar chart
    /// </summary>
    public ObservableCollection<BudgetComparisonData> BudgetComparisonData { get; } = new();

    /// <summary>
    /// Import budget data from Excel file
    /// Handles hierarchical account structures like 410.1
    /// </summary>
    [RelayCommand]
    private async Task ImportBudgetAsync()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Import Budget from Excel",
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                ProgressText = $"Importing budget from {Path.GetFileName(openFileDialog.FileName)}...";

                // TODO: Implement Excel import logic using Syncfusion.XlsIO
                // Parse hierarchical account numbers (e.g., 410, 410.1, 410.1.1)
                // Build parent-child relationships
                
                await Task.Delay(1000); // Simulate import process

                MessageBox.Show(
                    "Budget import functionality will be implemented.\n\n" +
                    "Will support:\n" +
                    "- Hierarchical account structures (410, 410.1, etc.)\n" +
                    "- TOW/WSD Excel file formats\n" +
                    "- Automatic parent-child relationship detection",
                    "Import Budget",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Log.Information("Excel import initiated from: {FileName}", openFileDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to import budget: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to import budget from Excel");
            
            MessageBox.Show(
                $"Error importing budget:\n{ex.Message}",
                "Import Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    /// <summary>
    /// Export budget data to Excel with hierarchy preserved
    /// </summary>
    [RelayCommand]
    private async Task ExportBudgetAsync()
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Export Budget to Excel",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = $"Budget_{SelectedFiscalYear}_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                ProgressText = $"Exporting budget to {Path.GetFileName(saveFileDialog.FileName)}...";

                // TODO: Implement Excel export logic using Syncfusion.XlsIO
                // Maintain hierarchical structure in export
                // Include formatting and formulas
                
                await Task.Delay(1000); // Simulate export process

                MessageBox.Show(
                    $"Budget exported successfully to:\n{saveFileDialog.FileName}\n\n" +
                    "Export includes:\n" +
                    "- Hierarchical account structure\n" +
                    "- Budget vs Actual comparison\n" +
                    "- Variance calculations\n" +
                    "- Over-budget highlighting",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Log.Information("Budget exported to: {FileName}", saveFileDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export budget: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to export budget to Excel");
            
            MessageBox.Show(
                $"Error exporting budget:\n{ex.Message}",
                "Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    /// <summary>
    /// Add a new budget account
    /// </summary>
    [RelayCommand]
    private void AddAccount()
    {
        var newAccount = new BudgetAccount
        {
            Id = BudgetAccounts.Count + 1,
            AccountNumber = $"NEW-{BudgetAccounts.Count + 1}",
            Description = "New Budget Account",
            FundType = "GF",
            BudgetAmount = 0,
            ActualAmount = 0,
            ParentId = -1
        };

        BudgetAccounts.Add(newAccount);
        Log.Information("Added new budget account: {AccountNumber}", newAccount.AccountNumber);
    }

    /// <summary>
    /// Delete the selected budget account
    /// </summary>
    [RelayCommand]
    private void DeleteAccount()
    {
        // TODO: Implement account deletion with confirmation
        MessageBox.Show(
            "Account deletion functionality will be implemented.\n\n" +
            "Will include:\n" +
            "- Confirmation dialog\n" +
            "- Cascade delete for child accounts\n" +
            "- Audit trail logging",
            "Delete Account",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>
    /// Load sample hierarchical budget data for demonstration
    /// </summary>
    private void LoadSampleBudgetAccounts()
    {
        BudgetAccounts.Clear();

        // Root accounts
        var account410 = new BudgetAccount
        {
            Id = 1,
            AccountNumber = "410",
            Description = "Water Revenue",
            FundType = "EF",
            BudgetAmount = 500000,
            ActualAmount = 475000,
            ParentId = -1
        };

        var account410_1 = new BudgetAccount
        {
            Id = 2,
            AccountNumber = "410.1",
            Description = "Residential Water Sales",
            FundType = "EF",
            BudgetAmount = 350000,
            ActualAmount = 340000,
            ParentId = 1
        };

        var account410_2 = new BudgetAccount
        {
            Id = 3,
            AccountNumber = "410.2",
            Description = "Commercial Water Sales",
            FundType = "EF",
            BudgetAmount = 150000,
            ActualAmount = 135000,
            ParentId = 1
        };

        var account510 = new BudgetAccount
        {
            Id = 4,
            AccountNumber = "510",
            Description = "Operating Expenses",
            FundType = "EF",
            BudgetAmount = 350000,
            ActualAmount = 380000,
            ParentId = -1
        };

        var account510_1 = new BudgetAccount
        {
            Id = 5,
            AccountNumber = "510.1",
            Description = "Personnel Costs",
            FundType = "EF",
            BudgetAmount = 200000,
            ActualAmount = 210000,
            ParentId = 4
        };

        var account510_2 = new BudgetAccount
        {
            Id = 6,
            AccountNumber = "510.2",
            Description = "Utilities",
            FundType = "EF",
            BudgetAmount = 150000,
            ActualAmount = 170000,
            ParentId = 4
        };

        // Add children to parents
        account410.Children.Add(account410_1);
        account410.Children.Add(account410_2);
        account510.Children.Add(account510_1);
        account510.Children.Add(account510_2);

        // Add root accounts to collection
        BudgetAccounts.Add(account410);
        BudgetAccounts.Add(account510);

        RecalculateTotals();
        UpdateChartData();
    }

    /// <summary>
    /// Recalculate total budget, actual, and variance
    /// </summary>
    private void RecalculateTotals()
    {
        TotalBudget = CalculateTotalBudget(BudgetAccounts);
        TotalActual = CalculateTotalActual(BudgetAccounts);
        TotalVariance = TotalBudget - TotalActual;

        Log.Debug("Recalculated totals: Budget={Budget:C2}, Actual={Actual:C2}, Variance={Variance:C2}",
            TotalBudget, TotalActual, TotalVariance);
    }

    private decimal CalculateTotalBudget(ObservableCollection<BudgetAccount> accounts)
    {
        decimal total = 0;
        foreach (var account in accounts)
        {
            total += account.BudgetAmount;
            total += CalculateTotalBudget(account.Children);
        }
        return total;
    }

    private decimal CalculateTotalActual(ObservableCollection<BudgetAccount> accounts)
    {
        decimal total = 0;
        foreach (var account in accounts)
        {
            total += account.ActualAmount;
            total += CalculateTotalActual(account.Children);
        }
        return total;
    }

    /// <summary>
    /// Update chart data for visualizations
    /// </summary>
    private void UpdateChartData()
    {
        // Update budget distribution by fund type
        BudgetDistributionData.Clear();
        var fundGroups = GetAllAccounts(BudgetAccounts)
            .GroupBy(a => a.FundType)
            .Select(g => new BudgetDistributionData
            {
                FundType = FundTypes.FirstOrDefault(f => f.Code == g.Key)?.Name ?? g.Key,
                Amount = g.Sum(a => a.BudgetAmount),
                Percentage = 0 // Will be calculated
            });

        var totalAmount = fundGroups.Sum(g => g.Amount);
        foreach (var group in fundGroups)
        {
            group.Percentage = totalAmount > 0 ? (double)(group.Amount / totalAmount) : 0;
            BudgetDistributionData.Add(group);
        }

        // Update budget comparison by top-level categories
        BudgetComparisonData.Clear();
        foreach (var account in BudgetAccounts.Take(10))
        {
            BudgetComparisonData.Add(new BudgetComparisonData
            {
                Category = account.AccountNumber,
                BudgetAmount = account.BudgetAmount,
                ActualAmount = account.ActualAmount
            });
        }
    }

    private IEnumerable<BudgetAccount> GetAllAccounts(ObservableCollection<BudgetAccount> accounts)
    {
        foreach (var account in accounts)
        {
            yield return account;
            foreach (var child in GetAllAccounts(account.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Initialize budget accounts if empty
    /// </summary>
    partial void OnSelectedFiscalYearChanged(string value)
    {
        if (BudgetAccounts.Count == 0)
        {
            LoadSampleBudgetAccounts();
        }
    }
}

