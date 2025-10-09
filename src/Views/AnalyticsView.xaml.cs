using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.UI.Xaml.Charts;
//using Syncfusion.UI.Xaml.Gauges;
//using WileyWidget.Services;
using WileyWidget.ViewModels;

namespace WileyWidget;

/// <summary>
/// High-impact analytics dashboard wiring Syncfusion visuals to Grok output.
/// </summary>
public partial class AnalyticsView : Window
{
    private readonly IServiceScope _viewScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsView"/> class.
    /// </summary>
    public AnalyticsView()
    {
        InitializeComponent();

        var provider = App.ServiceProvider ?? Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
        if (provider is null)
        {
            throw new InvalidOperationException("ServiceProvider is not available for AnalyticsView");
        }

        _viewScope = provider.CreateScope();
        var viewModel = _viewScope.ServiceProvider.GetRequiredService<AnalyticsViewModel>();
        DataContext = viewModel;

        viewModel.DataLoaded += OnAnalyticsLoaded!;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is AnalyticsViewModel viewModel)
        {
            viewModel.DataLoaded -= OnAnalyticsLoaded!;
        }

        _viewScope.Dispose();
        base.OnClosed(e);
    }

    private void OnAnalyticsLoaded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Analytics data loaded, UI can be refreshed if needed
            // Chart rendering is typically done in the view model or via data binding
        });
    }

    private void RenderChart(AnalyticsViewModel.AnalyticsDataEventArgs e)
    {
        AnalyticsChart.Series.Clear();

        var enterpriseNames = e.Report.Enterprises.Select(metric => metric.Name).ToList();
        foreach (WileyWidget.Services.ChartSeries series in e.Analytics.ChartData)
        {
            var points = CreateChartPoints(series, enterpriseNames).ToList();
            var lineSeries = new LineSeries
            {
                ItemsSource = points,
                XBindingPath = nameof(ChartPoint.Label),
                YBindingPath = nameof(ChartPoint.Value),
                Label = series.Name,
                EnableAnimation = true,
                ShowTooltip = true
            };

            AnalyticsChart.Series.Add(lineSeries);
        }
    }

    private static IEnumerable<ChartPoint> CreateChartPoints(WileyWidget.Services.ChartSeries series, IReadOnlyList<string> labels)
    {
        var dataPoints = series.DataPoints;
        for (var index = 0; index < labels.Count; index++)
        {
            var value = index < dataPoints.Count ? dataPoints[index].YValue : 0.0;
            yield return new ChartPoint(labels[index], value);
        }
    }

    //private void RenderGauge(AnalyticsViewModel.AnalyticsDataEventArgs e)
    //{
    //    if (InsightOdometer.Scales.Count == 0)
    //    {
    //        return;
    //    }

    //    var scale = InsightOdometer.Scales[0];
    //    scale.Pointers.Clear();

    //    var metrics = e.Analytics.GaugeData;
    //    if (metrics.Count == 0)
    //    {
    //        scale.Pointers.Add(new NeedlePointer { Value = 0, PointerCapBrush = System.Windows.Media.Brushes.White });
    //        return;
    //    }

    //    foreach (var metric in metrics)
    //    {
    //        var value = Math.Max(0, Math.Min(100, (double)metric.Value));
    //        scale.Pointers.Add(new NeedlePointer
    //        {
    //            Value = value,
    //            PointerCapBrush = System.Windows.Media.Brushes.White,
    //            Label = metric.Name
    //        });
    //    }
    //}

    private void OnChartSelectionChanged(object sender, ChartSelectionChangedEventArgs e)
    {
        // Commented out due to API changes in .NET 9.0
        //if (DataContext is not AnalyticsViewModel viewModel)
        //{
        //    return;
        //}

        //if (e.SelectedSeries == null || e.SelectedDataPoint == null)
        //{
        //    return;
        //}

        //var point = e.SelectedDataPoint as ChartPoint;
        //var payload = point ?? e.SelectedDataPoint;
        //if (viewModel.DrillDownCommand.CanExecute(payload))
        //{
        //    viewModel.DrillDownCommand.Execute(payload);
        //}
    }

    private sealed record ChartPoint(string Label, double Value);
}
