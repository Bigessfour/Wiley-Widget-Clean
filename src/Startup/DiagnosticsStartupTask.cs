using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Startup;

/// <summary>
/// Captures a lightweight diagnostics snapshot to aid in startup troubleshooting.
/// </summary>
public sealed class DiagnosticsStartupTask : IStartupTask
{
    private readonly ILogger<DiagnosticsStartupTask> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public DiagnosticsStartupTask(ILogger<DiagnosticsStartupTask> logger, IHostEnvironment hostEnvironment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
    }

    public string Name => "Diagnostics snapshot";

    public int Order => 300;

    public Task ExecuteAsync(StartupTaskContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();
        context.ProgressReporter.Report(76, "Capturing diagnostics...");

        try
        {
            var process = Process.GetCurrentProcess();
            _logger.LogInformation(
                "Startup diagnostics: Environment={EnvironmentName}, Machine={MachineName}, PID={ProcessId}, Threads={ThreadCount}, WorkingSet={WorkingSet}MB, PrivateMemory={PrivateMemory}MB",
                _hostEnvironment.EnvironmentName,
                Environment.MachineName,
                process.Id,
                process.Threads.Count,
                Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2),
                Math.Round(process.PrivateMemorySize64 / (1024.0 * 1024.0), 2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture startup diagnostics snapshot");
        }

        context.ProgressReporter.Report(78, "Diagnostics captured");
        return Task.CompletedTask;
    }
}
