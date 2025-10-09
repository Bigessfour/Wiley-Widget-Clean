#nullable enable

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Implementation of IDispatcherHelper for WPF dispatcher operations
/// </summary>
public class DispatcherHelper : IDispatcherHelper
{
    private readonly Dispatcher _dispatcher;

    public DispatcherHelper()
    {
        // Use Application.Current.Dispatcher to ensure we always get the main UI dispatcher
        // This is safer than Dispatcher.CurrentDispatcher which depends on which thread creates the instance
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public DispatcherHelper(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Checks if the current thread is the UI thread
    /// </summary>
    public bool CheckAccess()
    {
        return _dispatcher.CheckAccess();
    }

    /// <summary>
    /// Executes an action on the UI thread synchronously
    /// </summary>
    /// <param name="action">The action to execute</param>
    public void Invoke(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }

    /// <summary>
    /// Executes a function on the UI thread synchronously and returns the result
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <returns>The result of the function</returns>
    public T Invoke<T>(Func<T> func)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));

        if (CheckAccess())
        {
            return func();
        }
        else
        {
            return _dispatcher.Invoke(func);
        }
    }

    /// <summary>
    /// Executes an action on the UI thread asynchronously
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>A task representing the async operation</returns>
    public Task InvokeAsync(Action action)
    {
        return InvokeAsync(action, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Executes a function on the UI thread asynchronously and returns the result
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <returns>A task representing the async operation with result</returns>
    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        return InvokeAsync(func, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Executes an action on the UI thread asynchronously with priority
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="priority">The dispatcher priority</param>
    /// <returns>A task representing the async operation</returns>
    public Task InvokeAsync(Action action, DispatcherPriority priority)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        return _dispatcher.InvokeAsync(action, priority).Task;
    }

    /// <summary>
    /// Executes a function on the UI thread asynchronously with priority and returns the result
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <param name="priority">The dispatcher priority</param>
    /// <returns>A task representing the async operation with result</returns>
    public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));

        return _dispatcher.InvokeAsync(func, priority).Task;
    }
}