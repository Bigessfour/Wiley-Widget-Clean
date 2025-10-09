using System.Collections.ObjectModel;
using Xunit;
using Moq;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for AIAssistViewModel functionality
/// Tests conversation modes, message handling, AI service integration, and financial input validation
/// </summary>
public class AIAssistViewModelTests
{
    private readonly Mock<IAIService> _mockAIService;
    private readonly Mock<IChargeCalculatorService> _mockChargeCalculator;
    private readonly Mock<IWhatIfScenarioEngine> _mockScenarioEngine;
    private readonly Mock<IGrokSupercomputer> _mockGrokSupercomputer;
    private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepository;
    private readonly Mock<IDispatcherHelper> _mockDispatcherHelper;
    private readonly Mock<ILogger<AIAssistViewModel>> _mockLogger;
    private readonly AIAssistViewModel _viewModel;

    public AIAssistViewModelTests()
    {
        _mockAIService = new Mock<IAIService>();
        _mockChargeCalculator = new Mock<IChargeCalculatorService>();
        _mockScenarioEngine = new Mock<IWhatIfScenarioEngine>();
        _mockGrokSupercomputer = new Mock<IGrokSupercomputer>();
        _mockEnterpriseRepository = new Mock<IEnterpriseRepository>();
        _mockDispatcherHelper = new Mock<IDispatcherHelper>();
        _mockLogger = new Mock<ILogger<AIAssistViewModel>>();

        // Setup default mock behaviors
        _mockAIService.Setup(ai => ai.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mock AI response");

        // Use a callback to create the ServiceChargeRecommendation to avoid Moq issues
        _mockChargeCalculator.Setup(calc => calc.CalculateRecommendedChargeAsync(It.IsAny<int>()))
            .Returns((int id) => Task.FromResult(new ServiceChargeRecommendation
            {
                EnterpriseId = id,
                EnterpriseName = "Test Enterprise",
                RecommendedRate = 150.00M,
                BreakEvenAnalysis = new BreakEvenAnalysis { BreakEvenRate = 140.00M },
                ReserveAllocation = 15.00M,
                CurrentRate = 125.00M,
                TotalMonthlyExpenses = 10000.00M,
                MonthlyRevenueAtRecommended = 15000.00M,
                MonthlySurplus = 5000.00M,
                CalculationDate = DateTime.Now,
                Assumptions = new List<string> { "Test assumption" }
            }));

        _mockScenarioEngine.Setup(engine => engine.GenerateComprehensiveScenarioAsync(It.IsAny<int>(), It.IsAny<ScenarioParameters>()))
            .ReturnsAsync(new ComprehensiveScenario
            {
                ScenarioName = "Test Scenario",
                TotalImpact = new TotalImpact
                {
                    TotalAnnualExpenseIncrease = 12000.00M,
                    TotalMonthlyExpenseIncrease = 1000.00M,
                    RequiredRateIncrease = 25.00M,
                    NewMonthlyRate = 150.00M,
                    NewMonthlyRevenue = 15000.00M,
                    NewMonthlyBalance = 14000.00M
                },
                Recommendations = new List<string> { "Increase rates gradually", "Build reserves" },
                RiskAssessment = new RiskAssessment
                {
                    RiskLevel = "Medium",
                    Concerns = new List<string> { "Cash flow impact", "Ratepayer acceptance" },
                    MitigationStrategies = new List<string> { "Phased implementation", "Communication plan" }
                },
                GeneratedDate = DateTime.Now
            });

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
    public void Constructor_WithNullAIService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AIAssistViewModel(null!, _mockChargeCalculator.Object, _mockScenarioEngine.Object, _mockGrokSupercomputer.Object, _mockEnterpriseRepository.Object, _mockDispatcherHelper.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullChargeCalculator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AIAssistViewModel(_mockAIService.Object, null!, _mockScenarioEngine.Object, _mockGrokSupercomputer.Object, _mockEnterpriseRepository.Object, _mockDispatcherHelper.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullScenarioEngine_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AIAssistViewModel(_mockAIService.Object, _mockChargeCalculator.Object, null!, _mockGrokSupercomputer.Object, _mockEnterpriseRepository.Object, _mockDispatcherHelper.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithValidDependencies_InitializesProperties()
    {
        // Assert
        Assert.NotNull(_viewModel.ChatMessages);
        Assert.IsType<ObservableCollection<ChatMessage>>(_viewModel.ChatMessages);
        Assert.Empty(_viewModel.CurrentMessage);
        Assert.False(_viewModel.IsTyping);
        Assert.NotNull(_viewModel.AvailableModes);
        Assert.Equal(4, _viewModel.AvailableModes.Count);
        Assert.Equal("General Assistant", _viewModel.AvailableModes[0].Name);
        Assert.Equal("🤖", _viewModel.AvailableModes[0].Icon);
        Assert.NotNull(_viewModel.SelectedMode);
        Assert.Equal("General Assistant", _viewModel.SelectedMode.Name);
        Assert.True(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.Equal(0, _viewModel.AnnualExpenses);
        Assert.Equal(10, _viewModel.TargetReservePercentage);
        Assert.Equal(0, _viewModel.PayRaisePercentage);
        Assert.Equal(0, _viewModel.BenefitsIncreasePercentage);
        Assert.Equal(0, _viewModel.EquipmentCost);
        Assert.Equal(15, _viewModel.ReserveAllocationPercentage);
        Assert.False(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public async Task SendMessage_WithEmptyMessage_DoesNothing()
    {
        // Arrange
        _viewModel.CurrentMessage = "";

        // Act
        await InvokePrivateMethod(_viewModel, "SendMessage");

        // Assert
        Assert.Empty(_viewModel.ChatMessages);
        Assert.Empty(_viewModel.CurrentMessage);
        _mockAIService.Verify(ai => ai.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithWhitespaceMessage_DoesNothing()
    {
        // Arrange
        _viewModel.CurrentMessage = "   ";

        // Act
        await InvokePrivateMethod(_viewModel, "SendMessage");

        // Assert
        Assert.Empty(_viewModel.ChatMessages);
        Assert.Empty(_viewModel.CurrentMessage);
        _mockAIService.Verify(ai => ai.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithValidMessage_InGeneralMode_AddsMessages()
    {
        // Arrange
        _viewModel.CurrentMessage = "Test message";
        SetConversationMode(_viewModel, "General");

        // Act
        await InvokePrivateMethod(_viewModel, "SendMessage");

        // Assert
        Assert.Equal(2, _viewModel.ChatMessages.Count);
        Assert.Equal("Test message", _viewModel.ChatMessages[0].Text);
        Assert.True(_viewModel.ChatMessages[0].IsUser);
        Assert.Equal("Mock AI response", _viewModel.ChatMessages[1].Text);
        Assert.False(_viewModel.ChatMessages[1].IsUser);
        Assert.Empty(_viewModel.CurrentMessage);
        Assert.False(_viewModel.IsTyping);
    }

    [Fact]
    public async Task SendMessage_WithAIServiceException_HandlesError()
    {
        // Arrange
        _viewModel.CurrentMessage = "Test message";
        _mockAIService.Setup(ai => ai.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("AI service error"));

        // Act
        await InvokePrivateMethod(_viewModel, "SendMessage");

        // Assert
        Assert.Equal(2, _viewModel.ChatMessages.Count);
        Assert.Equal("Test message", _viewModel.ChatMessages[0].Text);
        Assert.True(_viewModel.ChatMessages[0].IsUser);
        Assert.Contains("encountered an error", _viewModel.ChatMessages[1].Text);
        Assert.False(_viewModel.ChatMessages[1].IsUser);
        Assert.False(_viewModel.IsTyping);
    }

    [Fact]
    public async Task ClearChat_ClearsAllMessages()
    {
        // Arrange
        _viewModel.ChatMessages.Add(new ChatMessage { Text = "Test", IsUser = true, Timestamp = DateTime.Now });
        _viewModel.ChatMessages.Add(new ChatMessage { Text = "Response", IsUser = false, Timestamp = DateTime.Now });

        // Act
        await InvokePrivateMethod(_viewModel, "ClearChat");

        // Assert
        Assert.Empty(_viewModel.ChatMessages);
    }

    [Fact]
    public async Task ExportChat_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        await InvokePrivateMethod(_viewModel, "ExportChat");
    }

    [Fact]
    public async Task ConfigureAI_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        await InvokePrivateMethod(_viewModel, "ConfigureAI");
    }

    [Fact]
    public async Task CalculateServiceCharge_InServiceChargeMode_CalculatesAndAddsResponse()
    {
        // Arrange
        SetConversationMode(_viewModel, "ServiceCharge");

        // Act
        await InvokePrivateMethod(_viewModel, "CalculateServiceCharge");

        // Assert
        Assert.Single(_viewModel.ChatMessages);
        Assert.Contains("Service Charge Calculation Results", _viewModel.ChatMessages[0].Text);
        Assert.Contains("$150.00", _viewModel.ChatMessages[0].Text);
        Assert.Contains("$140.00", _viewModel.ChatMessages[0].Text);
        Assert.False(_viewModel.ChatMessages[0].IsUser);
        Assert.False(_viewModel.IsTyping);
    }

    [Fact]
    public async Task CalculateServiceCharge_InWrongMode_DoesNothing()
    {
        // Arrange
        SetConversationMode(_viewModel, "General");

        // Act
        await InvokePrivateMethod(_viewModel, "CalculateServiceCharge");

        // Assert
        Assert.Empty(_viewModel.ChatMessages);
        _mockChargeCalculator.Verify(calc => calc.CalculateRecommendedChargeAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CalculateServiceCharge_WithException_HandlesError()
    {
        // Arrange
        SetConversationMode(_viewModel, "ServiceCharge");
        _mockChargeCalculator.Setup(calc => calc.CalculateRecommendedChargeAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("Calculation error"));

        // Act
        await InvokePrivateMethod(_viewModel, "CalculateServiceCharge");

        // Assert
        Assert.Single(_viewModel.ChatMessages);
        Assert.Contains("Sorry, I encountered an error calculating the service charge", _viewModel.ChatMessages[0].Text);
        Assert.False(_viewModel.ChatMessages[0].IsUser);
        Assert.False(_viewModel.IsTyping);
    }

    [Fact]
    public async Task GenerateWhatIfScenario_InWhatIfMode_WithEmptyMessage_ShowsPrompt()
    {
        // Arrange
        SetConversationMode(_viewModel, "WhatIf");
        _viewModel.CurrentMessage = "";

        // Act
        await InvokePrivateMethod(_viewModel, "GenerateWhatIfScenario");

        // Assert
        Assert.Single(_viewModel.ChatMessages);
        Assert.Contains("describe your what-if scenario", _viewModel.ChatMessages[0].Text);
        Assert.False(_viewModel.ChatMessages[0].IsUser);
        Assert.False(_viewModel.IsTyping);
    }

    [Fact]
    public async Task GenerateWhatIfScenario_InWhatIfMode_WithValidScenario_GeneratesAnalysis()
    {
        // Arrange
        SetConversationMode(_viewModel, "WhatIf");
        _viewModel.CurrentMessage = "15% pay raise, benefits improvement, 10% reserve";

        // Act
        await InvokePrivateMethod(_viewModel, "GenerateWhatIfScenario");

        // Assert
        Assert.Equal(2, _viewModel.ChatMessages.Count);
        Assert.Equal("15% pay raise, benefits improvement, 10% reserve", _viewModel.ChatMessages[0].Text);
        Assert.True(_viewModel.ChatMessages[0].IsUser);
        Assert.Contains("What-If Scenario Analysis", _viewModel.ChatMessages[1].Text);
        Assert.Contains("$12,000.00", _viewModel.ChatMessages[1].Text);
        Assert.False(_viewModel.ChatMessages[1].IsUser);
        Assert.Empty(_viewModel.CurrentMessage);
        Assert.False(_viewModel.IsTyping);
    }

    [Fact]
    public async Task GenerateWhatIfScenario_InWrongMode_DoesNothing()
    {
        // Arrange
        SetConversationMode(_viewModel, "General");
        _viewModel.CurrentMessage = "Test scenario";

        // Act
        await InvokePrivateMethod(_viewModel, "GenerateWhatIfScenario");

        // Assert
        Assert.Empty(_viewModel.ChatMessages);
        _mockScenarioEngine.Verify(engine => engine.GenerateComprehensiveScenarioAsync(It.IsAny<int>(), It.IsAny<ScenarioParameters>()), Times.Never);
    }

    [Fact]
    public async Task GetProactiveAdvice_InProactiveMode_GeneratesInsights()
    {
        // Arrange
        SetConversationMode(_viewModel, "Proactive");

        // Act
        await InvokePrivateMethod(_viewModel, "GetProactiveAdvice");

        // Assert
        Assert.Single(_viewModel.ChatMessages);
        Assert.Contains("Proactive Insights", _viewModel.ChatMessages[0].Text);
        Assert.Contains("temporarily disabled", _viewModel.ChatMessages[0].Text);
        Assert.False(_viewModel.ChatMessages[0].IsUser);
        Assert.False(_viewModel.IsTyping);
    }

    [Fact]
    public async Task GetProactiveAdvice_InWrongMode_DoesNothing()
    {
        // Arrange
        SetConversationMode(_viewModel, "General");

        // Act
        await InvokePrivateMethod(_viewModel, "GetProactiveAdvice");

        // Assert
        Assert.Empty(_viewModel.ChatMessages);
    }

    [Fact]
    public void SetConversationMode_ToGeneral_SetsCorrectProperties()
    {
        // Act
        SetConversationMode(_viewModel, "General");

        // Assert
        Assert.True(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.Equal("General Assistant", _viewModel.SelectedMode.Name);
        Assert.False(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void SetConversationMode_ToServiceCharge_SetsCorrectProperties()
    {
        // Act
        SetConversationMode(_viewModel, "ServiceCharge");

        // Assert
        Assert.False(_viewModel.IsGeneralMode);
        Assert.True(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.Equal("Service Charge Calculator", _viewModel.SelectedMode.Name);
        Assert.True(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void SetConversationMode_ToWhatIf_SetsCorrectProperties()
    {
        // Act
        SetConversationMode(_viewModel, "WhatIf");

        // Assert
        Assert.False(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.True(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.Equal("What-If Planner", _viewModel.SelectedMode.Name);
        Assert.True(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void SetConversationMode_ToProactive_SetsCorrectProperties()
    {
        // Act
        SetConversationMode(_viewModel, "Proactive");

        // Assert
        Assert.False(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.True(_viewModel.IsProactiveMode);
        Assert.Equal("Proactive Advisor", _viewModel.SelectedMode.Name);
        Assert.True(_viewModel.ShowFinancialInputs);
    }

    [Fact]
    public void SetConversationMode_WithInvalidMode_DefaultsToGeneral()
    {
        // Act
        SetConversationMode(_viewModel, "InvalidMode");

        // Assert
        Assert.True(_viewModel.IsGeneralMode);
        Assert.False(_viewModel.IsServiceChargeMode);
        Assert.False(_viewModel.IsWhatIfMode);
        Assert.False(_viewModel.IsProactiveMode);
        Assert.Equal("General Assistant", _viewModel.SelectedMode.Name);
        Assert.False(_viewModel.ShowFinancialInputs);
    }

    private static async Task InvokePrivateMethod(object obj, string methodName)
    {
        var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Method {methodName} not found");

        var result = method.Invoke(obj, null);
        if (result is Task task)
            await task;
    }

    private static void SetConversationMode(AIAssistViewModel viewModel, string mode)
    {
        var method = viewModel.GetType().GetMethod("SetConversationMode", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException("SetConversationMode method not found");

        method.Invoke(viewModel, new object[] { mode });
    }

    [Fact]
    public void ParseScenarioParameters_WithPayRaise_ParsesCorrectly()
    {
        // Arrange
        var scenario = "15% pay raise for employees";

        // Act
        var parameters = _viewModel.GetType().GetMethod("ParseScenarioParameters",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_viewModel, new object[] { scenario }) as ScenarioParameters;

        // Assert
        Assert.NotNull(parameters);
        Assert.Equal(15, parameters.PayRaisePercentage);
    }

    [Fact]
    public void ParseScenarioParameters_WithReserve_ParsesCorrectly()
    {
        // Arrange
        var scenario = "Build 20% reserve fund";

        // Act
        var parameters = _viewModel.GetType().GetMethod("ParseScenarioParameters",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_viewModel, new object[] { scenario }) as ScenarioParameters;

        // Assert
        Assert.NotNull(parameters);
        Assert.Equal(20, parameters.ReservePercentage);
    }

    [Fact]
    public void ParseScenarioParameters_WithEquipment_ParsesCorrectly()
    {
        // Arrange
        var scenario = "Equipment purchase for $50,000";

        // Act
        var parameters = _viewModel.GetType().GetMethod("ParseScenarioParameters",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_viewModel, new object[] { scenario }) as ScenarioParameters;

        // Assert
        Assert.NotNull(parameters);
        Assert.Equal(50000, parameters.EquipmentPurchaseAmount);
    }
}
