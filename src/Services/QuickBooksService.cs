using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using Intuit.Ipp.OAuth2PlatformClient;
using Intuit.Ipp.Security;
using Intuit.Ipp.QueryFilter;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// QuickBooks service using Intuit SDK + (placeholder) interactive flow. NOTE: MSAL does not directly broker Intuit auth codes; retained for future refinement.
/// For now implement token refresh + DataService access; initial interactive acquisition still handled by prior manual flow (to be unified later).
/// </summary>
public sealed class QuickBooksService : IQuickBooksService
{
    private readonly ILogger<QuickBooksService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri = "http://localhost:8080/callback";
    private readonly string _realmId;
    private readonly string _environment;
    private readonly OAuth2Client _oauthClient;
    private readonly SettingsService _settings;

    public QuickBooksService(SettingsService settings, IAzureKeyVaultService keyVaultService, ILogger<QuickBooksService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load QBO credentials from Azure Key Vault with fallback to environment variables
        _clientId = TryGetFromKeyVault(keyVaultService, "QBO-CLIENT-ID", logger) ??
                   Environment.GetEnvironmentVariable("QBO_CLIENT_ID", EnvironmentVariableTarget.User) ??
                   throw new InvalidOperationException("QBO_CLIENT_ID not found in Key Vault or environment variables.");

        _clientSecret = TryGetFromKeyVault(keyVaultService, "QBO-CLIENT-SECRET", logger) ??
                       Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET", EnvironmentVariableTarget.User) ??
                       string.Empty;

        _realmId = TryGetFromKeyVault(keyVaultService, "QBO-REALM-ID", logger) ??
                  Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User) ??
                  throw new InvalidOperationException("QBO_REALM_ID not found in Key Vault or environment variables.");

        _environment = TryGetFromKeyVault(keyVaultService, "QBO-ENVIRONMENT", logger) ??
                      Environment.GetEnvironmentVariable("QBO_ENVIRONMENT", EnvironmentVariableTarget.User) ??
                      "sandbox";

        _oauthClient = new OAuth2Client(_clientId, _clientSecret, _redirectUri, _environment);

        _logger.LogInformation("QuickBooks service initialized - ClientId: {ClientIdPrefix}..., RealmId: {RealmId}, Environment: {Environment}",
            _clientId.Substring(0, Math.Min(8, _clientId.Length)), _realmId, _environment);
    }

    private static string? TryGetFromKeyVault(IAzureKeyVaultService? keyVaultService, string secretName, ILogger logger)
    {
        try
        {
            if (keyVaultService == null)
            {
                logger.LogDebug("Azure Key Vault service not available for {SecretName}", secretName);
                return null;
            }

            var secretValue = keyVaultService.GetSecretAsync(secretName).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(secretValue))
            {
                logger.LogInformation("Successfully loaded {SecretName} from Azure Key Vault", secretName);
                return secretValue;
            }
            else
            {
                logger.LogDebug("{SecretName} not found in Azure Key Vault", secretName);
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load {SecretName} from Azure Key Vault", secretName);
            return null;
        }
    }

    public bool HasValidAccessToken()
    {
        var s = _settings.Current;
    // Consider token valid if set and expires more than 60s from now (renew early to avoid edge expiry in-flight)
    if (string.IsNullOrWhiteSpace(s.QboAccessToken)) return false;
    // Default(DateTime) means 'unset'
    if (s.QboTokenExpiry == default) return false;
    return s.QboTokenExpiry > DateTime.UtcNow.AddSeconds(60);
    }

    public async System.Threading.Tasks.Task RefreshTokenIfNeededAsync()
    {
        var s = _settings.Current;
        if (HasValidAccessToken()) return;
        if (string.IsNullOrWhiteSpace(s.QboRefreshToken)) throw new InvalidOperationException("No QBO refresh token available. Perform initial authorization.");
        await RefreshTokenAsync();
    }

    public async System.Threading.Tasks.Task RefreshTokenAsync()
    {
        var s = _settings.Current;
        var response = await _oauthClient.RefreshTokenAsync(s.QboRefreshToken);
        s.QboAccessToken = response.AccessToken;
        s.QboRefreshToken = response.RefreshToken;
        // SDK response no longer exposes ExpiresIn strongly-typed; assume 55 minutes (typical 60) unless reflection finds property.
        var assumedLifetime = TimeSpan.FromMinutes(55);
        var expiresInProp = response.GetType().GetProperty("ExpiresIn");
        if (expiresInProp != null)
        {
            try
            {
                var val = expiresInProp.GetValue(response);
                if (val is int seconds && seconds > 0) assumedLifetime = TimeSpan.FromSeconds(seconds);
            }
            catch { }
        }
        s.QboTokenExpiry = DateTime.UtcNow.Add(assumedLifetime);
        _settings.Save();
        Serilog.Log.Information("QBO token refreshed (exp {Expiry})", s.QboTokenExpiry);
    }

    private (ServiceContext Ctx, DataService Ds) GetDataService()
    {
        var s = _settings.Current;
        if (!HasValidAccessToken()) throw new InvalidOperationException("Access token invalid – refresh required.");
        var validator = new OAuth2RequestValidator(s.QboAccessToken);
        var ctx = new ServiceContext(_realmId, IntuitServicesType.QBO, validator);
        ctx.IppConfiguration.BaseUrl.Qbo = _environment == "sandbox" ? "https://sandbox-quickbooks.api.intuit.com/" : "https://quickbooks.api.intuit.com/";
        return (ctx, new DataService(ctx));
    }

    public async System.Threading.Tasks.Task<bool> TestConnectionAsync()
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var p = GetDataService();
            // Try to fetch a small amount of data to test the connection
            var customers = p.Ds.FindAll(new Customer(), 1, 1).ToList();
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO connection test failed");
            return false;
        }
    }

    public async System.Threading.Tasks.Task<List<Customer>> GetCustomersAsync()
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var p = GetDataService();
            // Fetch customers from QuickBooks
            return p.Ds.FindAll(new Customer(), 1, 100).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO customers fetch failed");
            throw;
        }
    }

    public async System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string enterprise = null)
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var p = GetDataService();
            if (string.IsNullOrWhiteSpace(enterprise))
                return p.Ds.FindAll(new Invoice(), 1, 100).ToList();
            var query = $"SELECT * FROM Invoice WHERE Metadata.CustomField['Enterprise'] = '{enterprise}'";
            var qs = new QueryService<Invoice>(p.Ctx);
            return qs.ExecuteIdsQuery(query).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO invoices fetch failed");
            throw;
        }
    }

    public async System.Threading.Tasks.Task<List<Account>> GetChartOfAccountsAsync()
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var p = GetDataService();
            // Fetch all active accounts from QuickBooks
            return p.Ds.FindAll(new Account(), 1, 500).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO chart of accounts fetch failed");
            throw;
        }
    }

    public async System.Threading.Tasks.Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var p = GetDataService();

            // Query journal entries within date range
            var query = $"SELECT * FROM JournalEntry WHERE TxnDate >= '{startDate:yyyy-MM-dd}' AND TxnDate <= '{endDate:yyyy-MM-dd}'";
            var qs = new QueryService<JournalEntry>(p.Ctx);
            return qs.ExecuteIdsQuery(query).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO journal entries fetch failed");
            throw;
        }
    }

    public async System.Threading.Tasks.Task<List<Budget>> GetBudgetsAsync()
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var p = GetDataService();
            // Fetch budgets from QuickBooks
            return p.Ds.FindAll(new Budget(), 1, 100).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO budgets fetch failed");
            throw;
        }
    }
}
