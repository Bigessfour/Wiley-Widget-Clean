using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for the About window, providing application information and version details.
/// </summary>
public partial class AboutViewModel : AsyncViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the AboutViewModel class.
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    public AboutViewModel(IDispatcherHelper dispatcherHelper, ILogger<AboutViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        LoadApplicationInfo();
    }

    /// <summary>
    /// Action to close the about window.
    /// </summary>
    public Action? CloseAction { get; set; }

    /// <summary>
    /// Gets the application version information.
    /// </summary>
    [ObservableProperty]
    private string version = "Loading...";

    /// <summary>
    /// Gets the application name.
    /// </summary>
    [ObservableProperty]
    private string applicationName = "Wiley Widget";

    /// <summary>
    /// Gets the application description.
    /// </summary>
    [ObservableProperty]
    private string applicationDescription = "AI Enhanced Utility Rate Study Program";

    /// <summary>
    /// Command to close the about window.
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
        Logger.LogInformation("About window close requested");
    }

    /// <summary>
    /// Command to open a URL in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Logger.LogInformation("Opened URL: {Url}", url);
            }
            else
            {
                Logger.LogWarning("URL opening is only supported on Windows platforms");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to open URL: {Url}", url);
        }
    }

    /// <summary>
    /// Loads comprehensive application information.
    /// </summary>
    private void LoadApplicationInfo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            Version = GetVersionInfo(assembly);
            Logger.LogInformation("Loaded application info: {Version}", Version);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load application info");
            Version = "Version: Unknown";
        }
    }

    /// <summary>
    /// Retrieves detailed version information from the assembly.
    /// </summary>
    private string GetVersionInfo(Assembly assembly)
    {
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var productVersion = assembly.GetCustomAttribute<AssemblyVersionAttribute>()?.Version;

        if (!string.IsNullOrEmpty(infoVersion))
        {
            return $"Version {infoVersion}";
        }
        else if (!string.IsNullOrEmpty(fileVersion))
        {
            return $"Version {fileVersion}";
        }
        else if (!string.IsNullOrEmpty(productVersion))
        {
            return $"Version {productVersion}";
        }
        else
        {
            return "Version: Unknown";
        }
    }
}