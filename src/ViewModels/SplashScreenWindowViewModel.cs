using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model backing the splash screen window visuals and progress indicators.
/// Provides strongly-typed properties consumed by the XAML bindings.
/// </summary>
public partial class SplashScreenWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "Wiley Widget";

    [ObservableProperty]
    private string subtitle = "Enterprise Business Solutions";

    [ObservableProperty]
    private bool isLoading = true;

    [ObservableProperty]
    private bool isIndeterminate = true;

    [ObservableProperty]
    private string statusText = "Starting Wiley Widget...";

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string versionInfo = "Version 1.0.0 • Build 2024.09.20";

    [ObservableProperty]
    private string systemInfo = BuildSystemInfo();

    [ObservableProperty]
    private string copyrightText = "© 2024 Wiley Widget Corporation. All rights reserved.";

    /// <summary>
    /// Refreshes the system information banner with the most recent runtime details.
    /// </summary>
    public void RefreshSystemInfo()
    {
        SystemInfo = BuildSystemInfo();
    }

    private static string BuildSystemInfo()
    {
        try
        {
            var osVersion = Environment.OSVersion;
            var framework = Environment.Version;
            var architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
            return $".NET {framework} • Windows {osVersion.Version.Major}.{osVersion.Version.Minor} • {architecture}";
        }
        catch
        {
            return ".NET 9.0 • Windows 11 • Enterprise Edition";
        }
    }
}
