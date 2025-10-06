using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using WileyWidget.Services.Threading;

namespace WileyWidget.LifecycleTests;

internal sealed class TestDispatcherHelper : IDispatcherHelper
{
    private readonly Dispatcher _dispatcher;

    public TestDispatcherHelper()
    {
        // Ensure the dispatcher is created for the current thread.
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public Dispatcher Dispatcher => _dispatcher;

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var operation = _dispatcher.InvokeAsync(action, priority);
        return operation.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (func == null)
        {
            throw new ArgumentNullException(nameof(func));
        }

        if (CheckAccess())
        {
            return Task.FromResult(func());
        }

        var operation = _dispatcher.InvokeAsync(func, priority);
        return operation.Task;
    }

    public async Task InvokeAsync(Func<Task> asyncAction, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (asyncAction == null)
        {
            throw new ArgumentNullException(nameof(asyncAction));
        }

        if (CheckAccess())
        {
            await asyncAction().ConfigureAwait(false);
            return;
        }

        var tcs = new TaskCompletionSource<object?>();

        _ = _dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await asyncAction().ConfigureAwait(false);
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, priority);

        await tcs.Task.ConfigureAwait(false);
    }

    public async Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunc, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (asyncFunc == null)
        {
            throw new ArgumentNullException(nameof(asyncFunc));
        }

        if (CheckAccess())
        {
            return await asyncFunc().ConfigureAwait(false);
        }

        var tcs = new TaskCompletionSource<T>();

        _ = _dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var result = await asyncFunc().ConfigureAwait(false);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, priority);

        return await tcs.Task.ConfigureAwait(false);
    }
}
