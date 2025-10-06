using System;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Interface for progress reporting operations.
/// Provides a standardized way to report progress and status updates.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Gets or sets the current status message.
    /// </summary>
    string StatusMessage { get; set; }

    /// <summary>
    /// Gets or sets the current progress percentage.
    /// </summary>
    double ProgressPercentage { get; set; }

    /// <summary>
    /// Reports progress with a message and percentage.
    /// </summary>
    /// <param name="message">The status message to display.</param>
    /// <param name="percentage">The completion percentage (0.0 to 100.0).</param>
    void ReportProgress(string message, double percentage);

    /// <summary>
    /// Reports progress with a percentage only.
    /// </summary>
    /// <param name="percentage">The completion percentage (0.0 to 100.0).</param>
    void ReportProgress(double percentage);

    /// <summary>
    /// Resets the progress to zero.
    /// </summary>
    void Reset();
}