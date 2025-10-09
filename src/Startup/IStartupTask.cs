using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Startup;

/// <summary>
/// Represents a unit of work that must run during the application's startup pipeline.
/// Tasks are executed sequentially based on <see cref="Order"/>.
/// </summary>
public interface IStartupTask
{
    /// <summary>
    /// Friendly name used for logging and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines execution order. Lower values execute earlier.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Executes the startup task.
    /// </summary>
    /// <param name="context">Shared execution context.</param>
    /// <param name="cancellationToken">Propagation token for graceful shutdown.</param>
    Task ExecuteAsync(StartupTaskContext context, CancellationToken cancellationToken);
}
