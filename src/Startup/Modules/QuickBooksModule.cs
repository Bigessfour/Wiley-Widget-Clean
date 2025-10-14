using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for QuickBooks Online initialization.
    /// Replaces QuickBooksStartupTask in the new bootstrapper-based architecture.
    /// </summary>
    public class QuickBooksModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing QuickBooksModule");

            // TODO: QuickBooks initialization temporarily disabled until views are working
            // The QB OAuth2 library has Serilog version conflicts that need to be resolved
            // Enable this after views are rendering and we can access the authentication controls
            
            /* COMMENTED OUT - QB INITIALIZATION DISABLED
            try
            {
                // QuickBooks services are optional - may not be registered yet
                IQuickBooksService quickBooksService = null;
                ISecretVaultService secretVaultService = null;

                try
                {
                    quickBooksService = containerProvider.Resolve<IQuickBooksService>();
                }
                catch
                {
                    Log.Warning("IQuickBooksService not registered - skipping QuickBooks initialization");
                    return;
                }

                try
                {
                    secretVaultService = containerProvider.Resolve<ISecretVaultService>();
                }
                catch
                {
                    Log.Debug("ISecretVaultService not available");
                }

                Log.Information("Starting QuickBooks Online service initialization");

                // Test secret vault connectivity for QBO secrets
                if (secretVaultService != null)
                {
                    try
                    {
                        var svTestResult = secretVaultService.TestConnectionAsync().GetAwaiter().GetResult();
                        if (svTestResult)
                        {
                            Log.Information("Secret vault connection verified for QBO secrets");
                        }
                        else
                        {
                            Log.Warning("Secret vault not available - QBO secrets will be loaded from environment variables");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to test secret vault connection");
                    }
                }

                // Test basic QBO connectivity (lightweight test)
                try
                {
                    var connectionTest = quickBooksService.TestConnectionAsync().GetAwaiter().GetResult();
                    if (connectionTest)
                    {
                        Log.Information("QuickBooks Online API connection test successful");
                    }
                    else
                    {
                        Log.Warning("QuickBooks Online API connection test failed - may require user authentication");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "QuickBooks Online connection test failed - service may not be fully configured yet");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize QuickBooks module");
            }
            END COMMENTED OUT SECTION */

            Log.Information("QuickBooksModule initialization completed (QB init disabled)");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // QuickBooks services are registered in the main bootstrapper or service registrations
            // This module only initializes the service
        }
    }
}
