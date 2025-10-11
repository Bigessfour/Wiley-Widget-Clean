using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Minimal contract used throughout the application for secret storage and retrieval.
/// Provides a local secret vault abstraction now that Azure services have been removed.
/// </summary>
public interface ISecretVaultService
{
    Task<string?> GetSecretAsync(string secretName);
    Task SetSecretAsync(string secretName, string value);
    Task<bool> TestConnectionAsync();
}
