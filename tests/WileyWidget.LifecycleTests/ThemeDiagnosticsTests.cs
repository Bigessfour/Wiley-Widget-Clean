using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using Xunit;

namespace WileyWidget.LifecycleTests;

public sealed class ThemeDiagnosticsTests : LifecycleTestBase
{
    private static readonly string[] RequiredThemeAssemblies =
    {
        "Syncfusion.Themes.FluentLight.WPF.dll",
        "Syncfusion.Themes.FluentDark.WPF.dll",
        "Syncfusion.SfSkinManager.WPF.dll"
    };

    [Fact]
    public void ThemeAssemblies_ArePresentInOutputDirectory()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var missingAssemblies = new List<string>();

        foreach (var assemblyName in RequiredThemeAssemblies)
        {
            var assemblyPath = Path.Combine(baseDirectory, assemblyName);
            if (!File.Exists(assemblyPath))
            {
                missingAssemblies.Add(assemblyName);
            }
        }

        Assert.True(missingAssemblies.Count == 0,
            $"Missing required Syncfusion theme assemblies in output directory '{baseDirectory}': {string.Join(", ", missingAssemblies)}");
    }

    [Fact]
    public async Task Views_ApplyThemesWithoutErrors()
    {
        await RunOnDispatcherAsync(async () =>
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }

            ValidateThemeAssembliesLoaded();

            Assert.True(IsSyncfusionLicenseConfigured(),
                "Syncfusion license key is not configured. Set the SYNCFUSION_LICENSE_KEY environment variable or update appsettings to prevent runtime license dialogs.");

            var reportedErrors = new List<(Exception Exception, string? Context)>();
            void Handler(Exception ex, string? context) => reportedErrors.Add((ex, context));
            ErrorReportingService.Instance.ErrorReported += Handler;

            try
            {
                var enterpriseView = new EnterpriseView();
                ValidateViewTheme(enterpriseView, nameof(EnterpriseView));

                var utilityRepository = new UtilityCustomerRepository(DbContextFactory);
                var utilityViewModel = new UtilityCustomerViewModel(utilityRepository, CreateDispatcherHelper(), CreateLogger<UtilityCustomerViewModel>());
                await utilityViewModel.LoadCustomersAsync();
                var utilityView = new UtilityCustomerView(utilityViewModel);
                ValidateViewTheme(utilityView, nameof(UtilityCustomerView));

                var enterpriseRepository = new EnterpriseRepository(DbContextFactory);
                var budgetViewModel = new BudgetViewModel(enterpriseRepository, CreateDispatcherHelper(), CreateLogger<BudgetViewModel>());
                await budgetViewModel.RefreshBudgetDataAsync();
                var budgetView = new BudgetView(budgetViewModel);
                ValidateViewTheme(budgetView, nameof(BudgetView));

                Assert.True(reportedErrors.Count == 0,
                    $"Encountered theme-related errors when loading views: {string.Join(", ", reportedErrors.Select(e => e.Context ?? e.Exception.Message))}");
            }
            finally
            {
                ErrorReportingService.Instance.ErrorReported -= Handler;
            }
        });
    }

    private static void ValidateViewTheme(Window view, string viewName)
    {
        try
        {
            view.Width = 800;
            view.Height = 600;
            view.WindowStartupLocation = WindowStartupLocation.Manual;
            view.Left = -32000;
            view.Top = -32000;

            view.ApplyTemplate();
            view.UpdateLayout();

            var visualStyle = SfSkinManager.GetVisualStyle(view);
            Assert.True(visualStyle == VisualStyles.FluentDark || visualStyle == VisualStyles.FluentLight,
                $"Expected {viewName} to apply a Fluent theme, but observed style '{visualStyle}'.");

            var loadedAssemblyNames = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(a => a.GetName().Name)
                .ToArray();

            Assert.Contains("Syncfusion.Themes.FluentLight.WPF", loadedAssemblyNames);
            Assert.Contains("Syncfusion.Themes.FluentDark.WPF", loadedAssemblyNames);
        }
        finally
        {
            if (view.IsLoaded)
            {
                view.Close();
            }
        }
    }

    private static void ValidateThemeAssembliesLoaded()
    {
        var loadedAssemblyNames = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetName().Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var required in RequiredThemeAssemblies)
        {
            var name = Path.GetFileNameWithoutExtension(required);
            if (!loadedAssemblyNames.Contains(name))
            {
                try
                {
                    _ = System.Reflection.Assembly.Load(name);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed to load required theme assembly '{name}': {ex.Message}");
                }
            }
        }
    }

    private static bool IsSyncfusionLicenseConfigured()
    {
        var environmentKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(environmentKey) && !environmentKey.Contains("${"))
        {
            return true;
        }

        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(appSettingsPath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
                if (document.RootElement.TryGetProperty("Syncfusion", out var syncfusionSection) &&
                    syncfusionSection.TryGetProperty("LicenseKey", out var licenseElement))
                {
                    var key = licenseElement.GetString();
                    if (!string.IsNullOrWhiteSpace(key) && !key.StartsWith("${", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed JSON; treat as not configured.
            }
        }

        return false;
    }
}
