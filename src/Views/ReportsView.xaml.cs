using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Prism.Ioc;
using Microsoft.Extensions.DependencyInjection;
//using Syncfusion.Windows.Reports.Viewer;
using WileyWidget.ViewModels;

namespace WileyWidget;

/// <summary>
/// Interactive report surface powered by Syncfusion controls.
/// </summary>
public partial class ReportsView : UserControl
{
    private string? _cachedReportPath;

    /// <summary>
    /// Prism-aware constructor - prefer this so the container can inject dependencies.
    /// </summary>
    public ReportsView(IContainerProvider containerProvider)
    {
        InitializeComponent();

        // DataContext will be auto-wired by Prism ViewModelLocator
        if (DataContext is ReportsViewModel vm)
        {
            vm.DataLoaded += OnDataLoaded;
            vm.ExportCompleted += OnExportCompleted;
        }
    }

    /// <summary>
    /// Parameterless constructor remains for XAML designer compatibility and for
    /// any code paths that instantiate the view without DI. It attempts to use
    /// the application container as a fallback but does not throw if unavailable.
    /// </summary>
    public ReportsView()
    {
        InitializeComponent();

        // DataContext will be auto-wired by Prism ViewModelLocator
        if (DataContext is ReportsViewModel vm)
        {
            vm.DataLoaded += OnDataLoaded;
            vm.ExportCompleted += OnExportCompleted;
        }
    }

    //private void OnReportViewerLoaded(object sender, RoutedEventArgs e)
    //{
    //    RefreshReportViewer();
    //}

    private void OnDataLoaded(object? sender, ReportsViewModel.ReportDataEventArgs e)
    {
        //Dispatcher.Invoke(RefreshReportViewer);
    }

    private void OnExportCompleted(object? sender, ReportsViewModel.ReportExportCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                $"Report exported to {e.FilePath}",
                $"Export ({e.Format.ToUpperInvariant()})",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    //private void RefreshReportViewer()
    //{
    //    if (DataContext is not ReportsViewModel viewModel)
    //    {
    //        return;
    //    }

    //    if (ReportViewer is null)
    //    {
    //        return;
    //    }

    //    ReportViewer.Reset();
    //    ReportViewer.ProcessingMode = ProcessingMode.Local;
    //    ReportViewer.ReportPath = EnsureReportDefinition();
    //    ReportViewer.DataSources.Clear();
    //    ReportViewer.DataSources.Add(new ReportDataSource
    //    {
    //        Name = "ReportItems",
    //        Value = viewModel.ReportItems
    //    });
    //    ReportViewer.RefreshReport();
    //}

    private string EnsureReportDefinition()
    {
        if (!string.IsNullOrEmpty(_cachedReportPath) && File.Exists(_cachedReportPath))
        {
            return _cachedReportPath;
        }

        var resourceUri = new Uri("pack://application:,,,/src/Reports/EnterpriseSummary.rdl", UriKind.Absolute);
        var resourceStream = Application.GetResourceStream(resourceUri)?.Stream;
        if (resourceStream is null)
        {
            throw new InvalidOperationException("Unable to locate EnterpriseSummary.rdl resource.");
        }

        var directory = Path.Combine(Path.GetTempPath(), "WileyWidget", "Reports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "EnterpriseSummary.rdl");

        using (resourceStream)
        using (var fileStream = File.Create(path))
        {
            resourceStream.CopyTo(fileStream);
        }

        _cachedReportPath = path;
        return path;
    }
}
