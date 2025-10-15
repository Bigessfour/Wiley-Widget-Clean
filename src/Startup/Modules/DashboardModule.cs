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

            if (containerProvider == null)
            {
                Log.Warning("ContainerProvider is null, skipping DashboardModule initialization");
                return;
            }

            IRegionManager regionManager;
            try
            {
                regionManager = containerProvider.Resolve<IRegionManager>();
                Log.Information("Successfully resolved IRegionManager from container");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to resolve IRegionManager from container");
                return;
            }

            // Log current region manager state for diagnostics
            Log.Information("Current region count: {RegionCount}", regionManager.Regions.Count());
            foreach (var region in regionManager.Regions)
            {
                Log.Debug("Available region: {RegionName}", region.Name);
            }

            // Check if MainRegion exists and log its status
            if (regionManager.Regions.ContainsRegionWithName("MainRegion"))
            {
                Log.Information("MainRegion found in region manager");

                // Use RequestNavigate to navigate to DashboardView with fallback handling
                try
                {
                    regionManager.RequestNavigate("MainRegion", "DashboardView", (result) =>
                    {
                        if (result.Success)
                        {
                            Log.Information("Successfully navigated to DashboardView in MainRegion");
                        }
                        else
                        {
                            Log.Warning("Navigation to DashboardView in MainRegion failed: {Exception}", result.Exception?.Message ?? "Navigation failed without specific error");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to request navigation to DashboardView in MainRegion");
                }
            }
            else
            {
                Log.Error("MainRegion not found in region manager. Cannot navigate to DashboardView. Ensure MainRegion is defined in MainWindow XAML with RegionName=\"MainRegion\".");
                Log.Information("Available regions at this time: [{AvailableRegions}]", 
                    string.Join(", ", regionManager.Regions.Select(r => r.Name)));
                
                // Log suggestions for troubleshooting
                Log.Information("Troubleshooting suggestions:");
                Log.Information("1. Check MainWindow.xaml for: prism:RegionManager.RegionName=\"MainRegion\"");
                Log.Information("2. Verify MainWindow is loaded before DashboardModule initialization");
                Log.Information("3. Consider using delayed navigation or region creation events");
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