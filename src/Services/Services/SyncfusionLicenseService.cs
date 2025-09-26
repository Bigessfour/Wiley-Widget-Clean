using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;

namespace WileyWidget.Services
{
    /// <summary>
    /// Syncfusion license service implementation
    /// </summary>
    public class SyncfusionLicenseService : ISyncfusionLicenseService
    {
        private readonly ILogger<SyncfusionLicenseService> _logger;

        public SyncfusionLicenseService(ILogger<SyncfusionLicenseService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ValidateLicenseAsync(string licenseKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    _logger.LogWarning("License key is null or empty");
                    return false;
                }

                _logger.LogInformation("Validating Syncfusion license key");

                // Register the license with Syncfusion
                var sanitizedLicenseKey = licenseKey.Trim();
                SyncfusionLicenseProvider.RegisterLicense(sanitizedLicenseKey);

                _logger.LogInformation("Syncfusion license registered successfully");

                await Task.CompletedTask; // Suppress async warning for future async operations
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Syncfusion license");
                return false;
            }
        }
    }
}