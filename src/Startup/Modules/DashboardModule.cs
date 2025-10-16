using System;
using System.Linq;
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

            // Null check for regionManager.Regions
            if (regionManager.Regions == null)
            {
                Log.Error("RegionManager.Regions is null - cannot proceed with registration");
                return;
            }

            // Create MainRegion if missing
            if (!regionManager.Regions.ContainsRegionWithName("MainRegion"))
            {
                Log.Information("MainRegion not found, creating it explicitly");
                try
                {
                    regionManager.Regions.Add(new SingleActiveRegion { Name = "MainRegion" });
                    Log.Information("MainRegion created successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to create MainRegion");
                    return;
                }
            }
            else
            {
                Log.Information("MainRegion already exists");
            }

            // Register DashboardView with MainRegion (thread-safe as Prism handles it)
            try
            {
                regionManager.RegisterViewWithRegion("MainRegion", typeof(DashboardView));
                Log.Information("Successfully registered DashboardView with MainRegion");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register DashboardView with MainRegion");
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