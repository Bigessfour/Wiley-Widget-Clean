using System.Windows;
using System.Windows.Input;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using WileyWidget.Data;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget;

/// <summary>
/// AI Assistant window providing xAI integration through custom chat interface
/// </summary>
public partial class AIAssistView : Window
{
    private readonly IServiceScope _viewScope;
    public AIAssistView()
    {
        InitializeComponent();

        // Create a scope for the view to resolve scoped services
        // Prefer the statically-initialized App.ServiceProvider; fall back to Application.Current.Properties when available
        var provider = App.ServiceProvider ?? Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
        if (provider == null)
            throw new InvalidOperationException("ServiceProvider is not available for AIAssistView");

        _viewScope = provider.CreateScope();
        // Resolve scoped services from the scope
        var aiService = _viewScope.ServiceProvider.GetRequiredService<IAIService>();
        var chargeCalculator = _viewScope.ServiceProvider.GetRequiredService<IChargeCalculatorService>();
        var whatIfEngine = _viewScope.ServiceProvider.GetRequiredService<IWhatIfScenarioEngine>();
    var grokSupercomputer = _viewScope.ServiceProvider.GetRequiredService<IGrokSupercomputer>();
        var enterpriseRepository = _viewScope.ServiceProvider.GetRequiredService<IEnterpriseRepository>();
        var dispatcherHelper = _viewScope.ServiceProvider.GetRequiredService<WileyWidget.Services.Threading.IDispatcherHelper>();
        var logger = _viewScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ViewModels.AIAssistViewModel>>();
        DataContext = new ViewModels.AIAssistViewModel(aiService, chargeCalculator, whatIfEngine, grokSupercomputer, enterpriseRepository, dispatcherHelper, logger);

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        Log.Information("AI Assist View initialized");
    }

    protected override void OnClosed(System.EventArgs e)
    {
        try
        {
            _viewScope?.Dispose();
        }
        catch { /* ignore */ }
        base.OnClosed(e);
    }

    private ViewModels.AIAssistViewModel? ViewModel
    {
        get => DataContext as ViewModels.AIAssistViewModel;
    }

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
        Services.ThemeUtility.TryApplyTheme(this, themeName);
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