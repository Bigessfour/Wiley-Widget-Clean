using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WileyWidget.Data;
using WileyWidget.Services;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels
{
    public partial class SettingsViewModel : AsyncViewModelBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IAzureKeyVaultService _azureKeyVaultService;
        private readonly IQuickBooksService _quickBooksService;
        private readonly ISyncfusionLicenseService _syncfusionLicenseService;

        // General Settings
        [ObservableProperty]
        private ThreadSafeObservableCollection<string> availableThemes = new() { "FluentDark", "FluentLight" };

        [ObservableProperty]
        private string selectedTheme = "FluentDark";

        partial void OnSelectedThemeChanged(string value)
        {
            // Apply the theme change immediately when selected in settings
            ApplyThemeToAllWindows(value);
        }

        [ObservableProperty]
        private int windowWidth = 1200;

        [ObservableProperty]
        private int windowHeight = 800;

        [ObservableProperty]
        private bool maximizeOnStartup;

        [ObservableProperty]
        private bool showSplashScreen = true;

        // Database Settings
        [ObservableProperty]
        private string databaseConnectionString;

        [ObservableProperty]
        private string databaseStatus = "Checking...";

        [ObservableProperty]
        private Brush databaseStatusColor = Brushes.Orange;

        // QuickBooks Settings
        [ObservableProperty]
        private string quickBooksClientId;

        [ObservableProperty]
        private string quickBooksClientSecret;

        [ObservableProperty]
        private string quickBooksRedirectUri;

        [ObservableProperty]
        private ThreadSafeObservableCollection<string> quickBooksEnvironments = new() { "Sandbox", "Production" };

        [ObservableProperty]
        private string selectedQuickBooksEnvironment = "Sandbox";

        [ObservableProperty]
        private string quickBooksConnectionStatus = "Not Connected";

        [ObservableProperty]
        private Brush quickBooksStatusColor = Brushes.Red;

        // Syncfusion License
        [ObservableProperty]
        private string syncfusionLicenseKey;

        [ObservableProperty]
        private string syncfusionLicenseStatus = "Checking...";

        [ObservableProperty]
        private Brush syncfusionLicenseStatusColor = Brushes.Orange;

        // Azure Settings
        [ObservableProperty]
        private string azureKeyVaultUrl;

        [ObservableProperty]
        private string azureConnectionStatus = "Not Connected";

        [ObservableProperty]
        private Brush azureStatusColor = Brushes.Red;

        [ObservableProperty]
        private string azureSqlServer;

        [ObservableProperty]
        private string azureSqlDatabase;

        [ObservableProperty]
        private ThreadSafeObservableCollection<string> azureAuthMethods = new() { "Managed Identity", "Service Principal", "Connection String" };

        [ObservableProperty]
        private string selectedAzureAuthMethod = "Managed Identity";

        // Advanced Settings
        [ObservableProperty]
        private bool enableDynamicColumns = true;

        [ObservableProperty]
        private bool enableDataCaching = true;

        [ObservableProperty]
        private int cacheExpirationMinutes = 30;

        [ObservableProperty]
        private ThreadSafeObservableCollection<string> logLevels = new() { "Debug", "Information", "Warning", "Error", "Critical" };

        [ObservableProperty]
        private string selectedLogLevel = "Information";

        [ObservableProperty]
        private bool enableFileLogging = true;

        [ObservableProperty]
        private string logFilePath = "logs/wiley-widget.log";

        // Status
        [ObservableProperty]
        private string settingsStatus = "Ready";

        [ObservableProperty]
        private string lastSaved = "Never";

        [ObservableProperty]
        private string systemInfo;

        public bool HasUnsavedChanges { get; private set; }

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            AppDbContext dbContext,
            IAzureKeyVaultService azureKeyVaultService,
            IQuickBooksService quickBooksService,
            ISyncfusionLicenseService syncfusionLicenseService,
            IDispatcherHelper dispatcherHelper)
            : base(dispatcherHelper, logger)
        {
            // Validate required dependencies
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _azureKeyVaultService = azureKeyVaultService ?? throw new ArgumentNullException(nameof(azureKeyVaultService));
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _syncfusionLicenseService = syncfusionLicenseService ?? throw new ArgumentNullException(nameof(syncfusionLicenseService));

            // Initialize configuration fields with defaults
            databaseConnectionString = string.Empty;
            quickBooksClientId = string.Empty;
            quickBooksClientSecret = string.Empty;
            quickBooksRedirectUri = string.Empty;
            syncfusionLicenseKey = string.Empty;
            azureKeyVaultUrl = string.Empty;
            azureSqlServer = string.Empty;
            azureSqlDatabase = string.Empty;

            // Initialize system info
            SystemInfo = $"OS: {Environment.OSVersion}\n" +
                        $".NET Version: {Environment.Version}\n" +
                        $"Machine: {Environment.MachineName}\n" +
                        $"User: {Environment.UserName}";

            // Set up property change tracking for unsaved changes
            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            if (e.PropertyName != nameof(HasUnsavedChanges) &&
                e.PropertyName != nameof(SettingsStatus) &&
                e.PropertyName != nameof(LastSaved))
            {
                HasUnsavedChanges = true;
            }
        }

        public async Task LoadSettingsAsync()
        {
            await ExecuteAsyncOperation(async (cancellationToken) =>
            {
                // Load from configuration and database
                await LoadGeneralSettingsAsync();
                await LoadDatabaseSettingsAsync();
                await LoadQuickBooksSettingsAsync();
                await LoadSyncfusionSettingsAsync();
                await LoadAzureSettingsAsync();
                await LoadAdvancedSettingsAsync();

                HasUnsavedChanges = false;
                LastSaved = DateTime.Now.ToString("g");

                Logger.LogInformation("Settings loaded successfully");
            }, statusMessage: "Loading settings...");
        }

        private async Task LoadGeneralSettingsAsync()
        {
            // Load from appsettings.json or database
            // For now, use default values
            SelectedTheme = "FluentDark";
            WindowWidth = 1200;
            WindowHeight = 800;
            MaximizeOnStartup = false;
            ShowSplashScreen = true;
            await Task.CompletedTask; // Suppress async warning for future async operations
        }

        private async Task LoadDatabaseSettingsAsync()
        {
            try
            {
                DatabaseConnectionString = "Connection string not available in this EF Core version";

                // Test database connection
                var canConnect = await _dbContext.Database.CanConnectAsync();
                DatabaseStatus = canConnect ? "Connected" : "Connection Failed";
                DatabaseStatusColor = canConnect ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                DatabaseStatus = $"Error: {ex.Message}";
                DatabaseStatusColor = Brushes.Red;
            }
        }

        private async Task LoadQuickBooksSettingsAsync()
        {
            try
            {
                // Prefer environment variables during troubleshooting; Key Vault fallback is commented below
                QuickBooksClientId = Environment.GetEnvironmentVariable("QUICKBOOKS_CLIENT_ID") ?? "";
                QuickBooksClientSecret = Environment.GetEnvironmentVariable("QUICKBOOKS_CLIENT_SECRET") ?? "";
                QuickBooksRedirectUri = Environment.GetEnvironmentVariable("QUICKBOOKS_REDIRECT_URI") ?? "";
                SelectedQuickBooksEnvironment = Environment.GetEnvironmentVariable("QUICKBOOKS_ENVIRONMENT") ?? "Sandbox";

                // Fallback (disabled): Uncomment to re-enable Key Vault retrieval
                // if (string.IsNullOrEmpty(QuickBooksClientId))
                //     QuickBooksClientId = await _azureKeyVaultService.GetSecretAsync("QuickBooks-ClientId") ?? "";
                // if (string.IsNullOrEmpty(QuickBooksClientSecret))
                //     QuickBooksClientSecret = await _azureKeyVaultService.GetSecretAsync("QuickBooks-ClientSecret") ?? "";
                // if (string.IsNullOrEmpty(QuickBooksRedirectUri))
                //     QuickBooksRedirectUri = await _azureKeyVaultService.GetSecretAsync("QuickBooks-RedirectUri") ?? "";
                // if (string.IsNullOrEmpty(SelectedQuickBooksEnvironment))
                //     SelectedQuickBooksEnvironment = await _azureKeyVaultService.GetSecretAsync("QuickBooks-Environment") ?? "Sandbox";

                // Test connection if credentials are available
                if (!string.IsNullOrEmpty(QuickBooksClientId))
                {
                    var isConnected = await _quickBooksService.TestConnectionAsync();
                    QuickBooksConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                    QuickBooksStatusColor = isConnected ? Brushes.Green : Brushes.Red;
                }
                else
                {
                    QuickBooksConnectionStatus = "Not Configured";
                    QuickBooksStatusColor = Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = $"Error: {ex.Message}";
                QuickBooksStatusColor = Brushes.Red;
            }
        }

        private async Task LoadSyncfusionSettingsAsync()
        {
            try
            {
                // Prefer environment variable for license during troubleshooting
                SyncfusionLicenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ?? "";

                // Fallback to Azure Key Vault if environment variable not set
                if (string.IsNullOrEmpty(SyncfusionLicenseKey))
                {
                    try
                    {
                        // Get secret name from configuration, fallback to default
                        // TODO: Inject IConfiguration to read Syncfusion:KeyVaultSecretName
                        var secretName = "Syncfusion-LicenseKey"; // Default name
                        SyncfusionLicenseKey = await _azureKeyVaultService.GetSecretAsync(secretName) ?? "";
                        if (!string.IsNullOrEmpty(SyncfusionLicenseKey))
                        {
                            Logger.LogInformation("Syncfusion license key loaded from Azure Key Vault");
                        }
                    }
                    catch (Exception kvEx)
                    {
                        Logger.LogWarning(kvEx, "Failed to retrieve Syncfusion license from Azure Key Vault");
                        // Continue with empty key - will show as invalid
                    }
                }

                // Simple license validation - check if key exists and is not empty
                var isValid = !string.IsNullOrEmpty(SyncfusionLicenseKey) && SyncfusionLicenseKey != "${SYNCFUSION_LICENSE_KEY}";
                SyncfusionLicenseStatus = isValid ? "Valid" : "Invalid or Missing";
                SyncfusionLicenseStatusColor = isValid ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                SyncfusionLicenseStatus = $"Error: {ex.Message}";
                SyncfusionLicenseStatusColor = Brushes.Red;
                Logger.LogError(ex, "Error loading Syncfusion settings");
            }
        }

        private async Task LoadAzureSettingsAsync()
        {
            try
            {
                // Prefer environment variables for DB configuration during troubleshooting
                AzureKeyVaultUrl = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URL") ?? "";
                AzureSqlServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER") ?? "";
                AzureSqlDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE") ?? "";

                // Skip Key Vault connectivity status while running from env vars
                AzureConnectionStatus = string.IsNullOrEmpty(AzureKeyVaultUrl) ? "Key Vault Skipped" : "Configured";
                AzureStatusColor = Brushes.Orange;

                // Keep method async without Key Vault calls
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                AzureConnectionStatus = $"Error: {ex.Message}";
                AzureStatusColor = Brushes.Red;
            }
        }

        private async Task LoadAdvancedSettingsAsync()
        {
            // Load advanced settings from configuration
            EnableDynamicColumns = true;
            EnableDataCaching = true;
            CacheExpirationMinutes = 30;
            SelectedLogLevel = "Information";
            EnableFileLogging = true;
            LogFilePath = "logs/wiley-widget.log";
            await Task.CompletedTask; // Suppress async warning for future async operations
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            await ExecuteAsyncOperation(async (cancellationToken) =>
            {
                // Save to secure storage and configuration
                await SaveGeneralSettingsAsync();
                await SaveQuickBooksSettingsAsync();
                await SaveSyncfusionSettingsAsync();
                await SaveAzureSettingsAsync();
                await SaveAdvancedSettingsAsync();

                HasUnsavedChanges = false;
                LastSaved = DateTime.Now.ToString("g");

                Logger.LogInformation("Settings saved successfully");
            }, statusMessage: "Saving settings...");
        }

        private async Task SaveGeneralSettingsAsync()
        {
            // Save general settings to configuration
            // Implementation would save to appsettings.json or database
            await Task.CompletedTask; // Suppress async warning for future async operations
        }

        private async Task SaveQuickBooksSettingsAsync()
        {
            if (!string.IsNullOrEmpty(QuickBooksClientId))
                await _azureKeyVaultService.SetSecretAsync("QuickBooks-ClientId", QuickBooksClientId);

            if (!string.IsNullOrEmpty(QuickBooksClientSecret))
                await _azureKeyVaultService.SetSecretAsync("QuickBooks-ClientSecret", QuickBooksClientSecret);

            if (!string.IsNullOrEmpty(QuickBooksRedirectUri))
                await _azureKeyVaultService.SetSecretAsync("QuickBooks-RedirectUri", QuickBooksRedirectUri);

            await _azureKeyVaultService.SetSecretAsync("QuickBooks-Environment", SelectedQuickBooksEnvironment);
        }

        private async Task SaveSyncfusionSettingsAsync()
        {
            if (!string.IsNullOrEmpty(SyncfusionLicenseKey))
                await _azureKeyVaultService.SetSecretAsync("Syncfusion-LicenseKey", SyncfusionLicenseKey);
        }

        private async Task SaveAzureSettingsAsync()
        {
            if (!string.IsNullOrEmpty(AzureKeyVaultUrl))
                await _azureKeyVaultService.SetSecretAsync("Azure-KeyVaultUrl", AzureKeyVaultUrl);

            if (!string.IsNullOrEmpty(AzureSqlServer))
                await _azureKeyVaultService.SetSecretAsync("Azure-SqlServer", AzureSqlServer);

            if (!string.IsNullOrEmpty(AzureSqlDatabase))
                await _azureKeyVaultService.SetSecretAsync("Azure-SqlDatabase", AzureSqlDatabase);
        }

        private async Task SaveAdvancedSettingsAsync()
        {
            // Save advanced settings to configuration
            // Implementation would save to appsettings.json
            await Task.CompletedTask; // Suppress async warning for future async operations
        }

        [RelayCommand]
        private async Task ResetSettingsAsync()
        {
            var result = MessageBox.Show("Are you sure you want to reset all settings to default values?",
                                       "Reset Settings", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                await LoadSettingsAsync();
            }
        }

        [RelayCommand]
        private async Task TestConnectionAsync()
        {
            await LoadDatabaseSettingsAsync();
        }

        [RelayCommand]
        private async Task TestQuickBooksConnectionAsync()
        {
            try
            {
                QuickBooksConnectionStatus = "Testing...";
                QuickBooksStatusColor = Brushes.Orange;

                var isConnected = await _quickBooksService.TestConnectionAsync();
                QuickBooksConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                QuickBooksStatusColor = isConnected ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = $"Error: {ex.Message}";
                QuickBooksStatusColor = Brushes.Red;
            }
        }

        [RelayCommand]
        private async Task ValidateLicenseAsync()
        {
            try
            {
                SyncfusionLicenseStatus = "Validating...";
                SyncfusionLicenseStatusColor = Brushes.Orange;

                var isValid = await _syncfusionLicenseService.ValidateLicenseAsync(SyncfusionLicenseKey);
                SyncfusionLicenseStatus = isValid ? "Valid" : "Invalid";
                SyncfusionLicenseStatusColor = isValid ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                SyncfusionLicenseStatus = $"Error: {ex.Message}";
                SyncfusionLicenseStatusColor = Brushes.Red;
            }
        }

        [RelayCommand]
        private async Task TestAzureConnectionAsync()
        {
            try
            {
                AzureConnectionStatus = "Testing...";
                AzureStatusColor = Brushes.Orange;

                var isConnected = await _azureKeyVaultService.TestConnectionAsync();
                AzureConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                AzureStatusColor = isConnected ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                AzureConnectionStatus = $"Error: {ex.Message}";
                AzureStatusColor = Brushes.Red;
            }
        }

        /// <summary>
        /// Apply theme to all open windows in the application
        /// </summary>
        private void ApplyThemeToAllWindows(string themeName)
        {
            try
            {
                // Apply to main window
                if (Application.Current.MainWindow != null)
                {
                    ThemeUtility.TryApplyTheme(Application.Current.MainWindow, themeName);
                }

                // Apply to all other windows
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != Application.Current.MainWindow)
                    {
                        ThemeUtility.TryApplyTheme(window, themeName);
                    }
                }

                // Save the theme preference
                SettingsService.Instance.Current.Theme = themeName;
                SettingsService.Instance.Save();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to apply theme {ThemeName}", themeName);
            }
        }
    }
}