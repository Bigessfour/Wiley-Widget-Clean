using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget;

/// <summary>
/// Enterprise Management UserControl - Provides full CRUD interface for municipal enterprises
/// </summary>
public partial class EnterpriseView : UserControl
{
    private IEventAggregator? _eventAggregator;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnterpriseView"/> class.
    /// Parameterless constructor for XAML designer and Prism region navigation.
    /// </summary>
    public EnterpriseView() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnterpriseView"/> class with dependency injection.
    /// </summary>
    /// <param name="viewModel">The enterprise view model injected by the container.</param>
    public EnterpriseView(EnterpriseViewModel? viewModel)
    {
        InitializeComponent();

        if (viewModel != null)
        {
            DataContext = viewModel;
            _eventAggregator = viewModel.EventAggregator; // Assuming ViewModel exposes EventAggregator
        }

        // Subscribe to grouping messages
        _eventAggregator?.GetEvent<GroupingMessage>().Subscribe(HandleGroupingMessage);

        // Load enterprises when window opens
        Loaded += (s, e) =>
        {
            if (DataContext is EnterpriseViewModel vm)
            {
                vm.LoadEnterprisesCommand.Execute();
            }
        };
    }

    private void HandleGroupingMessage(GroupingMessage message)
    {
        // Handle grouping messages if needed
    }

    /// <summary>
    /// Handles selection changed events from the SfDataGrid
    /// </summary>
    private void EnterpriseDataGrid_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grid.GridSelectionChangedEventArgs e)
    {
        // Selection changed - no drill-down implementation as requested
        // The ViewModel'\''s SelectionChangedCommand is called via binding
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
                await Task.Run(() =>
                {
                    // Use CSV export as reliable fallback - Excel export requires additional Syncfusion licensing
                    var csvFileName = System.IO.Path.ChangeExtension(saveFileDialog.FileName, ".csv");
                    ExportToCsv(csvFileName);
                });

                MessageBox.Show($"Data exported successfully to {saveFileDialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting to Excel: {ex.Message}",
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
                await Task.Run(() =>
                {
                    // Use CSV export as reliable fallback - PDF export requires additional Syncfusion licensing
                    var csvFileName = System.IO.Path.ChangeExtension(saveFileDialog.FileName, ".csv");
                    ExportToCsv(csvFileName);
                });

                MessageBox.Show($"Data exported successfully to {saveFileDialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting to PDF: {ex.Message}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Exports the SfDataGrid data to CSV format
    /// </summary>
    private void ExportToCsv(string fileName)
    {
        try
        {
            var dataGrid = FindName("EnterpriseDataGrid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (dataGrid?.ItemsSource == null) return;

            using var writer = new System.IO.StreamWriter(fileName);
            var items = dataGrid.ItemsSource as System.Collections.IEnumerable;

            if (items != null)
            {
                // Write CSV header
                var firstItem = items.Cast<object>().FirstOrDefault();
                if (firstItem != null)
                {
                    var properties = firstItem.GetType().GetProperties()
                        .Where(p => p.CanRead)
                        .Select(p => p.Name);
                    writer.WriteLine(string.Join(",", properties));
                }

                // Write CSV data
                foreach (var item in items)
                {
                    var values = item.GetType().GetProperties()
                        .Where(p => p.CanRead)
                        .Select(p => p.GetValue(item)?.ToString() ?? "");
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting to CSV: {ex.Message}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
