using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services;

namespace WileyWidget.Startup;

/// <summary>
/// Provides shared services to startup tasks.
/// </summary>
public sealed class StartupTaskContext
{
    private readonly IServiceProvider _serviceProvider;

    public StartupTaskContext(IServiceProvider serviceProvider, IStartupProgressReporter progressReporter, CancellationToken cancellationToken)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        ProgressReporter = progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// A scoped service provider for resolving additional services required by tasks.
    /// </summary>
    public IServiceProvider Services => _serviceProvider;

    /// <summary>
    /// Global startup progress reporter so tasks can surface status to the splash screen and logs.
    /// </summary>
    public IStartupProgressReporter ProgressReporter { get; }

    /// <summary>
    /// Cancellation token propagated from the host shutdown pipeline.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a child scope for resolving scoped dependencies.
    /// </summary>
    public IServiceScope CreateScope() => _serviceProvider.CreateScope();

    /// <summary>
    /// Allows strongly typed resolution helpers for convenience.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
}
