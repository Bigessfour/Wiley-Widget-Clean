using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
//using Syncfusion.Windows.Reports.Viewer;
using WileyWidget.ViewModels;

namespace WileyWidget;

/// <summary>
/// Interactive report surface powered by Syncfusion controls.
/// </summary>
public partial class ReportsView : Window
{
    private readonly IServiceScope _viewScope;
    private string? _cachedReportPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsView"/> class.
    /// </summary>
    public ReportsView()
    {
        InitializeComponent();

        var provider = App.ServiceProvider ?? Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
        if (provider is null)
        {
            throw new InvalidOperationException("ServiceProvider is not available for ReportsView");
        }

        _viewScope = provider.CreateScope();
        var viewModel = _viewScope.ServiceProvider.GetRequiredService<ReportsViewModel>();
        DataContext = viewModel;

        viewModel.DataLoaded += OnDataLoaded;
        viewModel.ExportCompleted += OnExportCompleted;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ReportsViewModel viewModel)
        {
            viewModel.DataLoaded -= OnDataLoaded;
            viewModel.ExportCompleted -= OnExportCompleted;
        }

        _viewScope.Dispose();
        base.OnClosed(e);
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
            MessageBox.Show(this,
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
