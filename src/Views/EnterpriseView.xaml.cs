using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Serilog;
using BusinessInterfaces = WileyWidget.Business.Interfaces;

namespace WileyWidget;

/// <summary>
/// Enterprise Management Window - Provides full CRUD interface for municipal enterprises
/// </summary>
public partial class EnterpriseView : Window
{
    private readonly IServiceScope? _viewScope;

    public EnterpriseView()
    {
        InitializeComponent();

        EnsureNamedElementsAreDiscoverable();

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        // Create a scope for the view and resolve the repository from the scope
        var provider = App.ServiceProvider ?? Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
        if (provider != null)
        {
            _viewScope = provider.CreateScope();
            var enterpriseRepository = _viewScope.ServiceProvider.GetRequiredService<BusinessInterfaces.IEnterpriseRepository>();
            DataContext = new EnterpriseViewModel(enterpriseRepository);

            // Dispose the scope when the window is closed
            this.Closed += (_, _) => { try { _viewScope.Dispose(); } catch { } };
        }
        else
        {
            // For testing purposes, allow view to load without ViewModel
            _viewScope = null;
            DataContext = null;
        }

        // Load enterprises when window opens
        Loaded += async (s, e) =>
        {
            if (DataContext is EnterpriseViewModel vm)
            {
                await vm.LoadEnterprisesAsync();
            }
        };
    }

    private T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T frameworkElement && frameworkElement.Name == name)
            {
                return frameworkElement;
            }

            var result = FindVisualChildByName<T>(child, name);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void EnsureNamedElementsAreDiscoverable()
    {
        RegisterNameIfMissing(nameof(EnterpriseTreeGrid), EnterpriseTreeGrid);
        RegisterNameIfMissing(nameof(SearchTextBox), SearchTextBox);
        RegisterNameIfMissing(nameof(StatusFilterCombo), StatusFilterCombo);
        RegisterNameIfMissing(nameof(dataPager), dataPager);
    }

    private void RegisterNameIfMissing(string name, FrameworkElement? element)
    {
        if (element is null || base.FindName(name) is not null)
        {
            return;
        }

        if (NameScope.GetNameScope(this) is not NameScope scope)
        {
            scope = new NameScope();
            NameScope.SetNameScope(this, scope);
        }

        if (scope.FindName(name) is null)
        {
            scope.RegisterName(name, element);
        }
    }

    /// <summary>
    /// Show the Enterprise Management window
    /// </summary>
    public static void ShowEnterpriseWindow()
    {
        var window = new EnterpriseView();
        window.Show();
    }

    /// <summary>
    /// Show the Enterprise Management window as dialog
    /// </summary>
    public static bool? ShowEnterpriseDialog()
    {
        var window = new EnterpriseView();
        return window.ShowDialog();
    }

    public new object? FindName(string name)
    {
        return name switch
        {
            nameof(EnterpriseTreeGrid) when EnterpriseTreeGrid is not null => EnterpriseTreeGrid,
            nameof(SearchTextBox) when SearchTextBox is not null => SearchTextBox,
            nameof(StatusFilterCombo) when StatusFilterCombo is not null => StatusFilterCombo,
            nameof(dataPager) when dataPager is not null => dataPager,
            _ => base.FindName(name) ?? TryResolveField(name) ?? TryFindInVisualTree(name)
        };
    }

    private object? TryResolveField(string name)
    {
        var field = GetType().GetField(
            name,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.IgnoreCase);

        return field?.GetValue(this);
    }

    private FrameworkElement? TryFindInVisualTree(string name)
    {
        return Content is DependencyObject dependencyObject
            ? FindVisualChildByName<FrameworkElement>(dependencyObject, name)
            : null;
    }

    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails.
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        Services.ThemeUtility.TryApplyTheme(this, themeName);
    }
}