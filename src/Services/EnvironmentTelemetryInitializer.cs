using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Hosting;

namespace WileyWidget.Services.Telemetry;

/// <summary>
/// Adds common environment and application metadata to every telemetry item.
/// Helps distinguish development vs production data and enables filtering in the portal.
/// </summary>
public sealed class EnvironmentTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHostEnvironment _environment;
    private readonly string _appVersion;

    public EnvironmentTelemetryInitializer(IHostEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _appVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
    }

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry == null) return;
        try
        {
            telemetry.Context.Cloud.RoleName = "WileyWidget.Desktop"; // Logical application role
            telemetry.Context.GlobalProperties["Environment"] = _environment.EnvironmentName;
            telemetry.Context.GlobalProperties["AppVersion"] = _appVersion;
        }
        catch
        {
            // Swallow – never let telemetry enrichment crash the app.
        }
    }
}
