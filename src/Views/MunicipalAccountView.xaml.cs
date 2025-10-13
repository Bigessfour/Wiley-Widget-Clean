using System;
using System.Windows;
using WileyWidget.ViewModels;
using Serilog;

namespace WileyWidget.Views;

/// <summary>
/// Interaction logic for MunicipalAccountView.xaml
/// </summary>
public partial class MunicipalAccountView : Window
{
    private readonly MunicipalAccountViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the MunicipalAccountView
    /// </summary>
    public MunicipalAccountView()
    {
        InitializeComponent();
        
        try
        {
            // Get ViewModel from DI container or create new instance
            _viewModel = App.ServiceProvider?.GetService(typeof(MunicipalAccountViewModel)) as MunicipalAccountViewModel
                        ?? throw new InvalidOperationException("MunicipalAccountViewModel could not be resolved from DI container");
            
            DataContext = _viewModel;
            
            Log.Information("MunicipalAccountView initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize MunicipalAccountView");
            MessageBox.Show(
                $"Failed to initialize Municipal Account View: {ex.Message}",
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Initializes a new instance with dependency injection
    /// </summary>
    /// <param name="viewModel">The view model to use</param>
    public MunicipalAccountView(MunicipalAccountViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        
        Log.Information("MunicipalAccountView initialized with injected ViewModel");
    }

    /// <summary>
    /// Handle window loaded event
    /// </summary>
    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        try
        {
            // Initialize data asynchronously
            await _viewModel.InitializeAsync();
            Log.Information("MunicipalAccountView data initialized");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize MunicipalAccountView data");
            MessageBox.Show(
                $"Failed to load account data: {ex.Message}",
                "Data Loading Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Handle window closing event
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        
        try
        {
            Log.Information("MunicipalAccountView closing");
            
            // Cleanup if needed
            if (_viewModel != null)
            {
                // Any cleanup operations here
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during MunicipalAccountView cleanup");
        }
    }
}
