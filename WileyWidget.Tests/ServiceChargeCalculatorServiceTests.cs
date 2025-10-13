#pragma warning disable IDE0005
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;
using EnterpriseRepo = WileyWidget.Business.Interfaces.IEnterpriseRepository;
using MunicipalRepo = WileyWidget.Business.Interfaces.IMunicipalAccountRepository;
using Enterprise = WileyWidget.Models.Enterprise;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the ServiceChargeCalculatorService
/// Tests charge calculations, break-even analysis, and what-if scenarios
/// </summary>
public class ServiceChargeCalculatorServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockScopedServiceProvider;
    private readonly Mock<EnterpriseRepo> _mockEnterpriseRepo;
    private readonly Mock<MunicipalRepo> _mockAccountRepo;
    private readonly ServiceChargeCalculatorService _service;

    public ServiceChargeCalculatorServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockScopedServiceProvider = new Mock<IServiceProvider>();
        _mockEnterpriseRepo = new Mock<EnterpriseRepo>();
        _mockAccountRepo = new Mock<MunicipalRepo>();

        // Setup the service provider to return IServiceScopeFactory when requested
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockScopeFactory.Object);

        // Setup scope factory to return our mock scope - mock the interface method, not extension method
        _mockScopeFactory.Setup(sf => sf.CreateScope()).Returns(_mockScope.Object);

        // Setup the scope's service provider
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockScopedServiceProvider.Object);

        // Setup GetService calls for the repositories (this is how DI actually works)
        _mockScopedServiceProvider.Setup(sp => sp.GetService(typeof(IEnterpriseRepository)))
            .Returns(_mockEnterpriseRepo.Object);
        _mockScopedServiceProvider.Setup(sp => sp.GetService(typeof(IMunicipalAccountRepository)))
            .Returns(_mockAccountRepo.Object);

        _service = new ServiceChargeCalculatorService(_mockServiceProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ServiceChargeCalculatorService(null!));
    }

    [Fact]
    public async Task CalculateRecommendedChargeAsync_WithValidEnterprise_ReturnsRecommendation()
    {
        // Arrange
        var enterpriseId = 1;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Water Department",
            Type = "Water",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 50000,
            // MonthlyRevenue = 125000.00m (calculated as CitizenCount * CurrentRate)
            // MonthlyBalance = 110000.00m (calculated as MonthlyRevenue - MonthlyExpenses)
        };

        var expenseAccounts = new List<MunicipalAccount>
        {
            new MunicipalAccount
            {
                Id = 1,
                AccountNumber = new AccountNumber("2000"),
                Name = "Water Expenses",
                Type = AccountType.Expense,
                Fund = MunicipalFundType.Water,
                BudgetAmount = 180000.00m // Annual budget
            },
            new MunicipalAccount
            {
                Id = 2,
                AccountNumber = new AccountNumber("2001"),
                Name = "Maintenance",
                Type = AccountType.Expense,
                Fund = MunicipalFundType.Water,
                BudgetAmount = 24000.00m // Annual budget
            }
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(expenseAccounts);

        // Act
        var result = await _service.CalculateRecommendedChargeAsync(enterpriseId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(enterpriseId, result.EnterpriseId);
        Assert.Equal("Water Department", result.EnterpriseName);
        Assert.Equal(2.50m, result.CurrentRate);
        Assert.True(result.RecommendedRate > 0);
        Assert.True(result.TotalMonthlyExpenses > 0);
        Assert.True(result.MonthlyRevenueAtRecommended > 0);
        Assert.True(result.MonthlySurplus > 0);
        Assert.True(result.ReserveAllocation > 0);
        Assert.NotNull(result.BreakEvenAnalysis);
        Assert.NotNull(result.Assumptions);
        Assert.True(result.Assumptions.Count > 0);
        Assert.True(result.CalculationDate <= DateTime.Now);
    }

    [Fact]
    public async Task CalculateRecommendedChargeAsync_WithNonExistentEnterprise_ThrowsException()
    {
        // Arrange
        var enterpriseId = 999;
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).Returns(Task.FromResult<Enterprise?>(null));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CalculateRecommendedChargeAsync(enterpriseId));
        Assert.Contains($"Enterprise with ID {enterpriseId} not found", exception.Message);
    }

    [Fact]
    public async Task CalculateRecommendedChargeAsync_WithWaterEnterprise_UsesCorrectFundType()
    {
        // Arrange
        var enterpriseId = 1;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Water Department",
            Type = "Water",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 50000
        };

        var expenseAccounts = new List<MunicipalAccount>();
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
    _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(expenseAccounts);

        // Act
        await _service.CalculateRecommendedChargeAsync(enterpriseId);

        // Assert
    _mockAccountRepo.Verify(r => r.GetByFundAsync(MunicipalFundType.Water), Times.Once);
    }

    [Fact]
    public async Task CalculateRecommendedChargeAsync_WithSewerEnterprise_UsesCorrectFundType()
    {
        // Arrange
        var enterpriseId = 2;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Sewer Department",
            Type = "Sewer",
            CurrentRate = 3.00m,
            MonthlyExpenses = 20000.00m,
            CitizenCount = 30000
        };

        var expenseAccounts = new List<MunicipalAccount>();
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
    _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Sewer)).ReturnsAsync(expenseAccounts);

        // Act
        await _service.CalculateRecommendedChargeAsync(enterpriseId);

        // Assert
    _mockAccountRepo.Verify(r => r.GetByFundAsync(MunicipalFundType.Sewer), Times.Once);
    }

    [Fact]
    public async Task CalculateRecommendedChargeAsync_WithOtherEnterprise_UsesEnterpriseFundType()
    {
        // Arrange
        var enterpriseId = 3;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Other Department",
            Type = "Other",
            CurrentRate = 2.00m,
            MonthlyExpenses = 12000.00m,
            CitizenCount = 25000
        };

        var expenseAccounts = new List<MunicipalAccount>();
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
    _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Enterprise)).ReturnsAsync(expenseAccounts);

        // Act
        await _service.CalculateRecommendedChargeAsync(enterpriseId);

        // Assert
    _mockAccountRepo.Verify(r => r.GetByFundAsync(MunicipalFundType.Enterprise), Times.Once);
    }

    [Fact]
    public async Task CalculateRecommendedChargeAsync_CalculatesExpensesCorrectly()
    {
        // Arrange
        var enterpriseId = 1;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Water Department",
            Type = "Water",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m, // Direct monthly expenses
            CitizenCount = 50000
        };

        var expenseAccounts = new List<MunicipalAccount>
        {
            new MunicipalAccount
            {
                Id = 1,
                AccountNumber = new AccountNumber("2000"),
                Name = "Water Expenses",
                Type = AccountType.Expense,
                Fund = MunicipalFundType.Water,
                BudgetAmount = 180000.00m // Annual budget = 15000 monthly
            },
            new MunicipalAccount
            {
                Id = 2,
                AccountNumber = new AccountNumber("2001"),
                Name = "Maintenance",
                Type = AccountType.Expense,
                Fund = MunicipalFundType.Water,
                BudgetAmount = 24000.00m // Annual budget = 2000 monthly
            },
            new MunicipalAccount
            {
                Id = 3,
                AccountNumber = new AccountNumber("1000"),
                Name = "Revenue",
                Type = AccountType.Revenue, // Should be ignored
                Fund = MunicipalFundType.Water,
                BudgetAmount = 600000.00m
            }
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
    _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(expenseAccounts);

        // Act
        var result = await _service.CalculateRecommendedChargeAsync(enterpriseId);

        // Assert
        // Expected: (15000 + 15000 + 2000) = 32000 monthly expenses
        Assert.Equal(32000.00m, result.TotalMonthlyExpenses);
    }

    [Fact]
    public async Task CalculateRecommendedChargeAsync_CalculatesRecommendedRateCorrectly()
    {
        // Arrange
        var enterpriseId = 1;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Water Department",
            Type = "Water",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 50000
        };

        var expenseAccounts = new List<MunicipalAccount>();
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
    _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(expenseAccounts);

        // Act
        var result = await _service.CalculateRecommendedChargeAsync(enterpriseId);

        // Assert
        // Calculation: 15000 * 1.10 (reserves) * 1.05 (profit) / 50000 = 1732.50
        var expectedRate = Math.Round(15000.00m * 1.10m * 1.05m / 50000, 2);
        Assert.Equal(expectedRate, result.RecommendedRate);
        Assert.Equal(expectedRate * 50000, result.MonthlyRevenueAtRecommended);
        Assert.Equal(15000.00m * 0.10m, result.ReserveAllocation);
    }

    [Fact]
    public async Task GenerateChargeScenarioAsync_WithValidParameters_ReturnsScenario()
    {
        // Arrange
        var enterpriseId = 1;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Water Department",
            Type = "Water",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 50000,
            // MonthlyRevenue = 125000.00m (calculated as CitizenCount * CurrentRate)
            // MonthlyBalance = 110000.00m (calculated as MonthlyRevenue - MonthlyExpenses)
        };

        var expenseAccounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            EnterpriseName = "Water Department",
            CurrentRate = 2.50m,
            RecommendedRate = 2.50m,
            TotalMonthlyExpenses = 15000.00m,
            MonthlyRevenueAtRecommended = 125000.00m,
            MonthlySurplus = 110000.00m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
    _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(expenseAccounts);

        // Act
        var result = await _service.GenerateChargeScenarioAsync(enterpriseId, 0.50m, 2000.00m);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Rate Increase: $0.50", result.ScenarioName);
        Assert.Contains("Expense Change: $2,000.00", result.ScenarioName);
        Assert.Equal(2.50m, result.CurrentRate);
        Assert.Equal(3.00m, result.ProposedRate);
        Assert.Equal(15000.00m, result.CurrentMonthlyExpenses);
        Assert.Equal(17000.00m, result.ProposedMonthlyExpenses);
        Assert.Equal(125000.00m, result.CurrentMonthlyRevenue);
        Assert.Equal(150000.00m, result.ProposedMonthlyRevenue);
        Assert.Equal(110000.00m, result.CurrentMonthlyBalance);
        Assert.Equal(133000.00m, result.ProposedMonthlyBalance);
        Assert.NotNull(result.ImpactAnalysis);
        Assert.NotNull(result.Recommendations);
    }

    [Fact]
    public async Task GenerateChargeScenarioAsync_WithNonExistentEnterprise_ThrowsException()
    {
        // Arrange
        var enterpriseId = 999;
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).Returns(Task.FromResult<Enterprise?>(null));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GenerateChargeScenarioAsync(enterpriseId, 0.50m));
        Assert.Contains($"Enterprise with ID {enterpriseId} not found", exception.Message);
    }

    [Fact]
    public async Task GenerateChargeScenarioAsync_WithZeroChanges_GeneratesNeutralScenario()
    {
        // Arrange
        var enterpriseId = 1;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Water Department",
            Type = "Water",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 50000,
            // MonthlyRevenue = 125000.00m (calculated as CitizenCount * CurrentRate)
            // MonthlyBalance = 110000.00m (calculated as MonthlyRevenue - MonthlyExpenses)
        };

        var expenseAccounts = new List<MunicipalAccount>();
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
    _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(expenseAccounts);

        // Act
        var result = await _service.GenerateChargeScenarioAsync(enterpriseId, 0.00m, 0.00m);

        // Assert
        Assert.Equal(2.50m, result.ProposedRate);
        Assert.Equal(15000.00m, result.ProposedMonthlyExpenses);
        Assert.Equal(125000.00m, result.ProposedMonthlyRevenue);
        Assert.Equal(110000.00m, result.ProposedMonthlyBalance);
        Assert.Contains("No change in monthly surplus", result.ImpactAnalysis);
    }

    [Fact]
    public async Task GenerateChargeScenarioAsync_WithNegativeBalance_IncludesWarning()
    {
        // Arrange
        var enterpriseId = 1;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Water Department",
            Type = "Water",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 50000,
            // MonthlyRevenue = 125000.00m (calculated as CitizenCount * CurrentRate)
            // MonthlyBalance = 110000.00m (calculated as MonthlyRevenue - MonthlyExpenses)
        };

        var expenseAccounts = new List<MunicipalAccount>();
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
    _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(expenseAccounts);

        // Act
        var result = await _service.GenerateChargeScenarioAsync(enterpriseId, -1.00m, 70000.00m);

        // Assert
        Assert.Equal(1.50m, result.ProposedRate);
        Assert.Equal(85000.00m, result.ProposedMonthlyExpenses);
        Assert.Equal(75000.00m, result.ProposedMonthlyRevenue);
        Assert.Equal(-10000.00m, result.ProposedMonthlyBalance); // 75000 - 85000
        Assert.Contains("Negative cash flow", result.ImpactAnalysis);
        Assert.Contains("service sustainability at risk", result.ImpactAnalysis);
    }
}
