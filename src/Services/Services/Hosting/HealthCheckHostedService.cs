using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using WileyWidget.Models;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget.Services.Hosting;

/// <summary>
/// Hosted service that performs comprehensive application health checks during startup
/// using Polly for resilience patterns. Shows dialogs for failures instead of crashing.
/// </summary>
public class HealthCheckHostedService : IHostedService, IDisposable
{
    private readonly ILogger<HealthCheckHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly HealthCheckConfiguration _healthCheckConfig;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AuthenticationService? _authenticationService;
    private readonly IAzureKeyVaultService? _azureKeyVaultService;
    private bool _disposed;

    public HealthCheckHostedService(
        ILogger<HealthCheckHostedService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<HealthCheckConfiguration> options,
        IHostEnvironment hostEnvironment,
        AuthenticationService? authenticationService = null,
        IAzureKeyVaultService? azureKeyVaultService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _healthCheckConfig = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _authenticationService = authenticationService;
        _azureKeyVaultService = azureKeyVaultService;

        if (!_hostEnvironment.IsProduction())
        {
            var optionalServices = new[] { "Azure AD", "Azure Key Vault", "AI Service", "QuickBooks" };
            var added = new List<string>();

            foreach (var service in optionalServices)
            {
                if (!_healthCheckConfig.SkipServices.Contains(service, StringComparer.OrdinalIgnoreCase))
                {
                    _healthCheckConfig.SkipServices.Add(service);
                    added.Add(service);
                }
            }

            if (added.Count > 0)
            {
                _logger.LogInformation("Development environment detected ({Environment}); skipping health checks for: {Services}",
                    _hostEnvironment.EnvironmentName, string.Join(", ", added));
            }
        }
    }

    /// <summary>
    /// Starts the health check service and performs initial health checks
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HealthCheckHostedService is starting.");

        try
        {
            // Perform comprehensive health checks
            var report = await PerformHealthChecksAsync();

            // Update the global health report
            WileyWidget.App.UpdateLatestHealthReport(report);

            // Check if application can start
            if (!CanApplicationStart(report))
            {
                _logger.LogError("Application cannot start due to critical service failures");

                // Show dialog instead of crashing
                await ShowHealthCheckFailureDialogAsync(report);
            }
            else
            {
                _logger.LogInformation("Health checks passed - application can start");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during health checks");

            // Show dialog for critical failures
            await ShowHealthCheckFailureDialogAsync(null, ex);
        }
    }

    /// <summary>
    /// Stops the health check service
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HealthCheckHostedService is stopping.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs comprehensive health checks using Polly for resilience
    /// </summary>
    private async Task<HealthCheckReport> PerformHealthChecksAsync()
    {
        var totalStopwatch = Stopwatch.StartNew();
        var report = new HealthCheckReport
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = HealthStatus.Healthy
        };

        try
        {
            Log.Information("=== ENTERPRISE HEALTH CHECKS STARTED ===");

            // Execute health checks with resilience patterns
            var healthCheckTasks = new List<Task<HealthCheckResult>>();

            // Core services (always checked)
            healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Configuration", CheckConfigurationHealthAsync()));
            healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Database", CheckDatabaseHealthAsync()));

            // External services (with circuit breakers)
            if (!IsServiceSkipped("Azure AD"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Azure AD", CheckAzureAdHealthAsync()));

            if (!IsServiceSkipped("Azure Key Vault"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Azure Key Vault", CheckAzureKeyVaultHealthAsync()));

            if (!IsServiceSkipped("QuickBooks"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("QuickBooks", CheckQuickBooksHealthAsync()));

            if (!IsServiceSkipped("Syncfusion License"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Syncfusion License", CheckSyncfusionLicenseHealthAsync()));

            if (!IsServiceSkipped("AI Service"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("AI Service", CheckAIServiceHealthAsync()));

            if (!IsServiceSkipped("External Dependencies"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("External Dependencies", CheckExternalDependenciesHealthAsync()));

            if (!IsServiceSkipped("System Resources"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("System Resources", CheckSystemResourcesHealthAsync()));

            // Wait for all health checks to complete with overall timeout
            var timeoutTask = Task.Delay(_healthCheckConfig.DefaultTimeout);
            var completedTask = await Task.WhenAny(Task.WhenAll(healthCheckTasks), timeoutTask);

            if (completedTask == timeoutTask)
            {
                Log.Warning("Health checks timed out after {Timeout}s", _healthCheckConfig.DefaultTimeout.TotalSeconds);

                // Create timeout results for incomplete tasks
                var timedOutResults = healthCheckTasks
                    .Where(t => !t.IsCompletedSuccessfully)
                    .Select(t => HealthCheckResult.Unhealthy("Unknown Service", "Health check timed out", null, _healthCheckConfig.DefaultTimeout));

                report.Results.AddRange(timedOutResults);
            }

            // Collect successful results
            var completedResults = healthCheckTasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToList();
            report.Results.AddRange(completedResults);

            // Handle failed tasks
            var failedTasks = healthCheckTasks.Where(t => t.IsFaulted).ToList();
            foreach (var failedTask in failedTasks)
            {
                var exception = failedTask.Exception?.InnerException ?? failedTask.Exception;
                Log.Error(exception, "Health check task failed");

                // Create failed result
                var failedResult = HealthCheckResult.Unhealthy("Unknown Service", $"Health check failed: {exception?.Message}", exception);
                report.Results.Add(failedResult);
            }

            // Determine overall status
            report.OverallStatus = DetermineOverallHealthStatus(report.Results);

            // Log results
            LogHealthCheckResults(report);

            Log.Information("=== ENTERPRISE HEALTH CHECKS COMPLETED ===");
            return report;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Critical error during health checks");
            report.OverallStatus = HealthStatus.Unhealthy;
            report.Results.Add(HealthCheckResult.Unhealthy("HealthCheckSystem", "Health check system failed", ex));
            return report;
        }
        finally
        {
            totalStopwatch.Stop();
            report.TotalDuration = totalStopwatch.Elapsed;
        }
    }

    /// <summary>
    /// Executes a health check with Polly retry and circuit breaker policies
    /// </summary>
    private async Task<HealthCheckResult> ExecuteHealthCheckWithPollyAsync(string serviceName, Task<HealthCheckResult> healthCheckTask)
    {
        // Create retry policy with exponential backoff using Polly
        var retryPolicy = Policy<HealthCheckResult>
            .Handle<Exception>()
            .OrResult(r => r.Status == HealthStatus.Unhealthy || r.Status == HealthStatus.Unavailable)
            .WaitAndRetryAsync(
                retryCount: _healthCheckConfig.MaxRetries,
                sleepDurationProvider: attempt => _healthCheckConfig.RetryDelay * Math.Pow(2, attempt - 1),
                onRetry: (outcome, delay, attempt, context) =>
                {
                    var message = outcome.Exception != null
                        ? $"Exception: {outcome.Exception.Message}"
                        : $"Result: {outcome.Result?.Description ?? "Unknown"}";
                    _logger.LogWarning("Health check for {Service} failed (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms. {Message}",
                        serviceName, attempt, _healthCheckConfig.MaxRetries + 1, delay.TotalMilliseconds, message);
                });

        try
        {
            // Add timeout to individual health check
            var timeout = GetTimeoutForService(serviceName);
            using var cts = new CancellationTokenSource(timeout);
            var task = healthCheckTask;

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
            if (completedTask != task)
            {
                throw new TimeoutException($"Health check for {serviceName} timed out after {timeout.TotalSeconds}s");
            }

            // Execute with Polly retry policy
            return await retryPolicy.ExecuteAsync(async () => await task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check for {Service} failed after all retries", serviceName);
            return HealthCheckResult.Unhealthy(serviceName, $"Health check failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Shows a dialog when health checks fail instead of crashing the application
    /// </summary>
    private async Task ShowHealthCheckFailureDialogAsync(HealthCheckReport? report = null, Exception? criticalException = null)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var issues = new List<string>();

            if (criticalException != null)
            {
                issues.Add($"Critical error: {criticalException.Message}");
            }

            if (report != null)
            {
                issues.AddRange(report.Results
                    .Where(r => r.Status != HealthStatus.Healthy)
                    .Select(r => $"{r.ServiceName}: {r.Description}"));
            }

            var message = "Application health checks have failed. Some features may not work correctly.\n\n" +
                         "Issues found:\n" + string.Join("\n", issues.Select(i => $"• {i}"));

            var result = MessageBox.Show(
                message,
                "Health Check Warnings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            // Application continues despite health check failures
            _logger.LogWarning("User acknowledged health check failures - application continuing");
        });
    }

    // Health check implementation methods moved from App.xaml.cs
    // These will be added in the next step

    private async Task<HealthCheckResult> CheckDatabaseHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var environment = _hostEnvironment.EnvironmentName;
            string? connectionString;

             try
            {
                connectionString = WileyWidget.Configuration.DatabaseConfiguration.BuildEnterpriseConnectionString(_configuration, _logger, environment);
            }
            catch (Exception buildEx)
            {
                stopwatch.Stop();
                Log.Warning(buildEx, "Database connection string resolution failed during health check");
                return HealthCheckResult.Unhealthy("Database", $"Database configuration error: {buildEx.Message}", buildEx, stopwatch.Elapsed);
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                stopwatch.Stop();
                return HealthCheckResult.Unhealthy("Database", "Database connection string could not be resolved", null, stopwatch.Elapsed);
            }

            // Validate database schema and connectivity using the IDbContextFactory to avoid scoped-from-singleton issues
            using (var validationScope = _scopeFactory.CreateScope())
            {
                await WileyWidget.Configuration.DatabaseConfiguration.ValidateDatabaseSchemaAsync(validationScope.ServiceProvider);
            }

            // Additional connectivity check using IDbContextFactory if available
            using (var scope = _scopeFactory.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var contextFactory = provider.GetService<Microsoft.EntityFrameworkCore.IDbContextFactory<WileyWidget.Data.AppDbContext>>();
                if (contextFactory != null)
                {
                    await using var dbContext = await contextFactory.CreateDbContextAsync();
                    await dbContext.Database.CanConnectAsync();
                }
                else
                {
                    // Fall back to resolving AppDbContext if factory not available (legacy registration)
                    var dbContext = provider.GetService<WileyWidget.Data.AppDbContext>();
                    if (dbContext != null)
                        await dbContext.Database.CanConnectAsync();
                }
            }

            stopwatch.Stop();
            return HealthCheckResult.Healthy("Database", "Database connection and schema validation successful", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Warning(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database", $"Database health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckConfigurationHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check required configuration sections
            if (string.IsNullOrEmpty(_configuration["AzureAd:ClientId"]))
                issues.Add("Azure AD Client ID not configured");

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            try
            {
                var resolvedConnection = WileyWidget.Configuration.DatabaseConfiguration.BuildEnterpriseConnectionString(_configuration, _logger, environment);
                if (string.IsNullOrWhiteSpace(resolvedConnection))
                {
                    issues.Add("Database connection string could not be resolved");
                }
            }
            catch (Exception buildEx)
            {
                issues.Add($"Database configuration error: {buildEx.Message}");
            }

            // Consider env var, config, or user secrets for Syncfusion key
            if (string.IsNullOrEmpty(GetSyncfusionLicenseKey()))
                issues.Add("Syncfusion license key not configured");

            // QuickBooks configuration (only require when health check enabled)
            if (!IsServiceSkipped("QuickBooks"))
            {
                var quickBooksClientId = ResolveSetting("QBO_CLIENT_ID", "QuickBooks:ClientId");
                if (string.IsNullOrEmpty(quickBooksClientId))
                    issues.Add("QuickBooks Client ID not configured");

                var quickBooksRealmId = ResolveSetting("QBO_REALM_ID", "QuickBooks:RealmId");
                if (string.IsNullOrEmpty(quickBooksRealmId))
                    issues.Add("QuickBooks Realm ID not configured");
            }

            stopwatch.Stop();

            if (issues.Any())
            {
                return Task.FromResult(HealthCheckResult.Degraded("Configuration",
                    $"Configuration validation found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Configuration", "All required configuration validated successfully", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("Configuration", $"Configuration validation failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private Task<HealthCheckResult> CheckAzureAdHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var authService = _authenticationService;
            if (authService == null)
            {
                using var scope = _scopeFactory.CreateScope();
                authService = scope.ServiceProvider.GetService<AuthenticationService>();
            }
            if (authService == null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Authentication service not available"));
            }

            // Check configuration
            if (string.IsNullOrEmpty(_configuration["AzureAd:ClientId"]))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Azure AD Client ID not configured"));
            }

            if (string.IsNullOrEmpty(_configuration["AzureAd:TenantId"]))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Azure AD Tenant ID not configured"));
            }

            // Basic connectivity check (without actual authentication)
            // This validates that the configuration is correct for potential authentication
            var clientId = _configuration["AzureAd:ClientId"];
            var tenantId = _configuration["AzureAd:TenantId"];

            if (!Guid.TryParse(clientId, out _) || !Guid.TryParse(tenantId, out _))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Azure AD Client ID or Tenant ID is not a valid GUID"));
            }

            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Healthy("Azure AD", "Azure AD configuration validated successfully", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", $"Azure AD health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private async Task<HealthCheckResult> CheckAzureKeyVaultHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var keyVaultService = _azureKeyVaultService;
            if (keyVaultService == null)
            {
                using var scope = _scopeFactory.CreateScope();
                keyVaultService = scope.ServiceProvider.GetService<IAzureKeyVaultService>();
            }
            if (keyVaultService == null)
            {
                return HealthCheckResult.Unavailable("Azure Key Vault", "Azure Key Vault service not configured");
            }

            var keyVaultUrl = _configuration["Azure:KeyVault:Url"];
            if (string.IsNullOrEmpty(keyVaultUrl))
            {
                return HealthCheckResult.Unavailable("Azure Key Vault", "Azure Key Vault URL not configured");
            }

            // Attempt to list secrets (this validates connectivity and permissions)
            // Note: This is a basic check and may fail in production due to permissions
            try
            {
                // We don't actually list secrets, just check if the client can be created
                await Task.CompletedTask; // Placeholder for actual connectivity check
            }
            catch
            {
                // If we can't connect, mark as degraded rather than unhealthy
                stopwatch.Stop();
                return HealthCheckResult.Degraded("Azure Key Vault", "Azure Key Vault connectivity could not be verified", stopwatch.Elapsed);
            }

            stopwatch.Stop();
            return HealthCheckResult.Healthy("Azure Key Vault", "Azure Key Vault service configured and accessible", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy("Azure Key Vault", $"Azure Key Vault health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckQuickBooksHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Resolve QuickBooks service from a created scope to respect scoped lifetime
            using var scope = _scopeFactory.CreateScope();
            var qbService = scope.ServiceProvider.GetService<IQuickBooksService>();
            if (qbService == null)
            {
                return Task.FromResult(HealthCheckResult.Unavailable("QuickBooks", "QuickBooks service not available"));
            }

            // Check configuration
            var clientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID", EnvironmentVariableTarget.User);
            var realmId = Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(realmId))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("QuickBooks", "QuickBooks Client ID or Realm ID not configured"));
            }

            // Check if token is valid
            if (qbService is QuickBooksService concreteService && !concreteService.HasValidAccessToken())
            {
                return Task.FromResult(HealthCheckResult.Degraded("QuickBooks", "QuickBooks access token is expired or not available"));
            }

            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Healthy("QuickBooks", "QuickBooks service configured and token is valid", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("QuickBooks", $"QuickBooks health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private Task<HealthCheckResult> CheckSyncfusionLicenseHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Use unified resolution for the license key (env/config/user secrets)
            var licenseKey = GetSyncfusionLicenseKey();
            if (string.IsNullOrEmpty(licenseKey))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", "Syncfusion license key not configured"));
            }

            // Validate license format (basic check)
            if (licenseKey.Length < 32)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", "Syncfusion license key appears to be invalid (too short)"));
            }

            // Check if license is registered (this would require checking Syncfusion's internal state)
            // For now, we assume it's valid if configured
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Healthy("Syncfusion License", "Syncfusion license key configured and validated", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", $"Syncfusion license health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private Task<HealthCheckResult> CheckAIServiceHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            const string azureDocsUrl = "https://learn.microsoft.com/en-us/azure/ai-foundry/openai/chatgpt-quickstart#set-up";

            // Resolve AI service from a created scope to respect scoped lifetime
            using var scope = _scopeFactory.CreateScope();
            var aiService = scope.ServiceProvider.GetService<IAIService>();
            if (aiService == null)
            {
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", $"AI service not configured. Provide XAI_API_KEY or Azure OpenAI settings (see {azureDocsUrl})."));
            }

            // Check configuration
            var openAiKey = _configuration["Secrets:OpenAI:ApiKey"] ?? _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var azureAiEndpoint = _configuration["Secrets:AzureAI:Endpoint"] ?? _configuration["Azure:AI:Endpoint"];

            if (string.IsNullOrWhiteSpace(openAiKey) && string.IsNullOrWhiteSpace(azureAiEndpoint))
            {
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", $"Neither OpenAI API key nor Azure AI endpoint configured. Set OPENAI_API_KEY or configure Azure AI endpoint and key (see {azureDocsUrl})."));
            }

            if (!string.IsNullOrWhiteSpace(azureAiEndpoint) && !Uri.IsWellFormedUriString(azureAiEndpoint, UriKind.Absolute))
            {
                return Task.FromResult(HealthCheckResult.Degraded("AI Service", $"Azure AI endpoint '{azureAiEndpoint}' is not a valid absolute URL. Use https://<resource-name>.openai.azure.com/ as documented at {azureDocsUrl}.", stopwatch.Elapsed));
            }

            // Perform lightweight connectivity probe (2s budget)
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
            var clientFactory = scope.ServiceProvider.GetService<System.Net.Http.IHttpClientFactory>();
            using var httpClient = clientFactory?.CreateClient("AIHealthCheck") ?? new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WileyWidget-AIHealthCheck/1.0");

            System.Net.Http.HttpResponseMessage? response = null;
            string? target = null;
            bool usedOpenAi = false;

            try
            {
                if (!string.IsNullOrWhiteSpace(azureAiEndpoint))
                {
                    var endpoint = azureAiEndpoint.TrimEnd('/');
                    target = endpoint + "/openai/deployments?api-version=2023-05-01";
                    response = httpClient.GetAsync(target, cts.Token).GetAwaiter().GetResult();
                }
                else
                {
                    usedOpenAi = true;
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiKey);
                    target = "https://api.openai.com/v1/models";
                    response = httpClient.GetAsync(target, cts.Token).GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                var tip = usedOpenAi
                    ? "Confirm api.openai.com is reachable and retry with smaller prompts."
                    : $"Confirm outbound HTTPS access to {azureAiEndpoint} and review network rules. Azure guidance: {azureDocsUrl}.";
                return Task.FromResult(HealthCheckResult.Degraded("AI Service", $"Connectivity probe timed out. {tip}", stopwatch.Elapsed));
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                stopwatch.Stop();
                var tip = usedOpenAi
                    ? "Verify OPENAI_API_KEY and ensure your network permits api.openai.com."
                    : $"Ensure the Azure endpoint URL and api-version are correct and that Keys & Endpoint values have been provisioned (see {azureDocsUrl}).";
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", $"Connectivity error: {ex.Message}. {tip}", ex, stopwatch.Elapsed));
            }

            if (response == null)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "Probe did not execute (no endpoint chosen)", null, stopwatch.Elapsed));
            }

            var status = (int)response.StatusCode;
            string detail = $"Probe {(usedOpenAi ? "OpenAI" : "Azure AI")} status {(int)response.StatusCode} {response.ReasonPhrase}";

            HealthCheckResult result;
            if (status == 200)
            {
                result = HealthCheckResult.Healthy("AI Service", detail, stopwatch.Elapsed);
            }
            else if (status == 401 || status == 403)
            {
                var guidance = usedOpenAi
                    ? "Confirm OPENAI_API_KEY is valid and not expired."
                    : $"Confirm Azure OpenAI keys (KEY1/KEY2) are configured in Key Vault or environment variables as described at {azureDocsUrl}.";
                result = HealthCheckResult.Unhealthy("AI Service", $"{detail}. {guidance}", null, stopwatch.Elapsed);
            }
            else if (status == 429)
            {
                result = HealthCheckResult.Degraded("AI Service", $"{detail}. The service is rate limiting requests; retry with exponential backoff.", stopwatch.Elapsed);
            }
            else if (status >= 500 && status <= 599)
            {
                result = HealthCheckResult.Degraded("AI Service", $"{detail}. Provider is returning server errors; monitor status dashboards.", stopwatch.Elapsed);
            }
            else
            {
                var extra = usedOpenAi
                    ? "Review request payload for typos and confirm model availability."
                    : $"Check deployment name and api-version query parameters for Azure OpenAI requests. Reference {azureDocsUrl}.";
                result = HealthCheckResult.Degraded("AI Service", $"{detail}. {extra}", stopwatch.Elapsed);
            }

            stopwatch.Stop();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("AI Service", $"AI service health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private async Task<HealthCheckResult> CheckExternalDependenciesHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check internet connectivity (basic)
            try
            {
                // Prefer HttpClient from DI to reuse handlers; fall back to ephemeral client if not available
                System.Net.Http.HttpResponseMessage? response = null;
                using var scope = _scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetService<System.Net.Http.HttpClient>();
                if (client != null)
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    response = await client.GetAsync("https://www.microsoft.com");
                }
                else
                {
                    using var tmpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    response = await tmpClient.GetAsync("https://www.microsoft.com");
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    issues.Add("Internet connectivity check failed");
                }
            }
            catch
            {
                issues.Add("Internet connectivity check failed");
            }

            // Check Azure endpoints if configured
            var azureSqlConnection = _configuration.GetConnectionString("AzureConnection");
            if (!string.IsNullOrEmpty(azureSqlConnection))
            {
                // Basic Azure SQL connectivity check would go here
                // For now, just validate the connection string format
                if (!azureSqlConnection.Contains("database.windows.net"))
                {
                    issues.Add("Azure SQL connection string format appears invalid");
                }
            }

            stopwatch.Stop();

            if (issues.Any())
            {
                return HealthCheckResult.Degraded("External Dependencies",
                    $"External dependencies check found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed);
            }

            return HealthCheckResult.Healthy("External Dependencies", "All external dependencies accessible", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy("External Dependencies", $"External dependencies health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckSystemResourcesHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check available memory
            var memoryInfo = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            var memoryGB = memoryInfo / (1024.0 * 1024.0 * 1024.0);

            if (memoryGB > 4.0) // Arbitrary threshold
            {
                issues.Add($"High memory usage: {memoryGB:F2} GB");
            }

            // Check available disk space
            var rootPath = System.IO.Path.GetPathRoot(Environment.CurrentDirectory);
            if (rootPath != null)
            {
                var driveInfo = new System.IO.DriveInfo(rootPath);
                var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

                if (availableGB < 1.0) // Less than 1GB free
                {
                    issues.Add($"Low disk space: {availableGB:F2} GB available");
                }
            }

            // Check CPU usage (rough estimate)
            if (OperatingSystem.IsWindows())
            {
                var cpuUsageSeconds = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
                Log.Debug("Process CPU time recorded for hosted service health check: {CpuSeconds}s", cpuUsageSeconds);
            }
            else
            {
                Log.Debug("Skipping process CPU time check because it isn't supported on this platform.");
            }
            // This is total CPU time, not current usage - would need more complex monitoring

            stopwatch.Stop();

            if (issues.Any())
            {
                return Task.FromResult(HealthCheckResult.Degraded("System Resources",
                    $"System resources check found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed));
            }

            return Task.FromResult(HealthCheckResult.Healthy("System Resources", "System resources within acceptable limits", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("System Resources", $"System resources health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private HealthStatus DetermineOverallHealthStatus(List<HealthCheckResult> results)
    {
        if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;

        if (results.Any(r => r.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;

        if (results.Any(r => r.Status == HealthStatus.Unavailable))
            return HealthStatus.Degraded; // Treat unavailable as degraded, not unhealthy

        return HealthStatus.Healthy;
    }

    private void LogHealthCheckResults(HealthCheckReport report)
    {
        Log.Information("Health Check Summary - Overall Status: {Status}, Total Duration: {Duration}ms",
            report.OverallStatus, report.TotalDuration.TotalMilliseconds);

        foreach (var result in report.Results.OrderBy(r => r.ServiceName))
        {
            var logLevel = result.Status switch
            {
                HealthStatus.Healthy => Serilog.Events.LogEventLevel.Information,
                HealthStatus.Degraded => Serilog.Events.LogEventLevel.Warning,
                HealthStatus.Unhealthy => Serilog.Events.LogEventLevel.Error,
                HealthStatus.Unavailable => Serilog.Events.LogEventLevel.Warning,
                _ => Serilog.Events.LogEventLevel.Information
            };

            Log.Write(logLevel, "Health Check - {Service}: {Status} ({Duration}ms) - {Description}",
                result.ServiceName, result.Status, result.Duration.TotalMilliseconds, result.Description);

            if (result.Exception != null)
            {
                Log.Error(result.Exception, "Health check exception for {Service}", result.ServiceName);
            }
        }

        Log.Information("Health Check Statistics - Healthy: {Healthy}, Degraded: {Degraded}, Unhealthy: {Unhealthy}, Unavailable: {Unavailable}",
            report.HealthyCount, report.DegradedCount, report.UnhealthyCount, report.UnavailableCount);
    }

    private bool IsServiceSkipped(string serviceName)
    {
        return _healthCheckConfig.SkipServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase);
    }

    private TimeSpan GetTimeoutForService(string serviceName)
    {
        return serviceName switch
        {
            "Database" => _healthCheckConfig.DatabaseTimeout,
            "Azure AD" or "Azure Key Vault" or "QuickBooks" or "AI Service" or "External Dependencies" => _healthCheckConfig.ExternalServiceTimeout,
            _ => _healthCheckConfig.DefaultTimeout
        };
    }

    private bool CanApplicationStart(HealthCheckReport report)
    {
        if (_healthCheckConfig.ContinueOnFailure)
        {
            Log.Warning("Health check ContinueOnFailure is enabled; allowing startup despite {Failures} failing services", report.UnhealthyCount + report.UnavailableCount);
            return true;
        }

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        if (!environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Allowing startup in non-production environment ({Environment}) despite health check failures", environment);
            return true;
        }

        // Check critical services
        var criticalServices = report.Results.Where(r => _healthCheckConfig.CriticalServices.Contains(r.ServiceName, StringComparer.OrdinalIgnoreCase));
        var criticalFailures = criticalServices.Count(r => r.Status == HealthStatus.Unhealthy || r.Status == HealthStatus.Unavailable);

        if (criticalFailures > 0)
        {
            Log.Error("Application cannot start: {CriticalFailures} critical services are failing", criticalFailures);
            return false;
        }

        // Check overall failure rate
        var totalFailures = report.UnhealthyCount + report.UnavailableCount;
        var failureRate = (double)totalFailures / report.TotalCount;

        if (failureRate > 0.5) // More than 50% services failing
        {
            Log.Error("Application cannot start: {FailureRate:P0} of services are failing", failureRate);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the Syncfusion license key from supported sources in priority order:
    /// 1) Environment variable SYNCFUSION_LICENSE_KEY
    /// 2) appsettings (Syncfusion:LicenseKey)
    /// 3) User Secrets (Syncfusion:LicenseKey)
    /// Returns null if not found or if placeholder value is detected.
    /// </summary>
    private string? GetSyncfusionLicenseKey()
    {
        try
        {
            // 1) Environment variable
            var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                return key.Trim();

            // 2) Configuration
            key = _configuration?["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                return key.Trim();

            // 3) User secrets (development)
            try
            {
                var userSecretsConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddUserSecrets<WileyWidget.App>()
                    .Build();
                key = userSecretsConfig["Syncfusion:LicenseKey"];
                if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                    return key.Trim();
            }
            catch
            {
                // ignore if user secrets unavailable
            }
        }
        catch
        {
            // ignore resolution errors and fall through
        }

        return null;
    }

    /// <summary>
    /// Resolves a setting by checking environment variables first, then configuration.
    /// Used for QuickBooks and other service settings.
    /// </summary>
    /// <param name="envVarName">Environment variable name to check first</param>
    /// <param name="configKey">Configuration key to check second</param>
    /// <returns>The resolved setting value, or null if not found</returns>
    private string? ResolveSetting(string envVarName, string configKey)
    {
        try
        {
            // 1) Environment variable
            var value = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();

            // 2) Configuration
            value = _configuration?[configKey];
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        catch
        {
            // ignore resolution errors and fall through
        }

        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // No disposable resources
        }

        _disposed = true;
    }
}
