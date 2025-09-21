using System.Windows;
using System.Windows.Input;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget;

/// <summary>
/// AI Assistant window providing xAI integration through custom chat interface
/// </summary>
public partial class AIAssistView : Window
{
    public AIAssistView()
    {
        InitializeComponent();

        // Get AI service from DI container
        var aiService = App.ServiceProvider.GetRequiredService<IAIService>();
        var chargeCalculator = App.ServiceProvider.GetRequiredService<Services.ServiceChargeCalculatorService>();
        var whatIfEngine = App.ServiceProvider.GetRequiredService<Services.WhatIfScenarioEngine>();
        DataContext = new ViewModels.AIAssistViewModel(aiService, chargeCalculator, whatIfEngine);

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        Log.Information("AI Assist View initialized");
    }

    private ViewModels.AIAssistViewModel ViewModel => DataContext as ViewModels.AIAssistViewModel;

    /// <summary>
    /// Handle Enter key in message input
    /// </summary>
    private void OnMessageInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel != null)
        {
            ViewModel.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }









    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        try
        {
            var canonical = NormalizeTheme(themeName);
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
            SfSkinManager.SetTheme(this, new Theme(canonical));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
        catch
        {
            if (themeName != "FluentLight")
            {
                // Fallback
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                try { SfSkinManager.SetTheme(this, new Theme("FluentLight")); } catch { /* ignore */ }
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }
    }

    private string NormalizeTheme(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "FluentDark";
        raw = raw.Replace(" ", string.Empty);
        return raw switch
        {
            "FluentDark" => "FluentDark",
            "FluentLight" => "FluentLight",
            _ => "FluentDark"
        };
    }

    /// <summary>
    /// Static method to show the AI Assist window (following existing pattern)
    /// </summary>
    public static void ShowAIAssistWindow()
    {
        try
        {
            var aiWindow = new AIAssistView();
            aiWindow.Show();
            Log.Information("AI Assist window opened successfully");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open AI Assistant: {ex.Message}",
                          "Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
            Log.Error(ex, "Failed to open AI Assist window");
        }
    }
}