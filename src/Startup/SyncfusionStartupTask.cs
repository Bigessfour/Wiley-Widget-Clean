using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.Startup;

/// <summary>
/// Registers the Syncfusion license and records the result so other services (health checks, diagnostics)
/// can make informed decisions about the application's licensing status.
/// </summary>
public sealed class SyncfusionStartupTask : IStartupTask
{
    private readonly IConfiguration _configuration;
    private readonly ISyncfusionLicenseService _licenseService;
    private readonly ILogger<SyncfusionStartupTask> _logger;
    private readonly SyncfusionLicenseState _licenseState;
    private readonly ISecretVaultService? _secretVaultService;

    public SyncfusionStartupTask(
        IConfiguration configuration,
        ISyncfusionLicenseService licenseService,
        ILogger<SyncfusionStartupTask> logger,
        SyncfusionLicenseState licenseState,
        ISecretVaultService? secretVaultService = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _licenseState = licenseState ?? throw new ArgumentNullException(nameof(licenseState));
        _secretVaultService = secretVaultService; // optional dependency
    }

    public string Name => "Syncfusion licensing";

    public int Order => 100;

    public async Task ExecuteAsync(StartupTaskContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting Syncfusion licensing task");
        context.ProgressReporter.Report(66, "Registering Syncfusion license...");

        try
        {
            var (licenseKey, source, message) = await ResolveLicenseKeyAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                var reason = string.IsNullOrWhiteSpace(message)
                    ? "Syncfusion license key not found; running in evaluation mode."
                    : message;

                _licenseState.MarkAttempt(false, reason);
                _logger.LogWarning("{Reason}", reason);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var success = await _licenseService.ValidateLicenseAsync(licenseKey).ConfigureAwait(false);
            if (success)
            {
                var registrationMessage = string.IsNullOrWhiteSpace(source)
                    ? "Syncfusion license registered successfully."
                    : $"Syncfusion license registered successfully using {source}.";
                _licenseState.MarkAttempt(true, registrationMessage);
                _logger.LogInformation("Syncfusion license registration succeeded (source: {Source})", source ?? "unknown");
            }
            else
            {
                var failureMessage = "Syncfusion license key failed validation.";
                _licenseState.MarkAttempt(false, failureMessage);
                _logger.LogWarning(failureMessage);
            }
        }
        catch (Exception ex)
        {
            _licenseState.MarkAttempt(false, $"License registration exception: {ex.Message}");
            _logger.LogError(ex, "Syncfusion license registration failed");
            // Do not rethrow: the application can still run in evaluation mode.
        }
        finally
        {
            stopwatch.Stop();
            context.ProgressReporter.Report(70, "Syncfusion license task complete");
            _logger.LogInformation("Syncfusion licensing task finished in {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<(string? LicenseKey, string? Source, string? Message)> ResolveLicenseKeyAsync(CancellationToken cancellationToken)
    {
        string? key;

        // 1) Environment variables (process/user/machine)
        key = GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Process);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return (key, "environment variable", null);
        }

        key = GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return (key, "user environment variable", null);
        }

        key = GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return (key, "machine environment variable", null);
        }

        // 2) Configuration (appsettings, user-secrets, etc.)
        key = _configuration["Syncfusion:LicenseKey"];
        if (!string.IsNullOrWhiteSpace(key) && !IsPlaceholder(key))
        {
            return (key, "configuration", null);
        }

        // 3) Secret vault (optional)
        var secretName = _configuration["Syncfusion:KeyVaultSecretName"] ?? "Syncfusion-LicenseKey";
        if (_secretVaultService != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                key = await _secretVaultService.GetSecretAsync(secretName).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(key) && !IsPlaceholder(key))
                {
                    return (key, $"Secret vault entry '{secretName}'", null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve Syncfusion license key from secret vault");
            }
        }

        if (!string.IsNullOrEmpty(key) && IsPlaceholder(key))
        {
            return (null, null, "Syncfusion license key placeholder detected; configure a real key.");
        }

    return (null, null, "Syncfusion license key not found (env/config/secret vault).");
    }

    private static string? GetEnvironmentVariable(string name, EnvironmentVariableTarget target)
    {
        var value = Environment.GetEnvironmentVariable(name, target);
        return string.IsNullOrWhiteSpace(value) || IsPlaceholder(value) ? null : value.Trim();
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.Equals("${SYNCFUSION_LICENSE_KEY}", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("INSERT_KEY_HERE", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("${", StringComparison.Ordinal);
    }
}
