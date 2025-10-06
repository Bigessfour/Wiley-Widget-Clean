#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace WileyWidget.Configuration;

/// <summary>
/// Strongly-typed configuration options for connection strings
/// </summary>
public class ConnectionStringsOptions
{
    /// <summary>
    /// Default database connection string (required)
    /// </summary>
    [Required(ErrorMessage = "DefaultConnection is required")]
    [ConnectionStringValidation]
    public string DefaultConnection { get; set; } = string.Empty;

    /// <summary>
    /// Azure SQL connection string (optional, used in production)
    /// </summary>
    [ConnectionStringValidation(AllowEmpty = true)]
    public string AzureConnection { get; set; } = string.Empty;
}

/// <summary>
/// Strongly-typed configuration options for QuickBooks integration
/// </summary>
public class QuickBooksOptions
{
    /// <summary>
    /// QuickBooks OAuth2 Client ID
    /// </summary>
    [Required(ErrorMessage = "QuickBooks.ClientId is required for QuickBooks integration")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// QuickBooks OAuth2 Client Secret
    /// </summary>
    [Required(ErrorMessage = "QuickBooks.ClientSecret is required for QuickBooks integration")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// QuickBooks OAuth2 Redirect URI
    /// </summary>
    [Required(ErrorMessage = "QuickBooks.RedirectUri is required for QuickBooks integration")]
    [Url(ErrorMessage = "QuickBooks.RedirectUri must be a valid URL")]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// QuickBooks environment (sandbox or production)
    /// </summary>
    [RegularExpression("^(sandbox|production)$", ErrorMessage = "QuickBooks.Environment must be either 'sandbox' or 'production'")]
    public string Environment { get; set; } = "sandbox";
}

/// <summary>
/// Strongly-typed configuration options for Azure AD authentication
/// </summary>
public class AzureAdOptions
{
    /// <summary>
    /// Azure AD authority URL
    /// </summary>
    [Required(ErrorMessage = "AzureAd.Authority is required for Azure AD authentication")]
    [Url(ErrorMessage = "AzureAd.Authority must be a valid URL")]
    public string Authority { get; set; } = "https://login.microsoftonline.com/common";

    /// <summary>
    /// Azure AD client ID
    /// </summary>
    [Required(ErrorMessage = "AzureAd.ClientId is required for Azure AD authentication")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD tenant ID
    /// </summary>
    [Required(ErrorMessage = "AzureAd.TenantId is required for Azure AD authentication")]
    public string TenantId { get; set; } = string.Empty;
}

/// <summary>
/// Custom validation attribute for connection strings
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ConnectionStringValidationAttribute : ValidationAttribute
{
    /// <summary>
    /// Whether empty strings are allowed
    /// </summary>
    public bool AllowEmpty { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var connectionString = value as string;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return AllowEmpty ? ValidationResult.Success : new ValidationResult($"{validationContext.DisplayName} cannot be empty");
        }

        // Basic validation for SQL Server connection string patterns
        var sqlServerPattern = @"^(Server|Data Source)=[^;]+;";

        if (!Regex.IsMatch(connectionString, sqlServerPattern, RegexOptions.IgnoreCase))
        {
            return new ValidationResult($"{validationContext.DisplayName} must be a valid SQL Server connection string");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Configuration options for database settings.
/// </summary>
public class DatabaseOptions
{
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;

    [System.ComponentModel.DataAnnotations.Range(10, 300, ErrorMessage = "CommandTimeout must be between 10 and 300 seconds")]
    public int CommandTimeout { get; set; } = 30;

    [System.ComponentModel.DataAnnotations.Range(0, 10, ErrorMessage = "MaxRetryCount must be between 0 and 10")]
    public int MaxRetryCount { get; set; } = 3;

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Configuration options for Azure integration settings.
/// </summary>
public class AzureOptions
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string SqlServer { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Azure Key Vault settings
    /// </summary>
    public AzureKeyVaultOptions KeyVault { get; set; } = new();
}

/// <summary>
/// Configuration options for Azure Key Vault.
/// </summary>
public class AzureKeyVaultOptions
{
    [System.ComponentModel.DataAnnotations.Url(ErrorMessage = "KeyVault.Url must be a valid URL")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Configuration options for Syncfusion licensing.
/// </summary>
public class SyncfusionOptions
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Syncfusion.LicenseKey is required for Syncfusion controls")]
    public string LicenseKey { get; set; } = string.Empty;

    public string KeyVaultSecretName { get; set; } = "Syncfusion-LicenseKey";
}