using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for managing step-based progress reporting and cancellation.
/// Provides detailed progress tracking for long-running operations.
/// </summary>
public partial class ProgressViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ProgressViewModel> _logger;
    private CancellationTokenSource _cancellationTokenSource;

    [ObservableProperty]
    private bool isOperationInProgress;

    [ObservableProperty]
    private string currentOperationName = string.Empty;

    [ObservableProperty]
    private string currentStatusMessage = string.Empty;

    [ObservableProperty]
    private int currentStepIndex;

    [ObservableProperty]
    private bool canCancel;

    public ObservableCollection<ProgressStep> ProgressSteps { get; } = new();

    public ProgressViewModel(ILogger logger)
    {
        _logger = (ILogger<ProgressViewModel>)logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Initializes progress tracking for a new operation.
    /// </summary>
    /// <param name="operationName">Name of the operation being performed.</param>
    /// <param name="steps">List of progress steps for the operation.</param>
    public void StartOperation(string operationName, IEnumerable<ProgressStep> steps)
    {
        _logger.LogInformation("Starting progress tracking for operation: {OperationName}", operationName);

        CurrentOperationName = operationName;
        ProgressSteps.Clear();
        foreach (var step in steps)
        {
            ProgressSteps.Add(step);
        }

        CurrentStepIndex = 0;
        CurrentStatusMessage = "Initializing...";
        IsOperationInProgress = true;
        CanCancel = true;

        // Reset cancellation token
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Updates the current progress step.
    /// </summary>
    /// <param name="stepIndex">Index of the current step.</param>
    /// <param name="statusMessage">Status message for the current step.</param>
    public void UpdateProgress(int stepIndex, string statusMessage)
    {
        if (stepIndex >= 0 && stepIndex < ProgressSteps.Count)
        {
            CurrentStepIndex = stepIndex;
            CurrentStatusMessage = statusMessage;

            // Mark previous steps as completed
            for (int i = 0; i < stepIndex; i++)
            {
                if (i < ProgressSteps.Count)
                {
                    ProgressSteps[i].IsCompleted = true;
                }
            }

            // Mark current step as in progress
            if (stepIndex < ProgressSteps.Count)
            {
                ProgressSteps[stepIndex].IsInProgress = true;
            }

            _logger.LogDebug("Progress updated - Step: {StepIndex}, Message: {Message}",
                           stepIndex, statusMessage);
        }
    }

    /// <summary>
    /// Marks the operation as completed successfully.
    /// </summary>
    public void CompleteOperation()
    {
        _logger.LogInformation("Operation completed successfully: {OperationName}", CurrentOperationName);

        // Mark all steps as completed
        foreach (var step in ProgressSteps)
        {
            step.IsCompleted = true;
            step.IsInProgress = false;
        }

        CurrentStatusMessage = "Operation completed successfully";
        IsOperationInProgress = false;
        CanCancel = false;
    }

    /// <summary>
    /// Marks the operation as failed.
    /// </summary>
    /// <param name="errorMessage">Error message describing the failure.</param>
    public void FailOperation(string errorMessage)
    {
        _logger.LogError("Operation failed: {OperationName} - {ErrorMessage}",
                        CurrentOperationName, errorMessage);

        CurrentStatusMessage = $"Operation failed: {errorMessage}";
        IsOperationInProgress = false;
        CanCancel = false;
    }

    /// <summary>
    /// Cancels the current operation.
    /// </summary>
    [RelayCommand]
    public void CancelOperation()
    {
        if (CanCancel && IsOperationInProgress)
        {
            _logger.LogInformation("Operation cancelled by user: {OperationName}", CurrentOperationName);

            _cancellationTokenSource.Cancel();
            CurrentStatusMessage = "Operation cancelled";
            IsOperationInProgress = false;
            CanCancel = false;
        }
    }

    /// <summary>
    /// Gets the cancellation token for the current operation.
    /// </summary>
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <summary>
    /// Resets the progress view model to its initial state.
    /// </summary>
    public void Reset()
    {
        _logger.LogDebug("Resetting progress view model");

        ProgressSteps.Clear();
        CurrentOperationName = string.Empty;
        CurrentStatusMessage = string.Empty;
        CurrentStepIndex = 0;
        IsOperationInProgress = false;
        CanCancel = false;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            _cancellationTokenSource?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a single step in a progress operation.
/// </summary>
public class ProgressStep : ObservableObject
{
    private bool _isCompleted;
    private bool _isInProgress;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    public bool IsInProgress
    {
        get => _isInProgress;
        set => SetProperty(ref _isInProgress, value);
    }

    public ProgressStep(string title, string description = "")
    {
        Title = title;
        Description = description;
    }
}