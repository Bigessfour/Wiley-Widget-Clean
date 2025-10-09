using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading; // Add WPF Dispatcher support
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace WileyWidget.Tests
{
    /// <summary>
    /// Thread pool for managing STA threads to run WPF UI tests efficiently.
    /// Reuses STA threads across test executions to avoid expensive thread creation.
    /// </summary>
    internal class StaThreadPool : IDisposable
    {
        private static readonly Lazy<StaThreadPool> _instance = new Lazy<StaThreadPool>(() => new StaThreadPool());
        private readonly BlockingCollection<(Func<Task<RunSummary>> work, TaskCompletionSource<RunSummary> tcs)> _workQueue = new();
        private readonly List<Thread> _staThreads = new();
        private readonly CancellationTokenSource _shutdownToken = new();
        private const int MaxStaThreads = 4; // Limit concurrent STA threads
        private bool _disposed;

        public static StaThreadPool Instance => _instance.Value;

        private StaThreadPool()
        {
            // Create initial STA threads
            for (int i = 0; i < Math.Min(Environment.ProcessorCount, MaxStaThreads); i++)
            {
                CreateStaThread();
            }
        }

        private void CreateStaThread()
        {
            var thread = new Thread(ProcessWorkItems)
            {
                IsBackground = true,
                Name = $"WPF STA Test Thread {_staThreads.Count + 1}"
            };
            thread.SetApartmentState(ApartmentState.STA);
            _staThreads.Add(thread);
            thread.Start();
        }

        private void ProcessWorkItems()
        {
            try
            {
                foreach (var (work, tcs) in _workQueue.GetConsumingEnumerable(_shutdownToken.Token))
                {
                    try
                    {
                        var result = work().GetAwaiter().GetResult();
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested
            }
        }

        public Task<RunSummary> RunAsync(Func<Task<RunSummary>> work, TimeSpan timeout)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(StaThreadPool));

            var tcs = new TaskCompletionSource<RunSummary>();
            _workQueue.Add((work, tcs), _shutdownToken.Token);

            // Add timeout handling
            var timeoutTask = Task.Delay(timeout);
            var completedTask = Task.WhenAny(tcs.Task, timeoutTask).GetAwaiter().GetResult();

            if (completedTask == timeoutTask)
            {
                tcs.TrySetException(new TimeoutException($"Test execution timed out after {timeout.TotalSeconds} seconds"));
            }

            return tcs.Task;
        }

        ~StaThreadPool()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _shutdownToken.Cancel();
            _workQueue.CompleteAdding();

            // Dispose the work queue
            _workQueue.Dispose();

            // Wait for threads to finish with timeout
            foreach (var thread in _staThreads)
            {
                if (thread.IsAlive && !thread.Join(TimeSpan.FromSeconds(5)))
                {
                    // Thread didn't finish in time, but we can't abort it safely
                    // Just log and continue - the thread will be terminated when the process exits
                }
            }

            _shutdownToken.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Attribute to run an xUnit fact on an STA thread. Useful for WPF UI tests.
    /// </summary>
    [XunitTestCaseDiscoverer("WileyWidget.Tests.StaFactDiscoverer", "WileyWidget.Tests")]
    [Trait("Category", "StaFact")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class StaFactAttribute : FactAttribute { }

    public class StaFactDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink _diagnosticMessageSink;

        public StaFactDiscoverer(IMessageSink diagnosticMessageSink)
        {
            _diagnosticMessageSink = diagnosticMessageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            yield return new StaXunitTestCase(_diagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod);
        }
    }

    public class StaXunitTestCase : XunitTestCase
    {
        private static readonly object _applicationLock = new object();

        /// <summary>
        /// Default constructor required by xUnit for serialization.
        /// Marked obsolete in xUnit v2 but still needed for framework compatibility.
        /// </summary>
        [Obsolete("Required by xUnit framework for serialization", false)]
        public StaXunitTestCase() { }

        public StaXunitTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod)
#pragma warning disable CS0618 // Obsolete constructor, but required for xUnit compatibility
            : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
#pragma warning restore CS0618
        {
        }

        public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            // Use the STA thread pool for efficient test execution
            return StaThreadPool.Instance.RunAsync(async () =>
            {
                // Validate we're running on an STA thread
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                {
                    throw new InvalidOperationException("Test must execute on an STA thread for WPF compatibility");
                }

                // Ensure WPF Dispatcher is available and properly initialized
                if (Dispatcher.CurrentDispatcher == null)
                {
                    throw new InvalidOperationException("WPF Dispatcher is not available on STA thread");
                }

                // Initialize WPF Application once per AppDomain (thread-safe)
                lock (_applicationLock)
                {
                    if (Application.Current == null)
                    {
                        var application = new Application();
                        
                        // Register Syncfusion license for test environment
                        RegisterSyncfusionLicenseForTests();
                        
                        // Load theme resources for testing
                        try
                        {
                            // Load Wiley Widget custom theme
                            var themeUri = new Uri("pack://application:,,,/WileyWidget;component/Themes/WileyTheme.xaml");
                            var themeResource = new ResourceDictionary { Source = themeUri };
                            application.Resources.MergedDictionaries.Add(themeResource);
                            
                            // Load Syncfusion FluentDark theme (required for proper control rendering)
                            var syncfusionThemeUri = new Uri("pack://application:,,,/Syncfusion.Themes.FluentDark.WPF;component/FluentDark/FluentDark.xaml");
                            var syncfusionThemeResource = new ResourceDictionary { Source = syncfusionThemeUri };
                            application.Resources.MergedDictionaries.Add(syncfusionThemeResource);
                            
                            // Note: Global theme setting removed - theme resources loaded into application resources
                            
                            System.Diagnostics.Debug.WriteLine("Theme resources loaded successfully for tests");
                        }
                        catch (Exception ex)
                        {
                            // Log theme loading failure but continue - tests should still work
                            System.Diagnostics.Debug.WriteLine($"Failed to load theme resources: {ex.Message}");
                        }
                    }
                }

                try
                {
                    // Execute the test on the STA thread
                    return await base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                }
                catch (Exception ex)
                {
                    // Log WPF-specific threading issues
                    if (ex.Message.Contains("thread") || ex.Message.Contains("STA") || ex.Message.Contains("dispatcher"))
                    {
                        throw new InvalidOperationException($"WPF threading error: {ex.Message}", ex);
                    }
                    throw;
                }
            }, TimeSpan.FromMinutes(5)); // 5 minute timeout for tests
        }

        /// <summary>
        /// Registers the Syncfusion license for test execution.
        /// </summary>
        private static void RegisterSyncfusionLicenseForTests()
        {
            try
            {
                var licenseKey = GetSyncfusionLicenseKeyForTests();
                if (!string.IsNullOrEmpty(licenseKey))
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    System.Diagnostics.Debug.WriteLine("Syncfusion license registered successfully for tests");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Warning: No Syncfusion license key found for tests. Syncfusion controls may not work properly.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register Syncfusion license for tests: {ex.Message}");
                // Don't throw - allow tests to continue even if licensing fails
            }
        }

        /// <summary>
        /// Gets the Syncfusion license key for test execution.
        /// </summary>
        private static string GetSyncfusionLicenseKeyForTests()
        {
            try
            {
                // 1) Environment variable (check all scopes: Process, User, Machine)
                var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                {
                    return key.Trim();
                }

                // Explicitly check machine scope
                key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                {
                    return key.Trim();
                }

                // 2) Check for license file in test directory
                var licenseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
                if (File.Exists(licenseFilePath))
                {
                    key = File.ReadAllText(licenseFilePath).Trim();
                    if (!string.IsNullOrWhiteSpace(key) && key != "YOUR_LICENSE_KEY_HERE")
                    {
                        return key;
                    }
                }

                // 3) Check for license file in project root
                licenseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "license.key");
                if (File.Exists(licenseFilePath))
                {
                    key = File.ReadAllText(licenseFilePath).Trim();
                    if (!string.IsNullOrWhiteSpace(key) && key != "YOUR_LICENSE_KEY_HERE")
                    {
                        return key;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resolving Syncfusion license key for tests: {ex.Message}");
            }

            return null;
        }
    }
}
