#nullable enable

using System;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Interface for progress reporting operations
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Reports progress with a message
    /// </summary>
    /// <param name="message">The progress message</param>
    void ReportProgress(string message);

    /// <summary>
    /// Reports progress with a message and percentage
    /// </summary>
    /// <param name="message">The progress message</param>
    /// <param name="percentage">The progress percentage (0-100)</param>
    void ReportProgress(string message, int percentage);

    /// <summary>
    /// Reports completion of an operation
    /// </summary>
    /// <param name="message">The completion message</param>
    void ReportCompletion(string message);

    /// <summary>
    /// Reports an error during progress
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="exception">The exception that occurred</param>
    void ReportError(string message, Exception? exception = null);
}