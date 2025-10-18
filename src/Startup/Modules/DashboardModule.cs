using System;
using System.Linq;
using System.Collections.Specialized;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using Serilog;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for dashboard functionality and view registration.
    /// Registers DashboardView with the MainRegion using RegisterViewWithRegion.
    /// </summary>
    [Module(ModuleName = "DashboardModule")]
    public class DashboardModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing DashboardModule");

            var moduleHealthService = containerProvider.Resolve<IModuleHealthService>();
            moduleHealthService.RegisterModule("DashboardModule");

            try
            {
                if (containerProvider == null)
                {
                    throw new ArgumentNullException(nameof(containerProvider));
                }

                var regionManager = containerProvider.Resolve<IRegionManager>();
                Log.Information("Successfully resolved IRegionManager from container");

                // Simplified region registration with retry logic
                RegisterDashboardViewWithRegion(regionManager);

                Log.Information("DashboardModule initialization completed");
                moduleHealthService.MarkModuleInitialized("DashboardModule", true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error during DashboardModule initialization");
                moduleHealthService.MarkModuleInitialized("DashboardModule", false, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Registers the DashboardView with the MainRegion using a simplified approach.
        /// Includes retry logic for cases where the region might not be immediately available.
        /// </summary>
        /// <param name="regionManager">The region manager to use for registration</param>
        private void RegisterDashboardViewWithRegion(IRegionManager regionManager)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 100;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (regionManager.Regions.ContainsRegionWithName("MainRegion"))
                    {
                        regionManager.RegisterViewWithRegion("MainRegion", typeof(DashboardView));
                        Log.Information("Successfully registered DashboardView with MainRegion on attempt {Attempt}", attempt);
                        return;
                    }
                    else if (attempt < maxRetries)
                    {
                        Log.Debug("MainRegion not available on attempt {Attempt}, retrying in {Delay}ms", attempt, retryDelayMs);
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to register DashboardView with MainRegion on attempt {Attempt}", attempt);
                    if (attempt == maxRetries)
                    {
                        Log.Error("Exhausted all {MaxRetries} attempts to register DashboardView. Module initialization may be incomplete.", maxRetries);
                        throw new InvalidOperationException("Failed to register DashboardView with MainRegion after all retry attempts", ex);
                    }
                }
            }

            Log.Warning("MainRegion not found after {MaxRetries} attempts. DashboardView registration deferred.", maxRetries);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register DashboardViewModel - this is the authoritative registration
            containerRegistry.Register<DashboardViewModel>();

            // Register DashboardView for navigation
            containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();

            Log.Debug("Dashboard types registered");
        }
    }
}