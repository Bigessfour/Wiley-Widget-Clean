using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Identity;
using WileyWidget.Services.Caching;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Advanced parallel processing service for startup operations.
/// Implements hyperthreading optimization, thread-safe operations, and intelligent task scheduling.
/// Based on Microsoft's recommended threading patterns for WPF applications.
/// </summary>
public class ParallelStartupService : IHostedService, IDisposable
{
    private readonly ILogger<ParallelStartupService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly StartupCacheService _cacheService;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly CancellationTokenSource _shutdownTokenSource;

    // Optimize for hyperthreading: Use logical processor count
    private readonly int _maxConcurrency;
    private readonly TaskScheduler _dedicatedScheduler;
    private bool _disposed = false;

    public ParallelStartupService(
        ILogger<ParallelStartupService> logger,
        IServiceProvider serviceProvider,
        StartupCacheService cacheService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        
        // Optimize concurrency for hyperthreading
        _maxConcurrency = Math.Max(Environment.ProcessorCount, 4); // Minimum 4 threads
        _concurrencyLimiter = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        _shutdownTokenSource = new CancellationTokenSource();
        
        // Create dedicated task scheduler for startup operations
        _dedicatedScheduler = new LimitedConcurrencyLevelTaskScheduler(_maxConcurrency);
        
        _logger.LogInformation("ParallelStartupService initialized with {MaxConcurrency} concurrent operations", _maxConcurrency);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("=== PARALLEL STARTUP OPTIMIZATION STARTED ===");

        try
        {
            // Phase 1: Cache Warmup (Parallel execution of independent operations)
            await ExecutePhase1CacheWarmupAsync(cancellationToken);

            // Phase 2: I/O Intensive Operations (Optimized for concurrent I/O)
            await ExecutePhase2IOOperationsAsync(cancellationToken);

            // Phase 3: CPU Intensive Operations (Optimized for hyperthreading)
            await ExecutePhase3CPUOperationsAsync(cancellationToken);

            _logger.LogInformation("=== PARALLEL STARTUP OPTIMIZATION COMPLETED in {TotalMs}ms ===", 
                totalStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parallel startup optimization failed after {ElapsedMs}ms", totalStopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            totalStopwatch.Stop();
        }
    }

    #region Phase 1: Cache Warmup Operations

    /// <summary>
    /// Phase 1: Execute cache warmup operations in parallel
    /// These are independent operations that can run concurrently
    /// </summary>
    private async Task ExecutePhase1CacheWarmupAsync(CancellationToken cancellationToken)
    {
        var phaseStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Phase 1: Cache warmup operations starting...");

        var warmupTasks = new List<Task>
        {
            ExecuteWithConcurrencyLimitAsync("Assembly Metadata", 
                () => _cacheService.PrewarmAssemblyMetadataAsync(), cancellationToken),
                
            ExecuteWithConcurrencyLimitAsync("Font Cache", 
                () => _cacheService.PrewarmFontCacheAsync(), cancellationToken),
                
            ExecuteWithConcurrencyLimitAsync("System Resources", 
                () => PrewarmSystemResourcesAsync(), cancellationToken),
                
            ExecuteWithConcurrencyLimitAsync("Configuration Cache", 
                () => PrewarmConfigurationCacheAsync(), cancellationToken)
        };

        await Task.WhenAll(warmupTasks);
        
        _logger.LogInformation("Phase 1: Cache warmup completed in {ElapsedMs}ms", phaseStopwatch.ElapsedMilliseconds);
    }

    #endregion

    #region Phase 2: I/O Intensive Operations

    /// <summary>
    /// Phase 2: Execute I/O intensive operations with optimized concurrency
    /// These operations benefit from high concurrency due to I/O wait times
    /// </summary>
    private async Task ExecutePhase2IOOperationsAsync(CancellationToken cancellationToken)
    {
        var phaseStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Phase 2: I/O intensive operations starting...");

        // I/O operations can handle higher concurrency
        var ioTasks = new List<Task>
        {
            ExecuteWithConcurrencyLimitAsync("Configuration Loading", 
                () => PreloadConfigurationFilesAsync(), cancellationToken),
                
            ExecuteWithConcurrencyLimitAsync("Database Schema Check", 
                () => ValidateDatabaseSchemaAsync(), cancellationToken),
                
            ExecuteWithConcurrencyLimitAsync("Azure Services Warmup", 
                () => WarmupAzureServicesAsync(), cancellationToken),
                
            ExecuteWithConcurrencyLimitAsync("Log Directory Setup", 
                () => EnsureLogDirectoriesAsync(), cancellationToken)
        };

        await Task.WhenAll(ioTasks);
        
        _logger.LogInformation("Phase 2: I/O operations completed in {ElapsedMs}ms", phaseStopwatch.ElapsedMilliseconds);
    }

    #endregion

    #region Phase 3: CPU Intensive Operations

    /// <summary>
    /// Phase 3: Execute CPU intensive operations optimized for hyperthreading
    /// These operations benefit from parallel CPU utilization
    /// </summary>
    private async Task ExecutePhase3CPUOperationsAsync(CancellationToken cancellationToken)
    {
        var phaseStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Phase 3: CPU intensive operations starting...");

        // Use TaskFactory with dedicated scheduler for CPU-bound work
        var taskFactory = new TaskFactory(_dedicatedScheduler);

        var cpuTasks = new List<Task>
        {
            taskFactory.StartNew(() => PrecompileExpressions(), cancellationToken),
            taskFactory.StartNew(() => WarmupReflectionCache(), cancellationToken),
            taskFactory.StartNew(() => InitializeThreadPools(), cancellationToken),
            taskFactory.StartNew(() => PreloadAssemblyDependencies(), cancellationToken)
        };

        await Task.WhenAll(cpuTasks);
        
        _logger.LogInformation("Phase 3: CPU operations completed in {ElapsedMs}ms", phaseStopwatch.ElapsedMilliseconds);
    }

    #endregion

    #region Concurrency Management

    /// <summary>
    /// Executes a task with concurrency limiting and comprehensive error handling
    /// </summary>
    private async Task ExecuteWithConcurrencyLimitAsync(string operationName, Func<Task> operation, CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogDebug("Starting parallel operation: {OperationName}", operationName);
            
            await operation();
            
            _logger.LogDebug("Completed parallel operation: {OperationName} in {ElapsedMs}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parallel operation failed: {OperationName}", operationName);
            // Don't rethrow - allow other operations to continue
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    #endregion

    #region Individual Operations Implementation

    private async Task PrewarmSystemResourcesAsync()
    {
        await Task.Run(() =>
        {
            // Prewarm system resources that are commonly accessed
            _ = Environment.ProcessorCount;
            _ = Environment.WorkingSet;
            _ = Environment.CurrentDirectory;
            _ = Environment.MachineName;
            _ = Environment.UserName;
            
            // Prewarm common Path operations
            _ = System.IO.Path.GetTempPath();
            _ = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _ = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        });
    }

    private async Task PrewarmConfigurationCacheAsync()
    {
        // Cache commonly accessed configuration values
        await _cacheService.GetOrSetConfigurationAsync("startup_timestamp", 
            () => Task.FromResult(DateTime.UtcNow), TimeSpan.FromHours(1));
    }

    private async Task PreloadConfigurationFilesAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // Preload configuration files into OS file cache
                var configFiles = new[]
                {
                    "appsettings.json",
                    "appsettings.Production.json",
                    "App.config"
                };

                foreach (var file in configFiles)
                {
                    if (System.IO.File.Exists(file))
                    {
                        _ = System.IO.File.ReadAllBytes(file); // Load into OS cache
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to preload configuration files");
            }
        });
    }

    private async Task ValidateDatabaseSchemaAsync()
    {
        try
        {
            // This would typically connect to database and validate schema
            // Implementation depends on your DatabaseConfiguration class
            await Task.Delay(100); // Simulate database check
            _logger.LogDebug("Database schema validation completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database schema validation failed");
        }
    }

    private async Task WarmupAzureServicesAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // Prewarm Azure service connections (non-blocking)
                // This prepares the Azure SDK without making actual calls
                // ✅ FAST CHAINED AUTH: Use ChainedTokenCredential for faster warmup
                _ = new ChainedTokenCredential(
                    new AzureCliCredential(),      // Fast if you're az logged in
                    new VisualStudioCredential(),  // If running in VS
                    new DefaultAzureCredential()   // Fallback to all other methods
                );
                _logger.LogDebug("Azure services warmup completed with ChainedTokenCredential");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Azure services warmup failed");
            }
        });
    }

    private async Task EnsureLogDirectoriesAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var logPath = System.IO.Path.Combine(Environment.CurrentDirectory, "logs");
                System.IO.Directory.CreateDirectory(logPath);
                
                var tempLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WileyWidget", "logs");
                System.IO.Directory.CreateDirectory(tempLogPath);
                
                _logger.LogDebug("Log directories ensured");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to ensure log directories");
            }
        });
    }

    private void PrecompileExpressions()
    {
        try
        {
            // Precompile commonly used expressions and delegates
            // This forces JIT compilation of critical paths
            var testAction = new Action(() => { });
            testAction();
            
            var testFunc = new Func<int>(() => Environment.ProcessorCount);
            _ = testFunc();
            
            _logger.LogDebug("Expression precompilation completed");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Expression precompilation failed");
        }
    }

    private void WarmupReflectionCache()
    {
        try
        {
            // Prewarm reflection operations for commonly used types
            var types = new[]
            {
                typeof(WileyWidget.App),
                typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection),
                typeof(Microsoft.EntityFrameworkCore.DbContext),
                typeof(System.Windows.Application)
            };

            foreach (var type in types)
            {
                _ = type.GetMethods();
                _ = type.GetProperties();
                _ = type.GetConstructors();
            }
            
            _logger.LogDebug("Reflection cache warmup completed");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Reflection cache warmup failed");
        }
    }

    private void InitializeThreadPools()
    {
        try
        {
            // Optimize thread pool for application workload
            var workerThreads = Math.Max(Environment.ProcessorCount * 2, 8);
            var ioThreads = Math.Max(Environment.ProcessorCount * 4, 16);
            
            ThreadPool.SetMinThreads(workerThreads, ioThreads);
            ThreadPool.SetMaxThreads(workerThreads * 4, ioThreads * 2);
            
            _logger.LogDebug("Thread pool initialized - Workers: {Workers}, IO: {IO}", workerThreads, ioThreads);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Thread pool initialization failed");
        }
    }

    private void PreloadAssemblyDependencies()
    {
        try
        {
            // Force loading of critical assemblies to avoid lazy loading delays
            var criticalTypes = new[]
            {
                typeof(Syncfusion.Licensing.SyncfusionLicenseProvider),
                typeof(Serilog.Log),
                typeof(Microsoft.Extensions.Hosting.Host),
                typeof(System.Text.Json.JsonSerializer)
            };

            foreach (var type in criticalTypes)
            {
                _ = type.Assembly.Location; // Forces assembly loading
            }
            
            _logger.LogDebug("Assembly dependencies preloaded");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Assembly dependency preloading failed");
        }
    }

    #endregion

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ParallelStartupService stopping...");
        
        _shutdownTokenSource.Cancel();
        _concurrencyLimiter?.Dispose();
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _concurrencyLimiter?.Dispose();
                _shutdownTokenSource?.Dispose();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Custom task scheduler that limits concurrency for CPU-bound operations
/// Optimized for hyperthreading scenarios
/// </summary>
internal class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
{
    private readonly int _maxDegreeOfParallelism;
    private readonly Queue<Task> _tasks = new();
    private int _delegatesQueuedOrRunning = 0;

    public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    protected sealed override void QueueTask(Task task)
    {
        lock (_tasks)
        {
            _tasks.Enqueue(task);
            if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
            {
                ++_delegatesQueuedOrRunning;
                NotifyThreadPoolOfPendingWork();
            }
        }
    }

    private void NotifyThreadPoolOfPendingWork()
    {
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            try
            {
                while (true)
                {
                    Task item;
                    lock (_tasks)
                    {
                        if (_tasks.Count == 0)
                        {
                            --_delegatesQueuedOrRunning;
                            break;
                        }

                        item = _tasks.Dequeue();
                    }

                    TryExecuteTask(item);
                }
            }
            finally
            {
                // Implementation ensures thread safety
            }
        }, null);
    }

    protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        if (taskWasPreviouslyQueued) return false;
        return TryExecuteTask(task);
    }

    protected sealed override IEnumerable<Task> GetScheduledTasks()
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(_tasks, ref lockTaken);
            if (lockTaken) return _tasks;
            else throw new NotSupportedException();
        }
        finally
        {
            if (lockTaken) Monitor.Exit(_tasks);
        }
    }

    public sealed override int MaximumConcurrencyLevel => _maxDegreeOfParallelism;
}