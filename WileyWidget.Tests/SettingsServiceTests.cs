using System;
using System.IO;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Tests for the SettingsService singleton, file operations, and migration logic
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly SettingsService _testInstance;

    public SettingsServiceTests()
    {
        // Create a temporary directory for testing
        _testDirectory = Path.Combine(Path.GetTempPath(), "WileyWidgetTest");
        Directory.CreateDirectory(_testDirectory);

        // Create a test instance directly instead of using the singleton
        _testInstance = new SettingsService();

        // Use reflection to set the test directory on our test instance
        typeof(SettingsService)
            .GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(_testInstance, _testDirectory);

        // Also set the file path to point to the test directory
        var fileField = typeof(SettingsService)
            .GetField("_file", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fileField != null)
        {
            fileField.SetValue(_testInstance, Path.Combine(_testDirectory, "settings.json"));
        }
    }

    [Fact]
    public void SettingsService_Current_IsNotNull()
    {
        // Act
        var current = _testInstance.Current;

        // Assert
        Assert.NotNull(current);
        Assert.IsType<AppSettings>(current);
    }

    [Fact]
    public void SettingsService_ResetForTests_Works()
    {
        // Arrange - Modify current settings
        _testInstance.Current.Theme = "ModifiedTheme";
        _testInstance.Current.WindowWidth = 999;

        // Act - Reset for tests
        _testInstance.ResetForTests();

        // Assert - Should be back to defaults
        Assert.Equal("FluentDark", _testInstance.Current.Theme);
        Assert.Null(_testInstance.Current.WindowWidth);
    }

    [Fact]
    public void SettingsService_Save_CreatesFile()
    {
        // Arrange
        _testInstance.ResetForTests();
        _testInstance.Current.Theme = "Dark";
        _testInstance.Current.WindowWidth = 800;
        _testInstance.Current.WindowHeight = 600;

        // Get the expected file path
        var filePath = Path.Combine(_testDirectory, "settings.json");

        // Act
        _testInstance.Save();

        // Assert
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void SettingsService_Load_ReadsFileCorrectly()
    {
        // Arrange - Create a test settings file
        var testSettings = new AppSettings
        {
            Theme = "Light",
            WindowWidth = 1024,
            WindowHeight = 768
        };

        var filePath = Path.Combine(_testDirectory, "settings.json");
        var json = System.Text.Json.JsonSerializer.Serialize(testSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        // Act - Reset and load
        _testInstance.ResetForTests();
        _testInstance.Load();

        // Assert
        Assert.Equal("Light", _testInstance.Current.Theme);
        Assert.Equal(1024, _testInstance.Current.WindowWidth);
        Assert.Equal(768, _testInstance.Current.WindowHeight);
    }

    [Fact]
    public void SettingsService_Load_HandlesCorruptFile()
    {
        // Arrange - Create a corrupt JSON file
        var filePath = Path.Combine(_testDirectory, "settings.json");
        File.WriteAllText(filePath, "{ invalid json content }");

        // Act - Reset and load (should handle corruption gracefully)
        _testInstance.ResetForTests();
        _testInstance.Load();

        // Assert - Should have default settings, not crash
        Assert.NotNull(_testInstance.Current);
        Assert.Equal("FluentDark", _testInstance.Current.Theme);
    }

    [Fact]
    public void SettingsService_Load_CreatesBackupOfCorruptFile()
    {
        // Arrange - Create a corrupt JSON file
        var filePath = Path.Combine(_testDirectory, "settings.json");
        File.WriteAllText(filePath, "{ invalid json content }");

        // Act - Load (should create backup)
        _testInstance.ResetForTests();
        _testInstance.Load();

        // Assert - Should have created a backup file
        var backupFiles = Directory.GetFiles(_testDirectory, "settings.json.bad_*");
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public void SettingsService_Migration_QuickBooksToQbo_Works()
    {
        // Arrange - Create settings with old QuickBooks properties
        var testSettings = new AppSettings
        {
            QuickBooksAccessToken = "old_token",
            QuickBooksRefreshToken = "old_refresh",
            QuickBooksTokenExpiresUtc = DateTime.UtcNow.AddHours(1)
        };

        var filePath = Path.Combine(_testDirectory, "settings.json");
        var json = System.Text.Json.JsonSerializer.Serialize(testSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        // Act - Load (should migrate old properties)
        _testInstance.ResetForTests();
        _testInstance.Load();

        // Assert - Should have migrated to new QBO properties
        Assert.Equal("old_token", _testInstance.Current.QboAccessToken);
        Assert.Equal("old_refresh", _testInstance.Current.QboRefreshToken);
        Assert.True(_testInstance.Current.QboTokenExpiry > DateTime.UtcNow);
    }

    [Fact]
    public void SettingsService_Load_HandlesMissingFile()
    {
        // Arrange - Ensure no settings file exists
        var filePath = Path.Combine(_testDirectory, "settings.json");
        if (File.Exists(filePath))
            File.Delete(filePath);

        // Act - Load from non-existent file
        _testInstance.ResetForTests();
        _testInstance.Load();

        // Assert - Should have default settings
        Assert.NotNull(_testInstance.Current);
        Assert.Equal("FluentDark", _testInstance.Current.Theme);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
