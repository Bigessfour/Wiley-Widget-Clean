using System;
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
    /// Prism module responsible for settings management functionality.
    /// Loads application settings and registers SettingsView with the SettingsRegion.
    /// </summary>
    [Module(ModuleName = "SettingsModule")]
    public class SettingsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing SettingsModule");

            try
            {
                var settingsService = containerProvider.Resolve<SettingsService>();

                // Load persisted settings
                settingsService.Load();
                Log.Information("Application settings loaded successfully");

                // Apply default theme if not set
                if (string.IsNullOrWhiteSpace(settingsService.Current.Theme))
                {
                    settingsService.Current.Theme = "FluentDark";
                    settingsService.Save();
                    Log.Information("Default theme applied (FluentDark) because no preference was persisted");
                }

                // Register SettingsView with SettingsRegion
                var regionManager = containerProvider.Resolve<IRegionManager>();
                regionManager.RegisterViewWithRegion("SettingsRegion", typeof(SettingsView));
                Log.Information("Successfully registered SettingsView with SettingsRegion");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load persisted settings; defaults will be used");
            }

            Log.Information("SettingsModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register SettingsViewModel
            containerRegistry.Register<SettingsViewModel>();

            // Register SettingsView for navigation
            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();

            // SettingsService is already registered in the main bootstrapper
            // This module only initializes the settings
        }
    }
}
