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
        private readonly SecretClient _secretClient;
        private readonly string _keyVaultUrl;

        public AzureKeyVaultService(ILogger<AzureKeyVaultService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _keyVaultUrl = configuration["Azure:KeyVault:Url"];

            // If the URL still contains placeholder syntax, try to get it from environment variables
            if (_keyVaultUrl != null && _keyVaultUrl.Contains("${"))
            {
                var envValue = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URL");
                if (!string.IsNullOrEmpty(envValue))
                {
                    _keyVaultUrl = envValue;
                    _logger.LogInformation("Resolved Azure Key Vault URL from environment variable: '{KeyVaultUrl}'", _keyVaultUrl);
                }
            }

            _logger.LogInformation("Azure Key Vault URL from config: '{KeyVaultUrl}'", _keyVaultUrl);

            if (string.IsNullOrEmpty(_keyVaultUrl))
            {
                _logger.LogWarning("Azure Key Vault URL not configured");
                _secretClient = null;
                return;
            }

            // Validate the URI format
            if (!Uri.TryCreate(_keyVaultUrl, UriKind.Absolute, out Uri? validatedUri))
            {
                _logger.LogError("Azure Key Vault URL is not a valid absolute URI: '{KeyVaultUrl}'", _keyVaultUrl);
                _secretClient = null;
                return;
            }

            try
            {
                // Use DefaultAzureCredential which tries multiple authentication methods
                // 1. Environment variables (AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID)
                // 2. Managed Identity
                // 3. Azure CLI
                // 4. Visual Studio
                // 5. Interactive browser
                var credential = new DefaultAzureCredential();
                _secretClient = new SecretClient(validatedUri, credential);
                _logger.LogInformation("Azure Key Vault client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Key Vault client");
                _secretClient = null;
            }
        }

        public async Task<string> GetSecretAsync(string secretName)
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