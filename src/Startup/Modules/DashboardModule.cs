using System;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using Serilog;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for dashboard functionality and view registration.
    /// Registers DashboardView with the MainRegion for navigation.
    /// </summary>
    public class DashboardModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing DashboardModule");

            try
            {
                var regionManager = containerProvider.Resolve<IRegionManager>();

                // Register DashboardView with MainRegion as the default view
                regionManager.RegisterViewWithRegion("MainRegion", typeof(DashboardView));

                Log.Information("DashboardView registered with MainRegion");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register DashboardView with region");
            }

            Log.Information("DashboardModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register DashboardViewModel if not already registered
            containerRegistry.Register<DashboardViewModel>();

            // Register DashboardView for navigation
            containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();

            Log.Debug("Dashboard types registered");
        }
    }
}