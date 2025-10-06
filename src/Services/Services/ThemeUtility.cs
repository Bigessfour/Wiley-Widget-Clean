using System;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Syncfusion.Themes.FluentLight.WPF;
using Syncfusion.Themes.FluentDark.WPF;
using Serilog;
using WileyWidget.Services;

namespace WileyWidget.Services;

/// <summary>
/// Shared utility class for theme management across the application.
/// Eliminates code duplication and provides consistent theme handling.
/// </summary>
public static class ThemeUtility
{
    /// <summary>
    /// Normalizes theme names to canonical Syncfusion theme names.
    /// Only FluentDark and FluentLight themes are authorized.
    /// </summary>
    public static VisualStyles NormalizeTheme(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return VisualStyles.FluentDark;
        raw = raw.Replace(" ", string.Empty); // allow "Fluent Dark" legacy
        return raw switch
        {
            "FluentDark" => VisualStyles.FluentDark,
            "FluentLight" => VisualStyles.FluentLight,
            _ => VisualStyles.FluentDark // default to FluentDark
        };
    }

    /// <summary>
    /// Attempts to apply a Syncfusion theme with fallback to FluentLight if requested theme fails.
    /// Sets global application styling to ensure consistent theme inheritance across all controls.
    /// </summary>
    public static void TryApplyTheme(System.Windows.Window window, string themeName)
    {
        try
        {
            var canonical = NormalizeTheme(themeName);
            
            // Enable global application styling for consistent theme inheritance
            // This ensures all Syncfusion controls inherit the theme automatically
            SfSkinManager.ApplyStylesOnApplication = true;
            
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
            SfSkinManager.SetVisualStyle(window, canonical);
#pragma warning restore CA2000 // Dispose objects before losing scope
            Log.Information("Successfully applied theme: {Theme} to window {WindowType} with global application styling enabled",
                           canonical, window.GetType().Name);
        }
        catch (Exception ex)
        {
            ErrorReportingService.Instance.ReportError(ex, $"Theme_Apply_{themeName}", showToUser: false);

            if (themeName != "FluentLight")
            {
                // Fallback to FluentLight
                try
                {
                    // Ensure global styling is still enabled for fallback
                    SfSkinManager.ApplyStylesOnApplication = true;
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                    SfSkinManager.SetVisualStyle(window, VisualStyles.FluentLight);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    Log.Warning("Applied fallback theme 'FluentLight' after failing to apply '{ThemeName}' to window {WindowType}",
                               themeName, window.GetType().Name);
                }
                catch (Exception fallbackEx)
                {
                    ErrorReportingService.Instance.ReportError(fallbackEx, "Theme_Fallback_Failed", showToUser: true);
                }
            }
        }
    }

    /// <summary>
    /// Applies the current theme from settings to a window.
    /// </summary>
    public static void ApplyCurrentTheme(System.Windows.Window window)
    {
        var currentTheme = SettingsService.Instance.Current.Theme;
        TryApplyTheme(window, currentTheme);
    }
}