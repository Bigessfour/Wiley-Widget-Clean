using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace WileyWidget.Tests
{
    /// <summary>
    /// Base class for StaFact tests that provides WPF Application context.
    /// Ensures Application.Current is available for WPF controls and Dispatcher operations.
    /// </summary>
    public class TestApplication : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the TestApplication class.
        /// Assumes Application.Current is already available (set by StaFact or other means).
        /// </summary>
        public TestApplication()
        {
            // Application.Current should be set by the test runner (StaFact) or other initialization
        }

        /// <summary>
        /// Gets the WPF Application instance for this test.
        /// </summary>
        public Application Application => Application.Current ?? throw new InvalidOperationException("WPF Application context is not available. Ensure tests are run with StaFact or Application is initialized.");

        /// <summary>
        /// Runs an action on the UI thread synchronously.
        /// </summary>
        /// <param name="action">The action to run on the UI thread.</param>
        public void RunOnUIThread(Action action)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TestApplication));

            if (Application.Current == null)
                throw new InvalidOperationException("WPF Application context is not available");

            Application.Current.Dispatcher.Invoke(action);
        }

        /// <summary>
        /// Runs an async action on the UI thread.
        /// </summary>
        /// <param name="action">The async action to run on the UI thread.</param>
        public Task RunOnUIThreadAsync(Func<Task> action)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TestApplication));

            if (Application.Current == null)
                throw new InvalidOperationException("WPF Application context is not available");

            return Application.Current.Dispatcher.InvokeAsync(action).Task;
        }

        /// <summary>
        /// Disposes the test application and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the test application and cleans up resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            // For StaFact tests, Application is managed by the test runner
            // No cleanup needed here as Application.Current is shared across tests
        }
    }
}