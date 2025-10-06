using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Extension methods for Dispatcher to provide async/await support.
/// Based on Microsoft WPF threading best practices.
/// </summary>
public static class DispatcherExtensions
{
    /// <summary>
    /// Executes an action on the UI thread asynchronously.
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task ExecuteOnUIThreadAsync(this IDispatcherHelper dispatcherHelper, Action action)
    {
        return dispatcherHelper.InvokeAsync(action);
    }

    /// <summary>
    /// Executes a function on the dispatcher thread asynchronously.
    /// </summary>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="priority">The dispatcher priority.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <summary>
    /// Executes an action on the dispatcher thread asynchronously.
    /// </summary>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="priority">The dispatcher priority.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task InvokeAsync(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            await dispatcher.InvokeAsync(action, priority);
        }
    }

    /// <summary>
    /// Executes a function on the dispatcher thread asynchronously and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="func">The function to execute.</param>
    /// <param name="priority">The dispatcher priority.</param>
    /// <returns>A task representing the asynchronous operation with result.</returns>
    public static async Task<T> InvokeAsync<T>(this Dispatcher dispatcher, Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
        if (func == null) throw new ArgumentNullException(nameof(func));

        if (dispatcher.CheckAccess())
        {
            return func();
        }
        else
        {
            return await dispatcher.InvokeAsync(func, priority);
        }
    }

    /// <summary>
    /// Executes an asynchronous action on the dispatcher thread.
    /// </summary>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="asyncAction">The asynchronous action to execute.</param>
    /// <param name="priority">The dispatcher priority.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task InvokeAsync(this Dispatcher dispatcher, Func<Task> asyncAction, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
        if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));

        if (dispatcher.CheckAccess())
        {
            await asyncAction();
        }
        else
        {
            await dispatcher.InvokeAsync(asyncAction, priority);
        }
    }

    /// <summary>
    /// Executes an asynchronous function on the dispatcher thread and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="asyncFunc">The asynchronous function to execute.</param>
    /// <param name="priority">The dispatcher priority.</param>
    /// <returns>A task representing the asynchronous operation with result.</returns>
    public static async Task<T> InvokeAsync<T>(this Dispatcher dispatcher, Func<Task<T>> asyncFunc, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
        if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

        if (dispatcher.CheckAccess())
        {
            return await asyncFunc();
        }
        else
        {
            return await (await dispatcher.InvokeAsync(asyncFunc, priority));
        }
    }
}