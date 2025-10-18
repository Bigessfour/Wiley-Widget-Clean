using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.Views;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for AI assistance functionality.
    /// Registers AIAssistView with the AIAssistRegion.
    /// </summary>
    [Module(ModuleName = "AIAssistModule")]
    public class AIAssistModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing AIAssistModule");

            var regionManager = containerProvider.Resolve<IRegionManager>();

            // Register AIAssistView with AIAssistRegion
            regionManager.RegisterViewWithRegion("AIAssistRegion", typeof(AIAssistView));
            Log.Information("Successfully registered AIAssistView with AIAssistRegion");

            Log.Information("AIAssistModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register AIAssistViewModel
            containerRegistry.Register<AIAssistViewModel>();

            // Register AIResponseViewModel
            containerRegistry.Register<AIResponseViewModel>();

            // Register views for navigation
            containerRegistry.RegisterForNavigation<AIAssistView, AIAssistViewModel>();

            Log.Debug("AI assist types registered");
        }
    }
}