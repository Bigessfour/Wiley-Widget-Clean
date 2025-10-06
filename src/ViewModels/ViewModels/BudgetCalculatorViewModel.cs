using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for budget calculator operations
/// Handles break-even calculations and financial projections
/// </summary>
public partial class BudgetCalculatorViewModel : AsyncViewModelBase
{
    /// <summary>
    /// Calculator fixed costs input
    /// </summary>
    [ObservableProperty]
    private double calculatorFixedCosts;

    /// <summary>
    /// Calculator variable cost per unit input
    /// </summary>
    [ObservableProperty]
    private double calculatorVariableCost;

    /// <summary>
    /// Calculator price per unit input
    /// </summary>
    [ObservableProperty]
    private double calculatorPricePerUnit;

    /// <summary>
    /// Calculator result - break-even units
    /// </summary>
    [ObservableProperty]
    private double calculatorBreakEvenUnits;

    /// <summary>
    /// Calculator result - break-even revenue
    /// </summary>
    [ObservableProperty]
    private double calculatorBreakEvenRevenue;

    /// <summary>
    /// Calculator result - profit margin percentage
    /// </summary>
    [ObservableProperty]
    private double calculatorProfitMargin;

    /// <summary>
    /// Calculator status message
    /// </summary>
    [ObservableProperty]
    private string calculatorStatus = "Enter values and click Calculate";

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetCalculatorViewModel(
        IDispatcherHelper dispatcherHelper,
        ILogger<BudgetViewModel> logger)
        : base(dispatcherHelper, logger)
    {
    }

    /// <summary>
    /// Performs break-even calculation
    /// </summary>
    [RelayCommand]
    public void CalculateBreakEven()
    {
        try
        {
            if (CalculatorFixedCosts <= 0)
            {
                CalculatorStatus = "Fixed costs must be greater than zero";
                return;
            }

            if (CalculatorVariableCost < 0)
            {
                CalculatorStatus = "Variable cost cannot be negative";
                return;
            }

            if (CalculatorPricePerUnit <= CalculatorVariableCost)
            {
                CalculatorStatus = "Price per unit must be greater than variable cost";
                return;
            }

            // Calculate break-even point
            var contributionMargin = CalculatorPricePerUnit - CalculatorVariableCost;
            CalculatorBreakEvenUnits = CalculatorFixedCosts / contributionMargin;
            CalculatorBreakEvenRevenue = CalculatorBreakEvenUnits * CalculatorPricePerUnit;

            // Calculate profit margin
            CalculatorProfitMargin = (contributionMargin / CalculatorPricePerUnit) * 100;

            CalculatorStatus = $"Break-even: {CalculatorBreakEvenUnits:F0} units (${CalculatorBreakEvenRevenue:F2})";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in break-even calculation");
            CalculatorStatus = $"Calculation error: {ex.Message}";
        }
    }

    /// <summary>
    /// Clears calculator inputs and results
    /// </summary>
    [RelayCommand]
    public void ClearCalculator()
    {
        CalculatorFixedCosts = 0;
        CalculatorVariableCost = 0;
        CalculatorPricePerUnit = 0;
        CalculatorBreakEvenUnits = 0;
        CalculatorBreakEvenRevenue = 0;
        CalculatorProfitMargin = 0;
        CalculatorStatus = "Calculator cleared";
    }

    /// <summary>
    /// Whether calculator has valid inputs for calculation
    /// </summary>
    public bool CanCalculate => CalculatorFixedCosts > 0 && CalculatorPricePerUnit > CalculatorVariableCost;
}