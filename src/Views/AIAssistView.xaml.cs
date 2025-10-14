using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using WileyWidget.Data;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using BusinessInterfaces = WileyWidget.Business.Interfaces;
using System.ComponentModel;

namespace WileyWidget;

/// <summary>
/// AI Assistant UserControl providing xAI integration through custom chat interface
/// </summary>
public partial class AIAssistView : UserControl
{
    private readonly IServiceScope _viewScope;
    public AIAssistView()
    {
        InitializeComponent();

        // Create a scope for the view to resolve scoped services
        IServiceProvider? provider = null;
        try
        {
            provider = App.GetActiveServiceProvider();
        }
        catch (InvalidOperationException)
        {
            provider = Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
        }

        if (provider == null)
            throw new InvalidOperationException("ServiceProvider is not available for AIAssistView");

        _viewScope = provider.CreateScope();
        // Resolve scoped services from the scope
        var aiService = _viewScope.ServiceProvider.GetRequiredService<IAIService>();
        var chargeCalculator = _viewScope.ServiceProvider.GetRequiredService<IChargeCalculatorService>();
        var whatIfEngine = _viewScope.ServiceProvider.GetRequiredService<IWhatIfScenarioEngine>();
    var grokSupercomputer = _viewScope.ServiceProvider.GetRequiredService<IGrokSupercomputer>();
        var enterpriseRepository = _viewScope.ServiceProvider.GetRequiredService<BusinessInterfaces.IEnterpriseRepository>();
        var dispatcherHelper = _viewScope.ServiceProvider.GetRequiredService<WileyWidget.Services.Threading.IDispatcherHelper>();
        var logger = _viewScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ViewModels.AIAssistViewModel>>();
        DataContext = new ViewModels.AIAssistViewModel(aiService, chargeCalculator, whatIfEngine, grokSupercomputer, enterpriseRepository, dispatcherHelper, logger);

        // Subscribe to ViewModel property changes for auto-scroll
        if (DataContext is ViewModels.AIAssistViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        Log.Information("AI Assist View initialized");
    }

    private void AIAssistView_Loaded(object sender, RoutedEventArgs e)
    {
        // Focus input on load
        MessageInput?.Focus();
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
            ViewModel.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handle ViewModel property changes for auto-scroll behavior
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Auto-scroll to bottom when Responses collection changes
        if (e.PropertyName == nameof(ViewModels.AIAssistViewModel.Responses))
        {
            // Scroll to bottom after a brief delay to allow rendering
            Dispatcher.InvokeAsync(() =>
            {
                var scrollViewer = FindName("ChatScrollViewer") as System.Windows.Controls.ScrollViewer;
                scrollViewer?.ScrollToBottom();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }









    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        // For UserControl, theme is applied at application level or parent level
        // SfSkinManager can be used on the parent Window
    }
}