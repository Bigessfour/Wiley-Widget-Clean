using Microsoft.Extensions.Options;
using WileyWidget.Data;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Configuration;

/// <summary>
/// Configures AppOptions by loading from database and configuration
/// </summary>
public class AppOptionsConfigurator : IConfigureOptions<AppOptions>
{
    private readonly AppDbContext _dbContext;
    private readonly ISecretVaultService _secretVaultService;
    private readonly ILogger<AppOptionsConfigurator> _logger;

    public AppOptionsConfigurator(
        AppDbContext dbContext,
        ISecretVaultService secretVaultService,
        ILogger<AppOptionsConfigurator> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _secretVaultService = secretVaultService ?? throw new ArgumentNullException(nameof(secretVaultService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Configure(AppOptions options)
    {
        try
        {
            _logger.LogInformation("Configuring AppOptions from database and secrets");

            // Load from database settings
            var dbSettings = _dbContext.AppSettings.Find(1);
            if (dbSettings != null)
            {
                options.Theme = dbSettings.Theme ?? options.Theme;
                options.WindowWidth = (int)(dbSettings.WindowWidth ?? options.WindowWidth);
                options.WindowHeight = (int)(dbSettings.WindowHeight ?? options.WindowHeight);
                options.MaximizeOnStartup = dbSettings.WindowMaximized ?? options.MaximizeOnStartup;
            }

            // Load secrets asynchronously (fire and forget for now)
            Task.Run(async () =>
            {
                try
                {
                    // QuickBooks settings
                    options.QuickBooksClientId = await _secretVaultService.GetSecretAsync("QuickBooks-ClientId") ?? options.QuickBooksClientId;
                    options.QuickBooksClientSecret = await _secretVaultService.GetSecretAsync("QuickBooks-ClientSecret") ?? options.QuickBooksClientSecret;
                    options.QuickBooksRedirectUri = await _secretVaultService.GetSecretAsync("QuickBooks-RedirectUri") ?? options.QuickBooksRedirectUri;
                    options.QuickBooksEnvironment = await _secretVaultService.GetSecretAsync("QuickBooks-Environment") ?? options.QuickBooksEnvironment;

                    // Syncfusion settings
                    options.SyncfusionLicenseKey = await _secretVaultService.GetSecretAsync("Syncfusion-LicenseKey") ?? options.SyncfusionLicenseKey;

                    // XAI settings
                    options.XaiApiKey = await _secretVaultService.GetSecretAsync("XAI-ApiKey") ?? options.XaiApiKey;
                    options.XaiBaseUrl = await _secretVaultService.GetSecretAsync("XAI-BaseUrl") ?? options.XaiBaseUrl;

                    _logger.LogInformation("AppOptions secrets loaded successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load secrets for AppOptions");
                }
            });

            _logger.LogInformation("AppOptions configured successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure AppOptions");
            // Continue with default values
        }
    }
}

/// <summary>
/// Validates AppOptions configuration
/// </summary>
public class AppOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        var failures = new List<string>();

        // Validate window dimensions
        if (options.WindowWidth < 800 || options.WindowWidth > 3840)
            failures.Add("WindowWidth must be between 800 and 3840");

        if (options.WindowHeight < 600 || options.WindowHeight > 2160)
            failures.Add("WindowHeight must be between 600 and 2160");

        // Validate AI settings
        if (options.XaiTimeoutSeconds < 5 || options.XaiTimeoutSeconds > 300)
            failures.Add("XaiTimeoutSeconds must be between 5 and 300");

        if (options.Temperature < 0.0 || options.Temperature > 2.0)
            failures.Add("Temperature must be between 0.0 and 2.0");

        if (options.MaxTokens < 1 || options.MaxTokens > 4096)
            failures.Add("MaxTokens must be between 1 and 4096");

        if (options.ContextWindowSize < 1024 || options.ContextWindowSize > 32768)
            failures.Add("ContextWindowSize must be between 1024 and 32768");

        // Validate fiscal year settings
        if (options.FiscalYearStartMonth < 1 || options.FiscalYearStartMonth > 12)
            failures.Add("FiscalYearStartMonth must be between 1 and 12");

        if (options.FiscalYearStartDay < 1 || options.FiscalYearStartDay > 31)
            failures.Add("FiscalYearStartDay must be between 1 and 31");

        // Validate cache settings
        if (options.CacheExpirationMinutes < 5 || options.CacheExpirationMinutes > 1440)
            failures.Add("CacheExpirationMinutes must be between 5 and 1440");

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}