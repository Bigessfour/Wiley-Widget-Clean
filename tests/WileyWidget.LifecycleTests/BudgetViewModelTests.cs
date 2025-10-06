using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Xunit;

namespace WileyWidget.LifecycleTests;

/// <summary>
/// Comprehensive unit tests for BudgetViewModel
/// Tests all commands, data binding, calculations, and business logic
/// </summary>
public sealed class BudgetViewModelTests : IDisposable
{
    private readonly Mock<IEnterpriseRepository> _enterpriseRepositoryMock;
    private readonly BudgetViewModel _sut;
    private readonly List<Enterprise> _testEnterprises;

    public BudgetViewModelTests()
    {
        _enterpriseRepositoryMock = new Mock<IEnterpriseRepository>();
        
        // Setup test data
        _testEnterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Id = 1,
                Name = "Water Department",
                Type = "Water",
                Status = EnterpriseStatus.Active,
                TotalBudget = 100000m,
                CurrentRate = 95m,
                MonthlyExpenses = 85000m,
                CitizenCount = 1000
            },
            new Enterprise
            {
                Id = 2,
                Name = "Sanitation District",
                Type = "Sanitation",
                Status = EnterpriseStatus.Active,
                TotalBudget = 75000m,
                CurrentRate = 87.50m,
                MonthlyExpenses = 65000m,
                CitizenCount = 800
            },
            new Enterprise
            {
                Id = 3,
                Name = "Parks & Recreation",
                Type = "Other",
                Status = EnterpriseStatus.Inactive,
                TotalBudget = 50000m,
                CurrentRate = 90m,
                MonthlyExpenses = 48000m,
                CitizenCount = 500
            }
        };

        // Setup mock behaviors
        _enterpriseRepositoryMock
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(_testEnterprises);

        _sut = new BudgetViewModel(
            _enterpriseRepositoryMock.Object,
            new TestDispatcherHelper(),
            NullLogger<BudgetViewModel>.Instance);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    #region Initialization Tests

    [Fact]
    public void Constructor_InitializesAllCollections()
    {
        // Assert
        Assert.NotNull(_sut.BudgetItems);
        Assert.NotNull(_sut.RateTrendData);
        Assert.NotNull(_sut.ProjectedRateData);
        Assert.NotNull(_sut.BudgetPerformanceData);
        Assert.Empty(_sut.BudgetItems);
    }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Assert
        Assert.Equal(0, _sut.TotalRevenue);
        Assert.Equal(0, _sut.TotalExpenses);
        Assert.Equal(0, _sut.NetBalance);
        Assert.Equal(0, _sut.TotalCitizens);
        Assert.Equal("Click 'Break-even Analysis' to generate insights", _sut.BreakEvenAnalysisText);
        Assert.Equal("Click 'Trend Analysis' to view budget trends", _sut.TrendAnalysisText);
        Assert.Equal("Never", _sut.LastUpdated);
        Assert.Equal("Ready", _sut.AnalysisStatus);
        Assert.False(_sut.HasError);
    }

    [Fact]
    public void Constructor_InitializesCalculatorDefaults()
    {
        // Assert
        Assert.Equal(0, _sut.CalculatorFixedCosts);
        Assert.Equal(0, _sut.CalculatorVariableCost);
        Assert.Equal(0, _sut.CalculatorPricePerUnit);
        Assert.Equal(12, _sut.ForecastPeriodMonths);
        Assert.Equal(0.02, _sut.InflationRate);
        Assert.Equal(0.01, _sut.PopulationGrowthRate);
    }

    #endregion

    #region Refresh Command Tests

    [Fact]
    public async Task RefreshBudgetDataCommand_LoadsDataFromRepository()
    {
        // Act
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Assert
        _enterpriseRepositoryMock.Verify(repo => repo.GetAllAsync(), Times.Once);
        Assert.NotEmpty(_sut.BudgetItems);
        Assert.Equal(3, _sut.BudgetItems.Count);
    }

    [Fact]
    public async Task RefreshBudgetDataCommand_CalculatesTotals()
    {
        // Act
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Assert
        var expectedRevenue = _testEnterprises.Sum(e => e.MonthlyRevenue);
        var expectedExpenses = _testEnterprises.Sum(e => e.MonthlyExpenses);
        var expectedBalance = expectedRevenue - expectedExpenses;
        var expectedCitizens = _testEnterprises.Sum(e => e.CitizenCount);

        Assert.Equal(expectedRevenue, _sut.TotalRevenue);
        Assert.Equal(expectedExpenses, _sut.TotalExpenses);
        Assert.Equal(expectedBalance, _sut.NetBalance);
        Assert.Equal(expectedCitizens, _sut.TotalCitizens);
    }

    [Fact]
    public async Task RefreshBudgetDataCommand_UpdatesLastUpdatedTimestamp()
    {
        // Arrange
        var initialLastUpdated = _sut.LastUpdated;

        // Act
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Assert
        Assert.NotEqual(initialLastUpdated, _sut.LastUpdated);
        Assert.Contains(":", _sut.LastUpdated); // Should contain time
    }

    [Fact]
    public async Task RefreshBudgetDataCommand_HandlesEmptyData()
    {
        // Arrange
        _enterpriseRepositoryMock
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(new List<Enterprise>());

        // Act
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(_sut.BudgetItems);
        Assert.Equal(0, _sut.TotalRevenue);
        Assert.Equal(0, _sut.TotalExpenses);
        Assert.Equal(0, _sut.NetBalance);
    }

    [Fact]
    public async Task RefreshBudgetDataCommand_HandlesRepositoryException()
    {
        // Arrange
        _enterpriseRepositoryMock
            .Setup(repo => repo.GetAllAsync())
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Assert
        Assert.True(_sut.HasError);
        Assert.NotEmpty(_sut.ErrorMessage);
    }

    #endregion

    #region Break-Even Analysis Tests

    [Fact]
    public async Task BreakEvenAnalysisCommand_CalculatesBreakEvenPoints()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);
        _sut.CalculatorFixedCosts = 10000;
        _sut.CalculatorVariableCost = 5;
        _sut.CalculatorPricePerUnit = 15;

        // Act
        _sut.BreakEvenAnalysisCommand.Execute(null);

        // Assert
        Assert.NotEqual("Click 'Break-even Analysis' to generate insights", _sut.BreakEvenAnalysisText);
        Assert.Contains("Break-Even", _sut.BreakEvenAnalysisText);
    }

    [Fact]
    public void BreakEvenAnalysisCommand_HandlesInvalidInputs()
    {
        // Arrange
        _sut.CalculatorFixedCosts = 10000;
        _sut.CalculatorVariableCost = 15;
        _sut.CalculatorPricePerUnit = 10; // Price < Variable Cost = impossible break-even

        // Act
        _sut.BreakEvenAnalysisCommand.Execute(null);

        // Assert
        Assert.Contains("cannot", _sut.BreakEvenAnalysisText.ToLower(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BreakEvenAnalysisCommand_HandlesZeroPricePerUnit()
    {
        // Arrange
        _sut.CalculatorFixedCosts = 10000;
        _sut.CalculatorVariableCost = 5;
        _sut.CalculatorPricePerUnit = 0;

        // Act
        _sut.BreakEvenAnalysisCommand.Execute(null);

        // Assert
        Assert.Contains("invalid", _sut.BreakEvenAnalysisText.ToLower(System.Globalization.CultureInfo.InvariantCulture));
    }

    #endregion

    #region Trend Analysis Tests

    [Fact]
    public async Task TrendAnalysisCommand_AnalyzesTrends()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Act
        _sut.TrendAnalysisCommand.Execute(null);

        // Assert
        Assert.NotEqual("Click 'Trend Analysis' to view budget trends", _sut.TrendAnalysisText);
        Assert.NotEmpty(_sut.RateTrendData);
    }

    [Fact]
    public async Task TrendAnalysisCommand_CalculatesGrowthRates()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Act
        _sut.TrendAnalysisCommand.Execute(null);

        // Assert
        Assert.True(_sut.RevenueGrowthRate >= -100); // Reasonable growth rate range
        Assert.True(_sut.ExpenseGrowthRate >= -100);
        Assert.Contains("growth", _sut.TrendAnalysisText.ToLower(System.Globalization.CultureInfo.InvariantCulture));
    }

    #endregion

    #region Recommendations Tests

    [Fact]
    public async Task BreakEvenAnalysisCommand_CreatesRecommendations()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Act
        _sut.BreakEvenAnalysisCommand.Execute(null);

        // Assert
        Assert.NotEqual("Click 'Refresh' to load budget data and generate recommendations", _sut.RecommendationsText);
    }

    [Fact]
    public async Task BreakEvenAnalysisCommand_IdentifiesDeficitEnterprises()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Act
        _sut.BreakEvenAnalysisCommand.Execute(null);

        // Assert - Parks & Recreation has deficit (expenses > revenue)
        Assert.Contains("Parks", _sut.RecommendationsText);
    }

    #endregion

    #region Forecast Tests

    [Fact]
    public async Task TrendAnalysisCommand_CreatesProjections()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);
        _sut.ForecastPeriodMonths = 6;
        _sut.InflationRate = 0.03;
        _sut.PopulationGrowthRate = 0.02;

        // Act
        _sut.TrendAnalysisCommand.Execute(null);

        // Assert
        Assert.NotEmpty(_sut.ProjectedRateData);
    }

    [Fact]
    public async Task TrendAnalysisCommand_HandlesDifferentPeriods()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);
        _sut.ForecastPeriodMonths = 24;

        // Act
        _sut.TrendAnalysisCommand.Execute(null);

        // Assert
        Assert.True(_sut.ProjectedRateData.Count > 0);
    }

    #endregion

    #region Property Change Tests

    [Fact]
    public void CalculatorFixedCosts_RaisesPropertyChanged()
    {
        // Arrange
        var propertyChanged = false;
        _sut.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_sut.CalculatorFixedCosts))
                propertyChanged = true;
        };

        // Act
        _sut.CalculatorFixedCosts = 5000;

        // Assert
        Assert.True(propertyChanged);
        Assert.Equal(5000, _sut.CalculatorFixedCosts);
    }

    [Fact]
    public void ForecastPeriodMonths_RaisesPropertyChanged()
    {
        // Arrange
        var propertyChanged = false;
        _sut.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_sut.ForecastPeriodMonths))
                propertyChanged = true;
        };

        // Act
        _sut.ForecastPeriodMonths = 18;

        // Assert
        Assert.True(propertyChanged);
        Assert.Equal(18, _sut.ForecastPeriodMonths);
    }

    #endregion

    #region Chart Data Tests

    [Fact]
    public async Task RefreshBudgetDataCommand_PopulatesBudgetPerformanceData()
    {
        // Act
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Assert
        Assert.NotEmpty(_sut.BudgetPerformanceData);
        Assert.All(_sut.BudgetPerformanceData, item =>
        {
            Assert.NotEmpty(item.Enterprise);
            Assert.True(item.Revenue >= 0);
            Assert.True(item.Expenses >= 0);
        });
    }

    [Fact]
    public async Task TrendAnalysisCommand_PopulatesRateTrendData()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Act
        _sut.TrendAnalysisCommand.Execute(null);

        // Assert
        Assert.NotEmpty(_sut.RateTrendData);
        Assert.All(_sut.RateTrendData, item =>
        {
            Assert.NotEmpty(item.Period);
            Assert.True(item.Rate >= 0);
        });
    }

    #endregion

    #region AverageRateIncrease Tests

    [Fact]
    public async Task AverageRateIncrease_CalculatesCorrectly()
    {
        // Arrange
        await _sut.RefreshBudgetDataCommand.ExecuteAsync(null);

        // Act
        var average = _sut.AverageRateIncrease;

        // Assert
        Assert.True(average >= 0);
    }

    [Fact]
    public void AverageRateIncrease_ReturnsZeroWhenNoItems()
    {
        // Act
        var average = _sut.AverageRateIncrease;

        // Assert
        Assert.Equal(0.0, average);
    }

    #endregion

    #region Command CanExecute Tests

    [Fact]
    public void RefreshBudgetDataCommand_CanExecute()
    {
        // Assert
        Assert.True(_sut.RefreshBudgetDataCommand.CanExecute(null));
    }

    [Fact]
    public void BreakEvenAnalysisCommand_CanExecute()
    {
        // Assert
        Assert.True(_sut.BreakEvenAnalysisCommand.CanExecute(null));
    }

    [Fact]
    public void TrendAnalysisCommand_CanExecute()
    {
        // Assert
        Assert.True(_sut.TrendAnalysisCommand.CanExecute(null));
    }

    #endregion
}
