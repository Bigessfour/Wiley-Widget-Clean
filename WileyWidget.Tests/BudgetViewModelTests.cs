using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Xunit;
using Moq;
using WileyWidget.ViewModels;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Tests for the BudgetViewModel functionality
/// </summary>
public class BudgetViewModelTests
{
    private readonly Mock<IEnterpriseRepository> _mockRepository;
    private readonly BudgetViewModel _viewModel;

    public BudgetViewModelTests()
    {
        _mockRepository = new Mock<IEnterpriseRepository>();
        _viewModel = new BudgetViewModel(_mockRepository.Object);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BudgetViewModel(null!));
    }

    [Fact]
    public void Constructor_WithValidRepository_InitializesProperties()
    {
        // Assert
        Assert.NotNull(_viewModel.BudgetDetails);
        Assert.IsType<ObservableCollection<WileyWidget.ViewModels.BudgetDetailItem>>(_viewModel.BudgetDetails);
        Assert.Equal(0, _viewModel.TotalRevenue);
        Assert.Equal(0, _viewModel.TotalExpenses);
        Assert.Equal(0, _viewModel.NetBalance);
        Assert.Equal(0, _viewModel.TotalCitizens);
        Assert.Equal("Click 'Break-even Analysis' to generate insights", _viewModel.BreakEvenAnalysisText);
        Assert.Equal("Click 'Trend Analysis' to view budget trends", _viewModel.TrendAnalysisText);
        Assert.Equal("Click 'Refresh' to load budget data and generate recommendations", _viewModel.RecommendationsText);
        Assert.Equal("Never", _viewModel.LastUpdated);
        Assert.Equal("Ready", _viewModel.AnalysisStatus);
    }

    [Fact]
    public async Task RefreshBudgetDataAsync_WithEmptyRepository_ClearsCollectionsAndSetsDefaults()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Enterprise>());

        // Act
        await _viewModel.RefreshBudgetDataAsync();

        // Assert
        Assert.Empty(_viewModel.BudgetDetails);
        Assert.Equal(0, _viewModel.TotalRevenue);
        Assert.Equal(0, _viewModel.TotalExpenses);
        Assert.Equal(0, _viewModel.NetBalance);
        Assert.Equal(0, _viewModel.TotalCitizens);
        Assert.Contains("Data loaded successfully", _viewModel.AnalysisStatus);
        Assert.NotEqual("Never", _viewModel.LastUpdated);
    }

    [Fact]
    public async Task RefreshBudgetDataAsync_WithMultipleEnterprises_CalculatesTotalsCorrectly()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Name = "Water Department",
                CurrentRate = 2.50m,
                MonthlyExpenses = 15000.00m,
                CitizenCount = 50000
            },
            new Enterprise
            {
                Name = "Sewer Department",
                CurrentRate = 3.00m,
                MonthlyExpenses = 20000.00m,
                CitizenCount = 30000
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(enterprises);

        // Act
        await _viewModel.RefreshBudgetDataAsync();

        // Assert
        Assert.Equal(2, _viewModel.BudgetDetails.Count);

        // Check first enterprise
        var waterDetail = _viewModel.BudgetDetails.First(b => b.EnterpriseName == "Water Department");
        Assert.Equal(50000, waterDetail.CitizenCount);
        Assert.Equal(2.50m, waterDetail.CurrentRate);
        Assert.Equal(125000.00m, waterDetail.MonthlyRevenue); // 2.50 * 50000
        Assert.Equal(15000.00m, waterDetail.MonthlyExpenses);
        Assert.Equal(110000.00m, waterDetail.MonthlyBalance); // 125000 - 15000
        Assert.Equal("Surplus", waterDetail.Status);

        // Check second enterprise
        var sewerDetail = _viewModel.BudgetDetails.First(b => b.EnterpriseName == "Sewer Department");
        Assert.Equal(30000, sewerDetail.CitizenCount);
        Assert.Equal(3.00m, sewerDetail.CurrentRate);
        Assert.Equal(90000.00m, sewerDetail.MonthlyRevenue); // 3.00 * 30000
        Assert.Equal(20000.00m, sewerDetail.MonthlyExpenses);
        Assert.Equal(70000.00m, sewerDetail.MonthlyBalance); // 90000 - 20000
        Assert.Equal("Surplus", sewerDetail.Status);

        // Check totals
        Assert.Equal(215000.00m, _viewModel.TotalRevenue); // 125000 + 90000
        Assert.Equal(35000.00m, _viewModel.TotalExpenses); // 15000 + 20000
        Assert.Equal(180000.00m, _viewModel.NetBalance); // 215000 - 35000
        Assert.Equal(80000, _viewModel.TotalCitizens); // 50000 + 30000
    }

    [Fact]
    public async Task RefreshBudgetDataAsync_WithDeficitEnterprise_SetsCorrectStatus()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Name = "Deficit Department",
                CurrentRate = 1.00m,
                MonthlyExpenses = 20000.00m,
                CitizenCount = 10000
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(enterprises);

        // Act
        await _viewModel.RefreshBudgetDataAsync();

        // Assert
        var detail = _viewModel.BudgetDetails.First();
        Assert.Equal(10000.00m, detail.MonthlyRevenue); // 1.00 * 10000
        Assert.Equal(20000.00m, detail.MonthlyExpenses);
        Assert.Equal(-10000.00m, detail.MonthlyBalance); // 10000 - 20000
        Assert.Equal("Deficit", detail.Status);
    }

    [Fact]
    public async Task RefreshBudgetDataAsync_RepositoryThrowsException_HandlesErrorGracefully()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        await _viewModel.RefreshBudgetDataAsync();

        // Assert
        Assert.Contains("Error:", _viewModel.AnalysisStatus);
        Assert.Contains("Database connection failed", _viewModel.AnalysisStatus);
    }

    [Fact]
    public void BreakEvenAnalysis_WithNoData_ShowsAppropriateMessage()
    {
        // Act
        _viewModel.BreakEvenAnalysisCommand.Execute(null);

        // Assert
        Assert.Contains("No budget data available", _viewModel.BreakEvenAnalysisText);
    }

    [Fact]
    public async Task BreakEvenAnalysis_WithData_GeneratesAnalysis()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Name = "Water Department",
                CurrentRate = 2.50m,
                MonthlyExpenses = 15000.00m,
                CitizenCount = 50000
            },
            new Enterprise
            {
                Name = "Sewer Department",
                CurrentRate = 2.00m,
                MonthlyExpenses = 20000.00m,
                CitizenCount = 30000
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(enterprises);
        await _viewModel.RefreshBudgetDataAsync();

        // Act
        _viewModel.BreakEvenAnalysisCommand.Execute(null);

        // Assert
        Assert.Contains("BREAK-EVEN ANALYSIS", _viewModel.BreakEvenAnalysisText);
        Assert.Contains("Water Department", _viewModel.BreakEvenAnalysisText);
        Assert.Contains("Sewer Department", _viewModel.BreakEvenAnalysisText);
        Assert.Contains("$2.50", _viewModel.BreakEvenAnalysisText);
        Assert.Contains("$2.00", _viewModel.BreakEvenAnalysisText);
    }

    [Fact]
    public void TrendAnalysis_WithNoData_ShowsAppropriateMessage()
    {
        // Act
        _viewModel.TrendAnalysisCommand.Execute(null);

        // Assert
        Assert.Contains("No budget data available", _viewModel.TrendAnalysisText);
    }

    [Fact]
    public async Task TrendAnalysis_WithData_GeneratesAnalysis()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Name = "Enterprise 1",
                CurrentRate = 2.50m,
                MonthlyExpenses = 15000.00m,
                CitizenCount = 50000
            },
            new Enterprise
            {
                Name = "Enterprise 2",
                CurrentRate = 3.00m,
                MonthlyExpenses = 20000.00m,
                CitizenCount = 30000
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(enterprises);
        await _viewModel.RefreshBudgetDataAsync();

        // Act
        _viewModel.TrendAnalysisCommand.Execute(null);

        // Assert
        Assert.Contains("BUDGET TREND ANALYSIS", _viewModel.TrendAnalysisText);
        Assert.Contains("Portfolio Overview", _viewModel.TrendAnalysisText);
        Assert.Contains("Revenue Distribution", _viewModel.TrendAnalysisText);
        Assert.Contains("Expense Analysis", _viewModel.TrendAnalysisText);
    }

    [Fact]
    public async Task RefreshBudgetDataAsync_GeneratesRecommendations()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Name = "Profitable Enterprise",
                CurrentRate = 3.00m,
                MonthlyExpenses = 15000.00m,
                CitizenCount = 50000
            },
            new Enterprise
            {
                Name = "Deficit Enterprise",
                CurrentRate = 1.00m,
                MonthlyExpenses = 20000.00m,
                CitizenCount = 10000
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(enterprises);
        await _viewModel.RefreshBudgetDataAsync();

        // Assert
        Assert.Contains("BUDGET RECOMMENDATIONS", _viewModel.RecommendationsText);
        Assert.Contains("Deficit Enterprise", _viewModel.RecommendationsText);
        Assert.Contains("Profitable Enterprise", _viewModel.RecommendationsText);
    }

    [Fact]
    public void ExportReport_ShowsNotImplementedMessage()
    {
        // Act
        _viewModel.ExportReportCommand.Execute(null);

        // Assert
        Assert.Contains("not yet implemented", _viewModel.AnalysisStatus);
    }

    [Fact]
    public async Task RefreshBudgetDataAsync_SetsLoadingStatusDuringOperation()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Name = "Test Enterprise",
                CurrentRate = 1.00m,
                MonthlyExpenses = 1000.00m,
                CitizenCount = 1000
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(enterprises);

        // Act
        await _viewModel.RefreshBudgetDataAsync();

        // Assert
        Assert.Contains("Data loaded successfully", _viewModel.AnalysisStatus);
    }
}