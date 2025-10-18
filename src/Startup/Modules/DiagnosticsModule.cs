using System;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for capturing diagnostics snapshot.
    /// Replaces DiagnosticsStartupTask in the new bootstrapper-based architecture.
    /// </summary>
    [Module(ModuleName = "DiagnosticsModule")]
    public class DiagnosticsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing DiagnosticsModule");

            try
            {
                // IHostEnvironment is optional - may not be registered in Prism-only setup
                IHostEnvironment hostEnvironment = null;
                try
                {
                    hostEnvironment = containerProvider.Resolve<IHostEnvironment>();
                }
                catch
                {
                    Log.Debug("IHostEnvironment not available - using default environment info");
                }

                var process = Process.GetCurrentProcess();
                var environmentName = hostEnvironment?.EnvironmentName ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

                Log.Information(
                    "Startup diagnostics: Environment={EnvironmentName}, Machine={MachineName}, PID={ProcessId}, Threads={ThreadCount}, WorkingSet={WorkingSet}MB, PrivateMemory={PrivateMemory}MB",
                    environmentName,
                    Environment.MachineName,
                    process.Id,
                    process.Threads.Count,
                    Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2),
                    Math.Round(process.PrivateMemorySize64 / (1024.0 * 1024.0), 2));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to capture startup diagnostics snapshot");
            }

            Log.Information("DiagnosticsModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // No specific registrations needed for diagnostics module
        }
    }
}
