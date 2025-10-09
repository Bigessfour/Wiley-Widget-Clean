using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.Startup;

/// <summary>
/// Ensures persisted application settings are loaded and normalized before the UI materializes.
/// </summary>
public sealed class SettingsStartupTask : IStartupTask
{
    private readonly SettingsService _settingsService;
    private readonly ILogger<SettingsStartupTask> _logger;

    public SettingsStartupTask(SettingsService settingsService, ILogger<SettingsStartupTask> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "Settings initialization";

    public int Order => 200;

    public Task ExecuteAsync(StartupTaskContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();
        context.ProgressReporter.Report(72, "Loading user settings...");

        try
        {
            _settingsService.Load();
            _logger.LogInformation("Application settings loaded successfully.");

            if (string.IsNullOrWhiteSpace(_settingsService.Current.Theme))
            {
                _settingsService.Current.Theme = "FluentDark";
                _settingsService.Save();
                _logger.LogInformation("Default theme applied (FluentDark) because no preference was persisted.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load persisted settings; defaults will be used.");
        }

        context.ProgressReporter.Report(74, "Settings ready");
        return Task.CompletedTask;
    }
}
