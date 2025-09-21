using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace WileyWidget;

/// <summary>
/// Enhanced modal dialog showing comprehensive application information, features, and technical details.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LoadApplicationInfo();
    }

    /// <summary>
    /// Loads comprehensive application information and displays it in the UI.
    /// </summary>
    private void LoadApplicationInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Version information
        var version = GetVersionInfo(assembly);
        VersionText.Text = version;

        // Additional runtime information could be added here
        // Such as: database connection status, license information, etc.
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

    /// <summary>
    /// Handles the close button click event.
    /// </summary>
    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Handles hyperlink navigation (if we add any hyperlinks in the future).
    /// </summary>
    private void OnNavigateToUrl(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
