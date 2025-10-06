using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using Serilog.Events;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace WileyWidget.Services;

/// <summary>
/// Centralized error reporting service for consistent error handling across the application.
/// Provides structured logging, user notifications, and recovery mechanisms.
/// </summary>
public class ErrorReportingService
{
    private static readonly Lazy<ErrorReportingService> _instance = new(() => new ErrorReportingService());
    public static ErrorReportingService Instance => _instance.Value;

    private readonly Dictionary<string, ErrorRecoveryStrategy> _recoveryStrategies = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();

    /// <summary>
    /// When set, user-facing dialogs are suppressed (useful for automated testing scenarios).
    /// </summary>
    public bool SuppressUserDialogs { get; set; }

    /// <summary>
    /// Raised whenever an error is reported. Primarily intended for diagnostics and testing.
    /// </summary>
    public event Action<Exception, string?>? ErrorReported;

    private ErrorReportingService()
    {
        // Register default recovery strategies
        RegisterRecoveryStrategy("Authentication", new RetryWithDelayStrategy(maxRetries: 3, initialDelayMs: 1000));
        RegisterRecoveryStrategy("Network", new RetryWithDelayStrategy(maxRetries: 2, initialDelayMs: 2000));
        RegisterRecoveryStrategy("Database", new RetryWithDelayStrategy(maxRetries: 1, initialDelayMs: 500));
    }

    /// <summary>
    /// Reports an error with structured logging and optional user notification.
    /// </summary>
    public void ReportError(Exception exception, string? context = null, bool showToUser = true,
                           LogEventLevel level = LogEventLevel.Error, string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString();

        // Structured logging with context
        var logContext = Log.ForContext("CorrelationId", correlationId)
                           .ForContext("Context", context ?? "Unknown")
                           .ForContext("ExceptionType", exception.GetType().Name)
                           .ForContext("ExceptionMessage", exception.Message);

        logContext.Write(level, exception, "Error occurred in {Context}: {Message}", context, exception.Message);

    ErrorReported?.Invoke(exception, context);

        // Show user-friendly dialog if requested
        if (showToUser && !SuppressUserDialogs && Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
                ShowErrorDialog(exception, context ?? "Unknown", correlationId));
        }
    }

    /// <summary>
    /// Lightweight telemetry: records an event with optional properties.
    /// Uses structured logging so it can be aggregated by sinks.
    /// </summary>
    public void TrackEvent(string eventName, IDictionary<string, object>? properties = null)
    {
        try
        {
            var logger = Log.ForContext("TelemetryEvent", eventName);
            if (properties != null)
            {
                // Destructure properties so they remain structured in sinks
                logger = logger.ForContext("Properties", properties, destructureObjects: true);
            }
            logger.Information("Telemetry event: {Event}", eventName);
        }
        catch
        {
            // Best effort only
        }
    }

    /// <summary>
    /// Lightweight telemetry: increments a named counter and logs occasionally.
    /// </summary>
    public long IncrementCounter(string name, long value = 1)
    {
        try
        {
            var current = _counters.AddOrUpdate(name, value, (_, existing) => existing + value);
            // Periodically emit snapshot (every ~100 increments)
            if (current % 100 == 0)
            {
                Log.ForContext("Counter", name)
                   .ForContext("Value", current)
                   .Information("Telemetry counter snapshot {Counter} = {Value}", name, current);
            }
            return current;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Reports a warning with structured logging.
    /// </summary>
    public void ReportWarning(string message, string? context = null, string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString();

        Log.ForContext("CorrelationId", correlationId)
           .ForContext("Context", context ?? "Unknown")
           .Warning("Warning in {Context}: {Message}", context, message);
    }

    /// <summary>
    /// Attempts to recover from an error using registered strategies.
    /// </summary>
    public async Task<bool> TryRecoverAsync(Exception? exception, string context, Func<Task<bool>> recoveryAction)
    {
        var correlationId = Guid.NewGuid().ToString();

        Log.ForContext("CorrelationId", correlationId)
           .ForContext("Context", context)
           .Information("Attempting error recovery for {Context}", context);

        if (_recoveryStrategies.TryGetValue(context, out var strategy))
        {
            try
            {
                var success = await strategy.ExecuteAsync(recoveryAction);
                if (success)
                {
                    Log.ForContext("CorrelationId", correlationId)
                       .Information("Successfully recovered from error in {Context}", context);
                    return true;
                }
            }
            catch (Exception recoveryEx)
            {
                Log.ForContext("CorrelationId", correlationId)
                   .Error(recoveryEx, "Recovery failed for {Context}", context);
            }
        }

        Log.ForContext("CorrelationId", correlationId)
           .Warning("No recovery strategy available or recovery failed for {Context}", context);
        return false;
    }

    /// <summary>
    /// Registers a recovery strategy for a specific error context.
    /// </summary>
    public void RegisterRecoveryStrategy(string context, ErrorRecoveryStrategy strategy)
    {
        _recoveryStrategies[context] = strategy;
    }

    /// <summary>
    /// Handles errors with fallback actions.
    /// </summary>
    public async Task<T> HandleWithFallbackAsync<T>(Func<Task<T>> primaryAction,
                                                   Func<Task<T>> fallbackAction,
                                                   string? context = null,
                                                   T defaultValue = default!)
    {
        try
        {
            return await primaryAction();
        }
        catch (Exception ex)
        {
            ReportError(ex, context, showToUser: false);

            try
            {
                Log.Information("Attempting fallback action for {Context}", context);
                return await fallbackAction();
            }
            catch (Exception fallbackEx)
            {
                ReportError(fallbackEx, $"{context ?? "Unknown"}_Fallback", showToUser: true);
                return defaultValue;
            }
        }
    }

    /// <summary>
    /// Safely executes an action that should not throw exceptions.
    /// </summary>
    public void SafeExecute(Action action, string? context = null, bool logErrors = true)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (logErrors)
            {
                ReportError(ex, context, showToUser: false, level: LogEventLevel.Warning);
            }
        }
    }

    /// <summary>
    /// Safely executes an async action that should not throw exceptions.
    /// </summary>
    public async Task SafeExecuteAsync(Func<Task> action, string? context = null, bool logErrors = true)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (logErrors)
            {
                ReportError(ex, context, showToUser: false, level: LogEventLevel.Warning);
            }
        }
    }

    private void ShowErrorDialog(Exception exception, string context, string correlationId)
    {
        if (SuppressUserDialogs)
        {
            Log.Information("Suppressed error dialog for context {Context} (CorrelationId: {CorrelationId})", context, correlationId);
            return;
        }

        try
        {
            // Create a custom dialog window for better error reporting
            var errorDialog = new Window
            {
                Title = "Application Error",
                Width = 500,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White,
                Icon = Application.Current?.MainWindow?.Icon
            };

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var titleText = new TextBlock
            {
                Text = $"An error occurred in {context ?? "the application"}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(titleText, 0);

            // Error details
            var detailsText = new TextBlock
            {
                Text = $"Error: {exception.Message}\n\nReference ID: {correlationId}\n\nThis error has been logged for review.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(detailsText, 1);

            // Buttons panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            // Continue button
            var continueButton = new Button
            {
                Content = "Continue",
                Width = 80,
                Height = 25,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            continueButton.Click += (s, e) =>
            {
                Log.Information("User chose to continue after error (CorrelationId: {CorrelationId})", correlationId);
                errorDialog.DialogResult = true;
                errorDialog.Close();
            };

            // View Details button
            var detailsButton = new Button
            {
                Content = "View Details",
                Width = 90,
                Height = 25,
                Margin = new Thickness(0, 0, 10, 0)
            };
            detailsButton.Click += (s, e) =>
            {
                ShowDetailedErrorDialog(exception, context, correlationId);
            };

            // Exit button
            var exitButton = new Button
            {
                Content = "Exit Application",
                Width = 110,
                Height = 25,
                IsCancel = true
            };
            exitButton.Click += (s, e) =>
            {
                Log.Information("User chose to exit after error (CorrelationId: {CorrelationId})", correlationId);
                errorDialog.DialogResult = false;
                errorDialog.Close();
            };

            buttonPanel.Children.Add(continueButton);
            buttonPanel.Children.Add(detailsButton);
            buttonPanel.Children.Add(exitButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(titleText);
            grid.Children.Add(detailsText);
            grid.Children.Add(buttonPanel);

            errorDialog.Content = grid;

            // Show dialog and handle result
            var result = errorDialog.ShowDialog();

            if (result == false) // User chose to exit
            {
                Application.Current?.Shutdown();
            }
        }
        catch (Exception dialogEx)
        {
            // If custom dialog fails, fall back to message box
            Log.Error(dialogEx, "Failed to show custom error dialog, falling back to MessageBox (CorrelationId: {CorrelationId})", correlationId);
            
            if (SuppressUserDialogs)
            {
                Log.Information("Suppressed fallback dialog for context {Context} (CorrelationId: {CorrelationId})", context, correlationId);
                return;
            }

            var message = $"An error occurred in {context ?? "the application"}.\n\n" +
                         $"Error: {exception.Message}\n\n" +
                         $"Reference ID: {correlationId}\n\n" +
                         "Would you like to continue? (Some features may not work properly)";

            var result = MessageBox.Show(message, "Application Error",
                                       MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                Log.Information("User chose to exit after error via fallback dialog (CorrelationId: {CorrelationId})", correlationId);
                Application.Current?.Shutdown();
            }
            else
            {
                Log.Information("User chose to continue after error via fallback dialog (CorrelationId: {CorrelationId})", correlationId);
            }
        }
    }

    /// <summary>
    /// Shows a detailed error dialog with full exception information
    /// </summary>
    private void ShowDetailedErrorDialog(Exception exception, string? context, string correlationId)
    {
        try
        {
            var detailWindow = new Window
            {
                Title = "Error Details",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = System.Windows.Media.Brushes.White,
                Icon = Application.Current?.MainWindow?.Icon
            };

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = $"Detailed Error Information - {context ?? "Application"}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(titleText, 0);

            var detailsBox = new TextBox
            {
                Text = $"Context: {context}\n" +
                       $"Correlation ID: {correlationId}\n" +
                       $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Exception Type: {exception.GetType().FullName}\n\n" +
                       $"Message: {exception.Message}\n\n" +
                       $"Stack Trace:\n{exception.StackTrace}",
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(detailsBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var copyButton = new Button
            {
                Content = "Copy to Clipboard",
                Width = 120,
                Height = 25,
                Margin = new Thickness(0, 0, 10, 0)
            };
            copyButton.Click += (s, e) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(detailsBox.Text);
                    MessageBox.Show("Error details copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch
                {
                    MessageBox.Show("Failed to copy to clipboard.", "Copy Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 60,
                Height = 25,
                IsDefault = true
            };
            closeButton.Click += (s, e) => detailWindow.Close();

            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(closeButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(titleText);
            grid.Children.Add(detailsBox);
            grid.Children.Add(buttonPanel);

            detailWindow.Content = grid;
            detailWindow.ShowDialog();
        }
        catch (Exception detailEx)
        {
            Log.Error(detailEx, "Failed to show detailed error dialog (CorrelationId: {CorrelationId})", correlationId);
            MessageBox.Show($"Error details:\n\n{exception.Message}\n\nReference ID: {correlationId}",
                           "Error Details", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

/// <summary>
/// Base class for error recovery strategies.
/// </summary>
public abstract class ErrorRecoveryStrategy
{
    public abstract Task<bool> ExecuteAsync(Func<Task<bool>> action);
}

/// <summary>
/// Retry strategy with exponential backoff.
/// </summary>
public class RetryWithDelayStrategy : ErrorRecoveryStrategy
{
    private readonly int _maxRetries;
    private readonly int _initialDelayMs;

    public RetryWithDelayStrategy(int maxRetries, int initialDelayMs)
    {
        _maxRetries = maxRetries;
        _initialDelayMs = initialDelayMs;
    }

    public override async Task<bool> ExecuteAsync(Func<Task<bool>> action)
    {
        var delay = _initialDelayMs;

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                Log.Information("Recovery attempt {Attempt}/{MaxRetries}", attempt, _maxRetries);
                var success = await action();
                if (success) return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Recovery attempt {Attempt} failed", attempt);
            }

            if (attempt < _maxRetries)
            {
                await Task.Delay(delay);
                delay *= 2; // Exponential backoff
            }
        }

        return false;
    }
}