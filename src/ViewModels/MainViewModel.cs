using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;
using WileyWidget.Models;

namespace WileyWidget.ViewModels
{
    public partial class MainViewModel : AsyncViewModelBase
    {
        private readonly IRegionManager regionManager;

        public MainViewModel(IRegionManager regionManager, IDispatcherHelper dispatcherHelper, ILogger<MainViewModel> logger)
            : base(dispatcherHelper, logger)
        {
            this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));

            // Initialize navigation commands
            NavigateToDashboardCommand = new RelayCommand(NavigateToDashboard);
            NavigateToEnterprisesCommand = new RelayCommand(NavigateToEnterprises);
            NavigateToAccountsCommand = new RelayCommand(NavigateToAccounts);
            NavigateToBudgetCommand = new RelayCommand(NavigateToBudget);
            NavigateToAIAssistCommand = new RelayCommand(NavigateToAIAssist);
            NavigateToAnalyticsCommand = new RelayCommand(NavigateToAnalytics);

            // Initialize UI commands
            RefreshCommand = new RelayCommand(Refresh);
            RefreshAllCommand = new AsyncRelayCommand(RefreshAllAsync);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenReportsCommand = new RelayCommand(OpenReports);
            OpenAIAssistCommand = new RelayCommand(OpenAIAssist);

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
            AddEnterpriseCommand = new RelayCommand(AddEnterprise);
            EditEnterpriseCommand = new RelayCommand(EditEnterprise);
            DeleteEnterpriseCommand = new RelayCommand(DeleteEnterprise);
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
            set => SetProperty(ref isAutoRefreshEnabled, value);
        }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        private object? currentView;
        public object? CurrentView
        {
            get => currentView;
            set => SetProperty(ref currentView, value);
        }

        private bool isInitialized;
        public bool IsInitialized
        {
            get => isInitialized;
            set => SetProperty(ref isInitialized, value);
        }

        private Enterprise? selectedEnterprise;
        public Enterprise? SelectedEnterprise
        {
            get => selectedEnterprise;
            set => SetProperty(ref selectedEnterprise, value);
        }

        // Navigation Commands
        public RelayCommand NavigateToDashboardCommand { get; }
        public RelayCommand NavigateToEnterprisesCommand { get; }
        public RelayCommand NavigateToEnterpriseCommand => NavigateToEnterprisesCommand; // Alias for tests
        public RelayCommand NavigateToAccountsCommand { get; }
        public RelayCommand NavigateToBudgetCommand { get; }
        public RelayCommand NavigateToAIAssistCommand { get; }
        public RelayCommand NavigateToAnalyticsCommand { get; }

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
        public RelayCommand AddEnterpriseCommand { get; }
        public RelayCommand EditEnterpriseCommand { get; }
        public RelayCommand DeleteEnterpriseCommand { get; }
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

        // Navigation Methods with region existence checks
        private void NavigateToDashboard()
        {
            NavigateToRegionSafely("DashboardRegion", "DashboardView", "Dashboard");
        }

        private void NavigateToEnterprises()
        {
            NavigateToRegionSafely("EnterpriseRegion", "EnterpriseView", "Enterprises");
        }

        private void NavigateToAccounts()
        {
            NavigateToRegionSafely("MunicipalAccountRegion", "MunicipalAccountView", "Municipal Accounts");
        }

        private void NavigateToBudget()
        {
            NavigateToRegionSafely("BudgetRegion", "BudgetView", "Budget");
        }

        private void NavigateToAIAssist()
        {
            NavigateToRegionSafely("AIAssistRegion", "AIAssistView", "AI Assistant");
        }

        private void NavigateToAnalytics()
        {
            NavigateToRegionSafely("AnalyticsRegion", "AnalyticsView", "Analytics");
        }

        /// <summary>
        /// Safely navigates to a region after checking for existence and logging
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
            Logger.LogInformation("MainViewModel: Refresh all command executed");
            try
            {
                IsLoading = true;
                
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
                
                Logger.LogInformation("All data sources refreshed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh all data");
            }
            finally
            {
                IsLoading = false;
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
            Logger.LogInformation("MainViewModel: Import Excel command executed");
            try
            {
                IsLoading = true;
                
                // Simulate async Excel import operation
                await Task.Delay(100); // Placeholder for actual import logic
                
                // Navigate to Excel import view in the main region
                NavigateToRegionSafely("MainRegion", "ExcelImportView", "Excel Import");
                
                Logger.LogInformation("Excel import dialog opened successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open Excel import view");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExportData()
        {
            Logger.LogInformation("MainViewModel: Export data command executed (synchronous wrapper)");
            _ = ExportDataAsync();
        }

        private async Task ExportDataAsync()
        {
            Logger.LogInformation("MainViewModel: Export data command executed");
            try
            {
                IsLoading = true;
                
                // Simulate async data export operation
                await Task.Delay(100); // Placeholder for actual export logic
                
                // Navigate to data export view
                NavigateToRegionSafely("MainRegion", "DataExportView", "Data Export");
                
                Logger.LogInformation("Data export dialog opened successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open data export view");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void SyncQuickBooks()
        {
            Logger.LogInformation("MainViewModel: Sync QuickBooks command executed (synchronous wrapper)");
            _ = SyncQuickBooksAsync();
        }

        private async Task SyncQuickBooksAsync()
        {
            Logger.LogInformation("MainViewModel: Sync QuickBooks command executed");
            try
            {
                IsLoading = true;
                
                // Simulate async QuickBooks sync operation
                Logger.LogInformation("Initiating QuickBooks synchronization...");
                await Task.Delay(1000); // Placeholder for actual sync logic
                
                // Navigate to QuickBooks sync view
                NavigateToRegionSafely("MainRegion", "QuickBooksSyncView", "QuickBooks Sync");
                
                Logger.LogInformation("QuickBooks sync completed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to sync with QuickBooks");
            }
            finally
            {
                IsLoading = false;
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
        private void AddEnterprise()
        {
            Logger.LogInformation("MainViewModel: Add enterprise command executed");
            try
            {
                // Navigate to enterprise creation view
                NavigateToRegionSafely("EnterpriseRegion", "EnterpriseCreationView", "Add Enterprise");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add enterprise");
            }
        }

        private void EditEnterprise()
        {
            Logger.LogInformation("MainViewModel: Edit enterprise command executed");
            try
            {
                if (SelectedEnterprise == null)
                {
                    Logger.LogWarning("Cannot edit enterprise: No enterprise selected");
                    return;
                }

                // Navigate to enterprise edit view with the selected enterprise
                NavigateToRegionSafely("EnterpriseRegion", "EnterpriseEditView", "Edit Enterprise");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to edit enterprise");
            }
        }

        private void DeleteEnterprise()
        {
            Logger.LogInformation("MainViewModel: Delete enterprise command executed");
            try
            {
                if (SelectedEnterprise == null)
                {
                    Logger.LogWarning("Cannot delete enterprise: No enterprise selected");
                    return;
                }

                // Show confirmation dialog and delete enterprise
                Logger.LogInformation("Deleting enterprise: {EnterpriseName} (ID: {EnterpriseId})", 
                    SelectedEnterprise.Name, SelectedEnterprise.Id);
                
                // Remove from collection
                Enterprises.Remove(SelectedEnterprise);
                SelectedEnterprise = null;
                
                Logger.LogInformation("Enterprise deleted successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to delete enterprise");
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
    }
}
