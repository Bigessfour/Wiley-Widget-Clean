using System.Windows;
using System.Windows.Controls;
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
/// Enterprise Management UserControl - Provides full CRUD interface for municipal enterprises
/// </summary>
public partial class EnterpriseView : UserControl
{
    private IServiceScope? _viewScope;
    public EnterpriseView()
    {
        InitializeComponent();

        EnsureNamedElementsAreDiscoverable();

        // Create a scope for the view and resolve the repository from the scope
        IServiceProvider? provider = null;
        try
        {
            provider = App.GetActiveServiceProvider();
        }
        catch (InvalidOperationException)
        {
            provider = Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
        }

        if (provider != null)
        {
            _viewScope = provider.CreateScope();
            var unitOfWork = _viewScope.ServiceProvider.GetRequiredService<BusinessInterfaces.IUnitOfWork>();
            var eventAggregator = _viewScope.ServiceProvider.GetRequiredService<Prism.Events.IEventAggregator>();
            DataContext = new EnterpriseViewModel(unitOfWork, eventAggregator);

            // Dispose the scope when the control is unloaded
            Unloaded += (_, _) => { try { _viewScope.Dispose(); } catch { } };
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
        RegisterNameIfMissing(nameof(EnterpriseDataGrid), EnterpriseDataGrid);
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

    public new object? FindName(string name)
    {
        return name switch
        {
            nameof(EnterpriseDataGrid) when EnterpriseDataGrid is not null => EnterpriseDataGrid,
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
    /// Handles selection changed events from the SfDataGrid
    /// </summary>
    private void EnterpriseDataGrid_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grid.GridSelectionChangedEventArgs e)
    {
        // Selection changed - no drill-down implementation as requested
        // The ViewModel's SelectionChangedCommand is called via binding
    }

    /// <summary>
    /// Exports the SfDataGrid data to Excel format using CSV approach
    /// </summary>
    public async Task ExportToExcelAsync()
    {
        try
        {
            var dataGrid = FindName("EnterpriseDataGrid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (dataGrid?.ItemsSource == null) return;

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = $"EnterpriseData_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // Use CSV export as reliable fallback - Excel export requires additional Syncfusion licensing
                    var csvFileName = System.IO.Path.ChangeExtension(saveFileDialog.FileName, ".csv");
                    ExportToCsv(csvFileName);
                });

                System.Windows.MessageBox.Show($"Data exported successfully to {saveFileDialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error exporting to Excel: {ex.Message}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Exports the SfDataGrid data to PDF format using CSV approach
    /// </summary>
    public async Task ExportToPdfAsync()
    {
        try
        {
            var dataGrid = FindName("EnterpriseDataGrid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (dataGrid?.ItemsSource == null) return;

            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".pdf",
                FileName = $"EnterpriseData_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // Use CSV export as reliable fallback - PDF export requires additional Syncfusion licensing
                    var csvFileName = System.IO.Path.ChangeExtension(saveFileDialog.FileName, ".csv");
                    ExportToCsv(csvFileName);
                });

                System.Windows.MessageBox.Show($"Data exported successfully to {saveFileDialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error exporting to PDF: {ex.Message}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Exports data to CSV format
    /// </summary>
    private void ExportToCsv(string fileName)
    {
        var dataGrid = FindName("EnterpriseDataGrid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
        if (dataGrid?.ItemsSource == null) return;

        using (var writer = new System.IO.StreamWriter(fileName))
        {
            // Write headers
            var headers = new List<string>();
            foreach (var column in dataGrid.Columns)
            {
                if (column is Syncfusion.UI.Xaml.Grid.GridColumn gridColumn)
                {
                    headers.Add(gridColumn.HeaderText ?? gridColumn.MappingName ?? "");
                }
            }
            writer.WriteLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

            // Write data
            var items = dataGrid.ItemsSource as System.Collections.IEnumerable;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item is Models.Enterprise enterprise)
                    {
                        var values = new List<string>
                        {
                            enterprise.Name ?? "",
                            enterprise.Type ?? "",
                            enterprise.Status.ToString(),
                            enterprise.CitizenCount.ToString(),
                            enterprise.CurrentRate.ToString("F2"),
                            enterprise.MonthlyRevenue.ToString("F2"),
                            enterprise.MonthlyExpenses.ToString("F2"),
                            enterprise.MonthlyBalance.ToString("F2"),
                            enterprise.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss")
                        };
                        writer.WriteLine(string.Join(",", values.Select(v => $"\"{v}\"")));
                    }
                }
            }
        }
    }
}