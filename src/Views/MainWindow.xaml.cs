using System;
using System.Windows;
using Serilog;
using Syncfusion.Windows.Tools.Controls;
using Prism;
using WileyWidget.Views;

namespace WileyWidget.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Log.Debug("MainWindow: Constructor called");
            
            InitializeComponent();
            
            // Register views with regions
            var regionManager = (IRegionManager)App.ServiceProvider.GetService(typeof(IRegionManager));
            if (regionManager != null)
            {
                regionManager.RegisterViewWithRegion("DashboardRegion", typeof(DashboardView));
                regionManager.RegisterViewWithRegion("EnterpriseRegion", typeof(EnterpriseView));
                regionManager.RegisterViewWithRegion("BudgetRegion", typeof(BudgetView));
                regionManager.RegisterViewWithRegion("MunicipalAccountRegion", typeof(MunicipalAccountView));
                regionManager.RegisterViewWithRegion("UtilityCustomerRegion", typeof(UtilityCustomerView));
                regionManager.RegisterViewWithRegion("ReportsRegion", typeof(ReportsView));
                regionManager.RegisterViewWithRegion("AnalyticsRegion", typeof(AnalyticsView));
                // Panel regions can be registered as needed
            }
            
            Log.Debug("MainWindow: Prism regions registered");
            
            // Configure DockingManager after initialization
            ConfigureDockingManager();
            
            // Add event handlers for diagnostics
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            SizeChanged += MainWindow_SizeChanged;
            Activated += MainWindow_Activated;
            ContentRendered += MainWindow_ContentRendered;
            
            Log.Debug("MainWindow: Constructor completed, event handlers attached");
        }

        private void ConfigureDockingManager()
        {
            try
            {
                // Configure DockingManager properties as per Syncfusion documentation
                MainDockingManager.UseDocumentContainer = true;
                MainDockingManager.ContainerMode = DocumentContainerMode.TDI;
                MainDockingManager.PersistState = true;


                Log.Information("DockingManager configured successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure DockingManager");
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Log.Information("MainWindow: Loaded event - Size: {Width}x{Height}, Position: ({Left}, {Top}), State: {State}, Visible: {IsVisible}",
                ActualWidth, ActualHeight, Left, Top, WindowState, IsVisible);
                
            // Resolve MainViewModel via DI and set DataContext
            var viewModel = (ViewModels.MainViewModel)App.ServiceProvider.GetService(typeof(ViewModels.MainViewModel));
            if (viewModel != null)
            {
                DataContext = viewModel;
                Log.Information("MainViewModel set as DataContext");
            }
            
            // Load docking state from Syncfusion docs
            try
            {
                MainDockingManager.LoadDockState();
                Log.Information("Docking state loaded successfully");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load docking state");
            }
                
            // Log content control state
            var contentControl = Content as System.Windows.Controls.ContentControl;
            if (contentControl != null)
            {
                Log.Information("MainWindow: ContentControl - ActualSize: {Width}x{Height}, Content: {ContentType}",
                    contentControl.ActualWidth, contentControl.ActualHeight, 
                    contentControl.Content?.GetType().Name ?? "null");
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Log.Debug("MainWindow: SizeChanged - NewSize: {Width}x{Height}, PreviousSize: {PrevWidth}x{PrevHeight}",
                e.NewSize.Width, e.NewSize.Height, e.PreviousSize.Width, e.PreviousSize.Height);
        }

        private void MainWindow_Activated(object sender, System.EventArgs e)
        {
            Log.Debug("MainWindow: Activated event - Window brought to foreground");
        }

        private void MainWindow_ContentRendered(object sender, System.EventArgs e)
        {
            Log.Information("MainWindow: ContentRendered event - All content has been rendered");
        }

        private void MainWindow_Closed(object sender, System.EventArgs e)
        {
            Log.Information("MainWindow: Closed event - Saving docking state");
            
            // Save docking state
            try
            {
                MainDockingManager.SaveDockState();
                Log.Information("Docking state saved successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save docking state");
            }
        }

        private void DockingManager_DockStateChanged(object sender, System.EventArgs e)
        {
            Log.Debug("DockingManager: DockStateChanged event");
            
            // Update ViewModel
            UpdateViewModel();
        }

        private void UpdateViewModel()
        {
            // Update the MainViewModel with current docking state
            var viewModel = DataContext as ViewModels.MainViewModel;
            if (viewModel != null)
            {
                // Notify property changes or update docking-related properties
                // For now, just log the change
                Log.Debug("ViewModel updated with docking state change");
            }
        }
    }
}