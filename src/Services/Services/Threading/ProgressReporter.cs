using System;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Implementation of progress reporting for UI operations.
/// Provides thread-safe progress updates with status messages.
/// </summary>
public class ProgressReporter : IProgressReporter
{
    private readonly Action<string, double>? _progressCallback;
    private string _statusMessage = string.Empty;
    private double _progressPercentage;

    /// <summary>
    /// Initializes a new instance of the ProgressReporter class.
    /// </summary>
    /// <param name="progressCallback">The callback to invoke when progress is reported. If null, progress reporting is disabled.</param>
    public ProgressReporter(Action<string, double>? progressCallback = null)
    {
        _progressCallback = progressCallback;
    }

    /// <inheritdoc/>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value ?? string.Empty;
            ReportProgress(_statusMessage, _progressPercentage);
        }
    }

    /// <inheritdoc/>
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            if (value < 0.0 || value > 100.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Percentage must be between 0.0 and 100.0");

            _progressPercentage = value;
            ReportProgress(_statusMessage, _progressPercentage);
        }
    }

    /// <inheritdoc/>
    public void ReportProgress(string message, double percentage)
    {
        if (percentage < 0.0 || percentage > 100.0)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be between 0.0 and 100.0");

        _statusMessage = message ?? string.Empty;
        _progressPercentage = percentage;
        _progressCallback?.Invoke(_statusMessage, _progressPercentage);
    }

    /// <inheritdoc/>
    public void ReportProgress(double percentage)
    {
        if (percentage < 0.0 || percentage > 100.0)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be between 0.0 and 100.0");

        _progressPercentage = percentage;
        _progressCallback?.Invoke(_statusMessage, _progressPercentage);
    }

    /// <inheritdoc/>
    public void Reset() => ReportProgress(0.0);
}