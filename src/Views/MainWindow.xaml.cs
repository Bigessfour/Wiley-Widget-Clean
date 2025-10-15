using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using Serilog;
using Syncfusion.Windows.Tools.Controls;
using Prism.Navigation.Regions;
using WileyWidget.ViewModels;
using WileyWidget.Services;

namespace WileyWidget.Views
{
    public partial class MainWindow : Window
    {
        private readonly IRegionManager _regionManager;
        private readonly MainViewModel _viewModel;

        // Parameterless constructor for test scenarios
        public MainWindow() : this(null, null)
        {
        }

        public MainWindow(IRegionManager regionManager) : this(regionManager, null)
        {
        }

        public MainWindow(IRegionManager regionManager, MainViewModel viewModel)
        {
            Log.Debug("MainWindow: Constructor called");
            
            _regionManager = regionManager;
            _viewModel = viewModel;
            
            InitializeComponent();
            
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
                // Find DockingManager by name since direct reference isn't working
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                
                if (dockingManager != null)
                {
                    // Set runtime configuration that can't be set in XAML
                    dockingManager.UseDocumentContainer = true;
                    dockingManager.PersistState = true; // Enable state persistence
                    dockingManager.MaximizeButtonEnabled = true;
                    dockingManager.MinimizeButtonEnabled = true;
                    dockingManager.IsEnableHotTracking = true;
                    
                    // Configure advanced features
                    dockingManager.EnableScrollableSidePanel = true;
                    dockingManager.IsVS2010DraggingEnabled = true;
                    dockingManager.ShowTabItemContextMenu = true;
                    
                    Log.Information("DockingManager configured successfully");
                }
                else
                {
                    Log.Warning("MainDockingManager not found by name - cannot configure");
                }
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
            
            // Step 1: Set DataContext first to ensure ViewModel is available
            if (_viewModel != null)
            {
                DataContext = _viewModel;
                Log.Information("MainViewModel set as DataContext");
                Log.Debug("DataContext type: {DataContextType}", DataContext.GetType().Name);
            }
            else
            {
                Log.Warning("MainViewModel was not injected via constructor");
            }
            
            // Step 2: Initialize Prism regions after DataContext is set
            InitializePrismRegions();
            
            // Step 3: Configure DockingManager after UI is loaded
            ConfigureDockingManager();
            
            // Step 4: Verify region status and log for diagnostics
            LogRegionStatus();
            
            // Step 5: Load docking state with enhanced error handling
            LoadDockStateWithFallback();
                
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
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager != null)
                {
                    dockingManager.SaveDockState();
                    Log.Information("Docking state saved successfully");
                }
                else
                {
                    Log.Warning("MainDockingManager not found - cannot save docking state");
                }
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

        private void DockingManager_WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Log.Debug("DockingManager: WindowClosing event for window");
            
            // Add any cleanup logic here if needed
            // e.Cancel = true; // Uncomment to prevent closing
        }

        private void DockingManager_WindowClosed(object sender, System.EventArgs e)
        {
            Log.Information("DockingManager: Window closed");
            
            // Handle window closed event - update region states
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

        /// <summary>
        /// Loads docking state with filtering of hidden states and fallback handling
        /// </summary>
        private void LoadDockStateWithFallback()
        {
            Log.Information("Attempting to load docking state from IsolatedStorage");

            try
            {
                // First, try to load and filter the docking state
                if (TryLoadFilteredDockState())
                {
                    Log.Information("Docking state loaded and filtered successfully");
                    return;
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Hidden state is not allowed"))
            {
                Log.Warning(ex, "Hidden state found in docking configuration - attempting to clean and reload");
                
                // Try to clean the state and reload
                if (TryCleanAndReloadDockState())
                {
                    Log.Information("Docking state cleaned and reloaded successfully");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load docking state from IsolatedStorage");
            }

            // Fallback to default layout
            LoadDefaultDockingLayout();
        }

        /// <summary>
        /// Attempts to load docking state with hidden state filtering
        /// </summary>
        private bool TryLoadFilteredDockState()
        {
            try
            {
                // Use Syncfusion's standard LoadDockState which reads from IsolatedStorage
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager != null)
                {
                    dockingManager.LoadDockState();
                    // If we got here, the load was successful
                    return true;
                }
                else
                {
                    Log.Warning("MainDockingManager not found - cannot load docking state");
                    return false;
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Hidden state is not allowed"))
            {
                // Re-throw this specific exception to be handled by the caller
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Standard docking state load failed");
                return false;
            }
        }

        /// <summary>
        /// Attempts to clean hidden states from IsolatedStorage and reload
        /// </summary>
        private bool TryCleanAndReloadDockState()
        {
            try
            {
                // Get the IsolatedStorage file
                using (var store = IsolatedStorageFile.GetUserStoreForAssembly())
                {
                    string[] fileNames = store.GetFileNames("*DockState*");
                    
                    foreach (string fileName in fileNames)
                    {
                        Log.Debug("Found docking state file: {FileName}", fileName);
                        
                        if (CleanHiddenStatesFromFile(store, fileName))
                        {
                            Log.Information("Cleaned hidden states from {FileName}", fileName);
                        }
                    }
                }

                // Try to load again after cleaning
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager != null)
                {
                    dockingManager.LoadDockState();
                    return true;
                }
                else
                {
                    Log.Warning("MainDockingManager not found - cannot reload after cleaning");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clean and reload docking state");
                return false;
            }
        }

        /// <summary>
        /// Cleans hidden states from a docking state file
        /// </summary>
        private bool CleanHiddenStatesFromFile(IsolatedStorageFile store, string fileName)
        {
            try
            {
                XDocument doc;
                using (var stream = new IsolatedStorageFileStream(fileName, FileMode.Open, store))
                {
                    doc = XDocument.Load(stream);
                }

                // Find and remove or correct hidden dock states
                bool modified = false;
                var dockStateElements = doc.Descendants().Where(e => 
                    e.Name.LocalName.Contains("DockState") || 
                    e.Attributes().Any(a => a.Name.LocalName == "DockState"));

                foreach (var element in dockStateElements.ToList())
                {
                    var dockStateAttr = element.Attributes().FirstOrDefault(a => 
                        a.Name.LocalName == "DockState" || a.Name.LocalName.Contains("State"));
                    
                    if (dockStateAttr != null && dockStateAttr.Value.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Debug("Found hidden dock state in element: {ElementName}", element.Name.LocalName);
                        
                        // Change Hidden to Dock (default safe state)
                        dockStateAttr.Value = "Dock";
                        modified = true;
                        
                        Log.Debug("Changed hidden state to Dock for element: {ElementName}", element.Name.LocalName);
                    }
                }

                if (modified)
                {
                    // Save the cleaned document back
                    using (var stream = new IsolatedStorageFileStream(fileName, FileMode.Create, store))
                    {
                        doc.Save(stream);
                    }
                    Log.Information("Successfully cleaned hidden states from {FileName}", fileName);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clean hidden states from file: {FileName}", fileName);
                return false;
            }
        }

        /// <summary>
        /// Loads a default docking layout when state loading fails
        /// </summary>
        private void LoadDefaultDockingLayout()
        {
            Log.Information("Loading default docking layout");
            
            try
            {
                // Reset to default docking state
                // This ensures all dock windows are in a valid, visible state
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager != null)
                {
                    foreach (var dockingChild in dockingManager.Children)
                    {
                        if (dockingChild is FrameworkElement element)
                        {
                            // Set default docking properties
                            DockingManager.SetState(element, DockState.Dock);
                            DockingManager.SetDesiredWidthInDockedMode(element, 200);
                            DockingManager.SetDesiredHeightInDockedMode(element, 200);
                            
                            Log.Debug("Set default dock state for: {ElementName}", element.Name ?? element.GetType().Name);
                        }
                    }
                    
                    Log.Information("Default docking layout applied successfully");
                }
                else
                {
                    Log.Warning("MainDockingManager not found - cannot set default layout");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply default docking layout");
            }
        }

        /// <summary>
        /// Initializes Prism regions after DataContext is set and DockingManager is ready
        /// </summary>
        private void InitializePrismRegions()
        {
            Log.Information("Initializing Prism regions using ViewRegistrationService");
            
            if (_regionManager == null)
            {
                Log.Warning("RegionManager is null - cannot initialize regions");
                return;
            }

            try
            {
                // Log current region count before initialization
                Log.Information("Current regions available: {RegionCount}", _regionManager.Regions.Count());
                
                // Check if regions are already available from XAML
                var availableRegions = _regionManager.Regions.Select(r => r.Name).ToArray();
                Log.Information("Available regions from XAML: [{Regions}]", string.Join(", ", availableRegions));
                
                // Use ViewRegistrationService for comprehensive registration
                var viewRegistrationService = new WileyWidget.Services.ViewRegistrationService(_regionManager);
                viewRegistrationService.RegisterAllViews();

                // Validate regions after registration
                var validationResult = viewRegistrationService.ValidateRegions();
                Log.Information("Region validation result: {Result}", validationResult);

                if (!validationResult.IsValid)
                {
                    Log.Warning("Some regions are missing: [{MissingRegions}]", 
                        string.Join(", ", validationResult.MissingRegions));
                }
                
                Log.Information("Prism regions initialization completed using ViewRegistrationService");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Prism regions");
            }
        }

        /// <summary>
        /// Safely registers a view with a region, checking for region existence first
        /// </summary>
        private void RegisterViewWithRegionSafely(string regionName, Type viewType)
        {
            try
            {
                if (_regionManager.Regions.ContainsRegionWithName(regionName))
                {
                    _regionManager.RegisterViewWithRegion(regionName, viewType);
                    Log.Debug("Successfully registered {ViewType} with region {RegionName}", viewType.Name, regionName);
                }
                else
                {
                    Log.Debug("Region {RegionName} not found - view {ViewType} not registered", regionName, viewType.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register {ViewType} with region {RegionName}", viewType.Name, regionName);
            }
        }

        /// <summary>
        /// Logs comprehensive region status for diagnostics
        /// </summary>
        private void LogRegionStatus()
        {
            Log.Information("=== Region Status Report ===");
            
            if (_regionManager == null)
            {
                Log.Warning("RegionManager is null - no region status available");
                return;
            }

            try
            {
                var totalRegions = _regionManager.Regions.Count();
                Log.Information("Total regions registered: {RegionCount}", totalRegions);
                
                foreach (var region in _regionManager.Regions)
                {
                    var viewCount = region.Views?.Count() ?? 0;
                    var activeView = region.ActiveViews?.FirstOrDefault()?.GetType().Name ?? "None";
                    
                    Log.Information("Region '{RegionName}': {ViewCount} views, Active: {ActiveView}", 
                        region.Name, viewCount, activeView);
                        
                    // Log each view in the region
                    if (region.Views != null)
                    {
                        foreach (var view in region.Views)
                        {
                            Log.Debug("  - View: {ViewType}", view.GetType().Name);
                        }
                    }
                }
                
                // Check specifically for MainRegion since it's critical
                if (_regionManager.Regions.ContainsRegionWithName("MainRegion"))
                {
                    var mainRegion = _regionManager.Regions["MainRegion"];
                    Log.Information("MainRegion status: {ViewCount} views, Active: {ActiveView}",
                        mainRegion.Views?.Count() ?? 0,
                        mainRegion.ActiveViews?.FirstOrDefault()?.GetType().Name ?? "None");
                }
                else
                {
                    Log.Warning("MainRegion not found in region manager!");
                }
                
                Log.Information("=== End Region Status Report ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate region status report");
            }
        }
    }
}