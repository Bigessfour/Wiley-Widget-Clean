using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services
{
    /// <summary>
    /// Azure Key Vault service implementation
    /// </summary>
    public class AzureKeyVaultService : IAzureKeyVaultService
    {
        private readonly ILogger<AzureKeyVaultService> _logger;
        private SecretClient? _secretClient;
        private string? _keyVaultUrl;

        public AzureKeyVaultService(ILogger<AzureKeyVaultService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _keyVaultUrl = configuration["Azure:KeyVault:Url"];

            if (string.IsNullOrEmpty(_keyVaultUrl))
            {
                _logger.LogWarning("Azure Key Vault URL not configured");
                _secretClient = null;
                return;
            }

            try
            {
                // ✅ FAST CHAINED AUTH: Use ChainedTokenCredential for faster authentication
                // Prioritizes Azure CLI and Visual Studio credentials for development speed
                var credential = new ChainedTokenCredential(
                    new AzureCliCredential(),      // Fast if you're az logged in
                    new VisualStudioCredential(),  // If running in VS
                    new DefaultAzureCredential()   // Fallback to all other methods
                );
                _secretClient = new SecretClient(new Uri(_keyVaultUrl), credential);
                _logger.LogInformation("Azure Key Vault client initialized successfully with ChainedTokenCredential");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Key Vault client");
                _secretClient = null;
            }
        }

        public async Task<string?> GetSecretAsync(string secretName)
        {
            try
            {
                if (_secretClient == null)
                {
                    _logger.LogWarning("Azure Key Vault client not initialized");
                    return null;
                }

                _logger.LogInformation("Attempting to retrieve secret: {SecretName}", secretName);

                var secret = await _secretClient.GetSecretAsync(secretName);
                var secretValue = secret.Value.Value;

                _logger.LogInformation("Successfully retrieved secret: {SecretName}", secretName);
                return secretValue;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Secret not found: {SecretName}", secretName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving secret: {SecretName}", secretName);
                return null;
            }
        }

        public async Task SetSecretAsync(string secretName, string value)
        {
            try
            {
                if (_secretClient == null)
                {
                    _logger.LogWarning("Azure Key Vault client not initialized");
                    return;
                }

                if (string.IsNullOrEmpty(value))
                {
                    _logger.LogWarning("Cannot set empty secret value for: {SecretName}", secretName);
                    return;
                }

                _logger.LogInformation("Attempting to set secret: {SecretName}", secretName);

                await _secretClient.SetSecretAsync(secretName, value);

                _logger.LogInformation("Successfully set secret: {SecretName}", secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting secret: {SecretName}", secretName);
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                if (_secretClient == null)
                {
                    _logger.LogWarning("Azure Key Vault client not initialized");
                    return false;
                }

                _logger.LogInformation("Testing Azure Key Vault connection");

                // Try to list secrets (this will fail if we don't have permissions, but will test connectivity)
                await _secretClient.GetPropertiesOfSecretsAsync().GetAsyncEnumerator().MoveNextAsync();

                _logger.LogInformation("Azure Key Vault connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Key Vault connection test failed");
                return false;
            }
        }
    }
}