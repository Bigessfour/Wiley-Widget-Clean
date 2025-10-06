using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Service for managing UI thread operations and dispatcher marshaling.
/// Implements Microsoft WPF threading best practices for thread-safe UI updates.
/// </summary>
public class DispatcherHelper : IDispatcherHelper
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<DispatcherHelper> _logger;

    /// <summary>
    /// Initializes a new instance of the DispatcherHelper class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    public DispatcherHelper(ILogger<DispatcherHelper> logger)
    {
        _dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("Application.Current.Dispatcher is not available. Ensure this is called from a WPF application context.");

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("DispatcherHelper initialized with dispatcher from thread {ThreadId}",
            _dispatcher.Thread.ManagedThreadId);
    }

    /// <inheritdoc/>
    public Dispatcher Dispatcher => _dispatcher;

    /// <inheritdoc/>
    public bool CheckAccess() => _dispatcher.CheckAccess();

    /// <inheritdoc/>
    public async Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        try
        {
            if (CheckAccess())
            {
                // Already on UI thread, execute directly
                action();
            }
            else
            {
                // Marshal to UI thread
                await _dispatcher.InvokeAsync(action, priority);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke action on UI thread");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));

        try
        {
            if (CheckAccess())
            {
                // Already on UI thread, execute directly
                return func();
            }
            else
            {
                // Marshal to UI thread
                return await _dispatcher.InvokeAsync(func, priority);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke function on UI thread");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(Func<Task> asyncAction, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));

        try
        {
            if (CheckAccess())
            {
                // Already on UI thread, execute directly
                await asyncAction();
            }
            else
            {
                // Marshal to UI thread and await the async operation
                await _dispatcher.InvokeAsync(asyncAction, priority);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke async action on UI thread");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunc, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

        try
        {
            if (CheckAccess())
            {
                // Already on UI thread, execute directly
                return await asyncFunc();
            }
            else
            {
                // Marshal to UI thread and await the async operation
                return await (await _dispatcher.InvokeAsync(asyncFunc, priority));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke async function on UI thread");
            throw;
        }
    }
}