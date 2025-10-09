using System;
using System.Windows;

namespace WileyWidget.Services;

/// <summary>
/// Interface for theme management service
/// </summary>
public interface IThemeManager
{
    /// <summary>
    /// Current active theme
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Check if current theme is dark
    /// </summary>
    bool IsDarkTheme { get; }

    /// <summary>
    /// Event raised when theme changes
    /// </summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Apply a theme to the application
    /// </summary>
    void ApplyTheme(string themeName, bool freezeResources = false);

    /// <summary>
    /// Apply theme to a specific control
    /// </summary>
    void ApplyThemeToControl(FrameworkElement control, string themeName);

    /// <summary>
    /// Reset to default theme
    /// </summary>
    void ResetToDefault();
}