using Microsoft.Extensions.Hosting;
using WileyWidget.Configuration;

namespace WileyWidget;

/// <summary>
/// Backwards-compatible extension wrapper that forwards to the consolidated configuration in <see cref="Configuration.WpfHostingExtensions"/>.
/// </summary>
public static class WpfApplicationHostExtensions
{
    public static IHostApplicationBuilder ConfigureWpfApplication(this IHostApplicationBuilder builder)
    {
        return Configuration.WpfHostingExtensions.ConfigureWpfApplication(builder);
    }
}