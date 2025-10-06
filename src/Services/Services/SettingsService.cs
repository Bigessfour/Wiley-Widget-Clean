using System;
using System.IO;
using System.Text.Json;
using Serilog;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Persisted user-facing settings. Keep only values that must survive restarts; transient UI state stays in memory.
/// Nullable primitives used to distinguish 'not yet set' from legitimate 0 values (e.g., window geometry).
/// </summary>
// AppSettings moved to Models/AppSettings.cs

/// <summary>
/// Simple singleton service for loading/saving <see cref="AppSettings"/> as JSON in AppData.
/// Handles corruption by renaming the bad file and regenerating defaults.
/// </summary>
public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> _lazy = new(() => new SettingsService());
    public static SettingsService Instance => _lazy.Value;

    private string _root = string.Empty;
    private string _file = string.Empty;

    public AppSettings Current { get; private set; } = new();

    private SettingsService()
    {
        InitializePaths();
    }

    private void InitializePaths()
    {
        var overrideDir = Environment.GetEnvironmentVariable("WILEYWIDGET_SETTINGS_DIR");
        _root = string.IsNullOrWhiteSpace(overrideDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget")
            : overrideDir;
        _file = Path.Combine(_root, "settings.json");
    }

    public void ResetForTests()
    {
        // Don't call InitializePaths() as it would overwrite the test directory set via reflection
        Current = new AppSettings();
    }

    /// <summary>
    /// Loads settings from disk or creates a new file if absent. If deserialization fails, the corrupt
    /// file is renamed with a timestamp suffix to aid post-mortem diagnostics.
    /// </summary>
    public void Load()
    {
        var loadTimer = System.Diagnostics.Stopwatch.StartNew();
        Log.Information("⚙️ Starting settings initialization - File: {FilePath}", _file);

        try
        {
            if (!File.Exists(_file))
            {
                Log.Information("⚙️ Settings file not found, creating default settings - Path: {FilePath}", _file);
                Directory.CreateDirectory(_root);
                Save();
                loadTimer.Stop();
                Log.Information("✅ Settings initialization completed (new file created) in {ElapsedMs}ms", loadTimer.ElapsedMilliseconds);
                return;
            }

            Log.Debug("⚙️ Loading settings from existing file - Size: {FileSize} bytes", new FileInfo(_file).Length);
            var json = File.ReadAllText(_file);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);

            if (loaded != null)
            {
                Current = loaded;
                Log.Debug("⚙️ Settings deserialized successfully - Theme: {Theme}, Maximized: {Maximized}",
                    Current.Theme ?? "default", Current.WindowMaximized ?? false);

                // Migration: populate new Qbo* from legacy QuickBooks* if empty (first-run after upgrade).
                if (string.IsNullOrWhiteSpace(Current.QboAccessToken) && !string.IsNullOrWhiteSpace(Current.QuickBooksAccessToken))
                {
                    Log.Information("⚙️ Migrating QuickBooks tokens to new QBO format");
                    Current.QboAccessToken = Current.QuickBooksAccessToken;
                    Current.QboRefreshToken = Current.QuickBooksRefreshToken;
                    if (Current.QuickBooksTokenExpiresUtc.HasValue)
                        Current.QboTokenExpiry = Current.QuickBooksTokenExpiresUtc.Value;
                    Save(); // Save migrated settings
                    Log.Information("⚙️ Token migration completed and saved");
                }

                loadTimer.Stop();
                Log.Information("✅ Settings loaded successfully in {ElapsedMs}ms", loadTimer.ElapsedMilliseconds);
            }
            else
            {
                Log.Warning("⚠️ Settings deserialization returned null, using defaults");
                Current = new AppSettings();
                loadTimer.Stop();
                Log.Information("⚠️ Settings initialization completed (null deserialization) in {ElapsedMs}ms", loadTimer.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            loadTimer.Stop();
            Log.Error(ex, "❌ Settings loading failed after {ElapsedMs}ms - attempting recovery", loadTimer.ElapsedMilliseconds);

            // rename corrupted file and recreate
            try
            {
                if (File.Exists(_file))
                {
                    var bad = _file + ".bad_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Move(_file, bad);
                    Log.Warning("📁 Corrupted settings file renamed to: {BadFilePath}", bad);
                }
            }
            catch (Exception moveEx)
            {
                Log.Error(moveEx, "❌ Failed to rename corrupted settings file");
            }

            Current = new AppSettings();
            Save();
            Log.Information("🔄 Settings recovery completed - new default settings created");
        }
    }

    /// <summary>
    /// Writes current settings to disk (indented JSON). Failures are swallowed intentionally: user
    /// experience should not degrade due to IO issues—consider surfacing via telemetry later.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_root);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_file, json);
        }
        catch { }
    }
}
