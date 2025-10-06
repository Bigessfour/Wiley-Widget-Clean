using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Serilog.Events;
using WileyWidget.Services;
using WileyWidget.Models;
using System.Text.RegularExpressions;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using Syncfusion.UI.Xaml.Chat;
using WileyWidget.Data;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for AI Assistant functionality
/// </summary>
public partial class AIAssistViewModel : AsyncViewModelBase
{
    private readonly IAIService _aiService;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly List<Enterprise> _enterpriseAnalyticsCache = new();

    /// <summary>
    /// Messages collection for SfAIAssistView
    /// </summary>
    public ObservableCollection<object> Messages { get; }

    /// <summary>
    /// Current user for SfAIAssistView
    /// </summary>
    public Author CurrentUser { get; } = new Author { Name = "You" };

    /// <summary>
    /// Legacy property for backward compatibility - use Messages instead
    /// </summary>
    public ThreadSafeObservableCollection<ChatMessage> ChatMessages { get; }

    [ObservableProperty]
    private string currentMessage = string.Empty;

    [ObservableProperty]
    private bool isTyping = false;

    [ObservableProperty]
    private int? enterpriseIdForAnalysis;

    /// <summary>
    /// Available conversation modes
    /// </summary>
    public List<ConversationMode> AvailableModes { get; } = new()
    {
        new ConversationMode { Name = "General Assistant", Description = "General questions and analysis", Icon = "🤖" },
        new ConversationMode { Name = "Service Charge Calculator", Description = "Calculate recommended service charges", Icon = "💰" },
        new ConversationMode { Name = "What-If Planner", Description = "Plan financial scenarios and upgrades", Icon = "🔮" },
        new ConversationMode { Name = "Proactive Advisor", Description = "Anticipate needs and provide insights", Icon = "🎯" }
    };

    /// <summary>
    /// Currently selected conversation mode
    /// </summary>
    [ObservableProperty]
    private ConversationMode? selectedMode;

    // Conversation mode properties
    [ObservableProperty]
    private bool isGeneralMode = true;

    [ObservableProperty]
    private bool isServiceChargeMode = false;

    [ObservableProperty]
    private bool isWhatIfMode = false;

    [ObservableProperty]
    private bool isProactiveMode = false;

    // Financial input properties
    [ObservableProperty]
    private decimal annualExpenses;

    [ObservableProperty]
    private decimal targetReservePercentage = 10;

    [ObservableProperty]
    private decimal payRaisePercentage;

    [ObservableProperty]
    private decimal benefitsIncreasePercentage;

    [ObservableProperty]
    private decimal equipmentCost;

    [ObservableProperty]
    private decimal reserveAllocationPercentage = 15;

    // UI visibility properties
    [ObservableProperty]
    private bool showFinancialInputs;

    /// <summary>
    /// Service charge calculator service
    /// </summary>
    private readonly IChargeCalculatorService _chargeCalculator;

    /// <summary>
    /// What-if scenario engine
    /// </summary>
    private readonly IWhatIfScenarioEngine _scenarioEngine;

    /// <summary>
    /// Grok Supercomputer for advanced calculations
    /// </summary>
    private readonly IGrokSupercomputer _grokSupercomputer;

    /// <summary>
    /// Constructor with AI service dependency
    /// </summary>
    public AIAssistViewModel(
        IAIService aiService,
        IChargeCalculatorService chargeCalculator,
        IWhatIfScenarioEngine scenarioEngine,
    IGrokSupercomputer grokSupercomputer,
        IEnterpriseRepository enterpriseRepository,
        IDispatcherHelper dispatcherHelper,
        Microsoft.Extensions.Logging.ILogger logger)
        : base(dispatcherHelper, logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _chargeCalculator = chargeCalculator ?? throw new ArgumentNullException(nameof(chargeCalculator));
        _scenarioEngine = scenarioEngine ?? throw new ArgumentNullException(nameof(scenarioEngine));
        _grokSupercomputer = grokSupercomputer ?? throw new ArgumentNullException(nameof(grokSupercomputer));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));

        Messages = new ObservableCollection<object>();
        ChatMessages = new ThreadSafeObservableCollection<ChatMessage>();

        // Set default mode to General Assistant
        SetConversationMode("General");
    }

    /// <summary>
    /// Cached snapshot of enterprises available for Grok analytics.
    /// </summary>
    public IReadOnlyList<Enterprise> EnterpriseAnalyticsCache => _enterpriseAnalyticsCache;

    /// <summary>
    /// Send message command
    /// </summary>
    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage))
        {
            CurrentMessage = string.Empty;
            return;
        }

        var userMessage = CurrentMessage.Trim();
        CurrentMessage = string.Empty;

        // Add user message to both collections for compatibility
        var userChatMessage = ChatMessage.CreateUserMessage(userMessage);
        Messages.Add(userChatMessage);
        await ChatMessages.AddAsync(userChatMessage);

        // Show typing indicator
        IsTyping = true;

        try
        {
            // Get AI response
            var aiResponse = await _aiService.GetInsightsAsync(
                "Wiley Widget Municipal Utility Management Application",
                userMessage
            );

            // Add AI response to both collections
            var aiChatMessage = ChatMessage.CreateAIMessage(aiResponse);
            Messages.Add(aiChatMessage);
            await ChatMessages.AddAsync(aiChatMessage);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating AI response");

            // Use error reporting service for structured error handling and UI feedback
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            Services.ErrorReportingService.Instance.ReportError(
                ex,
                "AI_Response_Generation",
                showToUser: true,
                level: LogEventLevel.Error,
                correlationId: correlationId);

            var errorMessage = ChatMessage.CreateAIMessage($"Sorry, I encountered an error processing your request. Please try again.\n\nReference ID: {correlationId}");
            Messages.Add(errorMessage);
            await ChatMessages.AddAsync(errorMessage);
        }
        finally
        {
            IsTyping = false;
        }
    }

    /// <summary>
    /// Clear chat command
    /// </summary>
    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        ChatMessages.Clear();
        Log.Information("Chat history cleared");
    }

    /// <summary>
    /// Export chat command
    /// </summary>
    [RelayCommand]
    private void ExportChat()
    {
        // Placeholder for chat export functionality
        Log.Information("Chat export requested");
    }

    /// <summary>
    /// Configure AI command
    /// </summary>
    [RelayCommand]
    private void ConfigureAI()
    {
        // Placeholder for AI configuration
        Log.Information("AI configuration requested");
    }

    /// <summary>
    /// Calculate service charge command
    /// </summary>
    [RelayCommand]
    private async Task CalculateServiceCharge()
    {
        if (SelectedMode?.Name != "Service Charge Calculator")
            return;

        IsTyping = true;

        try
        {
            // Get enterprise data for calculation
            var enterprise = await GetCurrentEnterpriseAsync();
            if (enterprise == null)
            {
                var errorMessage = ChatMessage.CreateAIMessage("Unable to calculate service charges: No enterprise data available.");
                Messages.Add(errorMessage);
                await ChatMessages.AddAsync(errorMessage);
                return;
            }

            var result = await _chargeCalculator.CalculateRecommendedChargeAsync(enterprise.Id);

            var response = $"**Service Charge Calculation Results:**\n\n" +
                          $"**Recommended Monthly Charge:** ${result.RecommendedRate:F2}\n" +
                          $"**Break-even Analysis:** ${result.BreakEvenAnalysis.BreakEvenRate:F2}\n" +
                          $"**Reserve Allocation:** ${result.ReserveAllocation:F2}\n\n" +
                          $"**Current Rate:** ${result.CurrentRate:F2}\n" +
                          $"**Total Monthly Expenses:** ${result.TotalMonthlyExpenses:F2}\n" +
                          $"**Monthly Revenue at Recommended:** ${result.MonthlyRevenueAtRecommended:F2}\n" +
                          $"**Monthly Surplus:** ${result.MonthlySurplus:F2}";

            // Use GrokSupercomputer for advanced financial analysis
            var financialAnalysis = await _grokSupercomputer.CalculateFinancialAsync(
                result.TotalMonthlyExpenses,
                result.RecommendedRate / result.TotalMonthlyExpenses, // rate as percentage
                12, // annual periods
                "service_charge_analysis"
            );

            if (financialAnalysis.IsSuccessful)
            {
                response += $"\n\n**AI-Powered Analysis:**\n{financialAnalysis.Result}";
            }

            var successMessage = ChatMessage.CreateAIMessage(response);
            Messages.Add(successMessage);
            await ChatMessages.AddAsync(successMessage);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating service charge");

            // Use error reporting service for structured error handling and UI feedback
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            Services.ErrorReportingService.Instance.ReportError(
                ex,
                "Service_Charge_Calculation",
                showToUser: true,
                level: LogEventLevel.Error,
                correlationId: correlationId);

            var errorMessage = ChatMessage.CreateAIMessage($"Sorry, I encountered an error calculating the service charge. Please try again.\n\nReference ID: {correlationId}");
            Messages.Add(errorMessage);
            await ChatMessages.AddAsync(errorMessage);
        }
        finally
        {
            IsTyping = false;
        }
    }

    /// <summary>
    /// Generate what-if scenario command
    /// </summary>
    [RelayCommand]
    private async Task GenerateWhatIfScenario()
    {
        if (SelectedMode?.Name != "What-If Planner")
            return;

        if (string.IsNullOrWhiteSpace(CurrentMessage))
        {
            await ChatMessages.AddAsync(new ChatMessage
            {
                Text = "Please describe your what-if scenario (e.g., '15% pay raise, benefits improvement, 10% reserve, equipment purchase').",
                IsUser = false,
                Timestamp = DateTime.Now
            });
            return;
        }

        var scenario = CurrentMessage.Trim();
        CurrentMessage = string.Empty;

        // Add user scenario
        await ChatMessages.AddAsync(new ChatMessage
        {
            Text = scenario,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        IsTyping = true;

        try
        {
            var enterprise = await GetCurrentEnterpriseAsync();
            if (enterprise == null)
            {
                await ChatMessages.AddAsync(new ChatMessage
                {
                    Text = "Unable to generate scenario: No enterprise data available.",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
                return;
            }

            var parameters = ParseScenarioParameters(scenario);
            var result = await _scenarioEngine.GenerateComprehensiveScenarioAsync(enterprise.Id, parameters);

            var response = $"**What-If Scenario Analysis:**\n\n" +
                          $"**Scenario:** {result.ScenarioName}\n\n" +
                          $"**Total Impact:**\n" +
                          $"- Annual Expense Increase: ${result.TotalImpact.TotalAnnualExpenseIncrease:N2}\n" +
                          $"- Monthly Expense Increase: ${result.TotalImpact.TotalMonthlyExpenseIncrease:N2}\n" +
                          $"- Required Rate Increase: ${result.TotalImpact.RequiredRateIncrease:N2}\n" +
                          $"- New Monthly Rate: ${result.TotalImpact.NewMonthlyRate:N2}\n\n" +
                          $"**Recommendations:**\n{string.Join("\n", result.Recommendations)}\n\n" +
                          $"**Risk Assessment:** {result.RiskAssessment.RiskLevel}\n" +
                          $"**Concerns:** {string.Join(", ", result.RiskAssessment.Concerns)}";

            // Use GrokSupercomputer for optimization analysis
            var optimizationConstraints = new[]
            {
                $"Annual expenses cannot exceed ${(enterprise.MonthlyExpenses * 12 * 1.5M):N0}",
                $"Rate increase must be between 0% and 25%",
                $"Reserve allocation must be at least 10% of annual expenses",
                $"Monthly surplus must be positive"
            };

            var optimization = await _grokSupercomputer.OptimizeAsync(
                "Minimize rate increase while maintaining financial stability",
                optimizationConstraints,
                $"Current scenario: {scenario} with impacts: Annual expense increase ${result.TotalImpact.TotalAnnualExpenseIncrease:N2}, Required rate increase {result.TotalImpact.RequiredRateIncrease:P2}"
            );

            if (optimization.IsSuccessful)
            {
                response += $"\n\n**AI Optimization Analysis:**\n{optimization.Result}";
            }

            await ChatMessages.AddAsync(new ChatMessage
            {
                Text = response,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating what-if scenario");

            // Use error reporting service for structured error handling and UI feedback
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            Services.ErrorReportingService.Instance.ReportError(
                ex,
                "WhatIf_Scenario_Generation",
                showToUser: true,
                level: LogEventLevel.Error,
                correlationId: correlationId);

            await ChatMessages.AddAsync(new ChatMessage
            {
                Text = $"Sorry, I encountered an error generating the scenario analysis. Please try again.\n\nReference ID: {correlationId}",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
        }
    }

    /// <summary>
    /// Get proactive advice command
    /// </summary>
    [RelayCommand]
    private async Task GetProactiveAdvice()
    {
        if (SelectedMode?.Name != "Proactive Advisor")
            return;

        IsTyping = true;

        try
        {
            await Task.CompletedTask; // Suppress async warning
            var recentActivity = GetRecentActivitySummary();
            var userProfile = GetUserProfileSummary();

            // Temporarily disabled due to GrokAIService compilation issues
            // var insights = await (_aiService as dynamic)?.GenerateAnticipatoryInsightsAsync(recentActivity, userProfile);
            var insights = new LocalAnticipatoryInsights
            {
                RecentActivity = recentActivity,
                Insights = "Proactive insights temporarily disabled due to service compilation issues.",
                GeneratedDate = DateTime.Now,
                SuggestedActions = new List<string> { "Check service status", "Review recent activity" }
            };

            // Insights is always created above, so no null check needed
            var response = $"**Proactive Insights & Recommendations:**\n\n" +
                          $"**Insights:** {insights.Insights}\n\n" +
                          $"**Suggested Actions:**\n{string.Join("\n", insights.SuggestedActions)}\n\n" +
                          $"*Generated on {insights.GeneratedDate:g}*";

            await ChatMessages.AddAsync(new ChatMessage
            {
                Text = response,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating proactive advice");

            // Use error reporting service for structured error handling and UI feedback
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            Services.ErrorReportingService.Instance.ReportError(
                ex,
                "Proactive_Advice_Generation",
                showToUser: true,
                level: LogEventLevel.Error,
                correlationId: correlationId);

            await ChatMessages.AddAsync(new ChatMessage
            {
                Text = $"Sorry, I encountered an error generating proactive advice. Please try again.\n\nReference ID: {correlationId}",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
        }
    }

    /// <summary>
    /// Resolve the enterprise that Grok should analyze using repository-backed data.
    /// </summary>
    private async Task<Enterprise?> GetCurrentEnterpriseAsync()
    {
        try
        {
            var enterprises = await _enterpriseRepository.GetAllAsync().ConfigureAwait(false);
            var enterpriseList = enterprises?.ToList() ?? new List<Enterprise>();

            _enterpriseAnalyticsCache.Clear();
            _enterpriseAnalyticsCache.AddRange(enterpriseList);

            if (EnterpriseIdForAnalysis is int targetId)
            {
                var requestedEnterprise = _enterpriseAnalyticsCache.FirstOrDefault(e => e.Id == targetId);
                if (requestedEnterprise != null)
                {
                    Logger.LogDebug("Using explicitly requested enterprise {EnterpriseName} (ID {EnterpriseId}) for AI analysis.", requestedEnterprise.Name, requestedEnterprise.Id);
                    return requestedEnterprise;
                }

                Logger.LogWarning("Requested enterprise with ID {EnterpriseId} was not found. Falling back to latest enterprise data for analysis.", targetId);
            }

            var enterpriseForAnalysis = _enterpriseAnalyticsCache
                .OrderByDescending(e => e.LastModified ?? DateTime.MinValue)
                .ThenByDescending(e => e.MonthlyRevenue)
                .ThenByDescending(e => e.Id)
                .FirstOrDefault();

            if (enterpriseForAnalysis == null)
            {
                Logger.LogWarning("No enterprise data available for AI analysis.");
            }
            else
            {
                Logger.LogDebug("Selected enterprise {EnterpriseName} (ID {EnterpriseId}) for AI analysis.", enterpriseForAnalysis.Name, enterpriseForAnalysis.Id);
            }

            return enterpriseForAnalysis;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to retrieve enterprise data for AI analysis.");
            throw;
        }
    }

    /// <summary>
    /// Get recent activity summary
    /// </summary>
    private string GetRecentActivitySummary()
    {
        // Summarize recent chat messages and user actions
        var recentMessages = ChatMessages.TakeLast(5).Select(m => m.Text);
        return string.Join("; ", recentMessages);
    }

    /// <summary>
    /// Get user profile summary
    /// </summary>
    private string GetUserProfileSummary()
    {
        // This should summarize user preferences, role, and context
        return "Municipal utility manager focused on financial planning and service optimization";
    }

    /// <summary>
    /// Parse scenario string into parameters
    /// </summary>
    private ScenarioParameters ParseScenarioParameters(string scenario)
    {
        var parameters = new ScenarioParameters();

        // Parse pay raise percentage
        var payRaiseMatch = Regex.Match(scenario, @"(\d+(?:\.\d+)?)%?\s*pay\s*raise", RegexOptions.IgnoreCase);
        if (payRaiseMatch.Success)
        {
            parameters.PayRaisePercentage = decimal.Parse(payRaiseMatch.Groups[1].Value);
        }

        // Parse benefits increase
        var benefitsMatch = Regex.Match(scenario, @"benefits?\s*improvement|\$\s*(\d+(?:,\d+)*(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (benefitsMatch.Success && benefitsMatch.Groups[1].Success)
        {
            parameters.BenefitsIncreaseAmount = decimal.Parse(benefitsMatch.Groups[1].Value.Replace(",", ""));
        }

        // Parse reserve percentage
        var reserveMatch = Regex.Match(scenario, @"(\d+(?:\.\d+)?)%?\s*reserve", RegexOptions.IgnoreCase);
        if (reserveMatch.Success)
        {
            parameters.ReservePercentage = decimal.Parse(reserveMatch.Groups[1].Value);
        }

        // Parse equipment purchase
        var equipmentMatch = Regex.Match(scenario, @"(?:equipment\s*purchase(?:\s*for)?\s*)?\$\s*(\d+(?:,\d+)*(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (equipmentMatch.Success && equipmentMatch.Groups[1].Success)
        {
            parameters.EquipmentPurchaseAmount = decimal.Parse(equipmentMatch.Groups[1].Value.Replace(",", ""));
        }

        return parameters;
    }

    /// <summary>
    /// Set conversation mode command
    /// </summary>
    [RelayCommand]
    private void SetConversationMode(string mode)
    {
        // Reset all modes
        IsGeneralMode = false;
        IsServiceChargeMode = false;
        IsWhatIfMode = false;
        IsProactiveMode = false;

        // Set selected mode
        switch (mode?.ToLowerInvariant())
        {
            case "general":
                IsGeneralMode = true;
                SelectedMode = AvailableModes[0];
                ShowFinancialInputs = false;
                break;
            case "servicecharge":
                IsServiceChargeMode = true;
                SelectedMode = AvailableModes[1];
                ShowFinancialInputs = true;
                break;
            case "whatif":
                IsWhatIfMode = true;
                SelectedMode = AvailableModes[2];
                ShowFinancialInputs = true;
                break;
            case "proactive":
                IsProactiveMode = true;
                SelectedMode = AvailableModes[3];
                ShowFinancialInputs = true;
                break;
            default:
                IsGeneralMode = true;
                SelectedMode = AvailableModes[0];
                ShowFinancialInputs = false;
                break;
        }

        Log.Information("Conversation mode changed to: {Mode}", SelectedMode?.Name ?? "Unknown");
    }
}

/// <summary>
/// <summary>
/// Local anticipatory insights for temporary use
/// </summary>
public class LocalAnticipatoryInsights
{
    public string RecentActivity { get; set; } = string.Empty;
    public string Insights { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; }
    public List<string> SuggestedActions { get; set; } = new();
}