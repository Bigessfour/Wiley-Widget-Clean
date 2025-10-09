#nullable enable

using System;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Implementation of IProgressReporter for reporting operation progress
/// </summary>
public class ProgressReporter : IProgressReporter
{
    private readonly ILogger<ProgressReporter>? _logger;

    public ProgressReporter(ILogger<ProgressReporter>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reports progress with a message
    /// </summary>
    /// <param name="message">The progress message</param>
    public void ReportProgress(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or whitespace", nameof(message));

        _logger?.LogInformation("Progress: {Message}", message);
        OnProgressChanged(new ProgressEventArgs(message));
    }

    /// <summary>
    /// Reports progress with a message and percentage
    /// </summary>
    /// <param name="message">The progress message</param>
    /// <param name="percentage">The progress percentage (0-100)</param>
    public void ReportProgress(string message, int percentage)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or whitespace", nameof(message));

        if (percentage < 0 || percentage > 100)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be between 0 and 100");

        var fullMessage = $"{message} ({percentage}%)";
        _logger?.LogInformation("Progress: {Message} - {Percentage}%", message, percentage);
        OnProgressChanged(new ProgressEventArgs(fullMessage, percentage));
    }

    /// <summary>
    /// Reports completion of an operation
    /// </summary>
    /// <param name="message">The completion message</param>
    public void ReportCompletion(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or whitespace", nameof(message));

        _logger?.LogInformation("Completed: {Message}", message);
        OnProgressChanged(new ProgressEventArgs(message, 100, true));
    }

    /// <summary>
    /// Reports an error during progress
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="exception">The exception that occurred</param>
    public void ReportError(string message, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or whitespace", nameof(message));

        _logger?.LogError(exception, "Error: {Message}", message);
        OnProgressChanged(new ProgressEventArgs(message, 0, false, true, exception));
    }

    /// <summary>
    /// Event raised when progress changes
    /// </summary>
    public event EventHandler<ProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Raises the ProgressChanged event
    /// </summary>
    /// <param name="e">The progress event args</param>
    protected virtual void OnProgressChanged(ProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }
}

/// <summary>
/// Event arguments for progress reporting
/// </summary>
public class ProgressEventArgs : EventArgs
{
    /// <summary>
    /// Gets the progress message
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the progress percentage (0-100)
    /// </summary>
    public int Percentage { get; }

    /// <summary>
    /// Gets a value indicating whether the operation is complete
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>
    /// Gets a value indicating whether an error occurred
    /// </summary>
    public bool IsError { get; }

    /// <summary>
    /// Gets the exception that occurred, if any
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Initializes a new instance of the ProgressEventArgs class
    /// </summary>
    /// <param name="message">The progress message</param>
    /// <param name="percentage">The progress percentage</param>
    /// <param name="isComplete">Whether the operation is complete</param>
    /// <param name="isError">Whether an error occurred</param>
    /// <param name="exception">The exception that occurred</param>
    public ProgressEventArgs(string message, int percentage = 0, bool isComplete = false, bool isError = false, Exception? exception = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Percentage = Math.Clamp(percentage, 0, 100);
        IsComplete = isComplete;
        IsError = isError;
        Exception = exception;
    }
}