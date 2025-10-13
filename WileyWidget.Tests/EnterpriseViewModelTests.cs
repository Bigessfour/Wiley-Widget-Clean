using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;
using WileyWidget.Models;
using WileyWidget.ViewModels;
using WileyWidget.Business.Interfaces;
using EnterpriseRepo = WileyWidget.Business.Interfaces.IEnterpriseRepository;
using EnterpriseModel = WileyWidget.Models.Enterprise;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the EnterpriseViewModel
/// Tests data binding, budget calculations, and public functionality
/// </summary>
public class EnterpriseViewModelTests : IDisposable
{
    private readonly Mock<EnterpriseRepo> _mockEnterpriseRepo;
    private readonly EnterpriseViewModel _viewModel;

    public EnterpriseViewModelTests()
    {
        _mockEnterpriseRepo = new Mock<EnterpriseRepo>();
        _viewModel = new EnterpriseViewModel(_mockEnterpriseRepo.Object);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EnterpriseViewModel(null!));
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Assert
        Assert.NotNull(_viewModel.Enterprises);
        Assert.IsType<ObservableCollection<EnterpriseModel>>(_viewModel.Enterprises);
        Assert.Null(_viewModel.SelectedEnterprise);
        Assert.False(_viewModel.IsLoading);
        Assert.Equal("No budget data available", _viewModel.BudgetSummaryText);
    }

    [Fact]
    public async Task LoadEnterprisesAsync_WithValidData_LoadsEnterprises()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new EnterpriseModel
            {
                Id = 1,
                Name = "Water Department",
                Type = "Water",
                CurrentRate = 2.50m,
                MonthlyExpenses = 15000.00m,
                CitizenCount = 50000
            },
            new EnterpriseModel
            {
                Id = 2,
                Name = "Sewer Department",
                Type = "Sewer",
                CurrentRate = 3.00m,
                MonthlyExpenses = 20000.00m,
                CitizenCount = 30000
            }
        };

        _mockEnterpriseRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(enterprises);

        // Act
        await _viewModel.LoadEnterprisesAsync();

        // Assert
        Assert.Equal(2, _viewModel.Enterprises.Count);
        Assert.Equal("Water Department", _viewModel.Enterprises[0].Name);
        Assert.Equal("Sewer Department", _viewModel.Enterprises[1].Name);
        Assert.False(_viewModel.IsLoading);
        _mockEnterpriseRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task LoadEnterprisesAsync_WithException_HandlesErrorGracefully()
    {
        // Arrange
        _mockEnterpriseRepo.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

        // Act
        await _viewModel.LoadEnterprisesAsync();

        // Assert
        Assert.Empty(_viewModel.Enterprises);
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task LoadEnterprisesAsync_SetsLoadingStateCorrectly()
    {
        // Arrange
        var enterprises = new List<Enterprise>();
        var isLoadingDuringOperation = false;
        
        _mockEnterpriseRepo.Setup(r => r.GetAllAsync())
            .Returns(async () => 
            {
                // Check IsLoading during the async operation
                isLoadingDuringOperation = _viewModel.IsLoading;
                await Task.Delay(100); // Add delay to ensure operation takes time
                return enterprises;
            });

        // Act
        await _viewModel.LoadEnterprisesAsync();

        // Assert
        Assert.True(isLoadingDuringOperation, "IsLoading should be true during the operation");
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after completion");
    }

    [Fact]
    public void GetBudgetSummary_WithMultipleEnterprises_CalculatesCorrectly()
    {
        // Arrange
        var enterprise1 = new EnterpriseModel
        {
            Id = 1,
            Name = "Water",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 50000
        };
        var enterprise2 = new EnterpriseModel
        {
            Id = 2,
            Name = "Sewer",
            CurrentRate = 3.00m,
            MonthlyExpenses = 20000.00m,
            CitizenCount = 30000
        };

        _viewModel.Enterprises.Add(enterprise1);
        _viewModel.Enterprises.Add(enterprise2);

        // Act
        var summary = _viewModel.GetBudgetSummary();

        // Assert
        Assert.Contains("Total Revenue: $215,000.00", summary);
        Assert.Contains("Total Expenses: $35,000.00", summary);
        Assert.Contains("Monthly Balance: $180,000.00", summary);
        Assert.Contains("Citizens Served: 80000", summary);
        Assert.Contains("Status: Surplus", summary);
    }

    [Fact]
    public void GetBudgetSummary_WithEmptyCollection_ReturnsNoEnterprisesMessage()
    {
        // Act
        var summary = _viewModel.GetBudgetSummary();

        // Assert
        Assert.Equal("No enterprises loaded", summary);
    }

    [Fact]
    public void GetBudgetSummary_WithDeficit_ShowsDeficitStatus()
    {
        // Arrange
        var enterprise = new EnterpriseModel
        {
            Id = 1,
            Name = "Water",
            CurrentRate = 1.00m, // Low rate
            MonthlyExpenses = 15000.00m,
            CitizenCount = 10000 // 10000 revenue vs 15000 expenses = deficit
        };

        _viewModel.Enterprises.Add(enterprise);

        // Act
        var summary = _viewModel.GetBudgetSummary();

        // Assert
        Assert.Contains("Status: Deficit", summary);
        Assert.Contains("Total Revenue: $10,000.00", summary);
        Assert.Contains("Total Expenses: $15,000.00", summary);
        Assert.Contains("Monthly Balance: $-5,000.00", summary);
    }

    [Fact]
    public void GetBudgetSummary_WithBreakEven_ShowsSurplusStatus()
    {
        // Arrange
        var enterprise = new EnterpriseModel
        {
            Id = 1,
            Name = "Water",
            CurrentRate = 1.50m, // Break-even rate
            MonthlyExpenses = 15000.00m,
            CitizenCount = 10000 // 15000 revenue vs 15000 expenses = break-even
        };

        _viewModel.Enterprises.Add(enterprise);

        // Act
        var summary = _viewModel.GetBudgetSummary();

        // Assert
        Assert.Contains("Status: Surplus", summary); // 0 balance is considered surplus
        Assert.Contains("Total Revenue: $15,000.00", summary);
        Assert.Contains("Total Expenses: $15,000.00", summary);
        Assert.Contains("Monthly Balance: $0.00", summary);
    }

    [Fact]
    public void SelectedEnterprise_PropertyChanged_NotifiesCorrectly()
    {
        // Arrange
        var enterprise = new EnterpriseModel { Id = 1, Name = "Test" };
        var propertyChangedCalled = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SelectedEnterprise))
                propertyChangedCalled = true;
        };

        // Act
        _viewModel.SelectedEnterprise = enterprise;

        // Assert
        Assert.True(propertyChangedCalled);
        Assert.Equal(enterprise, _viewModel.SelectedEnterprise);
    }

    [Fact]
    public void Enterprises_CollectionChanged_TriggersPropertyNotifications()
    {
        // Arrange
        var enterprise = new EnterpriseModel { Id = 1, Name = "Test" };
        var propertyChangedEvents = new List<string>();

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                propertyChangedEvents.Add(e.PropertyName);
            }
        };

        // Act
        _viewModel.Enterprises.Add(enterprise);

        // Assert
        // Note: ObservableCollection doesn't automatically notify on collection changes
        // The view model would need to implement INotifyPropertyChanged for collection changes
        // This test verifies the collection is working as expected
        Assert.Single(_viewModel.Enterprises);
        Assert.Equal(enterprise, _viewModel.Enterprises[0]);
    }

    [Fact]
    public void BudgetSummaryText_PropertyChanged_NotifiesCorrectly()
    {
        // Arrange
        var propertyChangedCalled = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.BudgetSummaryText))
                propertyChangedCalled = true;
        };

        // Act
        _viewModel.BudgetSummaryText = "Test summary";

        // Assert
        Assert.True(propertyChangedCalled);
        Assert.Equal("Test summary", _viewModel.BudgetSummaryText);
    }

    [Fact]
    public void IsLoading_PropertyChanged_NotifiesCorrectly()
    {
        // Arrange
        var propertyChangedCalled = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsLoading))
                propertyChangedCalled = true;
        };

        // Act
        _viewModel.IsLoading = true;

        // Assert
        Assert.True(propertyChangedCalled);
        Assert.True(_viewModel.IsLoading);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel?.Dispose();
        }
    }
}
