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
    /// Prism module responsible for municipal account functionality.
    /// Registers MunicipalAccountView for navigation.
    /// </summary>
    public class MunicipalAccountModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing MunicipalAccountModule");

            // Module initialization logic if needed
            Log.Information("MunicipalAccountModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register MunicipalAccountViewModel if not already registered
            containerRegistry.Register<MunicipalAccountViewModel>();

            // Register MunicipalAccountView for navigation
            containerRegistry.RegisterForNavigation<MunicipalAccountView, MunicipalAccountViewModel>();

            Log.Debug("Municipal account types registered");
        }
    }
}