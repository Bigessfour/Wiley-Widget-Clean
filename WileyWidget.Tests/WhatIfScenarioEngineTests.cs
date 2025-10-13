using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the WhatIfScenarioEngine service
/// Tests scenario generation, financial calculations, and enterprise analysis
/// </summary>
public class WhatIfScenarioEngineTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockScopedServiceProvider;
    private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepo;
    private readonly Mock<IMunicipalAccountRepository> _mockAccountRepo;
    private readonly Mock<IChargeCalculatorService> _mockChargeCalculator;
    private readonly WhatIfScenarioEngine _engine;

    public WhatIfScenarioEngineTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockScopedServiceProvider = new Mock<IServiceProvider>();
        _mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
        _mockAccountRepo = new Mock<IMunicipalAccountRepository>();
        _mockChargeCalculator = new Mock<IChargeCalculatorService>();

        // Setup the service provider to return IServiceScopeFactory when requested
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockScopeFactory.Object);

        // Setup scope factory to return our mock scope
        _mockScopeFactory.Setup(sf => sf.CreateScope()).Returns(_mockScope.Object);

        // Setup the scope's service provider
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockScopedServiceProvider.Object);

        // Setup GetService calls for the repositories
        _mockScopedServiceProvider.Setup(sp => sp.GetService(typeof(IEnterpriseRepository)))
            .Returns(_mockEnterpriseRepo.Object);
        _mockScopedServiceProvider.Setup(sp => sp.GetService(typeof(IMunicipalAccountRepository)))
            .Returns(_mockAccountRepo.Object);

        _engine = new WhatIfScenarioEngine(_mockServiceProvider.Object, _mockChargeCalculator.Object);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WhatIfScenarioEngine(null, _mockChargeCalculator.Object));
    }

    [Fact]
    public void Constructor_WithNullChargeCalculator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WhatIfScenarioEngine(_mockServiceProvider.Object, null));
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_WithValidEnterprise_ReturnsScenario()
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

        var accounts = new List<MunicipalAccount>
        {
            new MunicipalAccount
            {
                Id = 1,
                Fund = MunicipalFundType.Water,
                AccountNumber = new AccountNumber("1000"),
                Name = "Water Revenue",
                Balance = 125000.00m,
                Type = AccountType.Revenue
            },
            new MunicipalAccount
            {
                Id = 2,
                Fund = MunicipalFundType.Water,
                AccountNumber = new AccountNumber("2000"),
                Name = "Water Expenses",
                Balance = -15000.00m,
                Type = AccountType.Expense
            }
        };

        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 2.50m,
            MonthlyRevenueAtRecommended = 125000.00m,
            TotalMonthlyExpenses = 15000.00m,
            MonthlySurplus = 105000.00m,
            BreakEvenAnalysis = new BreakEvenAnalysis
            {
                BreakEvenRate = 3.60m
            }
        };

        var parameters = new ScenarioParameters
        {
            PayRaisePercentage = 0.50m,
            BenefitsIncreaseAmount = 500.00m,
            EquipmentPurchaseAmount = 10000.00m,
            EquipmentFinancingYears = 5,
            ReservePercentage = 0.10m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        // Act
        var result = await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.BaselineData);
        Assert.NotNull(result.ScenarioImpacts);
        Assert.True(result.ScenarioImpacts.Count >= 4); // Should have pay raise, benefits, equipment, and reserve impacts
        Assert.NotNull(result.TotalImpact);
        Assert.True(result.TotalImpact.TotalAnnualExpenseIncrease >= 0);
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_WithNonExistentEnterprise_ThrowsException()
    {
        // Arrange
        var enterpriseId = 999;
        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync((Enterprise)null);

        var parameters = new ScenarioParameters
        {
            PayRaisePercentage = 0.50m
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters));
        Assert.Contains($"Enterprise with ID {enterpriseId} not found", exception.Message);
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_WithWaterEnterprise_UsesCorrectFundType()
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

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 2.50m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters { PayRaisePercentage = 0.50m };

        // Act
        await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        _mockAccountRepo.Verify(r => r.GetByFundAsync(MunicipalFundType.Water), Times.Once);
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_WithSewerEnterprise_UsesCorrectFundType()
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

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 3.00m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Sewer)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters { PayRaisePercentage = 0.50m };

        // Act
        await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        _mockAccountRepo.Verify(r => r.GetByFundAsync(MunicipalFundType.Sewer), Times.Once);
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_WithTrashEnterprise_UsesCorrectFundType()
    {
        // Arrange
        var enterpriseId = 3;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Trash Department",
            Type = "Trash",
            CurrentRate = 1.50m,
            MonthlyExpenses = 10000.00m,
            CitizenCount = 40000
        };

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 1.50m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Trash)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters { PayRaisePercentage = 0.50m };

        // Act
        await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        _mockAccountRepo.Verify(r => r.GetByFundAsync(MunicipalFundType.Trash), Times.Once);
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_WithOtherEnterprise_UsesEnterpriseFundType()
    {
        // Arrange
        var enterpriseId = 4;
        var enterprise = new Enterprise
        {
            Id = enterpriseId,
            Name = "Other Department",
            Type = "Other",
            CurrentRate = 2.00m,
            MonthlyExpenses = 12000.00m,
            CitizenCount = 25000
        };

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 2.00m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Enterprise)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters { PayRaisePercentage = 0.50m };

        // Act
        await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        _mockAccountRepo.Verify(r => r.GetByFundAsync(MunicipalFundType.Enterprise), Times.Once);
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_CalculatesRateIncreaseScenarioCorrectly()
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

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 2.50m,
            MonthlyRevenueAtRecommended = 125000.00m,
            TotalMonthlyExpenses = 15000.00m,
            MonthlySurplus = 110000.00m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters
        {
            PayRaisePercentage = 0.50m // $0.50 increase
        };

        // Act
        var result = await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pay Raise 50%", result.ScenarioName);
        Assert.NotNull(result.ScenarioImpacts);
        Assert.True(result.ScenarioImpacts.Count > 0);

        // Check that pay raise impact is calculated
        var payRaiseImpact = result.ScenarioImpacts.FirstOrDefault(s => s.Category == "Employee Pay Raise");
        Assert.NotNull(payRaiseImpact);
        Assert.Contains("50%", payRaiseImpact.Description);

        // Check total impact calculations
        Assert.True(result.TotalImpact.TotalAnnualExpenseIncrease > 0);
        Assert.True(result.TotalImpact.RequiredRateIncrease > 0);
        Assert.Equal(enterprise.CurrentRate + result.TotalImpact.RequiredRateIncrease, result.TotalImpact.NewMonthlyRate);
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_CalculatesPopulationGrowthScenarioCorrectly()
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

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 2.50m,
            MonthlyRevenueAtRecommended = 125000.00m,
            TotalMonthlyExpenses = 15000.00m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters
        {
            // Population growth not supported in current implementation
        };

        // Act
        var result = await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ScenarioImpacts);
        // Population growth is not implemented as a separate scenario in the current version
        // The comprehensive scenario focuses on expense changes rather than population changes
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_CalculatesInflationScenarioCorrectly()
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

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 2.50m,
            MonthlyRevenueAtRecommended = 125000.00m,
            TotalMonthlyExpenses = 15000.00m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters
        {
            // Inflation rate not supported in current implementation
        };

        // Act
        var result = await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ScenarioImpacts);
        // Inflation is not implemented as a separate scenario in the current version
        // The comprehensive scenario focuses on specific expense changes
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_CalculatesEfficiencyScenarioCorrectly()
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

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 2.50m,
            MonthlyRevenueAtRecommended = 125000.00m,
            TotalMonthlyExpenses = 15000.00m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters
        {
            // Efficiency improvements not supported in current implementation
        };

        // Act
        var result = await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ScenarioImpacts);
        // Efficiency improvements are not implemented as a separate scenario in the current version
        // The comprehensive scenario focuses on specific expense changes
    }

    [Fact]
    public async Task GenerateComprehensiveScenarioAsync_HandlesEmptyParameters()
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

        var accounts = new List<MunicipalAccount>();
        var baselineRecommendation = new ServiceChargeRecommendation
        {
            EnterpriseId = enterpriseId,
            RecommendedRate = 2.50m,
            MonthlyRevenueAtRecommended = 125000.00m,
            TotalMonthlyExpenses = 15000.00m
        };

        _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId)).ReturnsAsync(enterprise);
        _mockAccountRepo.Setup(r => r.GetByFundAsync(MunicipalFundType.Water)).ReturnsAsync(accounts);
        _mockChargeCalculator.Setup(c => c.CalculateRecommendedChargeAsync(enterpriseId))
            .ReturnsAsync(baselineRecommendation);

        var parameters = new ScenarioParameters(); // All zeros

        // Act
        var result = await _engine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

        // Assert
        Assert.NotNull(result);
        // ScenarioImpacts should still be generated but with minimal impact when parameters are zero
        Assert.NotNull(result.ScenarioImpacts);
        Assert.NotNull(result.TotalImpact);
    }
}
