using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.SfSkinManager;
using WileyWidget;

namespace WileyWidget.Services;

/// <summary>
/// ThemeManager - Centralized service for managing Syncfusion themes with persistence.
/// Provides consistent theming for mayoral dashboards across the application.
/// </summary>
public class ThemeManager : IThemeManager
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ThemeManager> _logger;

    /// <summary>
    /// Available Syncfusion themes
    /// </summary>
    public static readonly string[] AvailableThemes = new[]
    {
        "FluentDark",
        "FluentLight"
    };

    /// <summary>
    /// Current active theme
    /// </summary>
    public string CurrentTheme => _settingsService.Current.Theme ?? "FluentDark";

    /// <summary>
    /// Event raised when theme changes
    /// </summary>
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public ThemeManager()
        : this(new SettingsService(), NullLogger<ThemeManager>.Instance)
    {
    }

    public ThemeManager(ISettingsService settingsService, ILogger<ThemeManager>? logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? NullLogger<ThemeManager>.Instance;
    }

    /// <summary>
    /// Apply a theme to the application
    /// </summary>
    public void ApplyTheme(string themeName, bool freezeResources = false)
    {
        // Normalize the theme name to handle legacy themes
        themeName = ThemeUtility.NormalizeTheme(themeName);

        if (string.IsNullOrEmpty(themeName) || !AvailableThemes.Contains(themeName))
        {
            _logger.LogWarning("Invalid theme name: {ThemeName}", themeName);
            return;
        }

        try
        {
            var oldTheme = CurrentTheme;

            // ✅ CRITICAL: SfSkinManager handles ALL Syncfusion theming
            // This single line applies the theme to all Syncfusion controls application-wide
            SfSkinManager.ApplicationTheme = new Theme(themeName);

            // Update settings for persistence
            _settingsService.Current.Theme = themeName;
            _settingsService.Save();

            // ✅ REMOVED: Redundant ResourceDictionary merging
            // SfSkinManager handles all Syncfusion control theming automatically
            // Custom ResourceDictionary resources are not needed for Syncfusion controls

            // Raise theme changed event
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, themeName));

            _logger.LogInformation("🎨 Theme changed from {OldTheme} to {NewTheme}", oldTheme, themeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme: {ThemeName}", themeName);
        }
    }

    /// <summary>
    /// Apply theme to a specific control
    /// </summary>
    public void ApplyThemeToControl(FrameworkElement control, string themeName)
    {
        if (control == null || string.IsNullOrEmpty(themeName)) return;

        try
        {
            var visualStyle = ThemeUtility.ToVisualStyle(themeName);
            SfSkinManager.SetVisualStyle(control, visualStyle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply theme {ThemeName} to control {ControlType}",
                themeName, control.GetType().Name);
        }
    }

    /// <summary>
    /// Check if current theme is dark
    /// </summary>
    public bool IsDarkTheme => CurrentTheme.Contains("Dark");

    /// <summary>
    /// Reset to default theme
    /// </summary>
    public void ResetToDefault()
    {
        ApplyTheme("FluentDark");
    }
}

/// <summary>
/// Event args for theme change notifications
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public string OldTheme { get; }
    public string NewTheme { get; }

    public ThemeChangedEventArgs(string oldTheme, string newTheme)
    {
        OldTheme = oldTheme;
        NewTheme = newTheme;
    }
}