using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using WileyWidget.Services;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Coordinates execution of registered <see cref="IStartupTask"/> instances.
    /// </summary>
    public sealed class StartupTaskRunner : IHostedService
    {
        private readonly IReadOnlyList<IStartupTask> _tasks;
        private readonly ILogger<StartupTaskRunner> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IStartupProgressReporter _progressReporter;

        public StartupTaskRunner(
            IEnumerable<IStartupTask> tasks,
            ILogger<StartupTaskRunner> logger,
            IServiceScopeFactory scopeFactory,
            IStartupProgressReporter progressReporter)
        {
            _tasks = tasks?.OrderBy(t => t.Order).ToList() ?? throw new ArgumentNullException(nameof(tasks));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _progressReporter = progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
        }

        /// <summary>
        /// Executes all registered startup tasks in sequence.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("=== STARTUP TASK RUNNER STARTING ===");
            _logger.LogInformation("Found {TaskCount} startup tasks registered", _tasks.Count);
            
            foreach (var task in _tasks)
            {
                _logger.LogInformation("Registered startup task: {TaskName} (Order: {Order})", task.Name, task.Order);
            }

            if (_tasks.Count == 0)
            {
                _logger.LogDebug("No startup tasks registered. Skipping execution.");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = new StartupTaskContext(scope.ServiceProvider, _progressReporter, cancellationToken);

            _logger.LogInformation("Executing {TaskCount} startup task(s)...", _tasks.Count);

            for (var index = 0; index < _tasks.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var task = _tasks[index];
                _logger.LogInformation("Starting startup task {TaskName} ({Index}/{Total})", task.Name, index + 1, _tasks.Count);

                try
                {
                    await task.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Completed startup task {TaskName} ({Index}/{Total})", task.Name, index + 1, _tasks.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Startup task {TaskName} failed", task.Name);
                    throw;
                }
            }

            _logger.LogInformation("All startup tasks completed successfully.");
        }

        /// <summary>
        /// Starts the hosted service by executing all startup tasks.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StartupTaskRunner.StartAsync called - executing startup tasks");
            return RunAsync(cancellationToken);
        }

        /// <summary>
        /// Stops the hosted service.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // No cleanup needed for startup tasks
            return Task.CompletedTask;
        }
    }
}
