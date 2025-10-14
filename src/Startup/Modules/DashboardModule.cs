using System;
using System.Linq;
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

            var regionManager = containerProvider.Resolve<IRegionManager>();

            // Register DashboardView with MainRegion as the default view
            // If this fails, let the exception propagate to indicate module initialization failure
            regionManager.RegisterViewWithRegion("MainRegion", typeof(DashboardView));

            Log.Information("DashboardView registered with MainRegion");

            // Force activation of the view in the region to ensure it's visible
            // This addresses the "MainRegion has no views" issue
            try
            {
                var region = regionManager.Regions["MainRegion"];
                if (region != null && region.Views.Any())
                {
                    var view = region.Views.FirstOrDefault();
                    if (view != null)
                    {
                        region.Activate(view);
                        Log.Information("DashboardView activated in MainRegion");
                    }
                    else
                    {
                        Log.Warning("MainRegion has no views to activate");
                    }
                }
                else
                {
                    Log.Warning("MainRegion not found or has no views after registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to activate DashboardView in MainRegion");
            }

            Log.Information("DashboardModule initialization completed");
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