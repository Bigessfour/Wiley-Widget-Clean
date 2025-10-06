using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// Base class for ViewModels that require threading support.
/// Provides common threading utilities and proper async operation handling.
/// Based on Microsoft WPF threading best practices.
/// </summary>
public abstract class AsyncViewModelBase : ObservableObject, IDisposable
{
    protected readonly IDispatcherHelper DispatcherHelper;
    protected readonly ILogger Logger;
    protected CancellationTokenSource CancellationTokenSource;

    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private double _progressPercentage;

    /// <summary>
    /// Initializes a new instance of the AsyncViewModelBase class.
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    protected AsyncViewModelBase(IDispatcherHelper dispatcherHelper, ILogger logger)
    {
        DispatcherHelper = dispatcherHelper ?? throw new ArgumentNullException(nameof(dispatcherHelper));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        CancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the ViewModel is currently loading.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        protected set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Gets or sets the current status message for user feedback.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        protected set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets or sets the current progress percentage (0.0 to 100.0).
    /// </summary>
    public double ProgressPercentage
    {
        get => _progressPercentage;
        protected set => SetProperty(ref _progressPercentage, value);
    }

    /// <summary>
    /// Executes an asynchronous operation with proper error handling and UI updates.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="progressReporter">Optional progress reporter for the operation.</param>
    /// <param name="statusMessage">Optional status message to display during the operation.</param>
    /// <returns>A task representing the asynchronous operation with result.</returns>
    protected async Task<T> ExecuteAsyncOperation<T>(
        Func<CancellationToken, Task<T>> operation,
        IProgressReporter? progressReporter = null,
        string? statusMessage = null)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        try
        {
            IsLoading = true;
            if (!string.IsNullOrEmpty(statusMessage))
            {
                StatusMessage = statusMessage;
            }

            progressReporter?.Reset();

            var result = await operation(CancellationTokenSource.Token);

            progressReporter?.ReportProgress(100.0);
            return result;
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing async operation");
            
            // Show user-friendly error dialog
            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                try
                {
                    System.Windows.MessageBox.Show(
                        $"An error occurred while performing the operation:\n\n{ex.Message}",
                        "Operation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                catch (Exception dialogEx)
                {
                    Logger.LogError(dialogEx, "Failed to show error dialog");
                }
            });
            
            throw;
        }
        finally
        {
            IsLoading = false;
            StatusMessage = string.Empty;
        }
    }

    /// <summary>
    /// Executes an asynchronous operation with proper error handling and UI updates.
    /// </summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="progressReporter">Optional progress reporter for the operation.</param>
    /// <param name="statusMessage">Optional status message to display during the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task ExecuteAsyncOperation(
        Func<CancellationToken, Task> operation,
        IProgressReporter? progressReporter = null,
        string? statusMessage = null)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        try
        {
            IsLoading = true;
            if (!string.IsNullOrEmpty(statusMessage))
            {
                StatusMessage = statusMessage;
            }

            progressReporter?.Reset();

            await operation(CancellationTokenSource.Token);

            progressReporter?.ReportProgress(100.0);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing async operation");
            
            // Show user-friendly error dialog
            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                try
                {
                    System.Windows.MessageBox.Show(
                        $"An error occurred while performing the operation:\n\n{ex.Message}",
                        "Operation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                catch (Exception dialogEx)
                {
                    Logger.LogError(dialogEx, "Failed to show error dialog");
                }
            });
            
            throw;
        }
        finally
        {
            IsLoading = false;
            StatusMessage = string.Empty;
        }
    }

    /// <summary>
    /// Cancels all pending operations.
    /// </summary>
    public void CancelOperations()
    {
        CancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Resets the cancellation token for new operations.
    /// </summary>
    public void ResetCancellation()
    {
        CancellationTokenSource.Dispose();
        CancellationTokenSource = new CancellationTokenSource();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by this ViewModel.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
        }
    }

    /// <summary>
    /// Executes a database operation with automatic progress reporting and proper error handling.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="operation">The database operation to execute.</param>
    /// <param name="statusMessage">Optional status message to display during the operation.</param>
    /// <param name="progressSteps">Number of progress steps to report (default is 100).</param>
    /// <returns>A task representing the asynchronous database operation with result.</returns>
    protected async Task<T> ExecuteDatabaseOperation<T>(
        Func<CancellationToken, Task<T>> operation,
        string? statusMessage = null,
        int progressSteps = 100)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        var progressReporter = new DatabaseProgressReporter(progressSteps);
        var progress = new Progress<double>(p => ProgressPercentage = p);

        return await ExecuteAsyncOperation(operation, progressReporter, statusMessage ?? "Executing database operation...");
    }

    /// <summary>
    /// Executes a database operation with automatic progress reporting and proper error handling.
    /// </summary>
    /// <param name="operation">The database operation to execute.</param>
    /// <param name="statusMessage">Optional status message to display during the operation.</param>
    /// <param name="progressSteps">Number of progress steps to report (default is 100).</param>
    /// <returns>A task representing the asynchronous database operation.</returns>
    protected async Task ExecuteDatabaseOperation(
        Func<CancellationToken, Task> operation,
        string? statusMessage = null,
        int progressSteps = 100)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        var progressReporter = new DatabaseProgressReporter(progressSteps);
        var progress = new Progress<double>(p => ProgressPercentage = p);

        await ExecuteAsyncOperation(operation, progressReporter, statusMessage ?? "Executing database operation...");
    }
}

/// <summary>
/// Progress reporter specifically designed for database operations.
/// Provides automatic progress reporting for common database operation patterns.
/// </summary>
public class DatabaseProgressReporter : IProgressReporter
{
    private readonly int _totalSteps;
    private int _currentStep;
    private readonly System.Diagnostics.Stopwatch _stopwatch;

    /// <summary>
    /// Initializes a new instance of the DatabaseProgressReporter class.
    /// </summary>
    /// <param name="totalSteps">Total number of progress steps.</param>
    public DatabaseProgressReporter(int totalSteps = 100)
    {
        _totalSteps = totalSteps;
        _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    }

    /// <summary>
    /// Gets or sets the current status message.
    /// </summary>
    public string StatusMessage { get; set; } = "Processing...";

    /// <summary>
    /// Gets or sets the current progress percentage.
    /// </summary>
    public double ProgressPercentage
    {
        get => _totalSteps > 0 ? (double)_currentStep / _totalSteps * 100.0 : 0.0;
        set => ReportProgress(value);
    }

    /// <summary>
    /// Reports progress with a message and percentage.
    /// </summary>
    /// <param name="message">The status message to display.</param>
    /// <param name="percentage">The completion percentage (0.0 to 100.0).</param>
    public void ReportProgress(string message, double percentage)
    {
        StatusMessage = message;
        ReportProgress(percentage);
    }

    /// <summary>
    /// Reports progress with a percentage only.
    /// </summary>
    /// <param name="percentage">The completion percentage (0.0 to 100.0).</param>
    public void ReportProgress(double percentage)
    {
        // Convert percentage to step-based progress
        var stepValue = Math.Min(Math.Max(percentage / 100.0, 0.0), 1.0);
        var step = (int)(stepValue * _totalSteps);
        _currentStep = Math.Max(_currentStep, step);

        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(_currentStep, _totalSteps));
    }

    /// <summary>
    /// Resets the progress to zero.
    /// </summary>
    public void Reset()
    {
        _currentStep = 0;
        _stopwatch.Restart();
        StatusMessage = "Ready";
        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(0, _totalSteps));
    }

    /// <summary>
    /// Gets the elapsed time for the operation.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Event raised when progress changes.
    /// </summary>
    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;
}

/// <summary>
/// Event arguments for progress changes.
/// </summary>
public class ProgressChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the current progress value.
    /// </summary>
    public int Current { get; }

    /// <summary>
    /// Gets the total progress value.
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// Gets the progress percentage (0.0 to 1.0).
    /// </summary>
    public double Percentage => Total > 0 ? (double)Current / Total : 0.0;

    /// <summary>
    /// Initializes a new instance of the ProgressChangedEventArgs class.
    /// </summary>
    /// <param name="current">Current progress value.</param>
    /// <param name="total">Total progress value.</param>
    public ProgressChangedEventArgs(int current, int total)
    {
        Current = current;
        Total = total;
    }
}