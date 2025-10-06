using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Data;
using System.Threading.Tasks;
using System.Linq;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using WileyWidget.Services;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for budget data loading and basic operations
/// Handles loading OverallBudgets data with high-performance EF Core operations
/// </summary>
public partial class BudgetDataViewModel : AsyncViewModelBase
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryProfiler _memoryProfiler;

    /// <summary>
    /// Collection of budget details for each enterprise
    /// </summary>
    public ThreadSafeObservableCollection<BudgetDetailItem> BudgetItems { get; } = new();

    /// <summary>
    /// Total number of budget records loaded
    /// </summary>
    [ObservableProperty]
    private int itemCount;

    /// <summary>
    /// Current memory usage in MB
    /// </summary>
    [ObservableProperty]
    private double memoryUsage;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    [ObservableProperty]
    private string lastUpdated = "Never";

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetDataViewModel(
        AppDbContext dbContext,
        IMemoryProfiler memoryProfiler,
        IDispatcherHelper dispatcherHelper,
        ILogger<BudgetViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _memoryProfiler = memoryProfiler ?? throw new ArgumentNullException(nameof(memoryProfiler));
    }

    /// <summary>
    /// High-performance async data loading using EF Core OverallBudgets.ToListAsync()
    /// with memory profiling and PixelRow virtualization support
    /// </summary>
    [RelayCommand]
    public async Task RefreshBudgetDataAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await ExecuteAsyncOperation(async (cancellationToken) =>
            {
                // Profile memory before loading
                var memoryBefore = _memoryProfiler.GetCurrentMemoryUsage();
                Logger.LogInformation("Memory before loading OverallBudgets: {Memory} MB", memoryBefore);

                // Async EF Core data loading with ToListAsync for optimal performance
                var overallBudgets = await _dbContext.OverallBudgets
                    .AsNoTracking() // Performance optimization - no change tracking needed for read-only data
                    .OrderByDescending(b => b.SnapshotDate) // Most recent first
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Profile memory after loading
                var memoryAfter = _memoryProfiler.GetCurrentMemoryUsage();
                MemoryUsage = memoryAfter;

                Logger.LogInformation("Memory after loading OverallBudgets: {Memory} MB, Increase: {Increase} MB",
                    memoryAfter, memoryAfter - memoryBefore);

                // Transform to BudgetDetailItem collection
                var budgetDetails = overallBudgets
                    .Select(budget => new BudgetDetailItem
                    {
                        EnterpriseName = $"Municipal Budget - {budget.SnapshotDate:yyyy-MM-dd}",
                        CitizenCount = budget.TotalCitizensServed,
                        CurrentRate = budget.AverageRatePerCitizen,
                        MonthlyRevenue = budget.TotalMonthlyRevenue,
                        MonthlyExpenses = budget.TotalMonthlyExpenses,
                        MonthlyBalance = budget.TotalMonthlyBalance,
                        BreakEvenRate = budget.TotalCitizensServed > 0 ?
                            (budget.TotalMonthlyExpenses / budget.TotalCitizensServed) : 0,
                        Status = budget.IsSurplus ? "Surplus" : "Deficit",
                        LastUpdated = budget.SnapshotDate,
                        Notes = budget.Notes
                    })
                    .ToList();

                await BudgetItems.ReplaceAllAsync(budgetDetails);
                ItemCount = budgetDetails.Count;
                LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                Logger.LogInformation("Loaded {Count} budget detail items in {Elapsed}ms",
                    budgetDetails.Count, stopwatch.ElapsedMilliseconds);

            }, statusMessage: "Loading budget data...");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load budget data");
            throw;
        }
    }
}