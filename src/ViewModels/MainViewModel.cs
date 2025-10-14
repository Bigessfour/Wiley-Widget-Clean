using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism;
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

            // Initialize commands
            NavigateToDashboardCommand = new RelayCommand(NavigateToDashboard);
            NavigateToEnterprisesCommand = new RelayCommand(NavigateToEnterprises);
            NavigateToAccountsCommand = new RelayCommand(NavigateToAccounts);
            NavigateToBudgetCommand = new RelayCommand(NavigateToBudget);
            RefreshCommand = new RelayCommand(Refresh);
            RefreshAllCommand = new RelayCommand(RefreshAll);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
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

        // Navigation Commands
        public RelayCommand NavigateToDashboardCommand { get; }
        public RelayCommand NavigateToEnterprisesCommand { get; }
        public RelayCommand NavigateToAccountsCommand { get; }
        public RelayCommand NavigateToBudgetCommand { get; }

        // Other Commands
        public RelayCommand RefreshCommand { get; }
        public RelayCommand RefreshAllCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand AddTestEnterpriseCommand { get; }

        // Navigation Methods
        private void NavigateToDashboard()
        {
            regionManager.RequestNavigate("DashboardRegion", "DashboardView");
        }

        private void NavigateToEnterprises()
        {
            regionManager.RequestNavigate("EnterpriseRegion", "EnterpriseView");
        }

        private void NavigateToAccounts()
        {
            regionManager.RequestNavigate("MunicipalAccountRegion", "MunicipalAccountView");
        }

        private void NavigateToBudget()
        {
            regionManager.RequestNavigate("BudgetRegion", "BudgetView");
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
    }
}
