using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Interface for dispatcher operations to enable proper UI thread marshaling.
/// Based on Microsoft WPF threading best practices.
/// </summary>
public interface IDispatcherHelper
{
    /// <summary>
    /// Executes an action on the UI thread asynchronously.
    /// </summary>
    /// <param name="action">The action to execute on the UI thread.</param>
    /// <param name="priority">The dispatcher priority for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal);

    /// <summary>
    /// Executes a function on the UI thread asynchronously and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="func">The function to execute on the UI thread.</param>
    /// <param name="priority">The dispatcher priority for the operation.</param>
    /// <returns>A task representing the asynchronous operation with result.</returns>
    Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal);

    /// <summary>
    /// Executes an asynchronous action on the UI thread.
    /// </summary>
    /// <param name="asyncAction">The asynchronous action to execute on the UI thread.</param>
    /// <param name="priority">The dispatcher priority for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvokeAsync(Func<Task> asyncAction, DispatcherPriority priority = DispatcherPriority.Normal);

    /// <summary>
    /// Executes an asynchronous function on the UI thread and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="asyncFunc">The asynchronous function to execute on the UI thread.</param>
    /// <param name="priority">The dispatcher priority for the operation.</param>
    /// <returns>A task representing the asynchronous operation with result.</returns>
    Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunc, DispatcherPriority priority = DispatcherPriority.Normal);

    /// <summary>
    /// Checks if the current thread has access to the dispatcher.
    /// </summary>
    /// <returns>True if the current thread is the UI thread, false otherwise.</returns>
    bool CheckAccess();

    /// <summary>
    /// Gets the underlying dispatcher instance.
    /// </summary>
    Dispatcher Dispatcher { get; }
}