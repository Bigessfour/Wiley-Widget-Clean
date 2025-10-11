using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Minimal contract used throughout the application for secret storage and retrieval.
/// Replaces the previous Azure Key Vault dependency with a local implementation while
/// preserving the existing call sites.
/// </summary>
public interface IAzureKeyVaultService
{
    Task<string?> GetSecretAsync(string secretName);
    Task SetSecretAsync(string secretName, string value);
    Task<bool> TestConnectionAsync();
}
