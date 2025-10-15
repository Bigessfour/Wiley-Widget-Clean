using System;
using System.Collections.ObjectModel;
using System.Linq;
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
            RefreshAllCommand = new RelayCommand(RefreshAll);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenReportsCommand = new RelayCommand(OpenReports);
            OpenAIAssistCommand = new RelayCommand(OpenAIAssist);

            // Initialize data commands  
            ImportExcelCommand = new RelayCommand(ImportExcel);
            ExportDataCommand = new RelayCommand(ExportData);
            SyncQuickBooksCommand = new RelayCommand(SyncQuickBooks);

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
        public RelayCommand RefreshAllCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand OpenReportsCommand { get; }
        public RelayCommand OpenAIAssistCommand { get; }

        // Data Commands
        public RelayCommand ImportExcelCommand { get; }
        public RelayCommand ExportDataCommand { get; }
        public RelayCommand SyncQuickBooksCommand { get; }

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
            // Implement refresh logic
            Logger.LogInformation("MainViewModel: Refresh command executed");
        }

        private void RefreshAll()
        {
            // Implement refresh all logic
            Logger.LogInformation("MainViewModel: Refresh all command executed");
        }

        private void OpenSettings()
        {
            // Implement open settings logic
            Logger.LogInformation("MainViewModel: Open settings command executed");
        }

        private void AddTestEnterprise()
        {
            // Implement add test enterprise logic
            Logger.LogInformation("MainViewModel: Add test enterprise command executed");
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
            Logger.LogInformation("MainViewModel: Import Excel command executed");
            // TODO: Implement Excel import functionality
        }

        private void ExportData()
        {
            Logger.LogInformation("MainViewModel: Export data command executed");
            // TODO: Implement data export functionality
        }

        private void SyncQuickBooks()
        {
            Logger.LogInformation("MainViewModel: Sync QuickBooks command executed");
            // TODO: Implement QuickBooks synchronization
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
            // TODO: Implement budget creation functionality
        }

        private void ImportBudget()
        {
            Logger.LogInformation("MainViewModel: Import budget command executed");
            // TODO: Implement budget import functionality
        }

        private void ExportBudget()
        {
            Logger.LogInformation("MainViewModel: Export budget command executed");
            // TODO: Implement budget export functionality
        }

        private void ShowBudgetAnalysis()
        {
            Logger.LogInformation("MainViewModel: Show budget analysis command executed");
            // TODO: Navigate to budget analysis view
        }

        private void ShowRateCalculator()
        {
            Logger.LogInformation("MainViewModel: Show rate calculator command executed");
            // TODO: Navigate to rate calculator view
        }

        // Enterprise Command Implementations
        private void AddEnterprise()
        {
            Logger.LogInformation("MainViewModel: Add enterprise command executed");
            // TODO: Implement enterprise addition functionality
        }

        private void EditEnterprise()
        {
            Logger.LogInformation("MainViewModel: Edit enterprise command executed");
            // TODO: Implement enterprise editing functionality
        }

        private void DeleteEnterprise()
        {
            Logger.LogInformation("MainViewModel: Delete enterprise command executed");
            // TODO: Implement enterprise deletion functionality
        }

        private void ManageServiceCharges()
        {
            Logger.LogInformation("MainViewModel: Manage service charges command executed");
            // TODO: Navigate to service charges management view
        }

        private void ManageUtilityBills()
        {
            Logger.LogInformation("MainViewModel: Manage utility bills command executed");
            // TODO: Navigate to utility bills management view
        }

        // Report Command Implementations
        private void GenerateFinancialSummary()
        {
            Logger.LogInformation("MainViewModel: Generate financial summary command executed");
            // TODO: Implement financial summary report generation
        }

        private void GenerateBudgetVsActual()
        {
            Logger.LogInformation("MainViewModel: Generate budget vs actual command executed");
            // TODO: Implement budget vs actual report generation
        }

        private void GenerateEnterprisePerformance()
        {
            Logger.LogInformation("MainViewModel: Generate enterprise performance command executed");
            // TODO: Implement enterprise performance report generation
        }

        private void CreateCustomReport()
        {
            Logger.LogInformation("MainViewModel: Create custom report command executed");
            // TODO: Navigate to custom report builder
        }

        private void ShowSavedReports()
        {
            Logger.LogInformation("MainViewModel: Show saved reports command executed");
            // TODO: Navigate to saved reports view
        }
    }
}
