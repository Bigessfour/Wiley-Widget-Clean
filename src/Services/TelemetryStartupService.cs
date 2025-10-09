using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Telemetry;

/// <summary>
/// Emits a structured startup event so we can verify telemetry is flowing in each environment
/// (dev vs production) and flushes telemetry on shutdown for desktop scenarios.
/// </summary>
public sealed class TelemetryStartupService : IHostedService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TelemetryStartupService> _logger;
    private readonly string _appVersion;

    public TelemetryStartupService(TelemetryClient telemetryClient, IHostEnvironment environment, ILogger<TelemetryStartupService> logger)
    {
        _telemetryClient = telemetryClient;
        _environment = environment;
        _logger = logger;
        _appVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _telemetryClient.TrackEvent("ApplicationStartup", new System.Collections.Generic.Dictionary<string, string>
            {
                { "Environment", _environment.EnvironmentName },
                { "AppVersion", _appVersion },
                { "TimestampUtc", DateTime.UtcNow.ToString("o") },
            });

            // Track a metric sample so we can validate ingestion (will aggregate in AI)
            _telemetryClient.GetMetric("StartupCount").TrackValue(1);

            _logger.LogInformation("Telemetry startup event emitted (Environment: {Environment}, Version: {Version})", _environment.EnvironmentName, _appVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit telemetry startup event");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Explicit flush to reduce data loss risk on fast desktop shutdown.
            _telemetryClient.Flush();

            // Give channel a brief moment to send (non-blocking best-effort)
            // Only in desktop; small delay is acceptable (<500ms)
            if (!_environment.IsDevelopment())
            {
                Task.Delay(250, cancellationToken).Wait(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Telemetry flush encountered an error during shutdown");
        }
        return Task.CompletedTask;
    }
}
