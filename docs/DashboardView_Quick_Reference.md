# DashboardView Quick Reference Guide

## 🎯 Quick Start

### File Locations
- **View**: `src/Views/DashboardView.xaml`
- **ViewModel**: `src/ViewModels/DashboardViewModel.cs`
- **Code-Behind**: `src/Views/DashboardView.xaml.cs`

### Key Features Implemented

#### 1. **Four Interactive Gauges**
```xml
<!-- Budget Utilization: Needle Pointer -->
<gauge:SfCircularGauge>
    <gauge:CircularPointer PointerType="NeedlePointer" Value="{Binding BudgetUtilizationScore}"/>
</gauge:SfCircularGauge>

<!-- System Health: Needle Pointer -->
<gauge:SfCircularGauge>
    <gauge:CircularPointer PointerType="NeedlePointer" Value="{Binding SystemHealthScore}"/>
</gauge:SfCircularGauge>

<!-- Enterprises: Range Pointer -->
<gauge:SfCircularGauge>
    <gauge:CircularPointer PointerType="RangePointer" Value="{Binding TotalEnterprises}"/>
</gauge:SfCircularGauge>

<!-- Projects: Symbol Pointer -->
<gauge:SfCircularGauge>
    <gauge:CircularPointer PointerType="SymbolPointer" Value="{Binding ActiveProjects}"/>
</gauge:SfCircularGauge>
```

#### 2. **Interactive Budget Trend Chart**
```xml
<chart:SfChart>
    <!-- Trackball for interactive tooltips -->
    <chart:ChartTrackBallBehavior ShowLine="True"/>
    
    <!-- Zoom and Pan -->
    <chart:ChartZoomPanBehavior EnableZooming="True" EnablePanning="True"/>
    
    <!-- Line Series with Animation -->
    <chart:LineSeries ItemsSource="{Binding BudgetTrendData}" EnableAnimation="True"/>
</chart:SfChart>
```

#### 3. **Alert Grid with Conditional Formatting**
```xml
<syncfusion:SfDataGrid ItemsSource="{Binding SystemAlerts}">
    <syncfusion:GridTextColumn MappingName="Priority">
        <syncfusion:GridTextColumn.CellStyle>
            <!-- Red/Bold for High Priority -->
            <DataTrigger Binding="{Binding Priority}" Value="High">
                <Setter Property="Foreground" Value="#F44336"/>
                <Setter Property="FontWeight" Value="Bold"/>
            </DataTrigger>
        </syncfusion:GridTextColumn.CellStyle>
    </syncfusion:GridTextColumn>
</syncfusion:SfDataGrid>
```

## 🔧 ViewModel Bindings

### KPI Properties
| Property | Type | Usage |
|----------|------|-------|
| `BudgetUtilizationScore` | int | Budget gauge value (0-100) |
| `SystemHealthScore` | int | Health gauge value (0-100) |
| `SystemHealthStatus` | string | Health text ("Excellent", "Good", etc.) |
| `TotalEnterprises` | int | Enterprise count |
| `ActiveProjects` | int | Project count |
| `TotalBudget` | decimal | Budget amount |

### Collections
| Property | Type | Usage |
|----------|------|-------|
| `BudgetTrendData` | ObservableCollection\<BudgetTrendItem\> | Budget chart |
| `HistoricalData` | ObservableCollection\<BudgetTrendItem\> | Alternative data source |
| `EnterpriseTypeData` | ObservableCollection\<EnterpriseTypeItem\> | Pie chart |
| `SystemAlerts` | ObservableCollection\<AlertItem\> | Alerts grid |
| `RecentActivities` | ObservableCollection\<ActivityItem\> | Activity grid |

### Commands
| Command | Usage |
|---------|-------|
| `RefreshDashboardCommand` | Manual refresh |
| `ToggleAutoRefreshCommand` | Enable/disable auto-refresh |
| `OpenEnterpriseManagementCommand` | Navigate to enterprises |
| `OpenBudgetAnalysisCommand` | Navigate to budget view |
| `OpenSettingsCommand` | Open settings dialog |
| `GenerateReportCommand` | Generate financial report |
| `BackupDataCommand` | Backup data |

## 🎨 Color Palette

| Color | Hex Code | Usage |
|-------|----------|-------|
| Blue | `#2196F3` | Budget indicators |
| Green | `#4CAF50` | Health/Success |
| Purple | `#9C27B0` | Enterprise metrics |
| Orange | `#FF9800` | Project indicators |
| Red | `#F44336` | Alerts/Critical |
| Yellow | `#FFC107` | Medium priority |

## 📊 Gauge Configurations

### Budget Utilization Gauge
- **Type**: NeedlePointer
- **Range**: 0-100
- **Green Zone**: 0-60% (healthy)
- **Yellow Zone**: 60-80% (warning)
- **Red Zone**: 80-100% (critical)
- **Animation**: 1500ms

### System Health Gauge
- **Type**: NeedlePointer
- **Range**: 0-100
- **Red Zone**: 0-50% (poor)
- **Yellow Zone**: 50-75% (fair)
- **Green Zone**: 75-100% (excellent)
- **Animation**: 1500ms

### Enterprise Count Gauge
- **Type**: RangePointer
- **Range**: 0-100
- **Stroke Thickness**: 20px
- **Animation**: 1500ms

### Active Projects Gauge
- **Type**: SymbolPointer
- **Range**: 0-50
- **Symbol**: InvertedTriangle
- **Size**: 15x15
- **Animation**: 1500ms

## 📈 Chart Features

### Budget Trend Chart
- **Series Type**: LineSeries
- **Axes**: Category (X), Numerical (Y)
- **Data Labels**: Currency format (C0)
- **Tooltips**: Custom template
- **Interactivity**:
  - ✅ Trackball on hover
  - ✅ Mouse wheel zoom
  - ✅ Click and drag pan
  - ✅ Animated drawing

### Enterprise Distribution Chart
- **Series Type**: PieSeries
- **Data Labels**: Percentage format (P0)
- **Legend**: Bottom position
- **Explode**: First segment (index 0)
- **Interactivity**:
  - ✅ Tooltips
  - ✅ Animated drawing

## 🚨 Alert Priority Formatting

| Priority | Color | Font Weight |
|----------|-------|-------------|
| High | Red (#F44336) | Bold |
| Medium | Yellow (#FFC107) | SemiBold |
| Low | Blue (#2196F3) | Normal |

## 🔄 Auto-Refresh Settings

### Configuration
- **Default Interval**: 5 minutes
- **Enabled by Default**: Yes
- **Pauses When**: Window minimized
- **Resumes When**: Window restored

### Code-Behind Timer
```csharp
private void SetupAutoRefreshTimer()
{
    _refreshTimer = new DispatcherTimer();
    _refreshTimer.Tick += async (s, e) => { /* refresh logic */ };
    _refreshTimer.Interval = TimeSpan.FromMinutes(RefreshIntervalMinutes);
    _refreshTimer.Start();
}
```

## 🎭 Theme Application

### Primary Theme
```xml
<Grid syncfusionskin:SfSkinManager.VisualStyle="FluentDark">
```

### Fallback Logic (Code-Behind)
```csharp
Services.ThemeUtility.TryApplyTheme(this, "FluentDark");
// Falls back to Fluent Light if FluentDark fails
```

## 🛠️ Customization Tips

### Change Gauge Ranges
```xml
<gauge:CircularScale.Ranges>
    <gauge:CircularRange StartValue="0" EndValue="60" Stroke="#4CAF50"/>
    <gauge:CircularRange StartValue="60" EndValue="80" Stroke="#FFC107"/>
    <gauge:CircularRange StartValue="80" EndValue="100" Stroke="#F44336"/>
</gauge:CircularScale.Ranges>
```

### Modify Animation Duration
```xml
<gauge:CircularPointer EnableAnimation="True" AnimationDuration="2000"/>
```

### Customize Chart Tooltip
```xml
<chart:LineSeries.TooltipTemplate>
    <DataTemplate>
        <StackPanel>
            <TextBlock Text="{Binding Item.Period}"/>
            <TextBlock Text="{Binding Item.Amount, StringFormat='{}{0:C0}'}"/>
        </StackPanel>
    </DataTemplate>
</chart:LineSeries.TooltipTemplate>
```

### Add New Quick Action Button
```xml
<syncfusion:ButtonAdv Content="New Action"
                     Command="{Binding NewActionCommand}"
                     Margin="5"
                     Width="180" Height="60"
                     IsEnabled="{Binding IsLoading, Converter={StaticResource BoolToVis}, ConverterParameter=invert}">
    <syncfusion:ButtonAdv.ToolTip>
        <TextBlock Text="Description of new action"/>
    </syncfusion:ButtonAdv.ToolTip>
</syncfusion:ButtonAdv>
```

## 🧪 Testing Checklist

### Visual Tests
- [ ] All gauges display with correct values
- [ ] Gauge animations play smoothly
- [ ] Chart tooltips appear on hover
- [ ] Alert grid colors match priorities
- [ ] Buttons show tooltips on hover

### Functional Tests
- [ ] Refresh command updates all data
- [ ] Auto-refresh timer works correctly
- [ ] Zoom/pan chart interactions work
- [ ] Navigation buttons open correct views
- [ ] Grid sorting functions properly

### Performance Tests
- [ ] Gauges animate at 60fps
- [ ] Large datasets load quickly
- [ ] Auto-refresh doesn't cause lag
- [ ] Memory usage remains stable

## 📚 Documentation References

### Syncfusion Controls
- [SfCircularGauge](https://help.syncfusion.com/wpf/radial-gauge/getting-started)
- [SfChart](https://help.syncfusion.com/wpf/charts/getting-started)
- [SfDataGrid](https://help.syncfusion.com/wpf/datagrid/getting-started)
- [SfSkinManager](https://help.syncfusion.com/wpf/themes/skin-manager)

### Project Docs
- Full Summary: `docs/DashboardView_Syncfusion_Polish_Summary.md`
- Architecture: `docs/ARCHITECTURE_COMPLETE_SUMMARY.md`
- Guidelines: `BusBuddy.instructions.md`

## 🐛 Troubleshooting

### Issue: Gauges not animating
**Solution**: Verify `EnableAnimation="True"` and `AnimationDuration` is set

### Issue: Chart tooltips not showing
**Solution**: Check `ShowTooltip="True"` on series

### Issue: Alert colors not applying
**Solution**: Verify DataTrigger bindings match Priority values exactly

### Issue: Theme not applying
**Solution**: Ensure SfSkinManager namespace is registered and VisualStyle is valid

### Issue: Build errors with Syncfusion controls
**Solution**: Verify NuGet packages are installed:
- Syncfusion.SfGauge.WPF
- Syncfusion.SfChart.WPF
- Syncfusion.SfSkinManager.WPF

## 💡 Pro Tips

1. **Performance**: Use virtualization for grids with 100+ items
2. **Accessibility**: Ensure sufficient color contrast for text
3. **Responsive**: Test at different window sizes
4. **Data Binding**: Use ObservableCollection for automatic UI updates
5. **Commands**: Prefer Commands over Click events for MVVM
6. **Animations**: Keep durations between 500-1500ms for best UX
7. **Tooltips**: Provide context-specific information
8. **Colors**: Maintain consistent palette across entire app

## 🚀 Quick Build & Run

```powershell
# Build project
dotnet build WileyWidget.csproj

# Run application
dotnet run --project WileyWidget.csproj

# Build with specific configuration
dotnet build WileyWidget.csproj -c Release
```

---

**Last Updated**: October 12, 2025  
**Version**: 1.0  
**Project**: Wiley Widget Municipal Financial Management
