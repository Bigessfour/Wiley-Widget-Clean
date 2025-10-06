using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Serilog;
#nullable enable

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for handling Azure AD authentication via MSAL - follows Microsoft's official WPF desktop app pattern
    /// Based on: https://learn.microsoft.com/en-us/entra/identity-platform/tutorial-desktop-wpf-dotnet-sign-in-build-app
    /// </summary>
    public class AuthenticationService : IDisposable
    {
        private IPublicClientApplication? _app;
        private readonly AzureAdConfig _config;
        private readonly bool _isConfigured;
        private readonly System.Timers.Timer? _tokenRefreshTimer;

        public event EventHandler<AuthenticationEventArgs>? AuthenticationStateChanged;

    public virtual bool IsAuthenticated
    {
        get
        {
            if (!_isConfigured || _app == null) return false;
            return _app.GetAccountsAsync().GetAwaiter().GetResult().Any();
        }
    }

    public virtual IAccount? CurrentAccount
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

            // Validate configuration at startup
            var validationErrors = _config.Validate();
            if (validationErrors.Any())
            {
                _isConfigured = false;
                var errorMessage = $"Azure AD configuration validation failed:{Environment.NewLine}{string.Join(Environment.NewLine, validationErrors)}";
                System.Diagnostics.Debug.WriteLine($"AuthenticationService: {errorMessage}");
                Log.Warning("AuthenticationService: {ErrorMessage}", errorMessage);
                return;
            }

            _isConfigured = true;

            try
            {
                // Create the PublicClientApplication following Microsoft's official pattern
                _app = PublicClientApplicationBuilder
                    .Create(_config.ClientId)
                    .WithAuthority(_config.Authority)
                    .WithDefaultRedirectUri()
                    .Build();

                // Enable token cache persistence using the official TokenCacheHelper
                TokenCacheHelper.EnableSerialization(_app);

                // Initialize token refresh timer (30 minutes)
                _tokenRefreshTimer = new System.Timers.Timer(30 * 60 * 1000);
                _tokenRefreshTimer.Elapsed += OnTokenRefreshTimerElapsed;
                _tokenRefreshTimer.AutoReset = true;
                _tokenRefreshTimer.Start();

                Log.Information("AuthenticationService initialized successfully with client ID: {ClientId}", _config.ClientId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize MSAL: {ex.Message}");
                _isConfigured = false;
                _app = null;
                Log.Error(ex, "Failed to initialize Microsoft Authentication Library");
            }
        }

        /// <summary>
        /// Parameterless constructor used by tests and scenarios where configuration is not available.
        /// Creates the service in an unconfigured state.
        /// </summary>
        public AuthenticationService()
        {
            _config = new AzureAdConfig();
            _isConfigured = false;
            _app = null;
        }

        private void EnsureConfigured()
        {
            if (!_isConfigured || _app == null)
                throw new AuthenticationException("AuthenticationService is not configured. AzureAd:ClientId must be set to use authentication features.");
        }

        /// <summary>
        /// Attempts silent authentication only - no UI interaction
        /// Returns null if user is not already signed in
        /// </summary>
        public async Task<AuthenticationResult?> TrySilentSignInAsync()
        {
            if (!_isConfigured || _app == null)
                return null;

            try
            {
                var accounts = await _app.GetAccountsAsync();
                if (!accounts.Any())
                {
                    Log.Debug("No cached accounts found for silent authentication");
                    return null;
                }

                var result = await _app.AcquireTokenSilent(_config.Scopes, accounts.First())
                    .ExecuteAsync();

                Log.Information("Silent authentication successful for user: {UserName}", result.Account.Username);
                OnAuthenticationStateChanged(true);
                return result;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Silent authentication failed - user not signed in or token expired");
                return null;
            }
        }

        /// <summary>
        /// Signs in the user interactively (or silently if an account is present)
        /// </summary>
        public async Task<AuthenticationResult> SignInAsync()
        {
            EnsureConfigured();

            var accounts = await _app!.GetAccountsAsync();
            AuthenticationResult result;

            if (accounts.Any())
            {
                // Try silent authentication first
                try
                {
                    result = await _app.AcquireTokenSilent(_config.Scopes, accounts.First())
                        .ExecuteAsync();
                    Log.Debug("Silent authentication successful for user: {UserName}", result.Account.Username);
                }
                catch (MsalUiRequiredException)
                {
                    // Silent authentication failed, fall back to interactive
                    result = await _app.AcquireTokenInteractive(_config.Scopes)
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync();
                }
            }
            else
            {
                // No cached accounts, use interactive authentication
                result = await _app.AcquireTokenInteractive(_config.Scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync();
            }

            OnAuthenticationStateChanged(true);
            Log.Information("User successfully authenticated: {UserName}", result.Account.Username);
            return result;
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
        /// Acquires an access token silently with fallback to interactive authentication
        /// </summary>
        public async Task<string> GetAccessTokenAsync()
        {
            EnsureConfigured();

            async Task<string> AcquireToken()
            {
                // Simulate slow token acquisition for testing timing
                await Task.Delay(1500);

                var accounts = await _app!.GetAccountsAsync();
                if (!accounts.Any())
                {
                    throw new AuthenticationException("No user is signed in");
                }

                try
                {
                    var result = await _app.AcquireTokenSilent(new[] { "User.Read" }, accounts.First())
                        .ExecuteAsync();
                    Log.Debug("Access token acquired successfully for user: {UserName}", result.Account.Username);
                    return result.AccessToken;
                }
                catch (MsalUiRequiredException ex)
                {
                    // Token expired or requires user interaction, try to refresh silently first
                    Log.Debug("Silent token acquisition failed, attempting refresh: {Message}", ex.Message);

                    try
                    {
                        // Try interactive authentication as fallback
                        var result = await _app.AcquireTokenInteractive(new[] { "User.Read" })
                            .WithPrompt(Prompt.NoPrompt) // Try to avoid prompting if possible
                            .ExecuteAsync();
                        Log.Debug("Token refreshed via interactive fallback for user: {UserName}", result.Account.Username);
                        return result.AccessToken;
                    }
                    catch (MsalException refreshEx)
                    {
                        Log.Warning(refreshEx, "Interactive token refresh also failed");
                        throw new AuthenticationException($"Token acquisition failed: {refreshEx.Message}", refreshEx);
                    }
                }
                catch (MsalException ex)
                {
                    Log.Warning(ex, "Silent token acquisition failed with MSAL error");
                    throw new AuthenticationException($"Failed to acquire access token: {ex.Message}", ex);
                }
            }

            try
            {
                // Try to acquire token with retry mechanism
                var recovered = await ErrorReportingService.Instance.TryRecoverAsync(
                    null, // No initial exception
                    "Authentication",
                    async () => {
                        await AcquireToken();
                        return true; // Success
                    });

                if (recovered)
                {
                    // If recovery succeeded, we need to get the token again
                    return await AcquireToken();
                }
                else
                {
                    // If no recovery or recovery failed, try once more without recovery
                    return await AcquireToken();
                }
            }
            catch (MsalException ex)
            {
                ErrorReportingService.Instance.ReportError(ex, "Authentication_GetToken", showToUser: true);
                throw new AuthenticationException($"Failed to acquire access token: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets user information from the current account
        /// </summary>
    public virtual UserInfo GetUserInfo()
        {
            EnsureConfigured();
            var account = CurrentAccount;
            if (account == null)
            {
                throw new AuthenticationException("No user is signed in");
            }

            var userInfo = new UserInfo
            {
                Username = account.Username,
                Name = account.Username, // Could be enhanced to get display name from Graph API
                AccountId = account.HomeAccountId?.Identifier ?? string.Empty,
                Email = account.Username?.Contains("@") == true ? account.Username : string.Empty
            };

            // Simple role assignment logic (can be enhanced with Azure AD groups later)
            userInfo.Roles = new List<string> { "User" }; // Default role

            // Assign Admin role based on username/email patterns (customize as needed)
            var uname = account.Username ?? string.Empty;
            if (uname.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
                uname.EndsWith("@yourcompany.com", StringComparison.OrdinalIgnoreCase))
            {
                userInfo.Roles.Add("Admin");
            }

            return userInfo;
        }

        private void OnAuthenticationStateChanged(bool isAuthenticated)
        {
            AuthenticationStateChanged?.Invoke(this, new AuthenticationEventArgs(isAuthenticated));
        }

        private async void OnTokenRefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsAuthenticated) return;

            try
            {
                // Try to refresh the token silently
                await GetAccessTokenAsync();
                System.Diagnostics.Debug.WriteLine("Token refreshed successfully");
            }
            catch (AuthenticationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token refresh failed: {ex.Message}");
                // If refresh fails, the user might need to re-authenticate
                OnAuthenticationStateChanged(false);
                Log.Warning(ex, "Automatic token refresh failed, user may need to re-authenticate");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error during token refresh: {ex.Message}");
                OnAuthenticationStateChanged(false);
                Log.Error(ex, "Unexpected error during automatic token refresh");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tokenRefreshTimer?.Dispose();
            }
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
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public bool IsAdmin => Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        public bool IsUser => Roles.Contains("User", StringComparer.OrdinalIgnoreCase);
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
    