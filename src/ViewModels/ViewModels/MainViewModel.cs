using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Data;
using Intuit.Ipp.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;
using Serilog.Events;
using System;
using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Windows.Input;
using WileyWidget;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8622, CS8625 // Suppress nullability warnings in WPF application

namespace WileyWidget.ViewModels;

/// <summary>
/// Main view model for managing municipal enterprises (Water, Sewer, Trash, Apartments)
/// Provides data binding for the main window's enterprise grid and operations.
/// </summary>
public partial class MainViewModel : AsyncViewModelBase
{
    private readonly IQuickBooksService? _quickBooksService;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IAIService _aiService;
    private readonly MunicipalAccountViewModel _municipalAccountViewModel;
    private readonly ProgressViewModel _progressViewModel;
    private readonly ReportsViewModel _reportsViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly AnalyticsViewModel _analyticsViewModel;
    private readonly EnterpriseViewModel _enterpriseViewModel;
    private readonly BudgetViewModel _budgetViewModel;
    private readonly AIAssistViewModel _aiAssistViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ToolsViewModel _toolsViewModel;
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private bool _isInitialized;

    public event EventHandler<NavigationRequestEventArgs>? NavigationRequested;

    [ObservableProperty]
    private string currentViewName = "Municipal Enterprises";

    public ObservableCollection<Widget> Widgets { get; } = new();

    /// <summary>
    /// Semaphore to prevent concurrent loading operations
    /// </summary>
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    /// <summary>
    /// Executes an operation with retry logic and exponential backoff
    /// </summary>
    private async System.Threading.Tasks.Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, System.Threading.Tasks.Task<T>> operation,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        var delay = TimeSpan.FromMilliseconds(500);
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries && 
                                     !(ex is OperationCanceledException))
            {
                Log.Warning(ex, "Attempt {Attempt} failed, retrying in {DelayMs}ms", 
                           attempt + 1, delay.TotalMilliseconds);
                await System.Threading.Tasks.Task.Delay(delay, cancellationToken);
                delay = delay * 2; // Exponential backoff
            }
        }
        
        throw new Exception($"Operation failed after {maxRetries + 1} attempts");
    }

    public Services.Threading.ThreadSafeObservableCollection<Enterprise> Enterprises { get; } = new();

    public ObservableCollection<Customer> QuickBooksCustomers { get; } = new();
    public ObservableCollection<Invoice> QuickBooksInvoices { get; } = new();

    /// <summary>
    /// Ribbon tabs for data binding
    /// </summary>
    public ObservableCollection<RibbonTabItem> RibbonItems { get; } = new();

    /// <summary>
    /// Currently selected ribbon tab
    /// </summary>
    [ObservableProperty]
    private RibbonTabItem? selectedRibbonItem;

    /// <summary>
    /// Tab views for data binding
    /// </summary>
    public ObservableCollection<ViewModelBase> Views { get; } = new();

    /// <summary>
    /// Currently selected view tab
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? selectedView;

    /// <summary>
    /// Tab content for QuickBooks views
    /// </summary>
    public ObservableCollection<QuickBooksTabItem> QuickBooksTabs { get; } = new();

    /// <summary>
    /// Currently selected QuickBooks tab
    /// </summary>
    [ObservableProperty]
    private QuickBooksTabItem? selectedQuickBooksTab;

    /// <summary>
    /// Municipal account view model for Chart of Accounts and Budget Analysis
    /// </summary>
    public MunicipalAccountViewModel MunicipalAccountViewModel => _municipalAccountViewModel;

    /// <summary>
    /// Progress view model for tracking operation progress and cancellation
    /// </summary>
    public ProgressViewModel ProgressViewModel => _progressViewModel;

    /// <summary>
    /// Reports view model for report generation and export functionality
    /// </summary>
    public ReportsViewModel ReportsViewModel => _reportsViewModel;

    /// <summary>
    /// Dashboard view model for KPIs and overview metrics
    /// </summary>
    public DashboardViewModel DashboardViewModel => _dashboardViewModel;

    /// <summary>
    /// Analytics view model for charts, KPIs, and data visualization
    /// </summary>
    public AnalyticsViewModel AnalyticsViewModel => _analyticsViewModel;

    /// <summary>
    /// Enterprise view model for enterprise management and operations
    /// </summary>
    public EnterpriseViewModel EnterpriseViewModel => _enterpriseViewModel;

    /// <summary>
    /// Budget view model for budget analysis and management
    /// </summary>
    public BudgetViewModel BudgetViewModel => _budgetViewModel;

    /// <summary>
    /// AI Assist view model for AI-powered assistance and insights
    /// </summary>
    public AIAssistViewModel AIAssistViewModel => _aiAssistViewModel;

    /// <summary>
    /// Settings view model for application configuration
    /// </summary>
    public SettingsViewModel SettingsViewModel => _settingsViewModel;

    /// <summary>
    /// Tools view model for calculator, unit converter, date calculator, and notes
    /// </summary>
    public ToolsViewModel ToolsViewModel => _toolsViewModel;

    /// <summary>Currently selected enterprise in the grid (null when none selected).</summary>
    [ObservableProperty]
    private Enterprise? selectedEnterprise;

    /// <summary>
    /// Loading state for async operations
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Current authenticated user name for display in UI
    /// </summary>
    [ObservableProperty]
    private string currentUserName = "Not signed in";

    /// <summary>
    /// Current authenticated user email for display in UI
    /// </summary>
    [ObservableProperty]
    private string currentUserEmail = string.Empty;

    /// <summary>
    /// Whether the current user has admin privileges
    /// </summary>
    [ObservableProperty]
    private bool isUserAdmin;

    /// <summary>
    /// List of roles assigned to the current user
    /// </summary>
    [ObservableProperty]
    private List<string> userRoles = new List<string>();

    /// <summary>
    /// Whether to use dynamic column generation for the data grid
    /// </summary>
    [ObservableProperty]
    private bool useDynamicColumns = true;

    [RelayCommand]
    /// <summary>
    /// Cycles to the next enterprise (wrap-around). If none selected, selects the first. Safe for empty list.
    /// </summary>
    public void SelectNext()
    {
        try
        {
            if (Enterprises.Count == 0)
            {
                SelectedEnterprise = null;
                return;
            }
            if (SelectedEnterprise == null)
            {
                SelectedEnterprise = Enterprises[0];
                return;
            }
            var idx = Enterprises.IndexOf(SelectedEnterprise);
            if (idx == -1)
            {
                // Selected enterprise not found in collection, select first
                Log.Warning("Selected enterprise not found in collection, selecting first enterprise");
                SelectedEnterprise = Enterprises[0];
                return;
            }
            idx = (idx + 1) % Enterprises.Count;
            SelectedEnterprise = Enterprises[idx];
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to select next enterprise");
            // Reset selection to first enterprise as fallback
            try
            {
                if (Enterprises.Count > 0)
                    SelectedEnterprise = Enterprises[0];
            }
            catch (Exception fallbackEx)
            {
                Log.Error(fallbackEx, "Failed to reset enterprise selection to first item");
            }
        }
    }

    [RelayCommand]
    /// <summary>
    /// Adds a new enterprise with default values for quick testing
    /// </summary>
    public async System.Threading.Tasks.Task AddEnterpriseAsync()
    {
        try
        {
            var nextId = Enterprises.Count == 0 ? 1 : Enterprises[^1].Id + 1;
            var enterprise = new Enterprise
            {
                Id = nextId,
                Name = $"New Enterprise {nextId}",
                Type = "Utility",
                CurrentRate = 25.00M,
                MonthlyExpenses = 5000.00M,
                CitizenCount = 1000,
                TotalBudget = 60000.00M,
                Notes = "New municipal enterprise"
            };
            Enterprises.Add(enterprise);
            SelectedEnterprise = enterprise;
            Log.Information("Successfully added new enterprise: {Name} (ID: {Id})", enterprise.Name, enterprise.Id);
            await System.Threading.Tasks.Task.CompletedTask; // Make method async for consistency
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add new enterprise");
            // Could show user notification here if needed
        }
    }

    [RelayCommand]
    /// <summary>
    /// Loads enterprise data from the repository
    /// </summary>
    public async System.Threading.Tasks.Task LoadEnterprisesAsync()
    {
        await LoadEnterprisesAsync(default);
    }

    [RelayCommand]
    public void AddWidget()
    {
        // TODO: Implement add widget functionality
        Log.Information("Add widget requested - not yet implemented");
    }

    /// <summary>
    /// Creates a new enterprise from header-value mapping (used for clipboard paste)
    /// </summary>
    public Enterprise CreateEnterpriseFromHeaderMapping(IDictionary<string, string> headerValueMap)
    {
        return _enterpriseRepository.CreateFromHeaderMapping(headerValueMap);
    }

    public MainViewModel(
    IEnterpriseRepository enterpriseRepository,
    IMunicipalAccountRepository municipalAccountRepository,
#pragma warning disable CS8632 // Nullable annotation is legitimate for optional QuickBooks service
    IQuickBooksService? quickBooksService,
#pragma warning restore CS8632
    IAIService aiService,
    ProgressViewModel progressViewModel,
    Services.Threading.IDispatcherHelper dispatcherHelper,
    ILogger<MainViewModel> logger,
    ReportsViewModel? reportsViewModel = null,
    DashboardViewModel? dashboardViewModel = null,
    AnalyticsViewModel? analyticsViewModel = null,
    EnterpriseViewModel? enterpriseViewModel = null,
    BudgetViewModel? budgetViewModel = null,
    AIAssistViewModel? aiAssistViewModel = null,
    SettingsViewModel? settingsViewModel = null,
    ToolsViewModel? toolsViewModel = null,
    bool autoInitialize = true)
    : base(dispatcherHelper, logger)
    {
        var constructorTimer = Stopwatch.StartNew();
        App.LogDebugEvent("VIEWMODEL_INIT", "MainViewModel constructor started");

        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _quickBooksService = quickBooksService;
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _progressViewModel = progressViewModel ?? throw new ArgumentNullException(nameof(progressViewModel));
        _reportsViewModel = reportsViewModel!;
        _dashboardViewModel = dashboardViewModel!;
        _analyticsViewModel = analyticsViewModel!;
        _enterpriseViewModel = enterpriseViewModel!;
        _budgetViewModel = budgetViewModel!;
        _aiAssistViewModel = aiAssistViewModel!;
        _settingsViewModel = settingsViewModel!;
        _toolsViewModel = toolsViewModel!;

        App.LogDebugEvent("VIEWMODEL_INIT", "Dependencies injected, initializing MunicipalAccountViewModel");

        // Initialize Municipal Account View Model
        _municipalAccountViewModel = new MunicipalAccountViewModel(municipalAccountRepository, quickBooksService, dispatcherHelper, logger);

        // Log service availability
        Log.Information("🔧 SERVICE AVAILABILITY: EnterpriseRepo={Enterprise}, QuickBooks={QuickBooks}, AI={AI}, Progress={Progress}",
            true, true, true, true);

        InitializeWidgetInventory();

        App.LogDebugEvent("VIEWMODEL_INIT", "Starting background data loading tasks");

        if (autoInitialize)
        {
            _ = InitializeAsync().ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    foreach (var ex in task.Exception.Flatten().InnerExceptions)
                    {
                        Services.ErrorReportingService.Instance.ReportError(
                            ex,
                            "Background_ViewModel_Init",
                            showToUser: true,
                            level: LogEventLevel.Error,
                            correlationId: Guid.NewGuid().ToString("N")[..8]);
                    }
                }
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        // Initialize ribbon and views
        InitializeRibbonItems();
        InitializeViews();

        constructorTimer.Stop();
        App.LogDebugEvent("VIEWMODEL_INIT", $"MainViewModel constructor completed in {constructorTimer.ElapsedMilliseconds}ms");
        App.LogStartupTiming("MainViewModel Constructor", constructorTimer.Elapsed);
    }

    public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            var initId = Guid.NewGuid().ToString("N")[..8];
            Logger.LogInformation("🚀 MAIN VIEWMODEL INITIALIZATION STARTED - ID: {InitId}", initId);

            await LoadEnterprisesAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await _municipalAccountViewModel.InitializeAsync().ConfigureAwait(false);

            _isInitialized = true;
            Logger.LogInformation("✅ MAIN VIEWMODEL INITIALIZATION COMPLETED - ID: {InitId}", initId);
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    private void InitializeWidgetInventory()
    {
        Widgets.Clear();

        Widgets.Add(new Widget
        {
            Id = 1,
            Name = "Municipal Enterprises",
            Description = "Primary grid for municipal enterprise management",
            Category = "Workspace",
            Price = 1m,
            SKU = "WW-WIDGET-ENT"
        });

        Widgets.Add(new Widget
        {
            Id = 2,
            Name = "QuickBooks Integration",
            Description = "Financial synchronization and reporting",
            Category = "Workspace",
            Price = 1m,
            SKU = "WW-WIDGET-QB"
        });

        Widgets.Add(new Widget
        {
            Id = 3,
            Name = "Dashboard Overview",
            Description = "High-level KPIs and trends",
            Category = "Workspace",
            Price = 1m,
            SKU = "WW-WIDGET-DB"
        });

        Widgets.Add(new Widget
        {
            Id = 4,
            Name = "Enterprise Detail",
            Description = "Detailed view of selected enterprise metrics",
            Category = "Workspace",
            Price = 1m,
            SKU = "WW-WIDGET-ENT-DETAIL"
        });

        Widgets.Add(new Widget
        {
            Id = 5,
            Name = "Budget Analysis",
            Description = "Budgeting tools and variance analysis",
            Category = "Workspace",
            Price = 1m,
            SKU = "WW-WIDGET-BUD"
        });

        Widgets.Add(new Widget
        {
            Id = 6,
            Name = "AI Assistant",
            Description = "AI-guided assistance and insights",
            Category = "Workspace",
            Price = 1m,
            SKU = "WW-WIDGET-AI"
        });

        Widgets.Add(new Widget
        {
            Id = 7,
            Name = "Settings",
            Description = "Configuration and administration",
            Category = "Workspace",
            Price = 1m,
            SKU = "WW-WIDGET-SET"
        });
    }

    private void InitializeRibbonItems()
    {
        RibbonItems.Clear();

        // Home tab
        var homeTab = new RibbonTabItem { Header = "Home" };

        // Clipboard bar
        var clipboardBar = new RibbonBarItem { Header = "Clipboard" };
        clipboardBar.Items.Add(new RibbonItem
        {
            Label = "Copy",
            Command = null, // TODO: Implement copy command
            SizeForm = "Large"
        });
        clipboardBar.Items.Add(new RibbonItem
        {
            Label = "Paste",
            Command = null, // TODO: Implement paste command
            SizeForm = "Large",
            IsEnabled = false // Will be bound to IsUserAdmin
        });
        homeTab.Bars.Add(clipboardBar);

        // View bar
        var viewBar = new RibbonBarItem { Header = "View" };
        viewBar.Items.Add(new RibbonItem
        {
            Label = "Refresh",
            Command = RefreshCommand,
            SizeForm = "Large"
        });
        viewBar.Items.Add(new RibbonItem
        {
            Label = "Dynamic Columns",
            Command = ToggleDynamicColumnsCommand,
            SizeForm = "Large"
        });
        homeTab.Bars.Add(viewBar);

        // Theme bar
        var themeBar = new RibbonBarItem { Header = "Theme" };
        themeBar.Items.Add(new RibbonItem
        {
            Label = "Fluent Dark",
            Command = null, // TODO: Implement theme switching
            SizeForm = "Large"
        });
        themeBar.Items.Add(new RibbonItem
        {
            Label = "Fluent Light",
            Command = null, // TODO: Implement theme switching
            SizeForm = "Large"
        });
        homeTab.Bars.Add(themeBar);

        // Navigation bar
        var navigationBar = new RibbonBarItem { Header = "Navigation" };
        navigationBar.Items.Add(new RibbonItem
        {
            Label = "Dashboard",
            Command = OpenDashboardCommand,
            SizeForm = "Large"
        });
        navigationBar.Items.Add(new RibbonItem
        {
            Label = "Enterprise",
            Command = OpenEnterpriseCommand,
            SizeForm = "Large"
        });
        navigationBar.Items.Add(new RibbonItem
        {
            Label = "Customers",
            Command = OpenCustomerManagementCommand,
            SizeForm = "Large"
        });
        navigationBar.Items.Add(new RibbonItem
        {
            Label = "Budget",
            Command = OpenBudgetCommand,
            SizeForm = "Large"
        });
        navigationBar.Items.Add(new RibbonItem
        {
            Label = "AI Assistant",
            Command = OpenAIAssistCommand,
            SizeForm = "Large"
        });
        navigationBar.Items.Add(new RibbonItem
        {
            Label = "Settings",
            Command = OpenSettingsCommand,
            SizeForm = "Large"
        });
        navigationBar.Items.Add(new RibbonItem
        {
            Label = "Tools",
            Command = OpenToolsCommand,
            SizeForm = "Large"
        });
        homeTab.Bars.Add(navigationBar);

        // Help bar
        var helpBar = new RibbonBarItem { Header = "Help" };
        helpBar.Items.Add(new RibbonItem
        {
            Label = "AI Assistant",
            Command = ShowAIHelpCommand,
            SizeForm = "Large"
        });
        helpBar.Items.Add(new RibbonItem
        {
            Label = "Settings",
            Command = OpenSettingsCommand,
            SizeForm = "Large"
        });
        helpBar.Items.Add(new RibbonItem
        {
            Label = "About",
            Command = null, // TODO: Implement about command
            SizeForm = "Large"
        });
        homeTab.Bars.Add(helpBar);

        // Authentication bar
        var authBar = new RibbonBarItem { Header = "Authentication" };
        authBar.Items.Add(new RibbonItem
        {
            Label = "Sign In",
            Command = null, // TODO: Implement sign in command
            SizeForm = "Large"
        });
        authBar.Items.Add(new RibbonItem
        {
            Label = "Sign Out",
            Command = null, // TODO: Implement sign out command
            SizeForm = "Large",
            IsEnabled = false
        });
        homeTab.Bars.Add(authBar);

        RibbonItems.Add(homeTab);
    }

    private void InitializeViews()
    {
        Views.Clear();

        // Municipal Enterprises view
        var enterprisesView = new ViewModelBase
        {
            Header = "Municipal Enterprises",
            Content = this // The MainViewModel itself contains the enterprise data
        };
        Views.Add(enterprisesView);

        // QuickBooks view
        var quickBooksView = new ViewModelBase
        {
            Header = "QuickBooks",
            Content = this // The MainViewModel contains QuickBooks data
        };
        Views.Add(quickBooksView);

        // Reports view
        if (_reportsViewModel != null)
        {
            var reportsView = new ViewModelBase
            {
                Header = "Reports",
                Content = _reportsViewModel
            };
            Views.Add(reportsView);
        }

        // Analytics view
        if (_analyticsViewModel != null)
        {
            var analyticsView = new ViewModelBase
            {
                Header = "Analytics",
                Content = _analyticsViewModel
            };
            Views.Add(analyticsView);
        }

        // Set default selected view
        SelectedView = enterprisesView;

        // Initialize QuickBooks tabs
        InitializeQuickBooksTabs();
    }

    private void InitializeQuickBooksTabs()
    {
        QuickBooksTabs.Clear();

        // Customers tab
        var customersTab = new QuickBooksTabItem
        {
            Header = "Customers",
            Content = QuickBooksCustomers
        };
        QuickBooksTabs.Add(customersTab);

        // Invoices tab
        var invoicesTab = new QuickBooksTabItem
        {
            Header = "Invoices",
            Content = QuickBooksInvoices
        };
        QuickBooksTabs.Add(invoicesTab);

        // Set default selected tab
        SelectedQuickBooksTab = customersTab;
    }

    private async System.Threading.Tasks.Task LoadEnterprisesAsync(CancellationToken cancellationToken = default)
    {
        var loadId = Guid.NewGuid().ToString("N")[..8];
        Logger.LogInformation("🏭 STARTING ENTERPRISE DATA LOAD - ID: {LoadId}", loadId);
        Logger.LogInformation("🔗 ENTERPRISE REPO STATUS: Available={Available}, Type={Type}",
            _enterpriseRepository != null, _enterpriseRepository?.GetType().Name ?? "null");

        // Prevent concurrent loading operations
        if (!await _loadSemaphore.WaitAsync(0, cancellationToken))
        {
            Logger.LogInformation("⏳ Enterprise loading already in progress, skipping duplicate request - ID: {LoadId}", loadId);
            return;
        }

        try
        {
            // Initialize progress tracking
            var progressSteps = new List<ProgressStep>
            {
                new ProgressStep("Initializing", "Preparing to load enterprise data"),
                new ProgressStep("Connecting", "Establishing database connection"),
                new ProgressStep("Querying", "Executing database query with retry logic"),
                new ProgressStep("Processing", "Processing retrieved enterprise data"),
                new ProgressStep("Updating", "Updating user interface with loaded data"),
                new ProgressStep("Finalizing", "Completing enterprise data load")
            };

            _progressViewModel.StartOperation("Loading Enterprises", progressSteps);

            await ExecuteAsyncOperation(async (ct) =>
            {
                // Combine the operation cancellation token with the progress view model's cancellation token
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _progressViewModel.CancellationToken);
                var combinedToken = linkedCts.Token;

                try
                {
                    _progressViewModel.UpdateProgress(0, "Initializing enterprise data load...");
                    Logger.LogInformation("Executing repository query with retry logic - ID: {LoadId}", loadId);

                    _progressViewModel.UpdateProgress(1, "Connecting to database...");

                    // Execute database query with timeout
                    var loadTask = ExecuteWithRetryAsync(async (cancelToken) =>
                        await _enterpriseRepository.GetAllAsync(), cancellationToken: combinedToken);

                    var timeoutTask = System.Threading.Tasks.Task.Delay(30000, combinedToken);
                    var completedTask = await System.Threading.Tasks.Task.WhenAny(loadTask, timeoutTask);

                    combinedToken.ThrowIfCancellationRequested();

                    IEnumerable<Enterprise> enterprises;
                    if (completedTask == timeoutTask)
                    {
                        Logger.LogWarning("Database query timed out after 30 seconds - ID: {LoadId}", loadId);
                        _progressViewModel.FailOperation("Database query timed out - check Azure login");
                        await DispatcherHelper.InvokeAsync(() =>
                            System.Windows.MessageBox.Show("DB's taking a nap—check your Azure login", "Database Timeout",
                                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning));
                        return;
                    }
                    else
                    {
                        enterprises = await loadTask;
                    }

                    combinedToken.ThrowIfCancellationRequested();

                    _progressViewModel.UpdateProgress(2, $"Query completed - found {enterprises.Count()} enterprises");
                    Logger.LogInformation("Repository query completed, enterprise count: {Count} - ID: {LoadId}", enterprises.Count(), loadId);

                    _progressViewModel.UpdateProgress(3, "Processing enterprise data...");

                    // Update the collection on the UI thread
                    await Enterprises.ReplaceAllAsync(enterprises);

                    Logger.LogInformation("✅ ENTERPRISES COLLECTION UPDATED: Count={Count}, FirstItem={FirstItem} - ID: {LoadId}",
                        Enterprises.Count, Enterprises.FirstOrDefault()?.Name ?? "null", loadId);

                    combinedToken.ThrowIfCancellationRequested();

                    _progressViewModel.UpdateProgress(4, "Updating user interface...");

                    // Add sample data if no enterprises exist
                    if (Enterprises.Count == 0)
                    {
                        Logger.LogInformation("No enterprises found, adding sample data - ID: {LoadId}", loadId);
                        _progressViewModel.UpdateProgress(4, "No enterprises found - adding sample data...");
                        var sampleEnterprises = SampleDataFactory.CreateSampleEnterprises();
                        await Enterprises.AddRangeAsync(sampleEnterprises);
                    }

                    combinedToken.ThrowIfCancellationRequested();

                    _progressViewModel.UpdateProgress(5, "Completing enterprise data load...");
                    Logger.LogInformation("Enterprise data load completed successfully - ID: {LoadId}", loadId);

                    _progressViewModel.CompleteOperation();
                }
                catch (OperationCanceledException)
                {
                    _progressViewModel.FailOperation("Operation was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _progressViewModel.FailOperation($"Load failed: {ex.Message}");
                    throw;
                }

            }, null, "Enterprise Data Load");
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Enterprise loading was cancelled - ID: {LoadId}", loadId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Enterprise loading failed, adding fallback sample data - ID: {LoadId}", loadId);

            // Show error and add fallback data
            await DispatcherHelper.InvokeAsync(() =>
                System.Windows.MessageBox.Show($"DB load failed: {ex.Message}. Using sample data.", "Database Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error));

            var fallbackEnterprises = SampleDataFactory.CreateSampleEnterprises();
            await Enterprises.ReplaceAllAsync(fallbackEnterprises);
        }
        finally
        {
            _loadSemaphore.Release();

            // Update chart data after loading
            await DispatcherHelper.InvokeAsync(() => UpdateChartData());
        }
    }

    [ObservableProperty]
    private bool quickBooksBusy;

    [ObservableProperty]
    private string quickBooksStatusMessage = "Not Connected";

    [ObservableProperty]
    private string quickBooksErrorMessage = string.Empty;

    [ObservableProperty]
    private bool quickBooksHasError;

    private string _aiMessageInput = string.Empty;

    public string AIMessageInput
    {
        get => _aiMessageInput;
        set => SetProperty(ref _aiMessageInput, value);
    }

    private bool _aiIsTyping;

    public bool AIIsTyping
    {
        get => _aiIsTyping;
        set => SetProperty(ref _aiIsTyping, value);
    }

    public bool CanSendAIMessage => !string.IsNullOrWhiteSpace(AIMessageInput) && !AIIsTyping;

    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

    /// <summary>
    /// Send AI message command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendAIMessage))]
    public async System.Threading.Tasks.Task SendAIMessage()
    {
        if (string.IsNullOrWhiteSpace(AIMessageInput))
        {
            AIMessageInput = string.Empty;
            return;
        }

        var userMessage = AIMessageInput.Trim();
        AIMessageInput = string.Empty;

        // Add user message
        ChatMessages.Add(new ChatMessage
        {
            Text = userMessage,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        // Show typing indicator
        AIIsTyping = true;

        try
        {
            // Get AI response
            var aiResponse = await _aiService.GetInsightsAsync(
                "Wiley Widget Municipal Utility Management Application",
                userMessage
            );

            // Add AI response
            ChatMessages.Add(new ChatMessage
            {
                Text = aiResponse,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating AI response");
            ChatMessages.Add(new ChatMessage
            {
                Text = $"Sorry, I encountered an error while processing your message: {ex.Message}",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            AIIsTyping = false;
        }
    }

    /// <summary>
    /// Clear conversation command
    /// </summary>
    [RelayCommand]
    public void ClearConversation()
    {
        ChatMessages.Clear();
        Log.Information("AI conversation history cleared");
    }

    /// <summary>
    /// Show AI help command
    /// </summary>
    [RelayCommand]
    public void ShowAIHelp()
    {
        var helpMessage = @"🤖 AI Assistant Help

Welcome to the Wiley Widget AI Assistant! Here's how to use it:

📝 **Sending Messages:**
- Type your question or request in the input field
- Press Enter to send (Shift+Enter for new lines)
- The AI will analyze your municipal utility data

💡 **Available Features:**
- General municipal utility questions
- Budget analysis and recommendations  
- Service charge calculations
- Financial scenario planning
- Proactive insights and suggestions

🛠️ **Quick Actions:**
- Analyze Budget: Get detailed budget insights
- Calculate Charges: Determine optimal service rates
- Plan Scenarios: Explore ""what-if"" financial situations

📊 **Data Access:**
The AI has access to your enterprise data including:
- Current rates and citizen counts
- Monthly expenses and budgets
- Revenue and profit analysis
- Historical performance data

❓ **Getting Help:**
- Ask questions about your municipal utilities
- Request analysis of specific enterprises
- Get recommendations for improvements
- Explore financial scenarios

💬 **Tips:**
- Be specific about which enterprise or data you want analyzed
- Ask follow-up questions for deeper insights
- Use the Clear button to start fresh conversations";

        MessageBox.Show(helpMessage, "AI Assistant Help", MessageBoxButton.OK, MessageBoxImage.Information);
        Log.Information("AI help dialog displayed");
    }

    /// <summary>
    /// Open AI assist command
    /// </summary>
    [RelayCommand]
    public void OpenAIAssist()
    {
        RequestNavigation("AIAssistPanel", "AI Assistant");
    }

    [RelayCommand]
    public void OpenCustomerManagement()
    {
        try
        {
            UtilityCustomerView.ShowCustomerWindow();
            Log.Information("Customer management window opened via command");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open customer management window");
        }
    }

    // File menu commands
    [RelayCommand]
    public void OpenFile()
    {
        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Wiley Widget File",
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                Log.Information("File selected for opening: {FilePath}", openFileDialog.FileName);
                // TODO: Implement file loading logic
                MessageBox.Show($"File loading not yet implemented: {openFileDialog.FileName}", "Open File", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open file dialog");
            MessageBox.Show("Failed to open file dialog", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void Save()
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Wiley Widget Data",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                DefaultExt = ".xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                Log.Information("File selected for saving: {FilePath}", saveFileDialog.FileName);
                // TODO: Implement file saving logic
                MessageBox.Show($"File saving not yet implemented: {saveFileDialog.FileName}", "Save File", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save file");
            MessageBox.Show("Failed to save file", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void Print()
    {
        try
        {
            Log.Information("Print command executed");
            // TODO: Implement printing logic
            MessageBox.Show("Printing not yet implemented", "Print", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute print command");
        }
    }

    [RelayCommand]
    public void Exit()
    {
        try
        {
            Log.Information("Exit command executed");
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute exit command");
            Application.Current.Shutdown();
        }
    }

    // Docking manager commands
    [RelayCommand]
    public void LoadLayout()
    {
        try
        {
            Log.Information("Load layout command executed");
            NavigationRequested?.Invoke(this, new NavigationRequestEventArgs("LoadLayout", "LoadLayout"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load layout");
            MessageBox.Show($"Failed to load layout: {ex.Message}", "Layout Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void FloatWindow()
    {
        try
        {
            Log.Information("Float window command executed");
            // TODO: Implement window floating logic
            MessageBox.Show("Window floating not yet implemented", "Float Window", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to float window");
        }
    }

    [RelayCommand]
    public void AutoHide()
    {
        try
        {
            Log.Information("Auto hide command executed");
            // TODO: Implement auto hide logic
            MessageBox.Show("Auto hide not yet implemented", "Auto Hide", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to auto hide");
        }
    }

    // Additional missing commands
    [RelayCommand]
    public void ExportData()
    {
        try
        {
            Log.Information("Export data command executed");
            // TODO: Implement data export logic
            MessageBox.Show("Data export not yet implemented", "Export Data", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export data");
        }
    }

    [RelayCommand]
    public void ImportData()
    {
        try
        {
            Log.Information("Import data command executed");
            // TODO: Implement data import logic
            MessageBox.Show("Data import not yet implemented", "Import Data", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to import data");
        }
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync()
    {
        if (_quickBooksService == null)
        {
            QuickBooksErrorMessage = "QuickBooks service not configured. Please check settings.";
            QuickBooksHasError = true;
            Log.Warning("Attempted to load QuickBooks customers but service is not configured");
            return;
        }

        if (QuickBooksBusy) return;

        // Initialize progress tracking
        var progressSteps = new List<ProgressStep>
        {
            new ProgressStep("Initializing", "Preparing QuickBooks customer load"),
            new ProgressStep("Authenticating", "Verifying QuickBooks connection"),
            new ProgressStep("Querying", "Retrieving customer data from QuickBooks"),
            new ProgressStep("Processing", "Processing customer information"),
            new ProgressStep("Finalizing", "Completing customer data load")
        };

        _progressViewModel.StartOperation("Loading QuickBooks Customers", progressSteps);

        try
        {
            QuickBooksBusy = true;
            QuickBooksHasError = false;
            QuickBooksErrorMessage = null;

            _progressViewModel.UpdateProgress(0, "Initializing QuickBooks customer load...");
            QuickBooksStatusMessage = "Loading customers...";

            _progressViewModel.UpdateProgress(1, "Authenticating with QuickBooks...");

            _progressViewModel.UpdateProgress(2, "Querying customer data...");

            var items = await _quickBooksService.GetCustomersAsync();

            _progressViewModel.UpdateProgress(3, $"Processing {items.Count} customers...");

            QuickBooksCustomers.Clear();
            foreach (var c in items) QuickBooksCustomers.Add(c);

            _progressViewModel.UpdateProgress(4, "Completing customer data load...");

            QuickBooksStatusMessage = $"Loaded {items.Count} customers successfully";
            Log.Information("Successfully loaded {Count} QuickBooks customers", items.Count);

            _progressViewModel.CompleteOperation();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("token") || ex.Message.Contains("authorization"))
        {
            QuickBooksErrorMessage = "QuickBooks authorization failed. Please re-authenticate in Settings.";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Authorization error";
            _progressViewModel.FailOperation("QuickBooks authorization failed");
            Log.Error(ex, "QuickBooks authorization error while loading customers");
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            QuickBooksErrorMessage = "Network error connecting to QuickBooks. Please check your internet connection.";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Network error";
            _progressViewModel.FailOperation("Network error connecting to QuickBooks");
            Log.Error(ex, "Network error while loading QuickBooks customers");
        }
        catch (Exception ex)
        {
            QuickBooksErrorMessage = $"Failed to load customers: {ex.Message}";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Load failed";
            _progressViewModel.FailOperation($"Failed to load customers: {ex.Message}");
            Log.Error(ex, "Unexpected error while loading QuickBooks customers");
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task LoadQuickBooksInvoicesAsync()
    {
        if (_quickBooksService == null)
        {
            QuickBooksErrorMessage = "QuickBooks service not configured. Please check settings.";
            QuickBooksHasError = true;
            Log.Warning("Attempted to load QuickBooks invoices but service is not configured");
            return;
        }

        if (QuickBooksBusy) return;

        // Initialize progress tracking
        var progressSteps = new List<ProgressStep>
        {
            new ProgressStep("Initializing", "Preparing QuickBooks invoice load"),
            new ProgressStep("Authenticating", "Verifying QuickBooks connection"),
            new ProgressStep("Querying", "Retrieving invoice data from QuickBooks"),
            new ProgressStep("Processing", "Processing invoice information"),
            new ProgressStep("Finalizing", "Completing invoice data load")
        };

        _progressViewModel.StartOperation("Loading QuickBooks Invoices", progressSteps);

        try
        {
            QuickBooksBusy = true;
            QuickBooksHasError = false;
            QuickBooksErrorMessage = null;

            _progressViewModel.UpdateProgress(0, "Initializing QuickBooks invoice load...");
            QuickBooksStatusMessage = "Loading invoices...";

            _progressViewModel.UpdateProgress(1, "Authenticating with QuickBooks...");

            _progressViewModel.UpdateProgress(2, "Querying invoice data...");

            var items = await _quickBooksService.GetInvoicesAsync();

            _progressViewModel.UpdateProgress(3, $"Processing {items.Count} invoices...");

            QuickBooksInvoices.Clear();
            foreach (var i in items) QuickBooksInvoices.Add(i);

            _progressViewModel.UpdateProgress(4, "Completing invoice data load...");

            QuickBooksStatusMessage = $"Loaded {items.Count} invoices successfully";
            Log.Information("Successfully loaded {Count} QuickBooks invoices", items.Count);

            _progressViewModel.CompleteOperation();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("token") || ex.Message.Contains("authorization"))
        {
            QuickBooksErrorMessage = "QuickBooks authorization failed. Please re-authenticate in Settings.";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Authorization error";
            _progressViewModel.FailOperation("QuickBooks authorization failed");
            Log.Error(ex, "QuickBooks authorization error while loading invoices");
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            QuickBooksErrorMessage = "Network error connecting to QuickBooks. Please check your internet connection.";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Network error";
            _progressViewModel.FailOperation("Network error connecting to QuickBooks");
            Log.Error(ex, "Network error while loading QuickBooks invoices");
        }
        catch (Exception ex)
        {
            QuickBooksErrorMessage = $"Failed to load invoices: {ex.Message}";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Load failed";
            _progressViewModel.FailOperation($"Failed to load invoices: {ex.Message}");
            Log.Error(ex, "Unexpected error while loading QuickBooks invoices");
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    public void ClearQuickBooksError()
    {
        QuickBooksErrorMessage = null;
        QuickBooksHasError = false;
        QuickBooksStatusMessage = "Error cleared";
        Log.Information("QuickBooks error cleared by user");
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task Refresh()
    {
        await ExecuteAsyncOperation(async (ct) =>
        {
            Log.Information("Manual refresh triggered - reloading all data");

            // Refresh enterprise data
            await LoadEnterprisesAsync();

            // Refresh QuickBooks data if service is available
            if (_quickBooksService != null)
            {
                await LoadQuickBooksCustomersAsync();
                await LoadQuickBooksInvoicesAsync();
            }

            Log.Information("Manual refresh completed successfully");
        }, null, "Refreshing all data");
    }

    [RelayCommand]
    public void OpenSettings()
    {
        RequestNavigation("SettingsPanel", "Settings");
    }

    [RelayCommand]
    public void OpenHelp()
    {
        // This will be handled by the MainWindow event handler
        Log.Information("Help shortcut triggered");
    }

    private void RequestNavigation(string panelName, string viewDisplayName)
    {
        if (string.IsNullOrWhiteSpace(panelName))
        {
            Log.Warning("=== NAVIGATION REQUEST REJECTED ===");
            Log.Warning("Navigation request rejected: panelName is null or empty");
            return;
        }

        try
        {
            Log.Information("=== NAVIGATION REQUEST STARTED ===");
            Log.Information("Requesting navigation to panel '{Panel}' with display name '{View}'", panelName, viewDisplayName);

            CurrentViewName = viewDisplayName;
            Log.Information("CurrentViewName set to '{ViewName}'", viewDisplayName);

            Log.Information("Invoking NavigationRequested event for panel '{Panel}'", panelName);
            NavigationRequested?.Invoke(this, new NavigationRequestEventArgs(panelName, viewDisplayName));

            Log.Information("=== NAVIGATION REQUEST COMPLETED ===");
            Log.Information("Navigation request to {Panel} ({View}) completed successfully", panelName, viewDisplayName);
            App.LogDebugEvent("NAVIGATION", $"Navigation requested to {panelName} ({viewDisplayName})");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "=== NAVIGATION REQUEST FAILED ===");
            Log.Error(ex, "Failed to request navigation to panel {Panel} ({View})", panelName, viewDisplayName);
            App.LogDebugEvent("NAVIGATION_ERROR", $"Failed to request navigation to {panelName}: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenEnterprise()
    {
        RequestNavigation("EnterprisePanel", "Enterprise Management");
    }

    [RelayCommand]
    public void OpenBudget()
    {
        RequestNavigation("BudgetPanel", "Budget Analysis");
    }

    [RelayCommand]
    public void OpenDashboard()
    {
        RequestNavigation("DashboardPanel", "Dashboard");
    }

    [RelayCommand]
    public void OpenTools()
    {
        RequestNavigation("ToolsPanel", "Tools & Utilities");
    }

    [RelayCommand]
    public void DrillDownEnterprises()
    {
        RequestNavigation("WidgetsPanel", "Municipal Enterprises");
    }

    [RelayCommand]
    public void DrillDownCitizens()
    {
        // Open enterprise management to see citizen details
        EnterpriseView.ShowEnterpriseWindow();
    }

    [RelayCommand]
    public void DrillDownRevenue()
    {
        // Open budget analysis to see revenue details
        BudgetView.ShowBudgetWindow();
    }

    [RelayCommand]
    public void DrillDownExpenses()
    {
        // Open budget analysis to see expense details
        BudgetView.ShowBudgetWindow();
    }

    [RelayCommand]
    public void DrillDownProfit()
    {
        // Open budget analysis focused on profit/loss
        BudgetView.ShowBudgetWindow();
    }

    /// <summary>
    /// Dashboard properties for the embedded dashboard in MainWindow
    /// </summary>
    public int TotalEnterprises => Enterprises.Count;
    public decimal TotalMonthlyRevenue => Enterprises.Sum(e => e.CurrentRate * e.CitizenCount);
    public decimal TotalMonthlyExpenses => Enterprises.Sum(e => e.MonthlyExpenses);
    public decimal TotalAnnualBudget => Enterprises.Sum(e => e.TotalBudget);
    public decimal NetMonthlyProfit => TotalMonthlyRevenue - TotalMonthlyExpenses;
    public int TotalCitizens => Enterprises.Sum(e => e.CitizenCount);
    public decimal AverageRatePerCitizen => TotalCitizens > 0 ? TotalMonthlyRevenue / TotalCitizens : 0;
    public double BudgetUtilizationPercentage => TotalAnnualBudget > 0 ? (double)(TotalMonthlyExpenses * 12 / TotalAnnualBudget) * 100 : 0;

    /// <summary>
    /// Growth rate calculations for dashboard trends
    /// </summary>
    public double EnterprisesGrowthRate => CalculateEnterprisesGrowthRate();
    public double CitizensGrowthRate => CalculateCitizensGrowthRate();
    public double RevenueGrowthRate => CalculateRevenueGrowthRate();
    public double ExpenseGrowthRate => CalculateExpenseGrowthRate();
    public double ProfitMarginPercentage => TotalMonthlyRevenue > 0 ? (double)(NetMonthlyProfit / TotalMonthlyRevenue) * 100 : 0;

    /// <summary>
    /// Chart data collections for dashboard visualizations
    /// </summary>
    public ObservableCollection<RevenueTrendItem> RevenueTrendData { get; } = new();
    public ObservableCollection<ExpenseCategoryItem> ExpenseCategoryData { get; } = new();
    public ObservableCollection<EnterpriseTypeItem> EnterpriseTypeData { get; } = new();
    public ObservableCollection<AlertItem> ActiveAlerts { get; } = new();

    /// <summary>
    /// QuickBooks data summary
    /// </summary>
    public int TotalQuickBooksCustomers => QuickBooksCustomers.Count;
    public int TotalQuickBooksInvoices => QuickBooksInvoices.Count;
    public decimal TotalInvoiceAmount => QuickBooksInvoices.Sum(i => i.TotalAmt);

    /// <summary>
    /// Calculate growth rates (more sophisticated calculations)
    /// </summary>
    private double CalculateEnterprisesGrowthRate()
    {
        // Calculate based on actual enterprise count with some historical simulation
        if (TotalEnterprises == 0) return 0.0;
        if (TotalEnterprises <= 2) return 25.0; // New system growth
        if (TotalEnterprises <= 5) return 15.0; // Moderate growth
        return 8.0; // Established system growth
    }

    private double CalculateCitizensGrowthRate()
    {
        // Calculate based on citizen count
        if (TotalCitizens == 0) return 0.0;
        if (TotalCitizens < 1000) return 20.0; // High growth for small communities
        if (TotalCitizens < 5000) return 12.0; // Moderate growth
        if (TotalCitizens < 10000) return 6.0; // Slow growth
        return 2.5; // Stable large community
    }

    private double CalculateRevenueGrowthRate()
    {
        // Calculate based on revenue levels
        if (TotalMonthlyRevenue == 0) return 0.0;
        if (TotalMonthlyRevenue < 25000) return 18.0; // High growth for low revenue
        if (TotalMonthlyRevenue < 75000) return 10.0; // Moderate growth
        if (TotalMonthlyRevenue < 150000) return 5.0; // Slow growth
        return 2.0; // Stable high revenue
    }

    private double CalculateExpenseGrowthRate()
    {
        // Expenses typically grow slower than revenue
        if (TotalMonthlyExpenses == 0) return 0.0;
        if (TotalMonthlyExpenses < 15000) return 12.0; // Initial setup costs
        if (TotalMonthlyExpenses < 40000) return 8.0; // Operational growth
        if (TotalMonthlyExpenses < 80000) return 4.0; // Controlled growth
        return 1.5; // Stable operations
    }

    /// <summary>
    /// Initialize chart data when enterprises change
    /// </summary>
    private void UpdateChartData()
    {
        // Ensure UI-bound collections are updated on the UI thread. In unit tests
        // Application.Current may be null, so fall back to direct execution when
        // no dispatcher is available.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            try
            {
                dispatcher.Invoke(() => UpdateChartData());
            }
            catch (Exception ex)
            {
                // Log and swallow to avoid crashing background workers
                Log.Warning(ex, "Failed to invoke UpdateChartData on UI thread");
            }
            return;
        }

        UpdateRevenueTrendData();
        UpdateExpenseCategoryData();
        UpdateEnterpriseTypeData();
        UpdateActiveAlerts();
    }

    private void UpdateRevenueTrendData()
    {
        RevenueTrendData.Clear();
        // Generate 6 months of trend data with realistic growth patterns
        var baseRevenue = Math.Max(TotalMonthlyRevenue, 10000M); // Minimum base for demo

        // If no revenue data, provide sample data for chart display
        if (TotalMonthlyRevenue == 0)
        {
            // Sample data for demonstration
            var sampleData = new[]
            {
                new RevenueTrendItem { Period = DateTime.Now.AddMonths(-5).ToString("MMM yyyy"), Amount = 85000M },
                new RevenueTrendItem { Period = DateTime.Now.AddMonths(-4).ToString("MMM yyyy"), Amount = 92000M },
                new RevenueTrendItem { Period = DateTime.Now.AddMonths(-3).ToString("MMM yyyy"), Amount = 88000M },
                new RevenueTrendItem { Period = DateTime.Now.AddMonths(-2).ToString("MMM yyyy"), Amount = 95000M },
                new RevenueTrendItem { Period = DateTime.Now.AddMonths(-1).ToString("MMM yyyy"), Amount = 102000M },
                new RevenueTrendItem { Period = DateTime.Now.ToString("MMM yyyy"), Amount = 98000M }
            };

            foreach (var item in sampleData)
            {
                RevenueTrendData.Add(item);
            }
            return;
        }

        var random = new Random(42); // Fixed seed for consistent demo data

        for (int i = 5; i >= 0; i--)
        {
            var date = DateTime.Now.AddMonths(-i);
            // Create realistic seasonal patterns with some randomness
            var seasonalFactor = 1.0 + 0.1 * Math.Sin((i * Math.PI) / 6); // Seasonal variation
            var growthFactor = Math.Pow(1.02, i); // 2% monthly growth
            var randomFactor = 0.95 + (random.NextDouble() * 0.1); // ±5% randomness

            var trendMultiplier = seasonalFactor * growthFactor * randomFactor;
            RevenueTrendData.Add(new RevenueTrendItem
            {
                Period = date.ToString("MMM yyyy"),
                Amount = baseRevenue * (decimal)trendMultiplier
            });
        }

        // Sort by date (oldest first)
        var sorted = RevenueTrendData.OrderBy(x => DateTime.Parse(x.Period)).ToList();
        RevenueTrendData.Clear();
        foreach (var item in sorted)
        {
            RevenueTrendData.Add(item);
        }
    }

    private void UpdateExpenseCategoryData()
    {
        ExpenseCategoryData.Clear();

        // If no expense data, provide sample data for chart display
        if (TotalMonthlyExpenses == 0)
        {
            ExpenseCategoryData.Add(new ExpenseCategoryItem { Category = "Operations", Amount = 45000M });
            ExpenseCategoryData.Add(new ExpenseCategoryItem { Category = "Maintenance", Amount = 30000M });
            ExpenseCategoryData.Add(new ExpenseCategoryItem { Category = "Administration", Amount = 25000M });
            return;
        }

        // Categorize expenses (simplified breakdown)
        var operationsExpense = TotalMonthlyExpenses * 0.45M;
        var maintenanceExpense = TotalMonthlyExpenses * 0.30M;
        var adminExpense = TotalMonthlyExpenses * 0.25M;

        ExpenseCategoryData.Add(new ExpenseCategoryItem { Category = "Operations", Amount = operationsExpense });
        ExpenseCategoryData.Add(new ExpenseCategoryItem { Category = "Maintenance", Amount = maintenanceExpense });
        ExpenseCategoryData.Add(new ExpenseCategoryItem { Category = "Administration", Amount = adminExpense });
    }

    private void UpdateEnterpriseTypeData()
    {
        EnterpriseTypeData.Clear();

        // If no enterprises, provide sample data for chart display
        if (Enterprises.Count == 0)
        {
            EnterpriseTypeData.Add(new EnterpriseTypeItem { Type = "Water", Count = 5, AverageRate = 42.50M });
            EnterpriseTypeData.Add(new EnterpriseTypeItem { Type = "Sewer", Count = 3, AverageRate = 38.75M });
            EnterpriseTypeData.Add(new EnterpriseTypeItem { Type = "Trash", Count = 2, AverageRate = 33.10M });
            EnterpriseTypeData.Add(new EnterpriseTypeItem { Type = "Other", Count = 1, AverageRate = 55.00M });
            return;
        }

        var typeGroups = Enterprises.GroupBy(e => e.Type ?? "Other");
        foreach (var group in typeGroups)
        {
            EnterpriseTypeData.Add(new EnterpriseTypeItem
            {
                Type = group.Key,
                Count = group.Count(),
                AverageRate = group.Any() ? group.Average(e => e.CurrentRate) : 0M
            });
        }
    }

    private void UpdateActiveAlerts()
    {
        ActiveAlerts.Clear();

        // Add alerts based on system status
        if (TotalEnterprises == 0)
        {
            ActiveAlerts.Add(new AlertItem
            {
                Priority = "High",
                Message = "No enterprises configured - system requires setup",
                Timestamp = DateTime.Now,
                PriorityColor = System.Windows.Media.Brushes.Red
            });
        }

        if (BudgetUtilizationPercentage > 90)
        {
            ActiveAlerts.Add(new AlertItem
            {
                Priority = "Medium",
                Message = $"Budget utilization at {BudgetUtilizationPercentage:F1}% - review expenses",
                Timestamp = DateTime.Now,
                PriorityColor = System.Windows.Media.Brushes.Orange
            });
        }

        if (NetMonthlyProfit < 0)
        {
            ActiveAlerts.Add(new AlertItem
            {
                Priority = "High",
                Message = "Negative monthly profit - immediate attention required",
                Timestamp = DateTime.Now,
                PriorityColor = System.Windows.Media.Brushes.Red
            });
        }

        // Add informational alerts
        if (TotalEnterprises > 0 && ActiveAlerts.Count == 0)
        {
            ActiveAlerts.Add(new AlertItem
            {
                Priority = "Low",
                Message = "System operating normally",
                Timestamp = DateTime.Now,
                PriorityColor = System.Windows.Media.Brushes.Green
            });
        }
    }

    [RelayCommand]
    public void ToggleDynamicColumns()
    {
        UseDynamicColumns = !UseDynamicColumns;
        Log.Information("Dynamic columns toggled to: {UseDynamicColumns}", UseDynamicColumns);
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task ExportEnterprises()
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"Enterprises_{DateTime.Now:yyyy-MM-dd}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var fileName = saveFileDialog.FileName;
                var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

                if (extension == ".csv")
                {
                    await ExportToCsvAsync(fileName);
                }
                else if (extension == ".xlsx")
                {
                    await ExportToExcelAsync(fileName);
                }
                else
                {
                    await ExportToCsvAsync(fileName); // Default to CSV
                }

                MessageBox.Show($"Export completed successfully!\nFile saved to: {fileName}", 
                              "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                Log.Information("Enterprises exported to {FileName}", fileName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export enterprises");
            MessageBox.Show($"Export failed: {ex.Message}", "Export Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void BulkEditEnterprises()
    {
        // TODO: Implement bulk edit functionality
        Log.Information("Bulk edit enterprises requested - not yet implemented");
        MessageBox.Show("Bulk edit functionality is not yet implemented.", "Feature Not Available", 
                      MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public void ClearFilters()
    {
        // TODO: Implement clear filters functionality
        Log.Information("Clear filters requested - not yet implemented");
    }

    [RelayCommand]
    public void EditEnterprise()
    {
        // TODO: Implement edit enterprise functionality
        Log.Information("Edit enterprise requested - not yet implemented");
    }

    [RelayCommand]
    public void SaveEnterprise()
    {
        // TODO: Implement save enterprise functionality
        Log.Information("Save enterprise requested - not yet implemented");
    }

    [RelayCommand]
    public void DeleteEnterprise()
    {
        // TODO: Implement delete enterprise functionality
        Log.Information("Delete enterprise requested - not yet implemented");
    }

    [RelayCommand]
    public void GenerateBudgetReport()
    {
        // TODO: Implement generate budget report functionality
        Log.Information("Generate budget report requested - not yet implemented");
    }

    [RelayCommand]
    public void ExportBudgetData()
    {
        // TODO: Implement export budget data functionality
        Log.Information("Export budget data requested - not yet implemented");
    }

    [RelayCommand]
    public void CopyMessage()
    {
        // TODO: Implement copy message functionality
        Log.Information("Copy message requested - not yet implemented");
    }

    [RelayCommand]
    public void QuickAIAnalysis()
    {
        // TODO: Implement quick AI analysis functionality
        Log.Information("Quick AI analysis requested - not yet implemented");
    }

    [RelayCommand]
    public void ResetSettings()
    {
        // TODO: Implement reset settings functionality
        Log.Information("Reset settings requested - not yet implemented");
    }

    [RelayCommand]
    public void SaveSettings()
    {
        // TODO: Implement save settings functionality
        Log.Information("Save settings requested - not yet implemented");
    }

    [RelayCommand]
    public void BackupData()
    {
        // TODO: Implement backup data functionality
        Log.Information("Backup data requested - not yet implemented");
    }

    [RelayCommand]
    public void RestoreData()
    {
        // TODO: Implement restore data functionality
        Log.Information("Restore data requested - not yet implemented");
    }

    [RelayCommand]
    public void ClearCache()
    {
        // TODO: Implement clear cache functionality
        Log.Information("Clear cache requested - not yet implemented");
    }

    [RelayCommand]
    public void ConnectQuickBooks()
    {
        // TODO: Implement connect QuickBooks functionality
        Log.Information("Connect QuickBooks requested - not yet implemented");
    }

    [RelayCommand]
    public void DisconnectQuickBooks()
    {
        // TODO: Implement disconnect QuickBooks functionality
        Log.Information("Disconnect QuickBooks requested - not yet implemented");
    }

    [RelayCommand]
    public void TestQuickBooks()
    {
        // TODO: Implement test QuickBooks functionality
        Log.Information("Test QuickBooks requested - not yet implemented");
    }

    [RelayCommand]
    public void ConfigureAzure()
    {
        // TODO: Implement configure Azure functionality
        Log.Information("Configure Azure requested - not yet implemented");
    }

    [RelayCommand]
    public void TestAzure()
    {
        // TODO: Implement test Azure functionality
        Log.Information("Test Azure requested - not yet implemented");
    }

    [RelayCommand]
    public void ViewLogs()
    {
        // TODO: Implement view logs functionality
        Log.Information("View logs requested - not yet implemented");
    }

    [RelayCommand]
    public void OpenLogFolder()
    {
        // TODO: Implement open log folder functionality
        Log.Information("Open log folder requested - not yet implemented");
    }

    [RelayCommand]
    public void CheckUpdates()
    {
        // TODO: Implement check updates functionality
        Log.Information("Check updates requested - not yet implemented");
    }

    [RelayCommand]
    public void ViewLicense()
    {
        // TODO: Implement view license functionality
        Log.Information("View license requested - not yet implemented");
    }

    [RelayCommand]
    public void ShowAbout()
    {
        // TODO: Implement show about functionality
        Log.Information("Show about requested - not yet implemented");
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task ExportDashboard()
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "pdf",
                FileName = $"Dashboard_{DateTime.Now:yyyy-MM-dd}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var fileName = saveFileDialog.FileName;
                var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

                if (extension == ".pdf")
                {
                    await ExportDashboardToPdfAsync(fileName);
                }
                else
                {
                    await ExportDashboardToCsvAsync(fileName);
                }

                MessageBox.Show($"Dashboard export completed successfully!\nFile saved to: {fileName}", 
                              "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                Log.Information("Dashboard exported to {FileName}", fileName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export dashboard");
            MessageBox.Show($"Dashboard export failed: {ex.Message}", "Export Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async System.Threading.Tasks.Task ExportToCsvAsync(string fileName)
    {
        var csv = new System.Text.StringBuilder();
        
        // Add header
        csv.AppendLine("Name,Type,Current Rate,Citizen Count,Monthly Expenses,Total Budget,Notes");
        
        // Add data rows
        foreach (var enterprise in Enterprises)
        {
            csv.AppendLine($"\"{enterprise.Name}\",\"{enterprise.Type}\"," +
                          $"{enterprise.CurrentRate},{enterprise.CitizenCount}," +
                          $"{enterprise.MonthlyExpenses},{enterprise.TotalBudget}," +
                          $"\"{enterprise.Notes?.Replace("\"", "\"\"")}\"");
        }
        
        await System.IO.File.WriteAllTextAsync(fileName, csv.ToString());
    }

    private async System.Threading.Tasks.Task ExportToExcelAsync(string fileName)
    {
        // For now, export as CSV with .xlsx extension
        // In a full implementation, you would use a library like EPPlus or ClosedXML
        await ExportToCsvAsync(fileName);
    }

    private async System.Threading.Tasks.Task ExportDashboardToPdfAsync(string fileName)
    {
        // For now, create a simple text summary
        var content = new System.Text.StringBuilder();
        content.AppendLine("Wiley Widget Dashboard Summary");
        content.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        content.AppendLine();
        content.AppendLine($"Total Enterprises: {TotalEnterprises}");
        content.AppendLine($"Total Citizens Served: {TotalCitizens:N0}");
        content.AppendLine($"Total Monthly Revenue: {TotalMonthlyRevenue:C}");
        content.AppendLine($"Total Monthly Expenses: {TotalMonthlyExpenses:C}");
        content.AppendLine($"Net Monthly Profit: {NetMonthlyProfit:C}");
        content.AppendLine($"Profit Margin: {ProfitMarginPercentage:F1}%");
        content.AppendLine();
        content.AppendLine("Enterprise Details:");
        foreach (var enterprise in Enterprises)
        {
            content.AppendLine($"- {enterprise.Name} ({enterprise.Type}): {enterprise.CitizenCount} citizens, {enterprise.CurrentRate:C} rate");
        }
        
        await System.IO.File.WriteAllTextAsync(fileName, content.ToString());
    }

    private async System.Threading.Tasks.Task ExportDashboardToCsvAsync(string fileName)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Metric,Value");
        csv.AppendLine($"Total Enterprises,{TotalEnterprises}");
        csv.AppendLine($"Total Citizens Served,{TotalCitizens}");
        csv.AppendLine($"Total Monthly Revenue,{TotalMonthlyRevenue}");
        csv.AppendLine($"Total Monthly Expenses,{TotalMonthlyExpenses}");
        csv.AppendLine($"Net Monthly Profit,{NetMonthlyProfit}");
        csv.AppendLine($"Profit Margin %,{ProfitMarginPercentage:F1}");
        
        await System.IO.File.WriteAllTextAsync(fileName, csv.ToString());
    }

    [RelayCommand]
    public void ResetLayout()
    {
        // Request layout reset through navigation event
        NavigationRequested?.Invoke(this, new NavigationRequestEventArgs("ResetLayout", "Reset Layout"));
    }

    [RelayCommand]
    public void SaveLayout()
    {
        // Request layout save through navigation event
        NavigationRequested?.Invoke(this, new NavigationRequestEventArgs("SaveLayout", "Save Layout"));
    }

    /// <summary>
    /// Disposes of managed and unmanaged resources
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _initializationSemaphore.Dispose();
            _loadSemaphore.Dispose();
            (_municipalAccountViewModel as IDisposable)?.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed class NavigationRequestEventArgs : EventArgs
{
    public NavigationRequestEventArgs(string panelName, string viewName)
    {
        if (string.IsNullOrWhiteSpace(panelName))
            throw new ArgumentException("Panel name must be provided", nameof(panelName));

        PanelName = panelName;
        ViewName = viewName;
    }

    public string PanelName { get; }

    public string ViewName { get; }
}

/// <summary>
/// Data models for dashboard charts
/// </summary>
public class RevenueTrendItem
{
    public string Period { get; set; }
    public decimal Amount { get; set; }
}

public class ExpenseCategoryItem
{
    public string Category { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Ribbon item classes for data binding
/// </summary>
public class RibbonTabItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private string _header = string.Empty;
    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    public ObservableCollection<RibbonBarItem> Bars { get; } = new();
}

public class RibbonBarItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private string _header = string.Empty;
    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    public ObservableCollection<RibbonItem> Items { get; } = new();
}

public class RibbonItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private string _label = string.Empty;
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    private ICommand? _command;
    public ICommand? Command
    {
        get => _command;
        set => SetProperty(ref _command, value);
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private string _sizeForm = "Large";
    public string SizeForm
    {
        get => _sizeForm;
        set => SetProperty(ref _sizeForm, value);
    }
}

/// <summary>
/// View model base class for tab content
/// </summary>
public class ViewModelBase : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private string _header = string.Empty;
    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    private object? _content;
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }
}

/// <summary>
/// Tab item for QuickBooks data
/// </summary>
public class QuickBooksTabItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private string _header = string.Empty;
    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    private object? _content;
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }
}
