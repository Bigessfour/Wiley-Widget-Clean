using Xunit;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;
using Moq;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Tests;

/// <summary>
/// Tests for AI conversation features
/// </summary>
public class AIConversationTests
{
    private readonly Mock<IAIService> _mockAIService;
    private readonly Mock<IChargeCalculatorService> _mockChargeCalculator;
    private readonly Mock<IWhatIfScenarioEngine> _mockScenarioEngine;
    private readonly Mock<IGrokSupercomputer> _mockGrokSupercomputer;
    private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepository;
    private readonly Mock<IDispatcherHelper> _mockDispatcherHelper;
    private readonly Mock<ILogger<AIAssistViewModel>> _mockLogger;
    private readonly AIAssistViewModel _viewModel;

    public AIConversationTests()
    {
        _mockAIService = new Mock<IAIService>();
        _mockChargeCalculator = new Mock<IChargeCalculatorService>();
        _mockScenarioEngine = new Mock<IWhatIfScenarioEngine>();
        _mockGrokSupercomputer = new Mock<IGrokSupercomputer>();
        _mockEnterpriseRepository = new Mock<IEnterpriseRepository>();
        _mockDispatcherHelper = new Mock<IDispatcherHelper>();
        _mockLogger = new Mock<ILogger<AIAssistViewModel>>();

        _viewModel = new AIAssistViewModel(
            _mockAIService.Object,
            _mockChargeCalculator.Object,
            _mockScenarioEngine.Object,
            _mockGrokSupercomputer.Object,
            _mockEnterpriseRepository.Object,
            _mockDispatcherHelper.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesWithGeneralMode()
    {
        // Assert
        Assert.True(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.False(_viewModel.ShowFinancialInputs);
        Assert.Equal("General Assistant", _viewModel.SelectedMode?.Name);
    }

    [Fact]
    public void SetConversationMode_General_SetsCorrectProperties()
    {
        // Act
        _viewModel.SetConversationModeCommand.Execute("General");

        // Assert
        Assert.True(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.False(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void SetConversationMode_ServiceCharge_SetsCorrectProperties()
    {
        // Act
        _viewModel.SetConversationModeCommand.Execute("ServiceCharge");

        // Assert
        Assert.False(_viewModel.IsGeneralMode);
        Assert.True(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.True(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void SetConversationMode_WhatIf_SetsCorrectProperties()
    {
        // Act
        _viewModel.SetConversationModeCommand.Execute("WhatIf");

        // Assert
        Assert.False(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.True(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.True(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void SetConversationMode_Proactive_SetsCorrectProperties()
    {
        // Act
        _viewModel.SetConversationModeCommand.Execute("Proactive");

        // Assert
        Assert.False(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.True(_viewModel.IsProactiveMode);
        Assert.True(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void SetConversationMode_InvalidMode_DefaultsToGeneral()
    {
        // Act
        _viewModel.SetConversationModeCommand.Execute("InvalidMode");

        // Assert
        Assert.True(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.False(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void AvailableModes_ContainsAllExpectedModes()
    {
        // Assert
        Assert.Equal(4, _viewModel.AvailableModes.Count);
        Assert.Contains(_viewModel.AvailableModes, m => m.Name == "General Assistant");
        Assert.Contains(_viewModel.AvailableModes, m => m.Name == "Service Charge Calculator");
        Assert.Contains(_viewModel.AvailableModes, m => m.Name == "What-If Planner");
        Assert.Contains(_viewModel.AvailableModes, m => m.Name == "Proactive Advisor");
    }

    [Fact]
    public void FinancialProperties_HaveCorrectDefaultValues()
    {
        // Assert
        Assert.Equal(0, _viewModel.AnnualExpenses);
        Assert.Equal(10, _viewModel.TargetReservePercentage);
        Assert.Equal(0, _viewModel.PayRaisePercentage);
        Assert.Equal(0, _viewModel.BenefitsIncreasePercentage);
        Assert.Equal(0, _viewModel.EquipmentCost);
        Assert.Equal(15, _viewModel.ReserveAllocationPercentage);
    }
}