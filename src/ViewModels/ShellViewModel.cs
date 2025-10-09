using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Shell;
using WileyWidget.ViewModels.Base;

#nullable enable

namespace WileyWidget.ViewModels;

/// <summary>
/// ShellViewModel coordinates high-level navigation for the PolishHost shell.
/// Navigation is now snapshot-driven, enabling rich history metadata, telemetry,
/// and clean separation from the WPF window code-behind.
/// </summary>
public partial class ShellViewModel : AsyncViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly TelemetryClient? _telemetryClient;
    private readonly Stack<NavigationSnapshot> _backStack = new();
    private readonly Stack<NavigationSnapshot> _forwardStack = new();
    private bool _suppressSelectionChange;

    [ActivatorUtilitiesConstructor]
    public ShellViewModel(
        SettingsService settingsService,
        DashboardViewModel dashboardViewModel,
        EnterpriseViewModel enterpriseViewModel,
        BudgetViewModel budgetViewModel,
        UtilityCustomerViewModel utilityCustomerViewModel,
        AIAssistViewModel aiAssistViewModel,
        ToolsViewModel toolsViewModel,
        SettingsViewModel settingsViewModel,
        IDispatcherHelper dispatcherHelper,
        ILogger<ShellViewModel> logger,
        TelemetryClient? telemetryClient = null)
        : this(
            settingsService,
            BuildDefaultNavigation(
                dashboardViewModel,
                enterpriseViewModel,
                budgetViewModel,
                utilityCustomerViewModel,
                aiAssistViewModel,
                toolsViewModel,
                settingsViewModel),
            dispatcherHelper,
            logger,
            telemetryClient)
    {
    }

    private ShellViewModel(
        SettingsService settingsService,
        IEnumerable<NavigationItem> navigationItems,
        IDispatcherHelper dispatcherHelper,
        ILogger<ShellViewModel> logger,
        TelemetryClient? telemetryClient = null)
        : base(dispatcherHelper, logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _telemetryClient = telemetryClient;

        var items = navigationItems?.ToList() ?? throw new ArgumentNullException(nameof(navigationItems));
        NavigationItems = new ObservableCollection<NavigationItem>(items);

        if (NavigationItems.Count == 0)
        {
            Logger.LogWarning("Shell navigation initialized with no items. The shell will load without content.");
        }

        Theme = _settingsService.Current.Theme ?? "FluentDark";
        LoadInitialContent();
    }

    /// <summary>
    /// Navigation entries displayed in the shell sidebar.
    /// </summary>
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    /// <summary>
    /// The active view model currently hosted by the shell.
    /// </summary>
    public ObservableObject? ActiveViewModel => CurrentSnapshot?.ViewModel;

    [ObservableProperty]
    private NavigationSnapshot? currentSnapshot;

    [ObservableProperty]
    private string currentViewName = "Dashboard";

    [ObservableProperty]
    private string currentBreadcrumb = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string theme = "FluentDark";

    [ObservableProperty]
    private NavigationItem? selectedNavigationItem;

    [ObservableProperty]
    private int navigationIndex;

    [ObservableProperty]
    private bool canNavigateBack;

    [ObservableProperty]
    private bool canNavigateForward;

    private void LoadInitialContent()
    {
        var initialItem = NavigationItems.FirstOrDefault();
        if (initialItem is null)
        {
            Logger.LogWarning("Shell navigation list is empty; nothing to display.");
            return;
        }

        ApplySnapshot(initialItem.CreateSnapshot(), NavigationTrigger.Startup, recordHistory: false);
    }

    private static IEnumerable<NavigationItem> BuildDefaultNavigation(
        DashboardViewModel dashboardViewModel,
        EnterpriseViewModel enterpriseViewModel,
        BudgetViewModel budgetViewModel,
        UtilityCustomerViewModel utilityCustomerViewModel,
        AIAssistViewModel aiAssistViewModel,
        ToolsViewModel toolsViewModel,
        SettingsViewModel settingsViewModel)
    {
        yield return CreateNavigationItem("dashboard", "Dashboard", "📊", "Overview and KPIs", dashboardViewModel);
        yield return CreateNavigationItem("enterprises", "Enterprises", "🏢", "Municipal enterprise management", enterpriseViewModel);
        yield return CreateNavigationItem("budget", "Budget", "💰", "Budget planning and analysis", budgetViewModel);
        yield return CreateNavigationItem("customers", "Customers", "👥", "Utility customer management", utilityCustomerViewModel);
        yield return CreateNavigationItem("ai-assistant", "AI Assistant", "🤖", "AI-powered assistance", aiAssistViewModel);
        yield return CreateNavigationItem("tools", "Tools", "🔧", "Administrative tools", toolsViewModel);
        yield return CreateNavigationItem("settings", "Settings", "⚙️", "Application settings", settingsViewModel);
    }

    private static NavigationItem CreateNavigationItem(
        string route,
        string name,
        string icon,
    string description,
    ObservableObject viewModel)
    {
        return new NavigationItem(route, name, icon, description, viewModel);
    }

    private void NavigateToItem(
        NavigationItem navigationItem,
        NavigationTrigger trigger,
        bool recordHistory)
    {
        var snapshot = navigationItem.CreateSnapshot();
        ApplySnapshot(snapshot, trigger, recordHistory);
    }

    private void ApplySnapshot(
        NavigationSnapshot snapshot,
        NavigationTrigger trigger,
        bool recordHistory)
    {
        if (recordHistory && CurrentSnapshot is { } current)
        {
            _backStack.Push(current);
        }

        if (recordHistory)
        {
            _forwardStack.Clear();
        }

        CurrentSnapshot = snapshot;
        CurrentViewName = snapshot.DisplayName;
        CurrentBreadcrumb = snapshot.Breadcrumb.Count == 0
            ? snapshot.DisplayName
            : string.Join(" › ", snapshot.Breadcrumb);

        NavigationIndex++;

        NavigationItem? selected = null;
        foreach (var item in NavigationItems)
        {
            var isMatch = string.Equals(item.Route, snapshot.Route, StringComparison.OrdinalIgnoreCase);
            item.IsSelected = isMatch;
            if (isMatch)
            {
                selected = item;
            }
        }

        try
        {
            _suppressSelectionChange = true;
            SelectedNavigationItem = selected;
        }
        finally
        {
            _suppressSelectionChange = false;
        }

        CanNavigateBack = _backStack.Count > 0;
        CanNavigateForward = _forwardStack.Count > 0;

        Logger.LogDebug("Shell navigation -> {Route} ({Trigger})", snapshot.Route, trigger);
        TrackNavigationTelemetry(snapshot, trigger);
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (_suppressSelectionChange || value is null)
        {
            return;
        }

        NavigateToItem(value, NavigationTrigger.Selection, recordHistory: true);
    }

    [RelayCommand]
    private void NavigateBack()
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        if (CurrentSnapshot is { } current)
        {
            _forwardStack.Push(current);
        }

        var previous = _backStack.Pop();
        ApplySnapshot(previous, NavigationTrigger.Back, recordHistory: false);
        CanNavigateBack = _backStack.Count > 0;
        CanNavigateForward = _forwardStack.Count > 0;
    }

    [RelayCommand]
    private void NavigateForward()
    {
        if (_forwardStack.Count == 0)
        {
            return;
        }

        if (CurrentSnapshot is { } current)
        {
            _backStack.Push(current);
        }

        var next = _forwardStack.Pop();
        ApplySnapshot(next, NavigationTrigger.Forward, recordHistory: false);
        CanNavigateBack = _backStack.Count > 0;
        CanNavigateForward = _forwardStack.Count > 0;
    }

    [RelayCommand]
    private void ClearNavigationHistory()
    {
        _backStack.Clear();
        _forwardStack.Clear();
        NavigationIndex = 0;
        CanNavigateBack = false;
        CanNavigateForward = false;

        Logger.LogDebug("Shell navigation history cleared.");
    }

    [RelayCommand]
    private async Task RefreshCurrentView()
    {
        if (ActiveViewModel is null)
        {
            return;
        }

        try
        {
            StatusMessage = $"Refreshing {CurrentViewName}...";
            IsLoading = true;

            await Task.Delay(100).ConfigureAwait(false);

            StatusMessage = $"{CurrentViewName} refreshed";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh current view {ViewName}", CurrentViewName);
            StatusMessage = $"Failed to refresh {CurrentViewName}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NavigateToDashboard() => NavigateToRoute("dashboard");

    [RelayCommand]
    private void NavigateToEnterprises() => NavigateToRoute("enterprises");

    [RelayCommand]
    private void NavigateToBudget() => NavigateToRoute("budget");

    [RelayCommand]
    private void NavigateToCustomers() => NavigateToRoute("customers");

    [RelayCommand]
    private void NavigateToAIAssistant() => NavigateToRoute("ai-assistant");

    [RelayCommand]
    private void NavigateToTools() => NavigateToRoute("tools");

    [RelayCommand]
    private void NavigateToSettings() => NavigateToRoute("settings");

    public void NavigateToRoute(string route, NavigationTrigger trigger = NavigationTrigger.Command, bool recordHistory = true)
    {
        var target = NavigationItems.FirstOrDefault(
            item => string.Equals(item.Route, route, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            Logger.LogWarning("Navigation route '{Route}' was not found.", route);
            return;
        }

        NavigateToItem(target, trigger, recordHistory);
    }

    private void TrackNavigationTelemetry(NavigationSnapshot snapshot, NavigationTrigger trigger)
    {
        if (_telemetryClient is null)
        {
            return;
        }

        var properties = new Dictionary<string, string>
        {
            ["Route"] = snapshot.Route,
            ["DisplayName"] = snapshot.DisplayName,
            ["Icon"] = snapshot.Icon,
            ["Trigger"] = trigger.ToString(),
            ["Breadcrumb"] = string.Join(">", snapshot.Breadcrumb),
            ["BackStackDepth"] = _backStack.Count.ToString(),
            ["ForwardStackDepth"] = _forwardStack.Count.ToString()
        };

        _telemetryClient.TrackEvent("ShellNavigation", properties);
    }
}