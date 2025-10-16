using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Syncfusion.UI.Xaml.Charts;
using Syncfusion.SfSkinManager;
using WileyWidget.ViewModels;

namespace WileyWidget;

/// <summary>
/// High-impact analytics dashboard wiring Syncfusion visuals to Grok output.
/// </summary>
public partial class AnalyticsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsView"/> class.
    /// Parameterless constructor for XAML designer and Prism region navigation.
    /// </summary>
    public AnalyticsView() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsView"/> class with dependency injection.
    /// </summary>
    /// <param name="viewModel">The analytics view model injected by the container.</param>
    public AnalyticsView(AnalyticsViewModel viewModel)
    {
        InitializeComponent();

        if (viewModel != null)
        {
            DataContext = viewModel;
            viewModel.DataLoaded += OnAnalyticsDataLoaded;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // Clean up when the control is unloaded
        Unloaded += AnalyticsView_Unloaded;

        // Initialize default selections if not set
        if (viewModel != null && string.IsNullOrEmpty(viewModel.SelectedChartType) && viewModel.ChartTypes.Any())
        {
            viewModel.SelectedChartType = viewModel.ChartTypes.First();
        }

        if (viewModel != null && string.IsNullOrEmpty(viewModel.SelectedTimePeriod) && viewModel.TimePeriods.Any())
        {
            viewModel.SelectedTimePeriod = viewModel.TimePeriods.First();
        }
    }

    private void AnalyticsView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyticsViewModel viewModel)
        {
            viewModel.DataLoaded -= OnAnalyticsDataLoaded;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnalyticsViewModel.IsDataLoaded) && DataContext is AnalyticsViewModel viewModel && viewModel.IsDataLoaded)
        {
            UpdateChartDisplay(viewModel);
        }
    }

    private void OnAnalyticsDataLoaded(object? sender, EventArgs e)
    {
        if (DataContext is AnalyticsViewModel viewModel)
        {
            UpdateChartDisplay(viewModel);
        }
    }

    private void UpdateChartDisplay(AnalyticsViewModel viewModel)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                AnalyticsChart.Series.Clear();

                // Create series based on the current analytics data
                if (viewModel.CurrentAnalyticsData.Any())
                {
                    // For now, create a simple series from the first data item
                    // This would need to be enhanced based on the actual chart requirements
                    var sampleSeries = new LineSeries
                    {
                        ItemsSource = viewModel.CurrentAnalyticsData.Take(10),
                        XBindingPath = "Category", // This would need to be adjusted based on actual data structure
                        YBindingPath = "TotalBudgeted", // This would need to be adjusted based on actual data structure
                        Label = viewModel.SelectedChartType ?? "Data",
                        EnableAnimation = true,
                        ShowTooltip = true
                    };

                    AnalyticsChart.Series.Add(sampleSeries);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the UI
                System.Diagnostics.Debug.WriteLine($"Error updating chart display: {ex.Message}");
            }
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
        if (DataContext is not AnalyticsViewModel viewModel)
        {
            return;
        }

        // Handle chart selection for drill-down
        // Simplified implementation - trigger drill-down when any series is selected
        if (e.SelectedSeries != null && viewModel.DrillDownCommand.CanExecute(null))
        {
            viewModel.DrillDownCommand.Execute(null);
        }
    }

    private void ThemeSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string themeName)
        {
            // Apply the selected theme to this control
            using var theme = new Theme(themeName);
            SfSkinManager.SetTheme(this, theme);
        }
    }

    private sealed record ChartPoint(string Label, double Value);
}
