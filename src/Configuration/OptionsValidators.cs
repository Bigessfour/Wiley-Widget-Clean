using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace WileyWidget.Configuration;

/// <summary>
/// Custom validation for ConnectionStringsOptions
/// </summary>
public class ConnectionStringsOptionsValidator : IValidateOptions<ConnectionStringsOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectionStringsOptions options)
    {
        var failures = new List<string>();

        // Validate that at least one connection string is properly configured
        if (string.IsNullOrWhiteSpace(options.DefaultConnection) &&
            string.IsNullOrWhiteSpace(options.AzureConnection))
        {
            failures.Add("At least one connection string (DefaultConnection or AzureConnection) must be configured");
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Custom validation for AzureOptions
/// </summary>
public class AzureOptionsValidator : IValidateOptions<AzureOptions>
{
    public ValidateOptionsResult Validate(string? name, AzureOptions options)
    {
        var failures = new List<string>();

        // If any Azure setting is configured, validate related settings are also present
        var hasAnyAzureSetting = !string.IsNullOrWhiteSpace(options.SubscriptionId) ||
                                !string.IsNullOrWhiteSpace(options.TenantId) ||
                                !string.IsNullOrWhiteSpace(options.SqlServer) ||
                                !string.IsNullOrWhiteSpace(options.Database) ||
                                !string.IsNullOrWhiteSpace(options.KeyVault?.Url);

        if (hasAnyAzureSetting)
        {
            // If Azure SQL settings are partially configured, require all SQL settings
            var hasSqlServer = !string.IsNullOrWhiteSpace(options.SqlServer);
            var hasSqlDatabase = !string.IsNullOrWhiteSpace(options.Database);

            if (hasSqlServer != hasSqlDatabase)
            {
                failures.Add("Both SqlServer and Database must be configured together for Azure SQL");
            }

            // If Key Vault URL is configured, validate it's a proper absolute URI
            if (!string.IsNullOrWhiteSpace(options.KeyVault?.Url))
            {
                if (!Uri.TryCreate(options.KeyVault.Url, UriKind.Absolute, out _))
                {
                    failures.Add("KeyVault.Url must be a valid absolute URI");
                }
            }
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Custom validation for QuickBooksOptions
/// </summary>
public class QuickBooksOptionsValidator : IValidateOptions<QuickBooksOptions>
{
    public ValidateOptionsResult Validate(string? name, QuickBooksOptions options)
    {
        var failures = new List<string>();

        // If any QuickBooks setting is configured, require all required settings
        var hasAnyQuickBooksSetting = !string.IsNullOrWhiteSpace(options.ClientId) ||
                                     !string.IsNullOrWhiteSpace(options.ClientSecret) ||
                                     !string.IsNullOrWhiteSpace(options.RedirectUri);

        if (hasAnyQuickBooksSetting)
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
                failures.Add("ClientId is required when QuickBooks integration is configured");

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
                failures.Add("ClientSecret is required when QuickBooks integration is configured");

            if (string.IsNullOrWhiteSpace(options.RedirectUri))
                failures.Add("RedirectUri is required when QuickBooks integration is configured");

            // Validate redirect URI is HTTPS in production
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                             "Development";

            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
                !options.RedirectUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("RedirectUri must use HTTPS in production environment");
            }
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Custom validation for SyncfusionOptions
/// </summary>
public class SyncfusionOptionsValidator : IValidateOptions<SyncfusionOptions>
{
    public ValidateOptionsResult Validate(string? name, SyncfusionOptions options)
    {
        var failures = new List<string>();

        // License key is always required for Syncfusion controls
        if (string.IsNullOrWhiteSpace(options.LicenseKey))
        {
            failures.Add("LicenseKey is required for Syncfusion controls. Configure via appsettings or environment variable SYNCFUSION_LICENSE_KEY");
        }
        else
        {
            // Basic validation that license key looks like a Syncfusion license
            if (!options.LicenseKey.Contains("@") || options.LicenseKey.Length < 50)
            {
                failures.Add("LicenseKey appears to be invalid. Please verify it's a valid Syncfusion license key");
            }
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Custom validation for AzureAdOptions
/// </summary>
public class AzureAdOptionsValidator : IValidateOptions<AzureAdOptions>
{
    public ValidateOptionsResult Validate(string? name, AzureAdOptions options)
    {
        var failures = new List<string>();

        // If any Azure AD setting is configured, require all required settings
        var hasAnyAzureAdSetting = !string.IsNullOrWhiteSpace(options.Authority) ||
                                  !string.IsNullOrWhiteSpace(options.ClientId) ||
                                  !string.IsNullOrWhiteSpace(options.TenantId);

        if (hasAnyAzureAdSetting)
        {
            if (string.IsNullOrWhiteSpace(options.Authority))
                failures.Add("Authority is required when Azure AD authentication is configured");

            if (string.IsNullOrWhiteSpace(options.ClientId))
                failures.Add("ClientId is required when Azure AD authentication is configured");

            if (string.IsNullOrWhiteSpace(options.TenantId))
                failures.Add("TenantId is required when Azure AD authentication is configured");

            // Validate authority URL format
            if (!string.IsNullOrWhiteSpace(options.Authority) &&
                !options.Authority.StartsWith("https://login.microsoftonline.com/", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("Authority must be a valid Azure AD authority URL (starting with 'https://login.microsoftonline.com/')");
            }
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}