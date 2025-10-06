using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for Azure Key Vault operations
    /// </summary>
    public interface IAzureKeyVaultService
    {
        Task<string?> GetSecretAsync(string secretName);
        Task SetSecretAsync(string secretName, string value);
        Task<bool> TestConnectionAsync();
    }
}