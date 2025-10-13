# Municipal Account View - Quick Reference

## 🚀 Quick Start

```csharp
// Open Municipal Account View
var viewModel = App.ServiceProvider.GetService<MunicipalAccountViewModel>();
var view = new MunicipalAccountView(viewModel);
view.Show();
```

## 📋 Key Features at a Glance

| Feature | Component | Status |
|---------|-----------|--------|
| Hierarchical Account Grid | SfDataGrid | ✅ Complete |
| FluentDark Theme | SfSkinManager | ✅ Applied |
| Account Details Panel | SfAccordion | ✅ Complete |
| Transaction History | SfDataGrid | ✅ Complete |
| Navigation Back | RelayCommand | ✅ Complete |
| Grouping by Fund | GroupColumnDescriptions | ✅ Complete |
| Balance Summaries | TableSummaryRows | ✅ Complete |
| Row Tooltips | GridColumn.ToolTip | ✅ Complete |
| Inline Editing | AllowEditing=True | ✅ Complete |
| Color-Coded Balances | BalanceColorConverter | ✅ Complete |

## 🎨 Visual Layout

```
┌─────────────────────────────────────────────────────────────┐
│  Ribbon: Data Ops | Filters | Navigation | View Options     │
├───────────────────────────────┬─────────────────────────────┤
│                               │                             │
│   Hierarchical Account Grid   │  Account Details Expander   │
│                               │  (SfAccordion)              │
│   - Account Number            │                             │
│   - Name (editable)           │  ┌─────────────────────────┤
│   - Fund (grouped)            │  │                         │
│   - Type                      │  │  Transaction Grid       │
│   - Balance (color-coded)     │  │  - Date                 │
│   - Department                │  │  - Description          │
│   - Notes (editable)          │  │  - Debit (green)        │
│                               │  │  - Credit (red)         │
│   [Summary: Total Count/Bal]  │  │  [Summary: Totals]      │
│                               │  │                         │
├───────────────────────────────┴─────────────────────────────┤
│  Status: Ready | Total Accounts: 125                        │
└─────────────────────────────────────────────────────────────┘
```

## 📊 Syncfusion Controls Used

### **SfDataGrid** (Main Account Grid)
```xml
<syncfusion:SfDataGrid 
    AllowEditing="True"
    AllowSorting="True"
    AllowFiltering="True"
    AllowGrouping="True"
    ShowGroupDropArea="True">
    
    <syncfusion:SfDataGrid.GroupColumnDescriptions>
        <syncfusion:GroupColumnDescription ColumnName="FundDescription" />
    </syncfusion:SfDataGrid.GroupColumnDescriptions>
    
    <syncfusion:SfDataGrid.TableSummaryRows>
        <!-- Total Count and Balance -->
    </syncfusion:SfDataGrid.TableSummaryRows>
</syncfusion:SfDataGrid>
```

### **Ribbon** (Top Command Bar)
```xml
<syncfusion:Ribbon>
    <syncfusion:RibbonTab Caption="Account Management">
        <syncfusion:RibbonBar Header="Data Operations">
            <syncfusion:RibbonButton Label="Load Accounts" 
                Command="{Binding LoadAccountsCommand}" />
        </syncfusion:RibbonBar>
    </syncfusion:RibbonTab>
</syncfusion:Ribbon>
```

### **SfAccordion** (Details Expander)
```xml
<syncfusion:SfAccordion ExpandMode="One">
    <syncfusion:SfAccordionItem Header="Account Details" IsExpanded="True">
        <!-- Account details grid -->
    </syncfusion:SfAccordionItem>
</syncfusion:SfAccordion>
```

### **SfBusyIndicator** (Loading Overlay)
```xml
<notification:SfBusyIndicator 
    AnimationType="DoubleCircle"
    IsBusy="{Binding IsBusy}" />
```

## 🎯 ViewModel Commands

| Command | Description | Binding |
|---------|-------------|---------|
| `LoadAccountsCommand` | Load all accounts from database | Button |
| `SyncFromQuickBooksCommand` | Sync accounts from QBO | Button |
| `LoadBudgetAnalysisCommand` | Load budget analysis data | Button |
| `FilterByFundCommand` | Filter by selected fund type | Button |
| `FilterByTypeCommand` | Filter by selected account type | Button |
| `NavigateBackCommand` | Close view, return to parent | Button |
| `ClearErrorCommand` | Clear error state | Button |
| `ExportToExcelCommand` | Export accounts to Excel | Placeholder |
| `PrintReportCommand` | Print account report | Placeholder |

## 🔧 Customization Points

### **Change Theme**
```xml
syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=FluentDark}"
```

Available themes: FluentDark, FluentLight, MaterialDark, MaterialLight, Office2019Black, etc.

### **Modify Grouping**
```xml
<syncfusion:SfDataGrid.GroupColumnDescriptions>
    <syncfusion:GroupColumnDescription ColumnName="TypeDescription" />
    <syncfusion:GroupColumnDescription ColumnName="FundDescription" />
</syncfusion:SfDataGrid.GroupColumnDescriptions>
```

### **Custom Balance Colors**
Edit `BalanceColorConverter` in `Converters.cs`:
```csharp
if (balance > 0)
    return new SolidColorBrush(Color.FromRgb(74, 222, 128)); // Green
if (balance < 0)
    return new SolidColorBrush(Color.FromRgb(248, 113, 113)); // Red
```

### **Add Summary Columns**
```xml
<syncfusion:GridTableSummaryRow.SummaryColumns>
    <syncfusion:GridSummaryColumn Name="AvgBalance" 
        Format="'{Average:C2}'" 
        MappingName="Balance" 
        SummaryType="DoubleAggregate" />
</syncfusion:GridTableSummaryRow.SummaryColumns>
```

## 🐛 Troubleshooting

### **Grid Not Displaying Data**
- Check `ItemsSource="{Binding MunicipalAccounts}"`
- Verify `LoadAccountsCommand` executes on initialization
- Check `InitializeAsync()` is called in `OnContentRendered`

### **Balance Colors Not Showing**
- Ensure `BalanceColorConverter` is registered in Resources
- Verify namespace: `xmlns:views="clr-namespace:WileyWidget.Views"`
- Check binding path: `{Binding Balance, Converter={StaticResource BalanceColorConverter}}`

### **Navigation Back Not Working**
- Verify `NavigateBackCommand` is bound correctly
- Check Window.DataContext is set to ViewModel
- Ensure `System.Windows` is imported in ViewModel

### **Transactions Not Loading**
- Check `SelectedAccount` is bound two-way: `Mode=TwoWay`
- Verify `Transactions` navigation property is loaded
- Enable lazy loading or use `.Include(a => a.Transactions)`

## 📚 Related Documentation

- **Full Implementation**: `docs/MunicipalAccountView_Implementation_Summary.md`
- **Architecture Guide**: `docs/ARCHITECTURE_COMPLETE_SUMMARY.md`
- **ViewModel Source**: `src/ViewModels/MunicipalAccountViewModel.cs`
- **View Source**: `src/Views/MunicipalAccountView.xaml`
- **Converters**: `src/Views/Converters.cs`

## 🔗 Syncfusion References

- **SfDataGrid Docs**: https://help.syncfusion.com/wpf/datagrid/overview
- **Ribbon Docs**: https://help.syncfusion.com/wpf/ribbon/overview
- **SfAccordion Docs**: https://help.syncfusion.com/wpf/accordion/overview
- **Theme Manager**: https://help.syncfusion.com/wpf/themes/overview

---

**Last Updated**: 2025-01-12  
**Version**: 1.0  
**Status**: ✅ Production Ready
