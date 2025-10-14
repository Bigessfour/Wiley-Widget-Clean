using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for Syncfusion license registration.
    /// Replaces SyncfusionStartupTask in the new bootstrapper-based architecture.
    /// </summary>
    public class SyncfusionModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing SyncfusionModule");

            try
            {
                var configuration = containerProvider.Resolve<IConfiguration>();
                var licenseService = containerProvider.Resolve<ISyncfusionLicenseService>();
                var licenseState = containerProvider.Resolve<SyncfusionLicenseState>();
                
                // SecretVaultService is optional
                ISecretVaultService secretVaultService = null;
                try
                {
                    secretVaultService = containerProvider.Resolve<ISecretVaultService>();
                }
                catch
                {
                    Log.Debug("ISecretVaultService not available - will skip vault lookup");
                }

                // Execute license registration synchronously during module initialization
                var licenseKey = ResolveLicenseKey(configuration, secretVaultService);

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    licenseState.MarkAttempt(false, "Syncfusion license key not found; running in evaluation mode.");
                    Log.Warning("Syncfusion license key not found; running in evaluation mode");
                    return;
                }

                var success = licenseService.ValidateLicenseAsync(licenseKey).GetAwaiter().GetResult();
                if (success)
                {
                    licenseState.MarkAttempt(true, "Syncfusion license registered successfully.");
                    Log.Information("Syncfusion license registration succeeded");
                }
                else
                {
                    licenseState.MarkAttempt(false, "Syncfusion license key failed validation.");
                    Log.Warning("Syncfusion license key failed validation");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Syncfusion license");
            }

            Log.Information("SyncfusionModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Services are already registered in the main bootstrapper
            // This module only initializes the license
        }

        private string ResolveLicenseKey(IConfiguration configuration, ISecretVaultService secretVaultService)
        {
            try
            {
                // Try environment variable first
                var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrWhiteSpace(envKey))
                {
                    Log.Debug("Syncfusion license key found in environment variable");
                    return envKey;
                }

                // Try configuration
                var configKey = configuration["Syncfusion:LicenseKey"];
                if (!string.IsNullOrWhiteSpace(configKey))
                {
                    Log.Debug("Syncfusion license key found in configuration");
                    return configKey;
                }

                // Try secret vault if available
                if (secretVaultService != null)
                {
                    var vaultKey = secretVaultService.GetSecretAsync("SyncfusionLicenseKey")
                        .GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(vaultKey))
                    {
                        Log.Debug("Syncfusion license key found in secret vault");
                        return vaultKey;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error resolving Syncfusion license key");
            }

            return null;
        }
    }
}
