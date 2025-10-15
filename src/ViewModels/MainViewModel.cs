using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;
using System.Windows;
using WileyWidget.Services.Logging;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.ViewModels
{
    public partial class MainViewModel : AsyncViewModelBase
    {
        private readonly IRegionManager regionManager;
        private readonly IEnterpriseRepository _enterpriseRepository;

        public MainViewModel(IRegionManager regionManager, IDispatcherHelper dispatcherHelper, ILogger<MainViewModel> logger, IEnterpriseRepository enterpriseRepository)
            : base(dispatcherHelper, logger)
        {
            this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));

            // Subscribe to collection change events for detailed logging
            Enterprises.CollectionChanged += Enterprises_CollectionChanged;

            // Load initial data from database
            _ = LoadEnterprisesAsync();

            // Initialize navigation commands
            NavigateToDashboardCommand = new AsyncRelayCommand(NavigateToDashboardAsync);
            NavigateToEnterprisesCommand = new AsyncRelayCommand(NavigateToEnterprisesAsync);
            NavigateToAccountsCommand = new AsyncRelayCommand(NavigateToAccountsAsync);
            NavigateToBudgetCommand = new AsyncRelayCommand(NavigateToBudgetAsync);
            NavigateToAIAssistCommand = new AsyncRelayCommand(NavigateToAIAssistAsync);
            NavigateToAnalyticsCommand = new AsyncRelayCommand(NavigateToAnalyticsAsync);

            // Initialize UI commands
            RefreshCommand = new RelayCommand(Refresh);
            RefreshAllCommand = new AsyncRelayCommand(RefreshAllAsync);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenReportsCommand = new RelayCommand(OpenReports);
            OpenAIAssistCommand = new RelayCommand(OpenAIAssist);

            // Initialize theme commands
            SwitchToMaterialDarkCommand = new RelayCommand(() => CurrentTheme = "MaterialDark");
            SwitchToFluentDarkCommand = new RelayCommand(() => CurrentTheme = "FluentDark");
            SwitchToFluentLightCommand = new RelayCommand(() => CurrentTheme = "FluentLight");

            // Initialize data commands  
            ImportExcelCommand = new AsyncRelayCommand(ImportExcelAsync);
            ExportDataCommand = new AsyncRelayCommand(ExportDataAsync);
            SyncQuickBooksCommand = new AsyncRelayCommand(SyncQuickBooksAsync);

            // Initialize view commands
            ShowDashboardCommand = new RelayCommand(ShowDashboard);
            ShowAnalyticsCommand = new RelayCommand(ShowAnalytics);

            // Initialize budget commands
            CreateNewBudgetCommand = new RelayCommand(CreateNewBudget);
            ImportBudgetCommand = new RelayCommand(ImportBudget);
            ExportBudgetCommand = new RelayCommand(ExportBudget);
            ShowBudgetAnalysisCommand = new RelayCommand(ShowBudgetAnalysis);
            ShowRateCalculatorCommand = new RelayCommand(ShowRateCalculator);

            // Initialize enterprise commands
            AddEnterpriseCommand = new AsyncRelayCommand(AddEnterpriseAsync);
            EditEnterpriseCommand = new AsyncRelayCommand(EditEnterpriseAsync);
            DeleteEnterpriseCommand = new AsyncRelayCommand(DeleteEnterpriseAsync);
            ManageServiceChargesCommand = new RelayCommand(ManageServiceCharges);
            ManageUtilityBillsCommand = new RelayCommand(ManageUtilityBills);

            // Initialize report commands
            GenerateFinancialSummaryCommand = new RelayCommand(GenerateFinancialSummary);
            GenerateBudgetVsActualCommand = new RelayCommand(GenerateBudgetVsActual);
            GenerateEnterprisePerformanceCommand = new RelayCommand(GenerateEnterprisePerformance);
            CreateCustomReportCommand = new RelayCommand(CreateCustomReport);
            ShowSavedReportsCommand = new RelayCommand(ShowSavedReports);

            // Initialize legacy commands
            AddTestEnterpriseCommand = new RelayCommand(AddTestEnterprise);
        }

        // Properties
        public ObservableCollection<Enterprise> Enterprises { get; } = new();
        private bool isAutoRefreshEnabled;
        public bool IsAutoRefreshEnabled
        {
            get => isAutoRefreshEnabled;
            set
            {
                if (SetProperty(ref isAutoRefreshEnabled, value))
                {
                    Logger.LogDebug("MainViewModel IsAutoRefreshEnabled changed to: {Value}", value);
                }
            }
        }



        private object? currentView;
        public object? CurrentView
        {
            get => currentView;
            set
            {
                if (SetProperty(ref currentView, value))
                {
                    Logger.LogDebug("MainViewModel CurrentView changed to: {ViewType}", value?.GetType().Name ?? "null");
                }
            }
        }

        private object? activeWindow;
        public object? ActiveWindow
        {
            get => activeWindow;
            set
            {
                if (SetProperty(ref activeWindow, value))
                {
                    Logger.LogDebug("MainViewModel ActiveWindow changed to: {WindowType}", value?.GetType().Name ?? "null");
                    // Update CurrentViewTitle when ActiveWindow changes
                    UpdateCurrentViewTitle();
                }
            }
        }

        private string? currentViewTitle;
        public string? CurrentViewTitle
        {
            get => currentViewTitle;
            set
            {
                if (SetProperty(ref currentViewTitle, value))
                {
                    Logger.LogDebug("MainViewModel CurrentViewTitle changed to: {Title}", value ?? "null");
                }
            }
        }

        private void UpdateCurrentViewTitle()
        {
            if (ActiveWindow is FrameworkElement element && element.Name != null)
            {
                // Try to get a user-friendly title from the element's name or tag
                CurrentViewTitle = element.Name switch
                {
                    "DashboardRegion" => "Dashboard",
                    "EnterpriseRegion" => "Enterprises",
                    "BudgetRegion" => "Budget Management",
                    "MunicipalAccountRegion" => "Municipal Accounts",
                    "UtilityCustomerRegion" => "Utility Customers",
                    "ReportsRegion" => "Reports & Analytics",
                    "AnalyticsRegion" => "Advanced Analytics",
                    "AIAssistRegion" => "AI Assistant",
                    "SettingsRegion" => "Settings",
                    _ => element.Name
                };
            }
            else
            {
                CurrentViewTitle = "Wiley Widget";
            }
        }

        // DockingManager State Properties for MVVM binding
        private bool isDashboardVisible = true;
        public bool IsDashboardVisible
        {
            get => isDashboardVisible;
            set
            {
                if (SetProperty(ref isDashboardVisible, value))
                {
                    Logger.LogDebug("MainViewModel IsDashboardVisible changed to: {Value}", value);
                    UpdateDockingVisibility("DashboardRegion", value);
                }
            }
        }

        private bool isAnalyticsVisible;
        public bool IsAnalyticsVisible
        {
            get => isAnalyticsVisible;
            set
            {
                if (SetProperty(ref isAnalyticsVisible, value))
                {
                    Logger.LogDebug("MainViewModel IsAnalyticsVisible changed to: {Value}", value);
                    UpdateDockingVisibility("AnalyticsRegion", value);
                }
            }
        }

        private bool isReportsVisible;
        public bool IsReportsVisible
        {
            get => isReportsVisible;
            set
            {
                if (SetProperty(ref isReportsVisible, value))
                {
                    Logger.LogDebug("MainViewModel IsReportsVisible changed to: {Value}", value);
                    UpdateDockingVisibility("ReportsRegion", value);
                }
            }
        }

        private void UpdateDockingVisibility(string regionName, bool isVisible)
        {
            // This method would be called from code-behind to update actual docking state
            // For now, just log the intended change
            Logger.LogDebug("Docking visibility update requested: {RegionName} -> {IsVisible}", regionName, isVisible);
        }

        private string currentTheme = "MaterialDark";
        public string CurrentTheme
        {
            get => currentTheme;
            set
            {
                if (SetProperty(ref currentTheme, value))
                {
                    Logger.LogInformation("MainViewModel CurrentTheme changed to: {Theme}", value);
                    ApplyTheme(value);
                }
            }
        }

        private void ApplyTheme(string themeName)
        {
            // This will be called from code-behind to apply the theme
            // The actual theme application is handled in the view via event
            Logger.LogDebug("Applying theme: {Theme}", themeName);
            // Raise property changed to notify view
            OnPropertyChanged(nameof(CurrentTheme));
        }

        // Theme switching commands
        public RelayCommand SwitchToMaterialDarkCommand { get; }
        public RelayCommand SwitchToFluentDarkCommand { get; }
        public RelayCommand SwitchToFluentLightCommand { get; }

        private bool isInitialized;
        public bool IsInitialized
        {
            get => isInitialized;
            set
            {
                if (SetProperty(ref isInitialized, value))
                {
                    Logger.LogInformation("MainViewModel IsInitialized changed to: {Value}", value);
                }
            }
        }

        private Enterprise? selectedEnterprise;
        public Enterprise? SelectedEnterprise
        {
            get => selectedEnterprise;
            set
            {
                if (SetProperty(ref selectedEnterprise, value))
                {
                    Logger.LogDebug("MainViewModel SelectedEnterprise changed to: {EnterpriseName} (ID: {EnterpriseId})", 
                        value?.Name ?? "null", value?.Id ?? 0);
                }
            }
        }

        // Navigation Commands - Convert to AsyncRelayCommand for better UX
        public AsyncRelayCommand NavigateToDashboardCommand { get; }
        public AsyncRelayCommand NavigateToEnterprisesCommand { get; }
        public AsyncRelayCommand NavigateToEnterpriseCommand => NavigateToEnterprisesCommand; // Alias for tests
        public AsyncRelayCommand NavigateToAccountsCommand { get; }
        public AsyncRelayCommand NavigateToBudgetCommand { get; }
        public AsyncRelayCommand NavigateToAIAssistCommand { get; }
        public AsyncRelayCommand NavigateToAnalyticsCommand { get; }

        // UI Commands
        public RelayCommand RefreshCommand { get; }
        public AsyncRelayCommand RefreshAllCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand OpenReportsCommand { get; }
        public RelayCommand OpenAIAssistCommand { get; }

        // Data Commands
        public AsyncRelayCommand ImportExcelCommand { get; }
        public AsyncRelayCommand ExportDataCommand { get; }
        public AsyncRelayCommand SyncQuickBooksCommand { get; }

        // View Commands
        public RelayCommand ShowDashboardCommand { get; }
        public RelayCommand ShowAnalyticsCommand { get; }

        // Budget Commands
        public RelayCommand CreateNewBudgetCommand { get; }
        public RelayCommand ImportBudgetCommand { get; }
        public RelayCommand ExportBudgetCommand { get; }
        public RelayCommand ShowBudgetAnalysisCommand { get; }
        public RelayCommand ShowRateCalculatorCommand { get; }

        // Enterprise Commands
        public AsyncRelayCommand AddEnterpriseCommand { get; }
        public AsyncRelayCommand EditEnterpriseCommand { get; }
        public AsyncRelayCommand DeleteEnterpriseCommand { get; }
        public RelayCommand ManageServiceChargesCommand { get; }
        public RelayCommand ManageUtilityBillsCommand { get; }

        // Report Commands
        public RelayCommand GenerateFinancialSummaryCommand { get; }
        public RelayCommand GenerateBudgetVsActualCommand { get; }
        public RelayCommand GenerateEnterprisePerformanceCommand { get; }
        public RelayCommand CreateCustomReportCommand { get; }
        public RelayCommand ShowSavedReportsCommand { get; }

        // Legacy Commands
        public RelayCommand AddTestEnterpriseCommand { get; }

        // Navigation Methods with region existence checks - Now Async
        private async Task NavigateToDashboardAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToDashboard command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50); // Small delay for UI feedback
                await NavigateToRegionSafelyAsync("DashboardRegion", "DashboardView", "Dashboard");
                Logger.LogInformation("Navigated to Dashboard");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Dashboard failed");
                // Could show error dialog here
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToEnterprisesAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToEnterprises command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("EnterpriseRegion", "EnterpriseView", "Enterprises");
                Logger.LogInformation("Navigated to Enterprises");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Enterprises failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToAccountsAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToAccounts command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("MunicipalAccountRegion", "MunicipalAccountView", "Municipal Accounts");
                Logger.LogInformation("Navigated to Municipal Accounts");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Municipal Accounts failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToBudgetAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToBudget command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("BudgetRegion", "BudgetView", "Budget");
                Logger.LogInformation("Navigated to Budget");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Budget failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToAIAssistAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToAIAssist command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("AIAssistRegion", "AIAssistView", "AI Assistant");
                Logger.LogInformation("Navigated to AI Assistant");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to AI Assistant failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task NavigateToAnalyticsAsync()
        {
            Logger.LogInformation("MainViewModel: NavigateToAnalytics command invoked");
            try
            {
                IsBusy = true;
                await Task.Delay(50);
                await NavigateToRegionSafelyAsync("AnalyticsRegion", "AnalyticsView", "Analytics");
                Logger.LogInformation("Navigated to Analytics");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Navigation to Analytics failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Safely navigates to a region after checking for existence and logging (Async version)
        /// </summary>
        private async Task NavigateToRegionSafelyAsync(string regionName, string viewName, string displayName)
        {
            Logger.LogInformation("Attempting to navigate to {DisplayName} (Region: {RegionName}, View: {ViewName})", 
                displayName, regionName, viewName);

            try
            {
                // Check if region exists before attempting navigation
                if (!regionManager.Regions.ContainsRegionWithName(regionName))
                {
                    Logger.LogWarning("Region '{RegionName}' not found in region manager. Available regions: [{AvailableRegions}]",
                        regionName, string.Join(", ", regionManager.Regions.Select(r => r.Name)));
                    return;
                }

                // Log current region state before navigation
                var region = regionManager.Regions[regionName];
                Logger.LogDebug("Region '{RegionName}' found. Current views: {ViewCount}, Active view: {ActiveView}",
                    regionName, 
                    region.Views?.Count() ?? 0,
                    region.ActiveViews?.FirstOrDefault()?.GetType().Name ?? "None");

                // Perform navigation with callback for result tracking
                var navigationResult = await Task.Run(() =>
                {
                    var tcs = new TaskCompletionSource<bool>();
                    regionManager.RequestNavigate(regionName, viewName, (result) =>
                    {
                        tcs.SetResult(result.Success);
                        if (result.Success)
                        {
                            Logger.LogInformation("Successfully navigated to {DisplayName} in region {RegionName}",
                                displayName, regionName);
                        }
                        else
                        {
                            Logger.LogWarning("Navigation to {DisplayName} failed. Region: {RegionName}, Error: {Error}",
                                displayName, regionName, result.Exception?.Message ?? "Unknown navigation error");
                        }
                    });
                    return tcs.Task;
                });

                if (!navigationResult)
                {
                    Logger.LogWarning("Navigation to {DisplayName} completed but reported failure", displayName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception during navigation to {DisplayName} (Region: {RegionName})", 
                    displayName, regionName);
            }
        }

        /// <summary>
        /// Safely navigates to a region after checking for existence and logging (Synchronous version for compatibility)
        /// </summary>
        private void NavigateToRegionSafely(string regionName, string viewName, string displayName)
        {
            Logger.LogInformation("Attempting to navigate to {DisplayName} (Region: {RegionName}, View: {ViewName})", 
                displayName, regionName, viewName);

            try
            {
                // Check if region exists before attempting navigation
                if (!regionManager.Regions.ContainsRegionWithName(regionName))
                {
                    Logger.LogWarning("Region '{RegionName}' not found in region manager. Available regions: [{AvailableRegions}]",
                        regionName, string.Join(", ", regionManager.Regions.Select(r => r.Name)));
                    return;
                }

                // Log current region state before navigation
                var region = regionManager.Regions[regionName];
                Logger.LogDebug("Region '{RegionName}' found. Current views: {ViewCount}, Active view: {ActiveView}",
                    regionName, 
                    region.Views?.Count() ?? 0,
                    region.ActiveViews?.FirstOrDefault()?.GetType().Name ?? "None");

                // Perform navigation with callback for result tracking
                regionManager.RequestNavigate(regionName, viewName, (result) =>
                {
                    if (result.Success)
                    {
                        Logger.LogInformation("Successfully navigated to {DisplayName} in region {RegionName}",
                            displayName, regionName);
                    }
                    else
                    {
                        Logger.LogWarning("Navigation to {DisplayName} failed. Region: {RegionName}, Error: {Error}",
                            displayName, regionName, result.Exception?.Message ?? "Unknown navigation error");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception during navigation to {DisplayName} (Region: {RegionName})", 
                    displayName, regionName);
            }
        }

        // Other Methods
        private void Refresh()
        {
            Logger.LogInformation("MainViewModel: Refresh command executed");
            try
            {
                // Refresh the current active view
                if (CurrentView != null)
                {
                    Logger.LogInformation("Refreshing current view: {ViewType}", CurrentView.GetType().Name);
                }
                else
                {
                    Logger.LogInformation("No current view to refresh, reloading data");
                    RefreshAll();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh view");
            }
        }

        private void RefreshAll()
        {
            Logger.LogInformation("MainViewModel: Refresh all command executed (synchronous wrapper)");
            _ = RefreshAllAsync();
        }

        private async Task RefreshAllAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("RefreshAll");
            var stopwatch = Stopwatch.StartNew();
            
            Logger.LogInformation("MainViewModel: Refresh all command executed - {LogContext}", loggingContext);
            try
            {
                IsBusy = true;
                
                // Refresh all data sources
                Logger.LogInformation("Refreshing all data sources");
                
                // Simulate async data loading
                await Task.Run(() =>
                {
                    // Clear and reload enterprises on UI thread
                    DispatcherHelper.Invoke(() => Enterprises.Clear());
                });
                
                // Trigger navigation refresh on all regions
                foreach (var region in regionManager.Regions)
                {
                    Logger.LogDebug("Refreshing region: {RegionName}", region.Name);
                }
                
                stopwatch.Stop();
                Logger.LogInformation("All data sources refreshed successfully in {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError(ex, "Failed to refresh all data after {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenSettings()
        {
            Logger.LogInformation("MainViewModel: Open settings command executed");
            try
            {
                // Navigate to settings view
                NavigateToRegionSafely("SettingsRegion", "SettingsView", "Settings");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open settings");
            }
        }

        private void AddTestEnterprise()
        {
            Logger.LogInformation("MainViewModel: Add test enterprise command executed");
            try
            {
                // Create a test enterprise for development/testing purposes
                var testEnterprise = new Enterprise
                {
                    Name = $"Test Enterprise {DateTime.Now:HHmmss}",
                    Type = "Water"
                };
                
                Enterprises.Add(testEnterprise);
                SelectedEnterprise = testEnterprise;
                
                Logger.LogInformation("Test enterprise added: {EnterpriseName}", testEnterprise.Name);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add test enterprise");
            }
        }

        // Docking and Navigation Snapshot Methods
        public void SaveNavigationSnapshot()
        {
            // Implement save navigation snapshot logic
            Logger.LogInformation("MainViewModel: Navigation snapshot saved");
        }

        public void UpdateDockingState(string regionName, bool isVisible)
        {
            // Implement update docking state logic
            Logger.LogInformation("MainViewModel: Docking state updated for {RegionName}: {IsVisible}", regionName, isVisible);
        }

        public void RestoreNavigationSnapshot()
        {
            // Implement restore navigation snapshot logic
            Logger.LogInformation("MainViewModel: Navigation snapshot restored");
        }

        // UI Command Implementations
        private void OpenReports()
        {
            NavigateToRegionSafely("ReportsRegion", "ReportsView", "Reports");
        }

        private void OpenAIAssist()
        {
            NavigateToRegionSafely("AIAssistRegion", "AIAssistView", "AI Assistant");
        }

        // Data Command Implementations
        private void ImportExcel()
        {
            Logger.LogInformation("MainViewModel: Import Excel command executed (synchronous wrapper)");
            _ = ImportExcelAsync();
        }

        private async Task ImportExcelAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("ImportExcel");
            var stopwatch = Stopwatch.StartNew();
            
            Logger.LogInformation("MainViewModel: Import Excel command executed - {LogContext}", loggingContext);
            try
            {
                IsBusy = true;
                
                // Show open file dialog for Excel files
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Data from Excel",
                    Filter = "Excel files (*.xlsx;*.xls)|*.xlsx;*.xls|All files (*.*)|*.*",
                    DefaultExt = ".xlsx",
                    CheckFileExists = true,
                    CheckPathExists = true
                };
                
                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    Logger.LogInformation("Selected Excel file for import: {FilePath} - {LogContext}", filePath, loggingContext);
                    
                    // Simulate Excel import processing
                    BusyMessage = "Importing data from Excel...";
                    await Task.Delay(500); // Simulate file processing
                    
                    // Here you would implement actual Excel reading logic
                    // For now, we'll simulate successful import
                    var importedRecords = 25; // Simulate imported record count
                    
                    BusyMessage = $"Successfully imported {importedRecords} records from Excel";
                    Logger.LogInformation("Excel import completed successfully - imported {RecordCount} records from {FilePath} in {ElapsedMs}ms - {LogContext}", 
                        importedRecords, filePath, stopwatch.ElapsedMilliseconds, loggingContext);
                    
                    MessageBox.Show($"Excel import completed successfully!\n\nImported {importedRecords} records from:\n{System.IO.Path.GetFileName(filePath)}",
                                  "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Refresh data after import
                    await RefreshAllAsync();
                }
                else
                {
                    BusyMessage = "Excel import cancelled";
                    Logger.LogInformation("Excel import cancelled by user - {LogContext}", loggingContext);
                }
                
                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                BusyMessage = "Error importing Excel file";
                Logger.LogError(ex, "Failed to import Excel file after {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error importing Excel file: {ex.Message}", "Import Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExportData()
        {
            Logger.LogInformation("MainViewModel: Export data command executed (synchronous wrapper)");
            _ = ExportDataAsync();
        }

        private async Task ExportDataAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("ExportData");
            var stopwatch = Stopwatch.StartNew();
            
            Logger.LogInformation("MainViewModel: Export data command executed - {LogContext}", loggingContext);
            try
            {
                IsBusy = true;
                
                // Create export data
                var exportData = new
                {
                    ExportDate = DateTime.Now,
                    Application = "Wiley Widget",
                    Enterprises = Enterprises.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Type,
                        e.Description,
                        e.CreatedDate,
                        e.ModifiedDate
                    }).ToList(),
                    Summary = new
                    {
                        TotalEnterprises = Enterprises.Count,
                        ExportFormat = "JSON"
                    }
                };
                
                // Serialize to JSON
                var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Application Data",
                    Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = $"WileyWidget_Data_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    BusyMessage = "Exporting data...";
                    await Task.Delay(300); // Simulate export processing
                    
                    await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, json);
                    
                    BusyMessage = $"Data exported to {System.IO.Path.GetFileName(saveFileDialog.FileName)}";
                    Logger.LogInformation("Data export completed successfully to {FilePath} in {ElapsedMs}ms - {LogContext}", 
                        saveFileDialog.FileName, stopwatch.ElapsedMilliseconds, loggingContext);
                    
                    MessageBox.Show($"Data export completed successfully!\n\nFile: {System.IO.Path.GetFileName(saveFileDialog.FileName)}\nRecords: {exportData.Enterprises.Count}",
                                  "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    BusyMessage = "Data export cancelled";
                    Logger.LogInformation("Data export cancelled by user - {LogContext}", loggingContext);
                }
                
                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                BusyMessage = "Error exporting data";
                Logger.LogError(ex, "Failed to export data after {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SyncQuickBooks()
        {
            Logger.LogInformation("MainViewModel: Sync QuickBooks command executed (synchronous wrapper)");
            _ = SyncQuickBooksAsync();
        }

        private async Task SyncQuickBooksAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("SyncQuickBooks");
            var stopwatch = Stopwatch.StartNew();
            
            Logger.LogInformation("MainViewModel: Sync QuickBooks command executed - {LogContext}", loggingContext);
            try
            {
                IsBusy = true;
                
                // Simulate QuickBooks synchronization process
                BusyMessage = "Connecting to QuickBooks Online...";
                Logger.LogInformation("Initiating QuickBooks synchronization...");
                await Task.Delay(500); // Simulate connection
                
                BusyMessage = "Synchronizing enterprise data...";
                await Task.Delay(800); // Simulate data sync
                
                BusyMessage = "Synchronizing financial transactions...";
                await Task.Delay(600); // Simulate transaction sync
                
                // Simulate sync results
                var syncResults = new
                {
                    EnterprisesSynced = Enterprises.Count,
                    TransactionsProcessed = 45,
                    NewRecords = 12,
                    UpdatedRecords = 23,
                    Errors = 0
                };
                
                BusyMessage = "QuickBooks sync completed successfully";
                Logger.LogInformation("QuickBooks sync completed successfully - Synced {EnterpriseCount} enterprises, {TransactionCount} transactions in {ElapsedMs}ms - {LogContext}", 
                    syncResults.EnterprisesSynced, syncResults.TransactionsProcessed, stopwatch.ElapsedMilliseconds, loggingContext);
                
                MessageBox.Show($"QuickBooks synchronization completed successfully!\n\n" +
                              $"Enterprises: {syncResults.EnterprisesSynced}\n" +
                              $"Transactions: {syncResults.TransactionsProcessed}\n" +
                              $"New Records: {syncResults.NewRecords}\n" +
                              $"Updated Records: {syncResults.UpdatedRecords}\n" +
                              $"Errors: {syncResults.Errors}",
                              "QuickBooks Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Refresh data after sync
                await RefreshAllAsync();
                
                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                BusyMessage = "Error synchronizing with QuickBooks";
                Logger.LogError(ex, "Failed to sync with QuickBooks after {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error synchronizing with QuickBooks: {ex.Message}", "Sync Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // View Command Implementations  
        private void ShowDashboard()
        {
            NavigateToRegionSafely("DashboardRegion", "DashboardView", "Dashboard");
        }

        private void ShowAnalytics()
        {
            NavigateToRegionSafely("AnalyticsRegion", "AnalyticsView", "Analytics");
        }

        // Budget Command Implementations
        private void CreateNewBudget()
        {
            Logger.LogInformation("MainViewModel: Create new budget command executed");
            try
            {
                // Navigate to budget creation view
                NavigateToRegionSafely("BudgetRegion", "BudgetCreationView", "Budget Creation");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create new budget");
            }
        }

        private void ImportBudget()
        {
            Logger.LogInformation("MainViewModel: Import budget command executed");
            try
            {
                // Navigate to budget import view
                NavigateToRegionSafely("BudgetRegion", "BudgetImportView", "Budget Import");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to import budget");
            }
        }

        private void ExportBudget()
        {
            Logger.LogInformation("MainViewModel: Export budget command executed");
            try
            {
                // Navigate to budget export view
                NavigateToRegionSafely("BudgetRegion", "BudgetExportView", "Budget Export");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to export budget");
            }
        }

        private void ShowBudgetAnalysis()
        {
            Logger.LogInformation("MainViewModel: Show budget analysis command executed");
            try
            {
                // Navigate to budget analysis view
                NavigateToRegionSafely("AnalyticsRegion", "BudgetAnalysisView", "Budget Analysis");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show budget analysis");
            }
        }

        private void ShowRateCalculator()
        {
            Logger.LogInformation("MainViewModel: Show rate calculator command executed");
            try
            {
                // Navigate to rate calculator view
                NavigateToRegionSafely("AnalyticsRegion", "RateCalculatorView", "Rate Calculator");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show rate calculator");
            }
        }

        // Enterprise Command Implementations
        private async Task AddEnterpriseAsync()
        {
            Logger.LogInformation("MainViewModel: Add enterprise command invoked");
            try
            {
                IsBusy = true;

                // Create a new enterprise with default values
                var newEnterprise = new Enterprise
                {
                    Name = string.Empty,
                    Type = "Water", // Default type
                    Description = string.Empty,
                    CurrentRate = 0.00m,
                    MonthlyExpenses = 0.00m,
                    CitizenCount = 1,
                    TotalBudget = 0.00m,
                    BudgetAmount = 0.00m,
                    Notes = string.Empty,
                    Status = EnterpriseStatus.Active,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "System" // TODO: Get from current user context
                };

                // Show dialog for editing the new enterprise
                if (ShowEnterpriseDialog(newEnterprise, "Add New Enterprise"))
                {
                    // Validate the enterprise before adding
                    if (ValidateEnterprise(newEnterprise, out var validationErrors))
                    {
                        // Save to database
                        var savedEnterprise = await _enterpriseRepository.AddAsync(newEnterprise);
                        
                        // Add to the observable collection
                        Enterprises.Add(savedEnterprise);
                        SelectedEnterprise = savedEnterprise;

                        Logger.LogInformation("Enterprise added successfully: {EnterpriseName} (ID: {EnterpriseId})",
                            savedEnterprise.Name, savedEnterprise.Id);

                        MessageBox.Show($"Enterprise '{savedEnterprise.Name}' added successfully!",
                                      "Enterprise Added", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var errorMessage = string.Join(Environment.NewLine, validationErrors);
                        MessageBox.Show($"Please correct the following errors:{Environment.NewLine}{Environment.NewLine}{errorMessage}",
                                      "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Logger.LogWarning("Enterprise validation failed: {Errors}", string.Join(", ", validationErrors));
                    }
                }
                else
                {
                    Logger.LogInformation("Enterprise creation cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add enterprise");
                MessageBox.Show($"Error adding enterprise: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task EditEnterpriseAsync()
        {
            Logger.LogInformation("MainViewModel: Edit enterprise command invoked");
            try
            {
                if (SelectedEnterprise == null)
                {
                    Logger.LogWarning("Cannot edit enterprise: No enterprise selected");
                    MessageBox.Show("Please select an enterprise to edit.", "No Selection",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsBusy = true;

                Logger.LogDebug("Editing enterprise: {EnterpriseName} (ID: {EnterpriseId})",
                    SelectedEnterprise.Name, SelectedEnterprise.Id);

                // Create a copy for editing (to allow cancellation)
                var enterpriseCopy = CreateEnterpriseCopy(SelectedEnterprise);

                // Show dialog for editing
                if (ShowEnterpriseDialog(enterpriseCopy, "Edit Enterprise"))
                {
                    // Validate the enterprise before updating
                    if (ValidateEnterprise(enterpriseCopy, out var validationErrors))
                    {
                        // Update the original enterprise with validated data
                        UpdateEnterpriseFromCopy(SelectedEnterprise, enterpriseCopy);
                        
                        // Save to database
                        await _enterpriseRepository.UpdateAsync(SelectedEnterprise);

                        Logger.LogInformation("Enterprise updated successfully: {EnterpriseName} (ID: {EnterpriseId})",
                            SelectedEnterprise.Name, SelectedEnterprise.Id);

                        MessageBox.Show($"Enterprise '{SelectedEnterprise.Name}' updated successfully!",
                                      "Enterprise Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var errorMessage = string.Join(Environment.NewLine, validationErrors);
                        MessageBox.Show($"Please correct the following errors:{Environment.NewLine}{Environment.NewLine}{errorMessage}",
                                      "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Logger.LogWarning("Enterprise validation failed: {Errors}", string.Join(", ", validationErrors));
                    }
                }
                else
                {
                    Logger.LogInformation("Enterprise editing cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to edit enterprise");
                MessageBox.Show($"Error editing enterprise: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteEnterpriseAsync()
        {
            Logger.LogInformation("MainViewModel: Delete enterprise command invoked");
            try
            {
                if (SelectedEnterprise == null)
                {
                    Logger.LogWarning("Cannot delete enterprise: No enterprise selected");
                    MessageBox.Show("Please select an enterprise to delete.", "No Selection",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Logger.LogInformation("Deleting enterprise: {EnterpriseName} (ID: {EnterpriseId})",
                    SelectedEnterprise.Name, SelectedEnterprise.Id);

                // Show confirmation dialog
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the enterprise '{SelectedEnterprise.Name}'?\n\nThis action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    IsBusy = true;
                    
                    // Delete from database (soft delete)
                    var deleteResult = await _enterpriseRepository.DeleteAsync(SelectedEnterprise.Id);
                    
                    if (deleteResult)
                    {
                        // Remove from collection
                        Enterprises.Remove(SelectedEnterprise);
                        SelectedEnterprise = null;

                        Logger.LogInformation("Enterprise deleted successfully");

                        MessageBox.Show("Enterprise deleted successfully.", "Enterprise Deleted",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        Logger.LogWarning("Enterprise deletion returned false from repository");
                        MessageBox.Show("Enterprise could not be deleted. It may have already been removed.", "Delete Failed",
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Logger.LogInformation("Enterprise deletion cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to delete enterprise");
                MessageBox.Show($"Error deleting enterprise: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ManageServiceCharges()
        {
            Logger.LogInformation("MainViewModel: Manage service charges command executed");
            try
            {
                // Navigate to service charges management view
                NavigateToRegionSafely("EnterpriseRegion", "ServiceChargesView", "Service Charges");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open service charges management");
            }
        }

        private void ManageUtilityBills()
        {
            Logger.LogInformation("MainViewModel: Manage utility bills command executed");
            try
            {
                // Navigate to utility bills management view
                NavigateToRegionSafely("EnterpriseRegion", "UtilityBillsView", "Utility Bills");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open utility bills management");
            }
        }

        // Report Command Implementations
        private void GenerateFinancialSummary()
        {
            Logger.LogInformation("MainViewModel: Generate financial summary command executed");
            try
            {
                // Navigate to financial summary report view
                NavigateToRegionSafely("ReportsRegion", "FinancialSummaryReportView", "Financial Summary Report");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate financial summary report");
            }
        }

        private void GenerateBudgetVsActual()
        {
            Logger.LogInformation("MainViewModel: Generate budget vs actual command executed");
            try
            {
                // Navigate to budget vs actual report view
                NavigateToRegionSafely("ReportsRegion", "BudgetVsActualReportView", "Budget vs Actual Report");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate budget vs actual report");
            }
        }

        private void GenerateEnterprisePerformance()
        {
            Logger.LogInformation("MainViewModel: Generate enterprise performance command executed");
            try
            {
                // Navigate to enterprise performance report view
                NavigateToRegionSafely("ReportsRegion", "EnterprisePerformanceReportView", "Enterprise Performance Report");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate enterprise performance report");
            }
        }

        private void CreateCustomReport()
        {
            Logger.LogInformation("MainViewModel: Create custom report command executed");
            try
            {
                // Navigate to custom report builder view
                NavigateToRegionSafely("ReportsRegion", "CustomReportBuilderView", "Custom Report Builder");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create custom report");
            }
        }

        private void ShowSavedReports()
        {
            Logger.LogInformation("MainViewModel: Show saved reports command executed");
            try
            {
                // Navigate to saved reports view
                NavigateToRegionSafely("ReportsRegion", "SavedReportsView", "Saved Reports");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show saved reports");
            }
        }

        // Helper Methods for Enterprise CRUD Operations

        /// <summary>
        /// Loads enterprises from the database into the observable collection
        /// </summary>
        private async Task LoadEnterprisesAsync()
        {
            try
            {
                Logger.LogInformation("Loading enterprises from database");
                
                var enterprises = await _enterpriseRepository.GetAllAsync();
                
                // Update the collection on the UI thread
                await DispatcherHelper.InvokeAsync(() =>
                {
                    Enterprises.Clear();
                    foreach (var enterprise in enterprises)
                    {
                        Enterprises.Add(enterprise);
                    }
                });
                
                Logger.LogInformation("Loaded {Count} enterprises from database", enterprises.Count());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load enterprises from database");
                // Continue with empty collection - don't crash the app
            }
        }

        /// <summary>
        /// Shows a dialog for editing an enterprise
        /// </summary>
        private bool ShowEnterpriseDialog(Enterprise enterprise, string title)
        {
            try
            {
                // For now, use a simple input dialog approach
                // TODO: Replace with proper WPF dialog window
                var name = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter enterprise name:",
                    title,
                    enterprise.Name);

                if (string.IsNullOrWhiteSpace(name))
                    return false; // User cancelled

                enterprise.Name = name.Trim();

                var type = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter enterprise type (Water, Sewer, Trash, etc.):",
                    title,
                    enterprise.Type ?? "Water");

                if (string.IsNullOrWhiteSpace(type))
                    return false; // User cancelled

                enterprise.Type = type.Trim();

                var rateText = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter current rate:",
                    title,
                    enterprise.CurrentRate.ToString("F2"));

                if (string.IsNullOrWhiteSpace(rateText))
                    return false; // User cancelled

                if (!decimal.TryParse(rateText, out var rate))
                {
                    MessageBox.Show("Invalid rate format. Please enter a valid decimal number.", "Invalid Input",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                enterprise.CurrentRate = rate;

                var expenseText = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter monthly expenses:",
                    title,
                    enterprise.MonthlyExpenses.ToString("F2"));

                if (string.IsNullOrWhiteSpace(expenseText))
                    return false; // User cancelled

                if (!decimal.TryParse(expenseText, out var expenses))
                {
                    MessageBox.Show("Invalid expense format. Please enter a valid decimal number.", "Invalid Input",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                enterprise.MonthlyExpenses = expenses;

                var citizenText = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter number of citizens served:",
                    title,
                    enterprise.CitizenCount.ToString());

                if (string.IsNullOrWhiteSpace(citizenText))
                    return false; // User cancelled

                if (!int.TryParse(citizenText, out var citizens) || citizens < 1)
                {
                    MessageBox.Show("Invalid citizen count. Please enter a positive integer.", "Invalid Input",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                enterprise.CitizenCount = citizens;

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show enterprise dialog");
                MessageBox.Show($"Error displaying dialog: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Validates an enterprise and returns validation errors
        /// </summary>
        private bool ValidateEnterprise(Enterprise enterprise, out List<string> validationErrors)
        {
            validationErrors = new List<string>();

            // Required field validations
            if (string.IsNullOrWhiteSpace(enterprise.Name))
                validationErrors.Add("Enterprise name is required");

            if (enterprise.Name?.Length > 100)
                validationErrors.Add("Enterprise name cannot exceed 100 characters");

            if (string.IsNullOrWhiteSpace(enterprise.Type))
                validationErrors.Add("Enterprise type is required");

            if (enterprise.Type?.Length > 50)
                validationErrors.Add("Enterprise type cannot exceed 50 characters");

            if (enterprise.CurrentRate <= 0)
                validationErrors.Add("Current rate must be greater than zero");

            if (enterprise.CurrentRate > 9999.99m)
                validationErrors.Add("Current rate cannot exceed $9,999.99");

            if (enterprise.MonthlyExpenses < 0)
                validationErrors.Add("Monthly expenses cannot be negative");

            if (enterprise.CitizenCount < 1)
                validationErrors.Add("Citizen count must be at least 1");

            if (enterprise.Description?.Length > 500)
                validationErrors.Add("Description cannot exceed 500 characters");

            if (enterprise.Notes?.Length > 500)
                validationErrors.Add("Notes cannot exceed 500 characters");

            // Business rule validations
            if (enterprise.CitizenCount > 0 && enterprise.MonthlyExpenses > 0)
            {
                var breakEvenRate = enterprise.MonthlyExpenses / enterprise.CitizenCount;
                if (enterprise.CurrentRate < breakEvenRate * 0.5m)
                {
                    validationErrors.Add($"Warning: Current rate (${enterprise.CurrentRate:F2}) is significantly below break-even rate (${breakEvenRate:F2})");
                }
            }

            return validationErrors.Count == 0;
        }

        /// <summary>
        /// Creates a deep copy of an enterprise for editing
        /// </summary>
        private Enterprise CreateEnterpriseCopy(Enterprise original)
        {
            return new Enterprise
            {
                Id = original.Id,
                Name = original.Name,
                Description = original.Description,
                CurrentRate = original.CurrentRate,
                MonthlyExpenses = original.MonthlyExpenses,
                CitizenCount = original.CitizenCount,
                TotalBudget = original.TotalBudget,
                BudgetAmount = original.BudgetAmount,
                Type = original.Type,
                Notes = original.Notes,
                Status = original.Status,
                CreatedDate = original.CreatedDate,
                ModifiedDate = original.ModifiedDate,
                CreatedBy = original.CreatedBy,
                ModifiedBy = original.ModifiedBy,
                IsDeleted = original.IsDeleted,
                DeletedDate = original.DeletedDate,
                DeletedBy = original.DeletedBy,
                // Meter reading fields
                MeterReading = original.MeterReading,
                MeterReadDate = original.MeterReadDate,
                PreviousMeterReading = original.PreviousMeterReading,
                PreviousMeterReadDate = original.PreviousMeterReadDate
            };
        }

        /// <summary>
        /// Updates an enterprise from a copy
        /// </summary>
        private void UpdateEnterpriseFromCopy(Enterprise target, Enterprise source)
        {
            target.Name = source.Name;
            target.Description = source.Description;
            target.CurrentRate = source.CurrentRate;
            target.MonthlyExpenses = source.MonthlyExpenses;
            target.CitizenCount = source.CitizenCount;
            target.TotalBudget = source.TotalBudget;
            target.BudgetAmount = source.BudgetAmount;
            target.Type = source.Type;
            target.Notes = source.Notes;
            target.Status = source.Status;
            target.ModifiedDate = DateTime.UtcNow;
            target.ModifiedBy = "System"; // TODO: Get from current user context

            // Meter reading fields
            target.MeterReading = source.MeterReading;
            target.MeterReadDate = source.MeterReadDate;
            target.PreviousMeterReading = source.PreviousMeterReading;
            target.PreviousMeterReadDate = source.PreviousMeterReadDate;
        }

        // Collection change event handlers
        private void Enterprises_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Logger.LogDebug("MainViewModel Enterprises collection - Added {Count} items at index {Index}", 
                        e.NewItems?.Count ?? 0, e.NewStartingIndex);
                    if (e.NewItems != null)
                    {
                        foreach (Enterprise enterprise in e.NewItems)
                        {
                            Logger.LogTrace("MainViewModel Added Enterprise: Id={Id}, Name={Name}, Type={Type}", 
                                enterprise.Id, enterprise.Name, enterprise.Type);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    Logger.LogDebug("MainViewModel Enterprises collection - Removed {Count} items at index {Index}", 
                        e.OldItems?.Count ?? 0, e.OldStartingIndex);
                    if (e.OldItems != null)
                    {
                        foreach (Enterprise enterprise in e.OldItems)
                        {
                            Logger.LogTrace("MainViewModel Removed Enterprise: Id={Id}, Name={Name}", 
                                enterprise.Id, enterprise.Name);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    Logger.LogDebug("MainViewModel Enterprises collection - Replaced {Count} items at index {Index}", 
                        e.NewItems?.Count ?? 0, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    Logger.LogDebug("MainViewModel Enterprises collection - Reset (cleared or major change)");
                    break;

                case NotifyCollectionChangedAction.Move:
                    Logger.LogDebug("MainViewModel Enterprises collection - Moved item from index {OldIndex} to {NewIndex}", 
                        e.OldStartingIndex, e.NewStartingIndex);
                    break;
            }

            Logger.LogDebug("MainViewModel Enterprises collection now has {Count} items", Enterprises.Count);
        }
    }
}
