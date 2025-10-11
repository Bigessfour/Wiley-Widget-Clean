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

namespace WileyWidget.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly AppDbContext _dbContext;
    private readonly ISecretVaultService _secretVaultService;
        private readonly IQuickBooksService _quickBooksService;
        private readonly ISyncfusionLicenseService _syncfusionLicenseService;

        // General Settings
        [ObservableProperty]
        private ObservableCollection<string> availableThemes = new() { "FluentDark", "FluentLight" };

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
        private ObservableCollection<string> quickBooksEnvironments = new() { "Sandbox", "Production" };

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

        // Fiscal Year Settings
        [ObservableProperty]
        private ObservableCollection<MonthOption> fiscalYearMonths = new()
        {
            new MonthOption { Name = "January", Value = 1 },
            new MonthOption { Name = "February", Value = 2 },
            new MonthOption { Name = "March", Value = 3 },
            new MonthOption { Name = "April", Value = 4 },
            new MonthOption { Name = "May", Value = 5 },
            new MonthOption { Name = "June", Value = 6 },
            new MonthOption { Name = "July (Common)", Value = 7 },
            new MonthOption { Name = "August", Value = 8 },
            new MonthOption { Name = "September", Value = 9 },
            new MonthOption { Name = "October", Value = 10 },
            new MonthOption { Name = "November", Value = 11 },
            new MonthOption { Name = "December", Value = 12 }
        };

        [ObservableProperty]
        private int fiscalYearStartMonth = 7; // Default to July

        [ObservableProperty]
        private int fiscalYearStartDay = 1;

        [ObservableProperty]
        private string currentFiscalYearDisplay = "Loading...";

        [ObservableProperty]
        private string fiscalYearPeriodDisplay = "Loading...";

        [ObservableProperty]
        private int daysRemainingInFiscalYear;

        [ObservableProperty]
        private ObservableCollection<string> availableFiscalYears = new();

        // Advanced Settings
        [ObservableProperty]
        private bool enableDynamicColumns = true;

        [ObservableProperty]
        private bool enableDataCaching = true;

        [ObservableProperty]
        private int cacheExpirationMinutes = 30;

        [ObservableProperty]
        private ObservableCollection<string> logLevels = new() { "Debug", "Information", "Warning", "Error", "Critical" };

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
            ISecretVaultService secretVaultService,
            IQuickBooksService quickBooksService,
            ISyncfusionLicenseService syncfusionLicenseService)
        {
            // Validate required dependencies
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _secretVaultService = secretVaultService ?? throw new ArgumentNullException(nameof(secretVaultService));
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _syncfusionLicenseService = syncfusionLicenseService ?? throw new ArgumentNullException(nameof(syncfusionLicenseService));

            // Initialize system info
            SystemInfo = $"OS: {Environment.OSVersion}\n" +
                        $".NET Version: {Environment.Version}\n" +
                        $"Machine: {Environment.MachineName}\n" +
                        $"User: {Environment.UserName}";

            // Set up property change tracking for unsaved changes
            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HasUnsavedChanges) &&
                e.PropertyName != nameof(SettingsStatus) &&
                e.PropertyName != nameof(LastSaved))
            {
                HasUnsavedChanges = true;
            }
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                SettingsStatus = "Loading settings...";

                // Load from configuration and database
                await LoadGeneralSettingsAsync();
                await LoadDatabaseSettingsAsync();
                await LoadQuickBooksSettingsAsync();
                await LoadSyncfusionSettingsAsync();
                await LoadAdvancedSettingsAsync();
                await LoadFiscalYearDisplayAsync();

                SettingsStatus = "Settings loaded successfully";
                HasUnsavedChanges = false;
                LastSaved = DateTime.Now.ToString("g");

                _logger.LogInformation("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                SettingsStatus = "Error loading settings";
                _logger.LogError(ex, "Error loading settings");
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error",
                              MessageBoxButton.OK);
            }
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
                //     QuickBooksClientId = await _secretVaultService.GetSecretAsync("QuickBooks-ClientId") ?? "";
                // if (string.IsNullOrEmpty(QuickBooksClientSecret))
                //     QuickBooksClientSecret = await _secretVaultService.GetSecretAsync("QuickBooks-ClientSecret") ?? "";
                // if (string.IsNullOrEmpty(QuickBooksRedirectUri))
                //     QuickBooksRedirectUri = await _secretVaultService.GetSecretAsync("QuickBooks-RedirectUri") ?? "";
                // if (string.IsNullOrEmpty(SelectedQuickBooksEnvironment))
                //     SelectedQuickBooksEnvironment = await _secretVaultService.GetSecretAsync("QuickBooks-Environment") ?? "Sandbox";

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

                // Fallback (disabled): Uncomment to re-enable Key Vault retrieval
                // if (string.IsNullOrEmpty(SyncfusionLicenseKey))
                //     SyncfusionLicenseKey = await _secretVaultService.GetSecretAsync("Syncfusion-LicenseKey") ?? "";

                // Simple license validation - check if key exists and is not empty
                var isValid = !string.IsNullOrEmpty(SyncfusionLicenseKey);
                SyncfusionLicenseStatus = isValid ? "Valid" : "Invalid or Missing";
                SyncfusionLicenseStatusColor = isValid ? Brushes.Green : Brushes.Red;
                // Keep method async without Key Vault calls
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                SyncfusionLicenseStatus = $"Error: {ex.Message}";
                SyncfusionLicenseStatusColor = Brushes.Red;
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
            try
            {
                SettingsStatus = "Saving settings...";

                // Save to secure storage and configuration
                await SaveGeneralSettingsAsync();
                await SaveQuickBooksSettingsAsync();
                await SaveSyncfusionSettingsAsync();
                await SaveAdvancedSettingsAsync();

                SettingsStatus = "Settings saved successfully";
                HasUnsavedChanges = false;
                LastSaved = DateTime.Now.ToString("g");

                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                SettingsStatus = "Error saving settings";
                _logger.LogError(ex, "Error saving settings");
                MessageBox.Show($"Error saving settings: {ex.Message}", "Settings Error",
                              MessageBoxButton.OK);
            }
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
                await _secretVaultService.SetSecretAsync("QuickBooks-ClientId", QuickBooksClientId);

            if (!string.IsNullOrEmpty(QuickBooksClientSecret))
                await _secretVaultService.SetSecretAsync("QuickBooks-ClientSecret", QuickBooksClientSecret);

            if (!string.IsNullOrEmpty(QuickBooksRedirectUri))
                await _secretVaultService.SetSecretAsync("QuickBooks-RedirectUri", QuickBooksRedirectUri);

            await _secretVaultService.SetSecretAsync("QuickBooks-Environment", SelectedQuickBooksEnvironment);
        }

        private async Task SaveSyncfusionSettingsAsync()
        {
            if (!string.IsNullOrEmpty(SyncfusionLicenseKey))
                await _secretVaultService.SetSecretAsync("Syncfusion-LicenseKey", SyncfusionLicenseKey);
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
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                    SfSkinManager.SetTheme(Application.Current.MainWindow, new Theme(themeName));
#pragma warning restore CA2000 // Dispose objects before losing scope
                }

                // Apply to all other windows
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != Application.Current.MainWindow)
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                        SfSkinManager.SetTheme(window, new Theme(themeName));
#pragma warning restore CA2000 // Dispose objects before losing scope
                    }
                }

                // Save the theme preference
                SettingsService.Instance.Current.Theme = themeName;
                SettingsService.Instance.Save();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme {ThemeName}", themeName);
            }
        }

        [RelayCommand]
        private async Task SaveFiscalYearSettingsAsync()
        {
            try
            {
                SettingsStatus = "Saving fiscal year settings...";

                // Update FiscalYearSettings in database
                var fySettings = await _dbContext.FiscalYearSettings.FindAsync(1);
                
                if (fySettings == null)
                {
                    fySettings = new Models.FiscalYearSettings
                    {
                        Id = 1,
                        FiscalYearStartMonth = FiscalYearStartMonth,
                        FiscalYearStartDay = FiscalYearStartDay,
                        LastModified = DateTime.UtcNow
                    };
                    _dbContext.FiscalYearSettings.Add(fySettings);
                }
                else
                {
                    fySettings.FiscalYearStartMonth = FiscalYearStartMonth;
                    fySettings.FiscalYearStartDay = FiscalYearStartDay;
                    fySettings.LastModified = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                await LoadFiscalYearDisplayAsync();

                SettingsStatus = "Fiscal year settings saved successfully";
                LastSaved = DateTime.Now.ToString("g");
                
                MessageBox.Show("Fiscal year settings saved successfully.\nChanges will affect budget periods and financial reports.",
                              "Settings Saved",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SettingsStatus = $"Error saving fiscal year settings: {ex.Message}";
                _logger.LogError(ex, "Failed to save fiscal year settings");
                
                MessageBox.Show($"Failed to save fiscal year settings:\n{ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private async Task LoadFiscalYearDisplayAsync()
        {
            try
            {
                var fySettings = await _dbContext.FiscalYearSettings.FindAsync(1);
                
                if (fySettings != null)
                {
                    FiscalYearStartMonth = fySettings.FiscalYearStartMonth;
                    FiscalYearStartDay = fySettings.FiscalYearStartDay;

                    var fyStart = fySettings.GetCurrentFiscalYearStart(DateTime.Now);
                    var fyEnd = fySettings.GetCurrentFiscalYearEnd(DateTime.Now);
                    
                    var fyNumber = fyStart.Month >= 7 ? fyStart.Year + 1 : fyStart.Year;
                    CurrentFiscalYearDisplay = $"FY{fyStart.Year}-{fyEnd.Year}";
                    FiscalYearPeriodDisplay = $"{fyStart:MMMM d, yyyy} - {fyEnd:MMMM d, yyyy}";
                    
                    var daysRemaining = (int)(fyEnd - DateTime.Now).TotalDays;
                    DaysRemainingInFiscalYear = Math.Max(0, daysRemaining);

                    // Populate available fiscal years (current ± 3 years)
                    AvailableFiscalYears.Clear();
                    for (int i = -3; i <= 1; i++)
                    {
                        var year = fyNumber + i;
                        var startYear = fySettings.FiscalYearStartMonth >= 7 ? year - 1 : year;
                        AvailableFiscalYears.Add($"FY{startYear}-{startYear + 1}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load fiscal year display");
                CurrentFiscalYearDisplay = "Error loading";
                FiscalYearPeriodDisplay = "Error loading";
            }
        }
    }

    /// <summary>
    /// Helper class for month dropdown
    /// </summary>
    public class MonthOption
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}