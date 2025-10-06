using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Diagnostics;

namespace WileyWidget.Services.Caching;

#pragma warning disable CS8600, CS8602 // Suppress nullable reference warnings for caching compatibility

/// <summary>
/// Enterprise-grade caching service for startup performance optimization.
/// Implements multiple caching strategies: in-memory, disk-based, and warm-up caching.
/// </summary>
public class StartupCacheService : IDisposable
{
    private readonly ILogger<StartupCacheService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ConcurrentDictionary<string, object> _warmupCache;
    private readonly string _diskCachePath;
    private bool _disposed = false;

    // Cache options optimized for startup scenarios
    private readonly MemoryCacheEntryOptions _shortTermPolicy = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        Priority = CacheItemPriority.High,
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };

    private readonly MemoryCacheEntryOptions _startupPolicy = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
        Priority = CacheItemPriority.NeverRemove
    };

    public StartupCacheService(ILogger<StartupCacheService> logger, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _warmupCache = new ConcurrentDictionary<string, object>();
        
        // Initialize disk cache in temp directory for cross-session persistence
        _diskCachePath = Path.Combine(Path.GetTempPath(), "WileyWidget", "StartupCache");
        Directory.CreateDirectory(_diskCachePath);
        
        _logger.LogInformation("StartupCacheService initialized with disk cache at: {CachePath}", _diskCachePath);
    }

    #region Assembly Loading Cache

    /// <summary>
    /// Pre-warms assembly metadata cache to reduce JIT compilation overhead
    /// </summary>
    public async Task PrewarmAssemblyMetadataAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await Task.Run(() =>
            {
                // Cache critical assembly metadata that's frequently accessed during startup
                var assemblies = new[]
                {
                    typeof(System.Windows.Application).Assembly,           // PresentationFramework
                    typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly,  // DI
                    typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly,  // EF Core
                    typeof(Syncfusion.Licensing.SyncfusionLicenseProvider).Assembly,  // Syncfusion
                    typeof(Serilog.Log).Assembly,                         // Serilog
                    typeof(WileyWidget.App).Assembly                      // Application
                };

                foreach (var assembly in assemblies)
                {
                    var key = $"assembly_metadata_{assembly.GetName().Name}";
                    var metadata = new
                    {
                        Name = assembly.GetName().Name,
                        Version = assembly.GetName().Version?.ToString(),
                        Location = assembly.Location,
                        Types = assembly.GetTypes().Length,
                        CachedAt = DateTime.UtcNow
                    };
                    
                    _memoryCache.Set(key, metadata, _startupPolicy);
                }
            });
            
            _logger.LogInformation("Assembly metadata prewarmed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prewarm assembly metadata after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    #endregion

    #region Configuration Cache

    /// <summary>
    /// Caches configuration values that are expensive to resolve (Azure Key Vault, etc.)
    /// </summary>
    public async Task<T> GetOrSetConfigurationAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : notnull
    {
        var cacheKey = $"config_{key}";
        
        // Try memory cache first
        if (_memoryCache.TryGetValue(cacheKey, out T cachedValue) && cachedValue != null)
        {
            _logger.LogDebug("Configuration cache hit for key: {Key}", key);
            return cachedValue;
        }

        // Try disk cache for persistence across app restarts
        var diskValue = await GetFromDiskCacheAsync<T>(cacheKey);
        if (diskValue != null)
        {
            _memoryCache.Set(cacheKey, diskValue, _shortTermPolicy);
            _logger.LogDebug("Configuration loaded from disk cache for key: {Key}", key);
            return diskValue;
        }

        // Cache miss - execute factory
        _logger.LogDebug("Configuration cache miss for key: {Key}, executing factory", key);
        var value = await factory() ?? throw new InvalidOperationException($"Factory for configuration key '{key}' returned null");
        
        var options = expiration.HasValue 
            ? new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration.Value }
            : _shortTermPolicy;
            
        _memoryCache.Set(cacheKey, value, options);
        await SetToDiskCacheAsync(cacheKey, value);

        return value;
    }

    #endregion

    #region Database Schema Cache

    /// <summary>
    /// Caches database schema information to avoid repeated queries during startup
    /// </summary>
    public async Task<bool> IsDatabaseSchemaValidAsync(string connectionStringHash, Func<Task<bool>> validator)
    {
        var cacheKey = $"db_schema_valid_{connectionStringHash}";
        
        if (_memoryCache.TryGetValue(cacheKey, out bool isValid))
        {
            _logger.LogDebug("Database schema validation cache hit");
            return isValid;
        }

        var result = await validator();
        
        // Cache valid schemas for longer, invalid ones for shorter duration
        var options = result ? _startupPolicy : _shortTermPolicy;
        _memoryCache.Set(cacheKey, result, options);
        
        _logger.LogInformation("Database schema validation cached: {IsValid}", result);
        return result;
    }

    #endregion

    #region Font Cache Optimization

    /// <summary>
    /// Pre-loads font information for Syncfusion controls to improve rendering performance
    /// </summary>
    public async Task PrewarmFontCacheAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await Task.Run(() =>
            {
                // Cache commonly used fonts for WPF/Syncfusion controls
                var fontFamilies = new[]
                {
                    "Segoe UI", "Segoe UI Semibold", "Segoe UI Symbol",
                    "Tahoma", "Arial", "Calibri", "Consolas"
                };

                foreach (var fontFamily in fontFamilies)
                {
                    try
                    {
                        var key = $"font_{fontFamily}";
                        var fontInfo = new System.Windows.Media.FontFamily(fontFamily);
                        _memoryCache.Set(key, fontInfo, _startupPolicy);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to cache font: {FontFamily}", fontFamily);
                    }
                }
            });
            
            _logger.LogInformation("Font cache prewarmed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prewarm font cache after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    #endregion

    #region Health Check Result Cache

    /// <summary>
    /// Caches health check results to avoid redundant checks during startup
    /// </summary>
    public bool TryGetCachedHealthResult(string serviceName, out DateTime lastCheck, out bool isHealthy)
    {
        var cacheKey = $"health_{serviceName}";
        
        if (_memoryCache.TryGetValue(cacheKey, out (DateTime timestamp, bool healthy) result))
        {
            lastCheck = result.timestamp;
            isHealthy = result.healthy;
            
            // Only use cached result if it's recent (within 5 minutes)
            if (DateTime.UtcNow - result.timestamp < TimeSpan.FromMinutes(5))
            {
                _logger.LogDebug("Health check cache hit for service: {ServiceName}", serviceName);
                return true;
            }
        }

        lastCheck = default;
        isHealthy = false;
        return false;
    }

    public void CacheHealthResult(string serviceName, bool isHealthy)
    {
        var cacheKey = $"health_{serviceName}";
        var result = (DateTime.UtcNow, isHealthy);
        
        var options = isHealthy ? _shortTermPolicy : new MemoryCacheEntryOptions 
        { 
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) // Cache failures for shorter time
        };
        
        _memoryCache.Set(cacheKey, result, options);
        _logger.LogDebug("Health check result cached for service: {ServiceName}, Healthy: {IsHealthy}", serviceName, isHealthy);
    }

    #endregion

    #region Warmup Cache Operations

    /// <summary>
    /// Performs comprehensive cache warmup operations for optimal startup performance
    /// </summary>
    public async Task PerformWarmupAsync()
    {
        var tasks = new[]
        {
            PrewarmAssemblyMetadataAsync(),
            PrewarmFontCacheAsync(),
            PrewarmCommonResourcesAsync()
        };

        await Task.WhenAll(tasks);
        _logger.LogInformation("Cache warmup completed");
    }

    private async Task PrewarmCommonResourcesAsync()
    {
        await Task.Run(() =>
        {
            // Cache common system information
            _warmupCache.TryAdd("system_info", new
            {
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                OSVersion = Environment.OSVersion.ToString(),
                WorkingSet = Environment.WorkingSet,
                CachedAt = DateTime.UtcNow
            });

            // Cache common directory paths
            _warmupCache.TryAdd("app_paths", new
            {
                ApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                LocalApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                TempPath = Path.GetTempPath(),
                CurrentDirectory = Environment.CurrentDirectory
            });
        });
    }

    #endregion

    #region Disk Cache Implementation

    private async Task<T?> GetFromDiskCacheAsync<T>(string key)
    {
        try
        {
            var filePath = Path.Combine(_diskCachePath, $"{key}.json");
            if (!File.Exists(filePath)) return default(T);

            var fileInfo = new FileInfo(filePath);
            if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > TimeSpan.FromHours(24))
            {
                File.Delete(filePath); // Clean up old cache files
                return default(T);
            }

            var json = await File.ReadAllTextAsync(filePath);
            var result = JsonSerializer.Deserialize<T>(json);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read from disk cache for key: {Key}", key);
            return default(T);
        }
    }

    private async Task SetToDiskCacheAsync<T>(string key, T value)
    {
        try
        {
            var filePath = Path.Combine(_diskCachePath, $"{key}.json");
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to write to disk cache for key: {Key}", key);
        }
    }

    #endregion

    #region Cache Statistics

    public void LogCacheStatistics()
    {
        try
        {
            var memoryPressure = GC.GetTotalMemory(false);
            // Note: IMemoryCache doesn't expose Count, using warmup cache size as approximation
            var warmupCacheSize = _warmupCache.Count;

            _logger.LogInformation("Cache Statistics - Memory: {MemoryMB}MB, Warmup: {WarmupSize}", 
                memoryPressure / 1024 / 1024, warmupCacheSize);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to log cache statistics");
        }
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _memoryCache?.Dispose();
                _warmupCache?.Clear();
            }
            _disposed = true;
        }
    }
}