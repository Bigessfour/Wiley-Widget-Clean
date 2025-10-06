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

namespace WileyWidget.Services;

#pragma warning disable CS8600, CS8602 // Suppress nullable reference warnings for QuickBooks SDK compatibility

/// <summary>
/// QuickBooks service using Intuit SDK + (placeholder) interactive flow. NOTE: MSAL does not directly broker Intuit auth codes; retained for future refinement.
/// For now implement token refresh + DataService access; initial interactive acquisition still handled by prior manual flow (to be unified later).
/// </summary>
public sealed class QuickBooksService : IQuickBooksService
{
    private readonly string _redirectUri = "http://localhost:8080/callback";
    private readonly string _environment = "sandbox";
    private readonly OAuth2Client? _oauthClient;
    private readonly MockQuickBooksDataService? _mockDataService;
    private readonly SettingsService _settings;
    private bool _useMockData;
    private string? _clientId;
    private string _clientSecret;
    private string? _realmId;

    public QuickBooksService(SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        // Check if we have the required environment variables
        _clientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID", EnvironmentVariableTarget.User);
        _clientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET", EnvironmentVariableTarget.User) ?? string.Empty;
        _realmId = Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User);

        _useMockData = string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_realmId);

        if (_useMockData)
        {
            _mockDataService = new MockQuickBooksDataService();
            Serilog.Log.Information("QBO API keys not configured. Using mock data for testing.");
        }
        else
        {
            _oauthClient = new OAuth2Client(_clientId, _clientSecret, _redirectUri, _environment);
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
        if (_oauthClient == null)
            throw new InvalidOperationException("OAuth client not initialized");
        var s = _settings.Current;
        ArgumentNullException.ThrowIfNull(s.QboRefreshToken);
        var response = await _oauthClient.RefreshTokenAsync(s.QboRefreshToken);
        s.QboAccessToken = response.AccessToken;
        s.QboRefreshToken = response.RefreshToken!;
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

    private sealed class DataServiceContext
    {
        public required ServiceContext Ctx { get; init; } = null!;
        public required DataService Ds { get; init; } = null!;
    }

    private DataServiceContext GetDataService()
    {
        var s = _settings.Current;
        ArgumentNullException.ThrowIfNull(s.QboAccessToken);
        if (!HasValidAccessToken()) throw new InvalidOperationException("Access token invalid – refresh required.");
        var validator = new Intuit.Ipp.Security.OAuth2RequestValidator(s.QboAccessToken);
        var ctx = new ServiceContext(_realmId, IntuitServicesType.QBO, validator);
        ctx.IppConfiguration.BaseUrl.Qbo = _environment == "sandbox" ? "https://sandbox-quickbooks.api.intuit.com/" : "https://quickbooks.api.intuit.com/";
        return new DataServiceContext { Ctx = ctx, Ds = new DataService(ctx)! };
    }

    public async System.Threading.Tasks.Task<bool> TestConnectionAsync()
    {
        if (_useMockData)
        {
            // Mock data is always available
            return true;
        }

        if (_oauthClient == null)
            throw new InvalidOperationException("OAuth client not initialized");

        try
        {
            await RefreshTokenIfNeededAsync();
            var dsc = GetDataService();
            // Try to fetch a small amount of data to test the connection
            if (dsc.Ds == null)
                return false;
            var customers = dsc.Ds.FindAll(new Customer(), 1, 1).ToList();
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
        if (_useMockData)
        {
            return _mockDataService.GenerateMockCustomers();
        }

        try
        {
            await RefreshTokenIfNeededAsync();
            var dsc = GetDataService();
            // Fetch customers from QuickBooks
            if (dsc.Ds == null)
                throw new InvalidOperationException("DataService is not initialized");
            return dsc.Ds.FindAll(new Customer(), 1, 100).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO customers fetch failed");
            throw;
        }
    }

    public async System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string enterprise = "")
    {
        if (_useMockData)
        {
            var customers = _mockDataService.GenerateMockCustomers();
            var invoices = _mockDataService.GenerateMockInvoices(customers);

            if (!string.IsNullOrWhiteSpace(enterprise))
            {
                // Filter invoices by enterprise (mock implementation)
                return invoices.Where(i => !string.IsNullOrEmpty(i.CustomerRef?.name) && i.CustomerRef.name.Contains(enterprise)).ToList();
            }

            return invoices;
        }

        try
        {
            await RefreshTokenIfNeededAsync();
            var dsc = GetDataService();
            if (string.IsNullOrWhiteSpace(enterprise))
            {
                if (dsc.Ds == null)
                    throw new InvalidOperationException("DataService is not initialized");
                return dsc.Ds.FindAll(new Invoice(), 1, 100).ToList();
            }
            var query = $"SELECT * FROM Invoice WHERE Metadata.CustomField['Enterprise'] = '{enterprise}'";
            var qs = new QueryService<Invoice>(dsc.Ctx);
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
        if (_useMockData)
        {
            return _mockDataService.GenerateMockChartOfAccounts();
        }

        try
        {
            await RefreshTokenIfNeededAsync();
            var dsc = GetDataService();
            // Fetch all active accounts from QuickBooks
            if (dsc.Ds == null)
                throw new InvalidOperationException("DataService is not initialized");
            return dsc.Ds.FindAll(new Account(), 1, 500).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO chart of accounts fetch failed");
            throw;
        }
    }

    public async System.Threading.Tasks.Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate)
    {
        if (_useMockData)
        {
            var accounts = _mockDataService.GenerateMockChartOfAccounts();
            return _mockDataService.GenerateMockJournalEntries(startDate, endDate, accounts);
        }

        try
        {
            await RefreshTokenIfNeededAsync();
            var dsc = GetDataService();

            // Query journal entries within date range
            var query = $"SELECT * FROM JournalEntry WHERE TxnDate >= '{startDate:yyyy-MM-dd}' AND TxnDate <= '{endDate:yyyy-MM-dd}'";
            var qs = new QueryService<JournalEntry>(dsc.Ctx);
            return qs.ExecuteIdsQuery(query).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO journal entries fetch failed");
            throw;
        }
    }

    // public async System.Threading.Tasks.Task<List<Budget>> GetBudgetsAsync()
    // {
    //     try
    //     {
    //         await RefreshTokenIfNeededAsync();
    //         var p = GetDataService();
    //         // Fetch budgets from QuickBooks
    //         return p.Ds.FindAll(new Budget(), 1, 100).ToList();
    //     }
    //     catch (Exception ex)
    //     {
    //         Serilog.Log.Error(ex, "QBO budgets fetch failed");
    //         throw;
    //     }
    // }
}
