using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using System.Text.RegularExpressions;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Syncfusion.UI.Xaml.Chat;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for AI Assistant functionality
/// </summary>
public partial class AIAssistViewModel : ObservableObject
{
    private readonly IAIService _aiService;

    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

    [ObservableProperty]
    private string messageText = string.Empty;

    [ObservableProperty]
    private bool isTyping = false;

    /// <summary>
    /// Available conversation modes
    /// </summary>
    public List<ConversationMode> AvailableModes { get; } = new()
    {
        new ConversationMode { Name = "General", Description = "General questions and analysis", Icon = "🤖" },
        new ConversationMode { Name = "WhatIf", Description = "Plan financial scenarios and upgrades", Icon = "🔮" },
        new ConversationMode { Name = "Advisory", Description = "Anticipate needs and provide insights", Icon = "🎯" }
    };

    /// <summary>
    /// Currently selected conversation mode
    /// </summary>
    [ObservableProperty]
    private ConversationMode selectedMode;

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
    /// Loading state for UI feedback
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Processing state for busy indicator
    /// </summary>
    [ObservableProperty]
    private bool isProcessing;

    /// <summary>
    /// Status message for user feedback
    /// </summary>
    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>
    /// Current user for chat interface
    /// </summary>
    public Author CurrentUser { get; } = new Author { Name = "You" };

    /// <summary>
    /// Messages collection for SfAIAssistView binding (alias for ChatMessages)
    /// </summary>
    public ObservableCollection<ChatMessage> Messages => ChatMessages;

    /// <summary>
    /// Conversation history for combo box
    /// </summary>
    public ObservableCollection<string> ConversationHistory { get; } = new() { "Budget Analysis - Q1", "Rate Increase Scenario", "Reserve Fund Planning" };

    /// <summary>
    /// Service charge calculator service
    /// </summary>
    private readonly IChargeCalculatorService _chargeCalculator;

    /// <summary>
    /// What-if scenario engine
    /// </summary>
    private readonly IWhatIfScenarioEngine _scenarioEngine;

    /// <summary>
    /// Constructor with AI service dependency
    /// </summary>
    public AIAssistViewModel(IAIService aiService, IChargeCalculatorService chargeCalculator, IWhatIfScenarioEngine scenarioEngine, IGrokSupercomputer grokSupercomputer, IEnterpriseRepository enterpriseRepository, IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<AIAssistViewModel> logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _chargeCalculator = chargeCalculator ?? throw new ArgumentNullException(nameof(chargeCalculator));
        _scenarioEngine = scenarioEngine ?? throw new ArgumentNullException(nameof(scenarioEngine));

        // Set default mode to General Assistant
        SetConversationMode("General");
    }

    /// <summary>
    /// Send message command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageText))
        {
            MessageText = string.Empty;
            return;
        }

        var userMessage = MessageText.Trim();
        MessageText = string.Empty;

        // Add user message
        ChatMessages.Add(new ChatMessage
        {
            Text = userMessage,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        // Show typing indicator and processing
        IsTyping = true;
        IsProcessing = true;

        try
        {
            // Get AI response
            var aiResponse = await _aiService.GetInsightsAsync(
                "Wiley Widget Municipal Utility Management Application",
                userMessage
            );

            // Add AI response
            ChatMessages.Add(new ChatMessage
            {
                Text = aiResponse,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating AI response");

            ChatMessages.Add(new ChatMessage
            {
                Text = "Sorry, I encountered an error processing your request. Please try again.",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
            IsProcessing = false;
        }
    }

    private bool CanSendMessage() => !string.IsNullOrWhiteSpace(MessageText);

    /// <summary>
    /// Clear chat command
    /// </summary>
    [RelayCommand]
    private void ClearChat()
    {
        ChatMessages.Clear();
        Log.Information("Chat history cleared");
    }

    /// <summary>
    /// Export chat command
    /// </summary>
    [RelayCommand]
    private async Task ExportChat()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Chat History",
                Filter = "Text Files (*.txt)|*.txt|Markdown Files (*.md)|*.md",
                DefaultExt = ".txt",
                FileName = $"AI_Chat_History_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var content = new System.Text.StringBuilder();
                content.AppendLine("# AI Assistant Chat History");
                content.AppendLine($"Exported on: {DateTime.Now:g}");
                content.AppendLine();

                foreach (var message in ChatMessages)
                {
                    var sender = message.IsUser ? "You" : "AI Assistant";
                    content.AppendLine($"**{sender}** ({message.Timestamp:g}):");
                    content.AppendLine(message.Text);
                    content.AppendLine();
                }

                await System.IO.File.WriteAllTextAsync(dialog.FileName, content.ToString());
                Log.Information("Chat history exported to {FileName}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting chat history");
        }
    }

    /// <summary>
    /// Configure AI command
    /// </summary>
    [RelayCommand]
    private void ConfigureAI()
    {
        try
        {
            // Open the settings dialog with XAI tab selected
            var settingsWindow = new WileyWidget.SettingsView();
            // Select the XAI Integration tab (index 3)
            if (settingsWindow.FindName("SettingsTabControl") is System.Windows.Controls.TabControl tabControl)
            {
                tabControl.SelectedIndex = 3; // XAI Integration tab
            }
            settingsWindow.ShowDialog();

            Log.Information("AI configuration dialog opened");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening AI configuration");
        }
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
                ChatMessages.Add(new ChatMessage
                {
                    Text = "Unable to calculate service charges: No enterprise data available.",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
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

            ChatMessages.Add(new ChatMessage
            {
                Text = response,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating service charge");

            ChatMessages.Add(new ChatMessage
            {
                Text = "Sorry, I encountered an error calculating the service charge. Please try again.",
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
    /// Generate what-if scenario command
    /// </summary>
    [RelayCommand]
    private async Task GenerateWhatIfScenario()
    {
        if (SelectedMode?.Name != "What-If Planner")
            return;

        if (string.IsNullOrWhiteSpace(MessageText))
        {
            ChatMessages.Add(new ChatMessage
            {
                Text = "Please describe your what-if scenario (e.g., '15% pay raise, benefits improvement, 10% reserve, equipment purchase').",
                IsUser = false,
                Timestamp = DateTime.Now
            });
            return;
        }

        var scenario = MessageText.Trim();
        MessageText = string.Empty;

        // Add user scenario
        ChatMessages.Add(new ChatMessage
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
                ChatMessages.Add(new ChatMessage
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

            ChatMessages.Add(new ChatMessage
            {
                Text = response,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating what-if scenario");

            ChatMessages.Add(new ChatMessage
            {
                Text = "Sorry, I encountered an error generating the scenario analysis. Please try again.",
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

            ChatMessages.Add(new ChatMessage
            {
                Text = response,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating proactive advice");

            ChatMessages.Add(new ChatMessage
            {
                Text = "Sorry, I encountered an error generating proactive advice. Please try again.",
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
    /// Get current enterprise (placeholder - implement based on your data access)
    /// </summary>
    private async Task<Enterprise> GetCurrentEnterpriseAsync()
    {
        // This should be implemented to get the current enterprise from your data context
        // For testing purposes, return a mock enterprise
        await Task.CompletedTask; // Suppress async warning
        return new Enterprise
        {
            Id = 1,
            Name = "Test Enterprise",
            CurrentRate = 125.00M,
            CitizenCount = 2500,
            MonthlyExpenses = 350000.00M
        };
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
}