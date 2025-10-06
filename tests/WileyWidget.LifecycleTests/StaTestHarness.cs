using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WileyWidget.LifecycleTests;

internal static class StaTestHarness
{
    public static Task RunAsync(Func<Task> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

    var completionSource = new TaskCompletionSource<object?>();

        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var dispatcher = Dispatcher.CurrentDispatcher;

                dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        await action().ConfigureAwait(false);
                        completionSource.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        completionSource.TrySetException(ex);
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    }
                });

                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                completionSource.TrySetException(ex);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completionSource.Task;
    }
}
