using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Lightweight secret store that persists values to the local application data directory.
/// Provides a drop-in replacement for the deprecated Azure Key Vault integration so that
/// existing workflows (saving credentials, testing connectivity) continue to function.
/// </summary>
public sealed class LocalSecretVaultService : IAzureKeyVaultService
{
    private readonly string _secretsPath;
    private readonly ILogger<LocalSecretVaultService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public LocalSecretVaultService(ILogger<LocalSecretVaultService> logger)
    {
        _logger = logger;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDirectory = Path.Combine(appData, "WileyWidget", "Secrets");
        Directory.CreateDirectory(baseDirectory);
        _secretsPath = Path.Combine(baseDirectory, "secrets.json");
    }

    public async Task<string?> GetSecretAsync(string secretName)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name is required", nameof(secretName));
        }

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            return secrets.TryGetValue(secretName, out var value) ? value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read secret {SecretName} from local vault", secretName);
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SetSecretAsync(string secretName, string value)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name is required", nameof(secretName));
        }

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            secrets[secretName] = value;
            await SaveSecretsAsync(secrets).ConfigureAwait(false);
            _logger.LogInformation("Secret {SecretName} stored in local vault", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist secret {SecretName} to local vault", secretName);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadSecretsAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local secret vault verification failed");
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadSecretsAsync()
    {
        if (!File.Exists(_secretsPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = new FileStream(_secretsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream).ConfigureAwait(false)
               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveSecretsAsync(Dictionary<string, string> secrets)
    {
        await using var stream = new FileStream(_secretsPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        await JsonSerializer.SerializeAsync(stream, secrets, options).ConfigureAwait(false);
    }
}
