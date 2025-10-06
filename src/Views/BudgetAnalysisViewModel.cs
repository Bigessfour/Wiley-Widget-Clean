using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;

namespace WileyWidget.Views;

/// <summary>
/// Analysis type enumeration.
/// </summary>
public enum AnalysisType
{
    /// <summary>
    /// Overview analysis.
    /// </summary>
    Overview,

    /// <summary>
    /// Fund-based analysis.
    /// </summary>
    FundAnalysis,

    /// <summary>
    /// Department-based analysis.
    /// </summary>
    DepartmentAnalysis,

    /// <summary>
    /// Variance analysis.
    /// </summary>
    VarianceAnalysis,

    /// <summary>
    /// Comprehensive analysis.
    /// </summary>
    Comprehensive
}

/// <summary>
/// ViewModel for the budget analysis functionality.
/// </summary>
public partial class BudgetAnalysisViewModel : AsyncViewModelBase
{
    private readonly AppDbContext _context;
    private readonly MunicipalAccountingService _accountingService;

    [ObservableProperty]
    private ObservableCollection<BudgetPeriod> availableBudgetPeriods = new();

    [ObservableProperty]
    private BudgetPeriod? selectedBudgetPeriod;

    [ObservableProperty]
    private ObservableCollection<AnalysisType> analysisTypes = new();

    [ObservableProperty]
    private AnalysisType selectedAnalysisType = AnalysisType.Comprehensive;

    [ObservableProperty]
    private BudgetAnalysisResult analysis = new();

    [ObservableProperty]
    private decimal varianceThreshold = 0.10m; // 10%

    [ObservableProperty]
    private ObservableCollection<FundNode> fundHierarchy = new();

    [ObservableProperty]
    private ObservableCollection<FlatFundItem> fundGridData = new();

    [ObservableProperty]
    private ObservableCollection<DepartmentNode> departmentHierarchy = new();

    [ObservableProperty]
    private ObservableCollection<FlatDepartmentItem> departmentGridData = new();

    [ObservableProperty]
    private ObservableCollection<VarianceNode> varianceHierarchy = new();

    [ObservableProperty]
    private ObservableCollection<VarianceChartItem> varianceChartData = new();

    [ObservableProperty]
    private ObservableCollection<SortOption> fundSortOptions = new();

    [ObservableProperty]
    private ObservableCollection<SortOption> departmentSortOptions = new();

    [ObservableProperty]
    private ObservableCollection<SortOption> varianceSortOptions = new();

    [ObservableProperty]
    private SortOption? selectedFundSortOption;

    [ObservableProperty]
    private SortOption? selectedDepartmentSortOption;

    [ObservableProperty]
    private SortOption? selectedVarianceSortOption;

    [ObservableProperty]
    private string fundFilterText = string.Empty;

    [ObservableProperty]
    private string departmentFilterText = string.Empty;

    [ObservableProperty]
    private string varianceFilterText = string.Empty;

    /// <summary>
    /// Gets whether analysis can be generated.
    /// </summary>
    public bool CanGenerateAnalysis => SelectedBudgetPeriod != null && !IsLoading;

    /// <summary>
    /// Initializes a new instance of the BudgetAnalysisViewModel class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="accountingService">The accounting service.</param>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations.</param>
    /// <param name="logger">Logger instance.</param>
    public BudgetAnalysisViewModel(
        AppDbContext context,
        MunicipalAccountingService accountingService,
        IDispatcherHelper dispatcherHelper,
        ILogger<BudgetAnalysisViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));

        InitializeAnalysisTypes();
        InitializeSortOptions();
        LoadBudgetPeriodsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Initializes the analysis types.
    /// </summary>
    private void InitializeAnalysisTypes()
    {
        AnalysisTypes = new ObservableCollection<AnalysisType>(
            Enum.GetValues(typeof(AnalysisType)).Cast<AnalysisType>());
    }

    /// <summary>
    /// Initializes the sort options for each analysis type.
    /// </summary>
    private void InitializeSortOptions()
    {
        // Fund sort options
        FundSortOptions = new ObservableCollection<SortOption>
        {
            new SortOption("Fund Name (A-Z)", "DisplayName"),
            new SortOption("Fund Name (Z-A)", "DisplayName", true),
            new SortOption("Budget (Low-High)", "TotalBudget"),
            new SortOption("Budget (High-Low)", "TotalBudget", true),
            new SortOption("Variance (Low-High)", "Variance"),
            new SortOption("Variance (High-Low)", "Variance", true)
        };
        SelectedFundSortOption = FundSortOptions.FirstOrDefault();

        // Department sort options
        DepartmentSortOptions = new ObservableCollection<SortOption>
        {
            new SortOption("Department Name (A-Z)", "DisplayName"),
            new SortOption("Department Name (Z-A)", "DisplayName", true),
            new SortOption("Budget (Low-High)", "TotalBudget"),
            new SortOption("Budget (High-Low)", "TotalBudget", true),
            new SortOption("Variance (Low-High)", "Variance"),
            new SortOption("Variance (High-Low)", "Variance", true)
        };
        SelectedDepartmentSortOption = DepartmentSortOptions.FirstOrDefault();

        // Variance sort options
        VarianceSortOptions = new ObservableCollection<SortOption>
        {
            new SortOption("Account Name (A-Z)", "DisplayName"),
            new SortOption("Account Name (Z-A)", "DisplayName", true),
            new SortOption("Variance % (Low-High)", "VariancePercent"),
            new SortOption("Variance % (High-Low)", "VariancePercent", true),
            new SortOption("Variance $ (Low-High)", "VarianceAmount"),
            new SortOption("Variance $ (High-Low)", "VarianceAmount", true)
        };
        SelectedVarianceSortOption = VarianceSortOptions.FirstOrDefault();
    }

    /// <summary>
    /// Loads available budget periods.
    /// </summary>
    /// <returns>Task that completes when budget periods are loaded.</returns>
    private async Task LoadBudgetPeriodsAsync()
    {
        try
        {
            var periods = await _context.BudgetPeriods
                .OrderByDescending(bp => bp.Year)
                .ThenByDescending(bp => bp.CreatedDate)
                .ToListAsync();

            AvailableBudgetPeriods = new ObservableCollection<BudgetPeriod>(periods);

            // Select the most recent period by default
            SelectedBudgetPeriod = AvailableBudgetPeriods.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading budget periods");
        }
    }

    /// <summary>
    /// Gets the generate analysis command.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGenerateAnalysis))]
    private async Task GenerateAnalysis()
    {
        if (SelectedBudgetPeriod == null)
            return;

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            Logger.LogInformation("Generating {Type} analysis for budget period {PeriodId}: {PeriodName}",
                SelectedAnalysisType, SelectedBudgetPeriod.Id, SelectedBudgetPeriod.Name);

            var result = new BudgetAnalysisResult();

            switch (SelectedAnalysisType)
            {
                case AnalysisType.Overview:
                    result.Overview = await GenerateOverviewAnalysisAsync();
                    break;

                case AnalysisType.FundAnalysis:
                    result.FundSummaries = await GenerateFundAnalysisAsync();
                    FundHierarchy = new ObservableCollection<FundNode>(BuildFundHierarchy(result.FundSummaries));
                    FundGridData = new ObservableCollection<FlatFundItem>(BuildFundGridData(result.FundSummaries));
                    break;

                case AnalysisType.DepartmentAnalysis:
                    result.DepartmentSummaries = await GenerateDepartmentAnalysisAsync();
                    DepartmentHierarchy = new ObservableCollection<DepartmentNode>(BuildDepartmentHierarchy(result.DepartmentSummaries));
                    DepartmentGridData = new ObservableCollection<FlatDepartmentItem>(BuildDepartmentGridData(result.DepartmentSummaries));
                    break;

                case AnalysisType.VarianceAnalysis:
                    result.Variance = await GenerateVarianceAnalysisAsync();
                    VarianceHierarchy = new ObservableCollection<VarianceNode>(BuildVarianceHierarchy(result.Variance?.Variances));
                    VarianceChartData = new ObservableCollection<VarianceChartItem>(BuildVarianceChartData(result.Variance?.Variances));
                    break;

                case AnalysisType.Comprehensive:
                    result.Overview = await GenerateOverviewAnalysisAsync();
                    result.FundSummaries = await GenerateFundAnalysisAsync();
                    result.DepartmentSummaries = await GenerateDepartmentAnalysisAsync();
                    result.Variance = await GenerateVarianceAnalysisAsync();

                    FundHierarchy = new ObservableCollection<FundNode>(BuildFundHierarchy(result.FundSummaries));
                    FundGridData = new ObservableCollection<FlatFundItem>(BuildFundGridData(result.FundSummaries));
                    DepartmentHierarchy = new ObservableCollection<DepartmentNode>(BuildDepartmentHierarchy(result.DepartmentSummaries));
                    DepartmentGridData = new ObservableCollection<FlatDepartmentItem>(BuildDepartmentGridData(result.DepartmentSummaries));
                    VarianceHierarchy = new ObservableCollection<VarianceNode>(BuildVarianceHierarchy(result.Variance?.Variances));
                    VarianceChartData = new ObservableCollection<VarianceChartItem>(BuildVarianceChartData(result.Variance?.Variances));
                    break;
            }

            Analysis = result;

            Logger.LogInformation("Analysis generation completed for {Type}", SelectedAnalysisType);

        }, statusMessage: $"Generating {SelectedAnalysisType} analysis...");
    }

    /// <summary>
    /// Gets the export fund data command.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportFundData))]
    private void ExportFundData()
    {
        // TODO: Implement fund data export to Excel/CSV
        Logger.LogInformation("Exporting fund analysis data");
    }

    /// <summary>
    /// Gets the export department data command.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportDepartmentData))]
    private void ExportDepartmentData()
    {
        // TODO: Implement department data export to Excel/CSV
        Logger.LogInformation("Exporting department analysis data");
    }

    /// <summary>
    /// Gets whether fund data can be exported.
    /// </summary>
    private bool CanExportFundData => FundGridData.Any();

    /// <summary>
    /// Gets whether department data can be exported.
    /// </summary>
    private bool CanExportDepartmentData => DepartmentGridData.Any();

    /// <summary>
    /// Generates overview analysis.
    /// </summary>
    /// <returns>Task that completes with the overview analysis.</returns>
    private async Task<BudgetOverview> GenerateOverviewAnalysisAsync()
    {
        var budgetAnalysis = await _accountingService.GetBudgetAnalysisAsync(SelectedBudgetPeriod!.Id);

        return new BudgetOverview
        {
            TotalAccounts = budgetAnalysis.TotalAccounts,
            TotalBudget = budgetAnalysis.TotalBudget,
            TotalBalance = budgetAnalysis.TotalBalance,
            Variance = budgetAnalysis.Variance,
            KeyRatios = budgetAnalysis.KeyRatios.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    /// <summary>
    /// Generates fund analysis.
    /// </summary>
    /// <returns>Task that completes with the fund summaries.</returns>
    private async Task<List<FundSummary>> GenerateFundAnalysisAsync()
    {
        var budgetAnalysis = await _accountingService.GetBudgetAnalysisAsync(SelectedBudgetPeriod!.Id);
        return budgetAnalysis.FundSummaries;
    }

    /// <summary>
    /// Generates department analysis.
    /// </summary>
    /// <returns>Task that completes with the department summaries.</returns>
    private async Task<List<DepartmentSummary>> GenerateDepartmentAnalysisAsync()
    {
        var budgetAnalysis = await _accountingService.GetBudgetAnalysisAsync(SelectedBudgetPeriod!.Id);
        return budgetAnalysis.DepartmentSummaries;
    }

    /// <summary>
    /// Generates variance analysis.
    /// </summary>
    /// <returns>Task that completes with the variance analysis.</returns>
    private async Task<BudgetVarianceAnalysis> GenerateVarianceAnalysisAsync()
    {
        return await _accountingService.GetBudgetVarianceAnalysisAsync(SelectedBudgetPeriod!.Id, VarianceThreshold);
    }

    /// <summary>
    /// Builds hierarchical fund data from fund summaries.
    /// </summary>
    /// <param name="fundSummaries">The fund summaries.</param>
    /// <returns>List of fund nodes.</returns>
    private List<FundNode> BuildFundHierarchy(List<FundSummary>? fundSummaries)
    {
        if (fundSummaries == null || !fundSummaries.Any())
            return new List<FundNode>();

        var nodes = new List<FundNode>();

        // Sort fund summaries based on selected sort option
        var sortedFunds = SortFundSummaries(fundSummaries);

        foreach (var fundSummary in sortedFunds)
        {
            var fundNode = new FundNode
            {
                FundSummary = fundSummary
            };

            // Get accounts for this fund (this would need to be implemented in the service)
            // For now, we'll create empty children
            fundNode.Children = new ObservableCollection<FundAccountNode>();

            nodes.Add(fundNode);
        }

        return nodes;
    }

    /// <summary>
    /// Builds flat fund data for grid display from fund summaries.
    /// </summary>
    /// <param name="fundSummaries">The fund summaries.</param>
    /// <returns>List of flat fund items.</returns>
    private List<FlatFundItem> BuildFundGridData(List<FundSummary>? fundSummaries)
    {
        if (fundSummaries == null || !fundSummaries.Any())
            return new List<FlatFundItem>();

        var items = new List<FlatFundItem>();

        // Sort fund summaries based on selected sort option
        var sortedFunds = SortFundSummaries(fundSummaries);

        foreach (var fundSummary in sortedFunds)
        {
            var item = new FlatFundItem
            {
                Fund = fundSummary.Fund switch
                {
                    FundType.General => "General Fund",
                    FundType.SpecialRevenue => "Special Revenue Fund",
                    FundType.CapitalProjects => "Capital Projects Fund",
                    FundType.DebtService => "Debt Service Fund",
                    FundType.Enterprise => "Enterprise Fund",
                    FundType.InternalService => "Internal Service Fund",
                    FundType.Trust => "Trust Fund",
                    FundType.Agency => "Agency Fund",
                    FundType.ConservationTrust => "Conservation Trust Fund",
                    FundType.Recreation => "Recreation Fund",
                    FundType.Utility => "Utility Fund",
                    _ => "Unknown"
                },
                AccountCount = fundSummary.AccountCount,
                Budget = fundSummary.TotalBudget,
                Actual = fundSummary.TotalBalance,
                Variance = fundSummary.Variance
            };

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Builds hierarchical department data from department summaries.
    /// </summary>
    /// <param name="departmentSummaries">The department summaries.</param>
    /// <returns>List of department nodes.</returns>
    private List<DepartmentNode> BuildDepartmentHierarchy(List<DepartmentSummary>? departmentSummaries)
    {
        if (departmentSummaries == null || !departmentSummaries.Any())
            return new List<DepartmentNode>();

        var nodes = new List<DepartmentNode>();

        // Sort department summaries based on selected sort option
        var sortedDepartments = SortDepartmentSummaries(departmentSummaries);

        foreach (var deptSummary in sortedDepartments)
        {
            var deptNode = new DepartmentNode
            {
                DepartmentSummary = deptSummary
            };

            // Get accounts for this department (this would need to be implemented in the service)
            // For now, we'll create empty children
            deptNode.Children = new ObservableCollection<DepartmentAccountNode>();

            nodes.Add(deptNode);
        }

        return nodes;
    }

    /// <summary>
    /// Builds flat department data for grid display from department summaries.
    /// </summary>
    /// <param name="departmentSummaries">The department summaries.</param>
    /// <returns>List of flat department items.</returns>
    private List<FlatDepartmentItem> BuildDepartmentGridData(List<DepartmentSummary>? departmentSummaries)
    {
        if (departmentSummaries == null || !departmentSummaries.Any())
            return new List<FlatDepartmentItem>();

        var items = new List<FlatDepartmentItem>();

        // Sort department summaries based on selected sort option
        var sortedDepartments = SortDepartmentSummaries(departmentSummaries);

        foreach (var deptSummary in sortedDepartments)
        {
            var item = new FlatDepartmentItem
            {
                Department = deptSummary.Department?.Name ?? "Unknown",
                Code = deptSummary.Department?.Code ?? string.Empty,
                AccountCount = deptSummary.AccountCount,
                Budget = deptSummary.TotalBudget,
                Actual = deptSummary.TotalBalance,
                Variance = deptSummary.Variance,
                Utilization = deptSummary.Metrics?.GetValueOrDefault("BudgetUtilization", 0m) ?? 0m
            };

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Builds hierarchical variance data from account variances.
    /// </summary>
    /// <param name="variances">The account variances.</param>
    /// <returns>List of variance nodes.</returns>
    private List<VarianceNode> BuildVarianceHierarchy(List<AccountVariance>? variances)
    {
        if (variances == null || !variances.Any())
            return new List<VarianceNode>();

        // Sort variances based on selected sort option
        var sortedVariances = SortAccountVariances(variances);

        // Filter by text if specified
        if (!string.IsNullOrWhiteSpace(VarianceFilterText))
        {
            sortedVariances = sortedVariances.Where(v =>
                (v.Account?.Name?.Contains(VarianceFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (v.Account?.AccountNumber?.Value?.Contains(VarianceFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (v.Account?.Department?.Name?.Contains(VarianceFilterText, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        return sortedVariances.Select(v => new VarianceNode { AccountVariance = v }).ToList();
    }

    /// <summary>
    /// Builds chart data from account variances for visualization.
    /// </summary>
    /// <param name="variances">The account variances.</param>
    /// <returns>List of variance chart items.</returns>
    private List<VarianceChartItem> BuildVarianceChartData(List<AccountVariance>? variances)
    {
        if (variances == null || !variances.Any())
            return new List<VarianceChartItem>();

        // Take top 20 variances by absolute amount for chart display
        var topVariances = variances
            .OrderByDescending(v => Math.Abs(v.VarianceAmount))
            .Take(20)
            .ToList();

        return topVariances.Select(v => new VarianceChartItem
        {
            AccountName = v.Account?.Name ?? "Unknown",
            Budget = v.Account?.BudgetAmount ?? 0,
            Actual = v.Account?.Balance ?? 0,
            Variance = v.VarianceAmount
        }).ToList();
    }

    /// <summary>
    /// Sorts fund summaries based on selected sort option.
    /// </summary>
    /// <param name="funds">The fund summaries to sort.</param>
    /// <returns>Sorted fund summaries.</returns>
    private List<FundSummary> SortFundSummaries(List<FundSummary> funds)
    {
        if (SelectedFundSortOption == null)
            return funds;

        return SelectedFundSortOption.PropertyName switch
        {
            "DisplayName" => SelectedFundSortOption.IsDescending
                ? funds.OrderByDescending(f => f.Fund.ToString()).ToList()
                : funds.OrderBy(f => f.Fund.ToString()).ToList(),
            "TotalBudget" => SelectedFundSortOption.IsDescending
                ? funds.OrderByDescending(f => f.TotalBudget).ToList()
                : funds.OrderBy(f => f.TotalBudget).ToList(),
            "Variance" => SelectedFundSortOption.IsDescending
                ? funds.OrderByDescending(f => f.Variance).ToList()
                : funds.OrderBy(f => f.Variance).ToList(),
            _ => funds
        };
    }

    /// <summary>
    /// Sorts department summaries based on selected sort option.
    /// </summary>
    /// <param name="departments">The department summaries to sort.</param>
    /// <returns>Sorted department summaries.</returns>
    private List<DepartmentSummary> SortDepartmentSummaries(List<DepartmentSummary> departments)
    {
        if (SelectedDepartmentSortOption == null)
            return departments;

        return SelectedDepartmentSortOption.PropertyName switch
        {
            "DisplayName" => SelectedDepartmentSortOption.IsDescending
                ? departments.OrderByDescending(d => d.Department?.Name ?? string.Empty).ToList()
                : departments.OrderBy(d => d.Department?.Name ?? string.Empty).ToList(),
            "TotalBudget" => SelectedDepartmentSortOption.IsDescending
                ? departments.OrderByDescending(d => d.TotalBudget).ToList()
                : departments.OrderBy(d => d.TotalBudget).ToList(),
            "Variance" => SelectedDepartmentSortOption.IsDescending
                ? departments.OrderByDescending(d => d.Variance).ToList()
                : departments.OrderBy(d => d.Variance).ToList(),
            _ => departments
        };
    }

    /// <summary>
    /// Sorts account variances based on selected sort option.
    /// </summary>
    /// <param name="variances">The account variances to sort.</param>
    /// <returns>Sorted account variances.</returns>
    private List<AccountVariance> SortAccountVariances(List<AccountVariance> variances)
    {
        if (SelectedVarianceSortOption == null)
            return variances;

        return SelectedVarianceSortOption.PropertyName switch
        {
            "DisplayName" => SelectedVarianceSortOption.IsDescending
                ? variances.OrderByDescending(v => v.Account?.Name ?? string.Empty).ToList()
                : variances.OrderBy(v => v.Account?.Name ?? string.Empty).ToList(),
            "VariancePercent" => SelectedVarianceSortOption.IsDescending
                ? variances.OrderByDescending(v => v.VariancePercent).ToList()
                : variances.OrderBy(v => v.VariancePercent).ToList(),
            "VarianceAmount" => SelectedVarianceSortOption.IsDescending
                ? variances.OrderByDescending(v => v.VarianceAmount).ToList()
                : variances.OrderBy(v => v.VarianceAmount).ToList(),
            _ => variances
        };
    }

    /// <summary>
    /// Refreshes the fund hierarchy with current sorting and filtering.
    /// </summary>
    private void RefreshFundHierarchy()
    {
        if (Analysis.FundSummaries != null)
        {
            FundHierarchy = new ObservableCollection<FundNode>(BuildFundHierarchy(Analysis.FundSummaries));
            FundGridData = new ObservableCollection<FlatFundItem>(BuildFundGridData(Analysis.FundSummaries));
        }
    }

    /// <summary>
    /// Refreshes the department hierarchy with current sorting and filtering.
    /// </summary>
    private void RefreshDepartmentHierarchy()
    {
        if (Analysis.DepartmentSummaries != null)
        {
            DepartmentHierarchy = new ObservableCollection<DepartmentNode>(BuildDepartmentHierarchy(Analysis.DepartmentSummaries));
            DepartmentGridData = new ObservableCollection<FlatDepartmentItem>(BuildDepartmentGridData(Analysis.DepartmentSummaries));
        }
    }

    /// <summary>
    /// Refreshes the variance hierarchy with current sorting and filtering.
    /// </summary>
    private void RefreshVarianceHierarchy()
    {
        if (Analysis.Variance?.Variances != null)
        {
            VarianceHierarchy = new ObservableCollection<VarianceNode>(BuildVarianceHierarchy(Analysis.Variance.Variances));
            VarianceChartData = new ObservableCollection<VarianceChartItem>(BuildVarianceChartData(Analysis.Variance.Variances));
        }
    }

    /// <summary>
    /// Handles property changed events to update command states.
    /// </summary>
    /// <param name="e">The property changed event args.</param>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(SelectedBudgetPeriod) || e.PropertyName == nameof(IsLoading))
        {
            GenerateAnalysisCommand.NotifyCanExecuteChanged();
        }

        // Refresh hierarchies when sort/filter options change
        if (e.PropertyName == nameof(SelectedFundSortOption) || e.PropertyName == nameof(FundFilterText))
        {
            RefreshFundHierarchy();
        }

        if (e.PropertyName == nameof(SelectedDepartmentSortOption) || e.PropertyName == nameof(DepartmentFilterText))
        {
            RefreshDepartmentHierarchy();
        }

        if (e.PropertyName == nameof(SelectedVarianceSortOption) || e.PropertyName == nameof(VarianceFilterText))
        {
            RefreshVarianceHierarchy();
        }
    }
}

/// <summary>
/// Result of budget analysis operations.
/// </summary>
public class BudgetAnalysisResult
{
    /// <summary>
    /// Gets or sets the overview analysis.
    /// </summary>
    public BudgetOverview? Overview { get; set; }

    /// <summary>
    /// Gets or sets the fund summaries.
    /// </summary>
    public List<FundSummary>? FundSummaries { get; set; }

    /// <summary>
    /// Gets or sets the department summaries.
    /// </summary>
    public List<DepartmentSummary>? DepartmentSummaries { get; set; }

    /// <summary>
    /// Gets or sets the variance analysis.
    /// </summary>
    public BudgetVarianceAnalysis? Variance { get; set; }
}

/// <summary>
/// Budget overview data.
/// </summary>
public class BudgetOverview
{
    /// <summary>
    /// Gets or sets the total number of accounts.
    /// </summary>
    public int TotalAccounts { get; set; }

    /// <summary>
    /// Gets or sets the total budget amount.
    /// </summary>
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Gets or sets the total balance.
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Gets or sets the total variance.
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets or sets the key financial ratios.
    /// </summary>
    public Dictionary<string, decimal> KeyRatios { get; set; } = new();
}

/// <summary>
/// Hierarchical node for fund analysis.
/// </summary>
public class FundNode
{
    /// <summary>
    /// Gets or sets the fund summary.
    /// </summary>
    public FundSummary? FundSummary { get; set; }

    /// <summary>
    /// Gets or sets the account children.
    /// </summary>
    public ObservableCollection<FundAccountNode> Children { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this is a fund node.
    /// </summary>
    public bool IsFund => FundSummary != null;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName => IsFund ? $"{FundSummary!.Fund}" : "Unknown";

    /// <summary>
    /// Gets the formatted budget amount.
    /// </summary>
    public string FormattedBudget => IsFund ? FundSummary!.TotalBudget.ToString("C0") : string.Empty;

    /// <summary>
    /// Gets the formatted balance amount.
    /// </summary>
    public string FormattedBalance => IsFund ? FundSummary!.TotalBalance.ToString("C0") : string.Empty;

    /// <summary>
    /// Gets the formatted variance amount.
    /// </summary>
    public string FormattedVariance => IsFund ? FundSummary!.Variance.ToString("C0") : string.Empty;

    /// <summary>
    /// Gets whether the variance is negative.
    /// </summary>
    public bool IsNegativeVariance => IsFund && FundSummary!.Variance < 0;
}

/// <summary>
/// Account node under a fund.
/// </summary>
public class FundAccountNode
{
    /// <summary>
    /// Gets or sets the municipal account.
    /// </summary>
    public MunicipalAccount? Account { get; set; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName => Account?.Name ?? "Unknown Account";

    /// <summary>
    /// Gets the account number.
    /// </summary>
    public string AccountNumber => Account?.AccountNumber?.Value ?? string.Empty;

    /// <summary>
    /// Gets the formatted budget amount.
    /// </summary>
    public string FormattedBudget => Account?.BudgetAmount.ToString("C0") ?? string.Empty;

    /// <summary>
    /// Gets the formatted balance amount.
    /// </summary>
    public string FormattedBalance => Account?.Balance.ToString("C0") ?? string.Empty;

    /// <summary>
    /// Gets the formatted variance amount.
    /// </summary>
    public string FormattedVariance => Account != null ? (Account.Balance - Account.BudgetAmount).ToString("C0") : string.Empty;

    /// <summary>
    /// Gets whether the variance is negative.
    /// </summary>
    public bool IsNegativeVariance => Account != null && (Account.Balance - Account.BudgetAmount) < 0;
}

/// <summary>
/// Hierarchical node for department analysis.
/// </summary>
public class DepartmentNode
{
    /// <summary>
    /// Gets or sets the department summary.
    /// </summary>
    public DepartmentSummary? DepartmentSummary { get; set; }

    /// <summary>
    /// Gets or sets the account children.
    /// </summary>
    public ObservableCollection<DepartmentAccountNode> Children { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this is a department node.
    /// </summary>
    public bool IsDepartment => DepartmentSummary != null;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName => IsDepartment ? $"{DepartmentSummary!.Department?.Name ?? "Unknown"} ({DepartmentSummary!.Department?.Code ?? "N/A"})" : "Unknown";

    /// <summary>
    /// Gets the formatted budget amount.
    /// </summary>
    public string FormattedBudget => IsDepartment ? DepartmentSummary!.TotalBudget.ToString("C0") : string.Empty;

    /// <summary>
    /// Gets the formatted balance amount.
    /// </summary>
    public string FormattedBalance => IsDepartment ? DepartmentSummary!.TotalBalance.ToString("C0") : string.Empty;

    /// <summary>
    /// Gets the formatted variance amount.
    /// </summary>
    public string FormattedVariance => IsDepartment ? DepartmentSummary!.Variance.ToString("C0") : string.Empty;

    /// <summary>
    /// Gets whether the variance is negative.
    /// </summary>
    public bool IsNegativeVariance => IsDepartment && DepartmentSummary!.Variance < 0;
}

/// <summary>
/// Account node under a department.
/// </summary>
public class DepartmentAccountNode
{
    /// <summary>
    /// Gets or sets the municipal account.
    /// </summary>
    public MunicipalAccount? Account { get; set; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName => Account?.Name ?? "Unknown Account";

    /// <summary>
    /// Gets the account number.
    /// </summary>
    public string AccountNumber => Account?.AccountNumber?.Value ?? string.Empty;

    /// <summary>
    /// Gets the fund type.
    /// </summary>
    public string Fund => Account?.Fund.ToString() ?? string.Empty;

    /// <summary>
    /// Gets the formatted budget amount.
    /// </summary>
    public string FormattedBudget => Account?.BudgetAmount.ToString("C0") ?? string.Empty;

    /// <summary>
    /// Gets the formatted balance amount.
    /// </summary>
    public string FormattedBalance => Account?.Balance.ToString("C0") ?? string.Empty;

    /// <summary>
    /// Gets the formatted variance amount.
    /// </summary>
    public string FormattedVariance => Account != null ? (Account.Balance - Account.BudgetAmount).ToString("C0") : string.Empty;

    /// <summary>
    /// Gets whether the variance is negative.
    /// </summary>
    public bool IsNegativeVariance => Account != null && (Account.Balance - Account.BudgetAmount) < 0;
}

/// <summary>
/// Hierarchical node for variance analysis.
/// </summary>
public class VarianceNode
{
    /// <summary>
    /// Gets or sets the account variance.
    /// </summary>
    public AccountVariance? AccountVariance { get; set; }

    /// <summary>
    /// Gets or sets whether this is an account node.
    /// </summary>
    public bool IsAccount => AccountVariance != null;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName => IsAccount ? $"{AccountVariance!.Account?.Name ?? "Unknown"} ({AccountVariance!.Account?.AccountNumber?.Value ?? "N/A"})" : "Unknown";

    /// <summary>
    /// Gets the department name.
    /// </summary>
    public string Department => AccountVariance?.Account?.Department?.Name ?? string.Empty;

    /// <summary>
    /// Gets the fund type.
    /// </summary>
    public string Fund => AccountVariance?.Account?.Fund.ToString() ?? string.Empty;

    /// <summary>
    /// Gets the formatted budget amount.
    /// </summary>
    public string FormattedBudget => AccountVariance?.Account?.BudgetAmount.ToString("C0") ?? string.Empty;

    /// <summary>
    /// Gets the formatted balance amount.
    /// </summary>
    public string FormattedBalance => AccountVariance?.Account?.Balance.ToString("C0") ?? string.Empty;

    /// <summary>
    /// Gets the formatted variance amount.
    /// </summary>
    public string FormattedVarianceAmount => AccountVariance?.VarianceAmount.ToString("C0") ?? string.Empty;

    /// <summary>
    /// Gets the formatted variance percentage.
    /// </summary>
    public string FormattedVariancePercent => AccountVariance?.VariancePercent.ToString("P2") ?? string.Empty;

    /// <summary>
    /// Gets whether the variance is negative.
    /// </summary>
    public bool IsNegativeVariance => AccountVariance?.VarianceAmount < 0;
}

/// <summary>
/// Flat data item for fund grid display.
/// </summary>
public class FlatFundItem
{
    /// <summary>
    /// Gets or sets the fund name.
    /// </summary>
    public string Fund { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account number.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account name.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account count.
    /// </summary>
    public int AccountCount { get; set; }

    /// <summary>
    /// Gets or sets the budget amount.
    /// </summary>
    public decimal Budget { get; set; }

    /// <summary>
    /// Gets or sets the actual amount.
    /// </summary>
    public decimal Actual { get; set; }

    /// <summary>
    /// Gets or sets the variance amount.
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets the formatted budget amount.
    /// </summary>
    public string FormattedBudget => Budget.ToString("C0");

    /// <summary>
    /// Gets the formatted actual amount.
    /// </summary>
    public string FormattedActual => Actual.ToString("C0");

    /// <summary>
    /// Gets the formatted variance amount.
    /// </summary>
    public string FormattedVariance => Variance.ToString("C0");

    /// <summary>
    /// Gets whether the variance is negative.
    /// </summary>
    public bool IsNegativeVariance => Variance < 0;
}

/// <summary>
/// Flat data item for department grid display.
/// </summary>
public class FlatDepartmentItem
{
    /// <summary>
    /// Gets or sets the department name.
    /// </summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the department code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account count.
    /// </summary>
    public int AccountCount { get; set; }

    /// <summary>
    /// Gets or sets the budget amount.
    /// </summary>
    public decimal Budget { get; set; }

    /// <summary>
    /// Gets or sets the actual amount.
    /// </summary>
    public decimal Actual { get; set; }

    /// <summary>
    /// Gets or sets the variance amount.
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets or sets the utilization percentage.
    /// </summary>
    public decimal Utilization { get; set; }

    /// <summary>
    /// Gets the formatted budget amount.
    /// </summary>
    public string FormattedBudget => Budget.ToString("C0");

    /// <summary>
    /// Gets the formatted actual amount.
    /// </summary>
    public string FormattedActual => Actual.ToString("C0");

    /// <summary>
    /// Gets the formatted variance amount.
    /// </summary>
    public string FormattedVariance => Variance.ToString("C0");

    /// <summary>
    /// Gets the formatted utilization.
    /// </summary>
    public string FormattedUtilization => Utilization.ToString("P2");

    /// <summary>
    /// Gets whether the variance is negative.
    /// </summary>
    public bool IsNegativeVariance => Variance < 0;
}

/// <summary>
/// Chart data item for variance visualization.
/// </summary>
public class VarianceChartItem
{
    /// <summary>
    /// Gets or sets the account name.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the budget amount.
    /// </summary>
    public decimal Budget { get; set; }

    /// <summary>
    /// Gets or sets the actual amount.
    /// </summary>
    public decimal Actual { get; set; }

    /// <summary>
    /// Gets or sets the variance amount.
    /// </summary>
    public decimal Variance { get; set; }
}