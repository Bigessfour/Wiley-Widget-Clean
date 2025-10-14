using System;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for loading application settings.
    /// Replaces SettingsStartupTask in the new bootstrapper-based architecture.
    /// </summary>
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
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load persisted settings; defaults will be used");
            }

            Log.Information("SettingsModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // SettingsService is already registered in the main bootstrapper
            // This module only initializes the settings
        }
    }
}
