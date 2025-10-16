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

            // If MainRegion already exists, register immediately
            if (regionManager.Regions.ContainsRegionWithName("MainRegion"))
            {
                try
                {
                    regionManager.RegisterViewWithRegion("MainRegion", typeof(DashboardView));
                    Log.Information("Successfully registered DashboardView with MainRegion (immediate)");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to register DashboardView with MainRegion (immediate)");
                }
            }
            else
            {
                // Delay registration until the region is created by the shell/layout
                Log.Information("MainRegion not found - subscribing to Regions.CollectionChanged to register when added");

                try
                {
                    var notifier = regionManager.Regions as INotifyCollectionChanged;
                    NotifyCollectionChangedEventHandler? handler = null;
                    handler = (s, args) =>
                    {
                        try
                        {
                            if (args.NewItems != null)
                            {
                                foreach (var item in args.NewItems)
                                {
                                    if (item is IRegion r && string.Equals(r.Name, "MainRegion", StringComparison.Ordinal))
                                    {
                                        try
                                        {
                                            regionManager.RegisterViewWithRegion("MainRegion", typeof(DashboardView));
                                            Log.Information("Successfully registered DashboardView with MainRegion (deferred)");
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex, "Failed to register DashboardView with MainRegion (deferred)");
                                        }

                                        // Unsubscribe handler after successful registration attempt
                                        try
                                        {
                                            notifier.CollectionChanged -= handler;
                                        }
                                        catch { /* best-effort unsubscribe */ }

                                        return;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error while handling Regions.CollectionChanged for MainRegion");
                        }
                    };

                    notifier.CollectionChanged += handler;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to subscribe to Regions.CollectionChanged for deferred MainRegion registration");
                }
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