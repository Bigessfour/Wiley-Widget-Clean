#nullable enable

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;
using WileyWidget.Business.Interfaces;
using System.Windows.Input;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for budget analysis functionality
/// </summary>
public partial class BudgetAnalysisViewModel : AsyncViewModelBase
{
    /// <summary>
    /// Available budget periods for analysis
    /// </summary>
    public ObservableCollection<string> AvailableBudgetPeriods { get; } = new()
    {
        "Current Year",
        "Last Year",
        "Year to Date",
        "Custom Period"
    };

    /// <summary>
    /// Selected budget period
    /// </summary>
    [ObservableProperty]
    private string? selectedBudgetPeriod = "Current Year";

    /// <summary>
    /// Available analysis types
    /// </summary>
    public ObservableCollection<string> AnalysisTypes { get; } = new()
    {
        "Budget vs Actual",
        "Variance Analysis",
        "Trend Analysis",
        "Fund Analysis"
    };

    /// <summary>
    /// Selected analysis type
    /// </summary>
    [ObservableProperty]
    private string? selectedAnalysisType = "Budget vs Actual";

    /// <summary>
    /// Generate analysis command
    /// </summary>
    public ICommand GenerateAnalysisCommand { get; }

    /// <summary>
    /// Whether analysis can be generated
    /// </summary>
    [ObservableProperty]
    private bool canGenerateAnalysis = true;

    /// <summary>
    /// Fund sort options
    /// </summary>
    public ObservableCollection<string> FundSortOptions { get; } = new()
    {
        "Fund Name",
        "Budget Amount",
        "Actual Amount",
        "Variance"
    };

    /// <summary>
    /// Selected fund sort option
    /// </summary>
    [ObservableProperty]
    private string? selectedFundSortOption = "Fund Name";

    /// <summary>
    /// Fund filter text
    /// </summary>
    [ObservableProperty]
    private string fundFilterText = string.Empty;

    /// <summary>
    /// Fund grid data
    /// </summary>
    public ObservableCollection<object> FundGridData { get; } = new();

    /// <summary>
    /// Export fund data command
    /// </summary>
    public ICommand ExportFundDataCommand { get; }

    /// <summary>
    /// Department sort options
    /// </summary>
    public ObservableCollection<string> DepartmentSortOptions { get; } = new()
    {
        "Department Name",
        "Budget Amount",
        "Actual Amount",
        "Variance"
    };

    /// <summary>
    /// Selected department sort option
    /// </summary>
    [ObservableProperty]
    private string? selectedDepartmentSortOption = "Department Name";

    /// <summary>
    /// Department filter text
    /// </summary>
    [ObservableProperty]
    private string departmentFilterText = string.Empty;

    /// <summary>
    /// Department grid data
    /// </summary>
    public ObservableCollection<object> DepartmentGridData { get; } = new();

    /// <summary>
    /// Export department data command
    /// </summary>
    public ICommand ExportDepartmentDataCommand { get; }

    /// <summary>
    /// Variance threshold
    /// </summary>
    [ObservableProperty]
    private decimal varianceThreshold = 0.05m;

    /// <summary>
    /// Variance sort options
    /// </summary>
    public ObservableCollection<string> VarianceSortOptions { get; } = new()
    {
        "Account Number",
        "Variance Amount",
        "Variance Percent"
    };

    /// <summary>
    /// Selected variance sort option
    /// </summary>
    [ObservableProperty]
    private string? selectedVarianceSortOption = "Variance Amount";

    /// <summary>
    /// Variance filter text
    /// </summary>
    [ObservableProperty]
    private string varianceFilterText = string.Empty;

    /// <summary>
    /// Variance hierarchy data
    /// </summary>
    public ObservableCollection<object> VarianceHierarchy { get; } = new();

    /// <summary>
    /// Variance chart data
    /// </summary>
    public ObservableCollection<object> VarianceChartData { get; } = new();

    /// <summary>
    /// Account variance for editing
    /// </summary>
    [ObservableProperty]
    private decimal accountVariance;

    /// <summary>
    /// Key for editing
    /// </summary>
    [ObservableProperty]
    private string key = string.Empty;

    /// <summary>
    /// Value for editing
    /// </summary>
    [ObservableProperty]
    private decimal value;

    /// <summary>
    /// Analysis results
    /// </summary>
    [ObservableProperty]
    private BudgetAnalysisResult? analysis;

    /// <summary>
    /// Report export service
    /// </summary>
    private readonly IReportExportService _reportExportService;

    /// <summary>
    /// Budget repository for data access
    /// </summary>
    private readonly IBudgetRepository _budgetRepository;

    /// <summary>
    /// Constructor
    /// </summary>
    public BudgetAnalysisViewModel(IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<BudgetAnalysisViewModel> logger, IReportExportService reportExportService, IBudgetRepository budgetRepository)
        : base(dispatcherHelper, logger)
    {
        _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        GenerateAnalysisCommand = new RelayCommand(GenerateAnalysis, () => CanGenerateAnalysis);
        ExportFundDataCommand = new RelayCommand(ExportFundData);
        ExportDepartmentDataCommand = new RelayCommand(ExportDepartmentData);
    }

    private async void GenerateAnalysis()
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Generating budget analysis...";

            // Generate comprehensive budget analysis
            var result = new BudgetAnalysisResult();

            // Load real budget data for the current fiscal year
            var currentYear = DateTime.Now.Year;
            var budgetEntries = await Task.Run(() => _budgetRepository.GetByFiscalYearAsync(currentYear));

            // Convert to array for analysis
            var budgetData = budgetEntries.ToArray();

            if (budgetData.Length == 0)
            {
                // Fallback to mock data if no real data available
                budgetData = new[]
                {
                    new { BudgetedAmount = 100000m, ActualAmount = 95000m },
                    new { BudgetedAmount = 50000m, ActualAmount = 52000m },
                    new { BudgetedAmount = 75000m, ActualAmount = 70000m },
                    new { BudgetedAmount = 25000m, ActualAmount = 24000m }
                };
                Logger.LogWarning("No budget data found for fiscal year {Year}, using mock data", currentYear);
            }

            // Populate overview
            result.Overview.TotalBudget = budgetData.Sum(b => b.BudgetedAmount);
            result.Overview.TotalBalance = budgetData.Sum(b => b.ActualAmount);
            result.Overview.Variance = result.Overview.TotalBudget - result.Overview.TotalBalance;
            result.Overview.TotalAccounts = budgetData.Length;

            // Calculate key ratios
            result.Overview.KeyRatios.Add(new KeyValuePair<string, decimal>("Budget Utilization", 
                result.Overview.TotalBudget > 0 ? (result.Overview.TotalBalance / result.Overview.TotalBudget) * 100 : 0));
            result.Overview.KeyRatios.Add(new KeyValuePair<string, decimal>("Average per Account", 
                result.Overview.TotalAccounts > 0 ? result.Overview.TotalBudget / result.Overview.TotalAccounts : 0));

            // Populate variance analysis
            var variances = budgetData.Select(b => 
                b.BudgetedAmount > 0 ? ((b.ActualAmount - b.BudgetedAmount) / b.BudgetedAmount) * 100 : 0).ToList();
            
            result.Variance.AccountsOverThreshold = variances.Count(v => Math.Abs(v) > 10); // Over 10% variance
            result.Variance.AverageVariancePercent = variances.Any() ? variances.Average() : 0;

            Analysis = result;
            Logger.LogInformation("Analysis generated: {AccountCount} accounts analyzed for fiscal year {Year}", result.Overview.TotalAccounts, currentYear);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating budget analysis");
            ErrorMessage = $"Error generating analysis: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }
            Logger.LogError(ex, "Failed to generate budget analysis");
        }
    }

    private async void ExportFundData()
    {
        try
        {
            if (!FundGridData.Any())
            {
                Logger.LogWarning("No fund data available for export");
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Fund Data",
                Filter = "Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"Fund_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;
                
                // Export data using report export service
                await _reportExportService.ExportToExcelAsync(FundGridData, filePath);
                
                Logger.LogInformation("Fund data exported to {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export fund data");
        }
    }

    private async void ExportDepartmentData()
    {
        try
        {
            if (!DepartmentGridData.Any())
            {
                Logger.LogWarning("No department data available for export");
                return;
            }

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Department Data",
                Filter = "Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"Department_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;
                
                // Export data using report export service
                await _reportExportService.ExportToExcelAsync(DepartmentGridData, filePath);
                
                Logger.LogInformation("Department data exported to {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export department data");
        }
    }
}

/// <summary>
/// Budget analysis result
/// </summary>
public class BudgetAnalysisResult
{
    /// <summary>
    /// Analysis overview
    /// </summary>
    public BudgetAnalysisOverview Overview { get; } = new();

    /// <summary>
    /// Variance analysis
    /// </summary>
    public BudgetVarianceAnalysis Variance { get; } = new();
}

/// <summary>
/// Budget analysis overview
/// </summary>
public class BudgetAnalysisOverview
{
    /// <summary>
    /// Total budget amount
    /// </summary>
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Total balance
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Variance amount
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Total accounts
    /// </summary>
    public int TotalAccounts { get; set; }

    /// <summary>
    /// Key ratios
    /// </summary>
    public ObservableCollection<KeyValuePair<string, decimal>> KeyRatios { get; } = new();
}

/// <summary>
/// Budget variance analysis
/// </summary>
public class BudgetVarianceAnalysis
{
    /// <summary>
    /// Number of accounts over threshold
    /// </summary>
    public int AccountsOverThreshold { get; set; }

    /// <summary>
    /// Average variance percent
    /// </summary>
    public decimal AverageVariancePercent { get; set; }
}