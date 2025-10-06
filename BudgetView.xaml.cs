using System.Windows;
using System.Windows.Media;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Serilog;

namespace WileyWidget;

/// <summary>
/// Budget Analysis and Reporting Window
/// Provides comprehensive financial analysis and reporting capabilities
/// </summary>
public partial class BudgetView : Window
{
    private readonly IServiceScope _viewScope;

    public BudgetView()
        : this(null)
    {
    }

    /// <summary>
    /// Constructor for testing - allows injection of repository
    /// </summary>
    /// <param name="enterpriseRepository">Repository to use, or null to use service provider</param>
    public BudgetView(IEnterpriseRepository enterpriseRepository)
    {
        InitializeComponent();

        // Apply current theme
        ThemeUtility.TryApplyTheme(this, SettingsService.Instance.Current.Theme);

        IEnterpriseRepository repository;
        if (enterpriseRepository != null)
        {
            // Use injected repository for testing
            repository = enterpriseRepository;
            _viewScope = null; // No scope needed for injected repository
        }
        else
        {
            // Create a scope for the view and resolve the repository from the scope
            var provider = App.ServiceProvider ?? Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
            if (provider != null)
            {
                _viewScope = provider.CreateScope();
                repository = _viewScope.ServiceProvider.GetRequiredService<IEnterpriseRepository>();
            }
            else
            {
                // For testing purposes, create a mock or null repository
                repository = null;
                _viewScope = null;
            }
        }

        DataContext = repository != null ? new BudgetViewModel(repository) : null;

        // Dispose the scope when the window is closed (only if we created it)
        if (_viewScope != null)
        {
            this.Closed += (_, _) => { try { _viewScope.Dispose(); } catch { } };
        }

        // Load budget data when window opens
        Loaded += async (s, e) =>
        {
            if (DataContext is BudgetViewModel vm)
            {
                await vm.RefreshBudgetDataAsync();
            }
        };
    }

    /// <summary>
    /// Show the Budget Analysis window
    /// </summary>
    public static void ShowBudgetWindow()
    {
        var window = new BudgetView();
        window.Show();
    }

    /// <summary>
    /// Show the Budget Analysis window as dialog
    /// </summary>
    public static bool? ShowBudgetDialog()
    {
        var window = new BudgetView();
        return window.ShowDialog();
    }
}