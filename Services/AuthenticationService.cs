using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
#nullable enable

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for handling Azure AD authentication via MSAL.
    /// This implementation replaces prior corrupted/duplicated content.
    /// </summary>
    public class AuthenticationService
    {
        private IPublicClientApplication? _app;
        private readonly AzureAdConfig _config;
        private readonly bool _isConfigured;

        public event EventHandler<AuthenticationEventArgs>? AuthenticationStateChanged;

        public bool IsAuthenticated
        {
            get
            {
                if (!_isConfigured || _app == null) return false;
                return _app.GetAccountsAsync().GetAwaiter().GetResult().Any();
            }
        }

        public IAccount? CurrentAccount
        {
            get
            {
                if (!_isConfigured || _app == null) return null;
                return _app.GetAccountsAsync().GetAwaiter().GetResult().FirstOrDefault();
            }
        }

        public AuthenticationService(IConfiguration configuration)
        {
            _config = new AzureAdConfig();
            configuration.GetSection("AzureAd").Bind(_config);

            if (string.IsNullOrWhiteSpace(_config.ClientId))
            {
                _isConfigured = false;
                System.Diagnostics.Debug.WriteLine("AuthenticationService: AzureAd:ClientId not configured. Service created in unconfigured state.");
                return;
            }

            _isConfigured = true;

            try
            {
                var builder = PublicClientApplicationBuilder
                    .Create(_config.ClientId)
                    .WithAuthority(_config.Authority)
                    .WithDefaultRedirectUri();

                // First build the app
                _app = builder.Build();

                // Enable token cache persistence (best effort)
                try
                {
                    TokenCacheHelper.EnableSerialization(_app);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Token cache init failed: {ex.Message}");
                }

                // Try to build a brokered variant and prefer it if available (best effort)
                try
                {
                    var brokerBuilder = PublicClientApplicationBuilder.Create(_config.ClientId)
                        .WithAuthority(_config.Authority)
                        .WithDefaultRedirectUri();

                    var brokeredApp = brokerBuilder.Build();
                    try
                    {
                        TokenCacheHelper.EnableSerialization(brokeredApp);
                    }
                    catch { /* ignore */ }

                    // prefer brokered app if build succeeded
                    _app = brokeredApp;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Broker not available: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize MSAL: {ex.Message}");
                _isConfigured = false;
                _app = null;
            }
        }

        private void EnsureConfigured()
        {
            if (!_isConfigured || _app == null)
                throw new AuthenticationException("AuthenticationService is not configured. AzureAd:ClientId must be set to use authentication features.");
        }

        /// <summary>
        /// Signs in the user interactively (or silently if an account is present)
        /// </summary>
        public async Task<AuthenticationResult> SignInAsync()
        {
            EnsureConfigured();
            try
            {
                var accounts = await _app!.GetAccountsAsync();
                AuthenticationResult result;

                if (accounts.Any())
                {
                    // Try silent authentication first
                    result = await _app.AcquireTokenSilent(new[] { "User.Read" }, accounts.First())
                        .ExecuteAsync();
                }
                else
                {
                    // Interactive authentication
                    result = await _app.AcquireTokenInteractive(new[] { "User.Read" })
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync();
                }

                OnAuthenticationStateChanged(true);
                return result;
            }
            catch (MsalException ex)
            {
                OnAuthenticationStateChanged(false);
                throw new AuthenticationException($"Authentication failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Signs out the current user
        /// </summary>
        public async Task SignOutAsync()
        {
            EnsureConfigured();
            try
            {
                var accounts = await _app!.GetAccountsAsync();
                foreach (var account in accounts)
                {
                    await _app.RemoveAsync(account);
                }
                OnAuthenticationStateChanged(false);
            }
            catch (Exception ex)
            {
                throw new AuthenticationException($"Sign out failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Acquires an access token silently
        /// </summary>
        public async Task<string> GetAccessTokenAsync()
        {
            EnsureConfigured();
            try
            {
                var accounts = await _app!.GetAccountsAsync();
                if (!accounts.Any())
                {
                    throw new AuthenticationException("No user is signed in");
                }

                var result = await _app.AcquireTokenSilent(new[] { "User.Read" }, accounts.First())
                    .ExecuteAsync();

                return result.AccessToken;
            }
            catch (MsalException ex)
            {
                throw new AuthenticationException($"Failed to acquire access token: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets user information from the current account
        /// </summary>
        public UserInfo GetUserInfo()
        {
            EnsureConfigured();
            var account = CurrentAccount;
            if (account == null)
            {
                throw new AuthenticationException("No user is signed in");
            }

            return new UserInfo
            {
                Username = account.Username,
                Name = account.Username, // Could be enhanced to get display name from Graph API
                AccountId = account.HomeAccountId?.Identifier ?? string.Empty
            };
        }

        private void OnAuthenticationStateChanged(bool isAuthenticated)
        {
            AuthenticationStateChanged?.Invoke(this, new AuthenticationEventArgs(isAuthenticated));
        }
    }

    /// <summary>
    /// Event arguments for authentication state changes
    /// </summary>
    public class AuthenticationEventArgs : EventArgs
    {
        public bool IsAuthenticated { get; }

        public AuthenticationEventArgs(bool isAuthenticated)
        {
            IsAuthenticated = isAuthenticated;
        }
    }

    /// <summary>
    /// User information
    /// </summary>
    public class UserInfo
    {
        public string Username { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Authentication exception
    /// </summary>
    public class AuthenticationException : Exception
    {
        public AuthenticationException(string message) : base(message) { }

        public AuthenticationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
    