using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using System;
using System.Linq;
using Serilog;
using System.Threading;
using System.Globalization;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using Syncfusion.UI.Xaml.TreeGrid;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.XlsIO;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Microsoft.Win32;
using System.IO;
using System.Drawing;
using Syncfusion.Drawing;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model for managing municipal enterprises (Phase 1)
/// Provides data binding for enterprise CRUD operations and budget calculations
/// </summary>
public partial class EnterpriseViewModel : AsyncViewModelBase
{
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly Timer _refreshTimer;

    /// <summary>
    /// Collection of all enterprises for data binding
    /// </summary>
    public ThreadSafeObservableCollection<Enterprise> Enterprises { get; } = new();

    /// <summary>
    /// Currently selected enterprise in the UI
    /// </summary>
    private Enterprise? _selectedEnterprise;
    public Enterprise? SelectedEnterprise
    {
        get => _selectedEnterprise;
        set
        {
            if (_selectedEnterprise != value)
            {
                _selectedEnterprise = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedEnterprise));
                OnPropertyChanged(nameof(CanSaveEnterprise));
            }
        }
    }

    /// <summary>
    /// Whether an enterprise is currently selected
    /// </summary>
    public bool HasSelectedEnterprise => SelectedEnterprise != null;

    /// <summary>
    /// Whether the selected enterprise can be saved (has changes)
    /// </summary>
    public bool CanSaveEnterprise => SelectedEnterprise != null;

    /// <summary>
    /// Search text for filtering enterprises
    /// </summary>
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Available status options for filtering
    /// </summary>
    public ObservableCollection<EnterpriseStatus> StatusOptions { get; } = new()
    {
        EnterpriseStatus.Active,
        EnterpriseStatus.Inactive,
        EnterpriseStatus.Suspended
    };

    /// <summary>
    /// Selected status filter
    /// </summary>
    private EnterpriseStatus? _selectedStatusFilter;
    public EnterpriseStatus? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (_selectedStatusFilter != value)
            {
                _selectedStatusFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Hierarchical enterprise node for tree structure
    /// </summary>
    public class EnterpriseNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        private Enterprise? _enterprise;
        public Enterprise? Enterprise
        {
            get => _enterprise;
            set
            {
                if (_enterprise != value)
                {
                    _enterprise = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<EnterpriseNode> _children = new();
        public ObservableCollection<EnterpriseNode> Children
        {
            get => _children;
            set
            {
                if (_children != value)
                {
                    _children = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsExpanded { get; set; } = true;
        public bool HasChildren => Children.Any();

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Advanced filter for enterprise data
    /// </summary>
    public class AdvancedFilter : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _propertyName = string.Empty;
        public string PropertyName
        {
            get => _propertyName;
            set
            {
                if (_propertyName != value)
                {
                    _propertyName = value;
                    OnPropertyChanged();
                }
            }
        }

        private FilterOperator _operator = FilterOperator.Equals;
        public FilterOperator Operator
        {
            get => _operator;
            set
            {
                if (_operator != value)
                {
                    _operator = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Matches(Enterprise enterprise)
        {
            if (!IsEnabled) return true;

            var property = enterprise.GetType().GetProperty(PropertyName);
            if (property == null) return false;

            var propertyValue = property.GetValue(enterprise);
            if (propertyValue == null) return false;

            return Operator switch
            {
                FilterOperator.Equals => propertyValue.ToString() == Value,
                FilterOperator.NotEquals => propertyValue.ToString() != Value,
                FilterOperator.Contains => propertyValue.ToString()?.Contains(Value, StringComparison.OrdinalIgnoreCase) == true,
                FilterOperator.GreaterThan => CompareValues(propertyValue, Value) > 0,
                FilterOperator.LessThan => CompareValues(propertyValue, Value) < 0,
                FilterOperator.GreaterThanOrEqual => CompareValues(propertyValue, Value) >= 0,
                FilterOperator.LessThanOrEqual => CompareValues(propertyValue, Value) <= 0,
                _ => false
            };
        }

        private int CompareValues(object propertyValue, string filterValue)
        {
            if (propertyValue is decimal dec && decimal.TryParse(filterValue, out var filterDec))
                return dec.CompareTo(filterDec);
            if (propertyValue is int intVal && int.TryParse(filterValue, out var filterInt))
                return intVal.CompareTo(filterInt);
            if (propertyValue is DateTime date && DateTime.TryParse(filterValue, out var filterDate))
                return date.CompareTo(filterDate);

            return string.Compare(propertyValue.ToString(), filterValue, StringComparison.OrdinalIgnoreCase);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Filter operators for advanced filtering
    /// </summary>
    public enum FilterOperator
    {
        Equals,
        NotEquals,
        Contains,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    /// <summary>
    /// Selected node in the tree view
    /// </summary>
    private EnterpriseNode? _selectedNode;
    public EnterpriseNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
                _selectedNode = value;
                OnPropertyChanged();
                // Update SelectedEnterprise when node changes
                SelectedEnterprise = _selectedNode?.Enterprise;
            }
        }
    }

    /// <summary>
    /// Budget summary text for display
    /// </summary>
    private string _budgetSummaryText = "No budget data available";
    public string BudgetSummaryText
    {
        get => _budgetSummaryText;
        set
        {
            if (_budgetSummaryText != value)
            {
                _budgetSummaryText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Hierarchical enterprises collection for tree view binding
    /// </summary>
    public ObservableCollection<EnterpriseNode> HierarchicalEnterprises { get; } = new();

    /// <summary>
    /// Paged hierarchical enterprises collection for SfTreeGrid binding
    /// </summary>
    public ObservableCollection<EnterpriseNode> PagedHierarchicalEnterprises { get; } = new();

    /// <summary>
    /// Collection of advanced filters for enterprise data
    /// </summary>
    public ObservableCollection<AdvancedFilter> AdvancedFilters { get; } = new();

    /// <summary>
    /// Filtered enterprises collection for advanced filtering
    /// </summary>
    private ObservableCollection<Enterprise> _filteredEnterprises = new();
    public ObservableCollection<Enterprise> FilteredEnterprises
    {
        get => _filteredEnterprises;
        set
        {
            if (_filteredEnterprises != value)
            {
                _filteredEnterprises = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Page size for the data pager
    /// </summary>
    private int _pageSize = 50;
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (_pageSize != value)
            {
                _pageSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageCount));
                UpdatePagedData();
            }
        }
    }

    /// <summary>
    /// Current page index for the data pager
    /// </summary>
    private int _currentPageIndex = 0;
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (_currentPageIndex != value)
            {
                _currentPageIndex = value;
                OnPropertyChanged();
                UpdatePagedData();
            }
        }
    }

    /// <summary>
    /// Total number of pages based on item count and page size
    /// </summary>
    public int PageCount => HierarchicalEnterprises.Any() ? (int)Math.Ceiling(HierarchicalEnterprises.Count / (double)PageSize) : 0;

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalItemCount => HierarchicalEnterprises.Count;

    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseViewModel(
        IEnterpriseRepository enterpriseRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<EnterpriseViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));

        // Auto-refresh every 5 minutes
        _refreshTimer = new Timer(async _ =>
        {
            await DispatcherHelper.InvokeAsync(async () =>
            {
                await LoadEnterprisesAsync();
            });
        }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Loads all enterprises from the database (public for View access)
    /// </summary>
    [RelayCommand]
    public async Task LoadEnterprisesAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = await _enterpriseRepository.GetAllAsync();
            await Enterprises.ReplaceAllAsync(enterprises);
            ApplyFilters();
        }, statusMessage: "Loading enterprises...");
    }

    /// <summary>
    /// Loads enterprises incrementally with progress reporting for large datasets
    /// </summary>
    [RelayCommand]
    public async Task LoadEnterprisesIncrementalAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var progress = new Progress<int>(percent =>
            {
                StatusMessage = $"Loading enterprises... {percent}%";
                ProgressPercentage = percent;
            });

            // Load all enterprises at once (simplified from chunked loading)
            var enterprises = await _enterpriseRepository.GetAllAsync();
            await Enterprises.ReplaceAllAsync(enterprises);
            (progress as IProgress<int>)?.Report(100);

            ApplyFilters();
        }, statusMessage: "Loading enterprises...");
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    [RelayCommand]
    private async Task AddEnterpriseAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var newEnterprise = new Enterprise
            {
                Name = "New Enterprise",
                CurrentRate = 0.00m,
                MonthlyExpenses = 0.00m,
                CitizenCount = 0,
                Notes = "New enterprise - update details"
            };

            var addedEnterprise = await _enterpriseRepository.AddAsync(newEnterprise);
            await Enterprises.AddAsync(addedEnterprise);
            SelectedEnterprise = addedEnterprise;
        }, statusMessage: "Adding new enterprise...");
    }

    /// <summary>
    /// Saves changes to the selected enterprise
    /// </summary>
    [RelayCommand]
    private async Task SaveEnterpriseAsync()
    {
        if (SelectedEnterprise == null) return;

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // MonthlyRevenue is now automatically calculated from CitizenCount * CurrentRate
            await _enterpriseRepository.UpdateAsync(SelectedEnterprise);
        }, statusMessage: "Saving enterprise changes...");
    }

    /// <summary>
    /// Deletes the selected enterprise
    /// </summary>
    [RelayCommand]
    private async Task DeleteEnterpriseAsync()
    {
        if (SelectedEnterprise == null) return;

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var success = await _enterpriseRepository.DeleteAsync(SelectedEnterprise.Id);
            if (success)
            {
                await Enterprises.RemoveAsync(SelectedEnterprise);
                SelectedEnterprise = Enterprises.FirstOrDefault();
            }
        }, statusMessage: "Deleting enterprise...");
    }

    /// <summary>
    /// Calculates and displays budget summary
    /// </summary>
    [RelayCommand]
    private void UpdateBudgetSummary()
    {
        BudgetSummaryText = GetBudgetSummary();
    }

    /// <summary>
    /// Clears all filters
    /// </summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = null;
    }

    /// <summary>
    /// Exports enterprises to Excel
    /// </summary>
    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // Get the SfTreeGrid from the view (we'll need to pass it or get it from a service)
            // For now, we'll create a temporary tree grid for export
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Create Excel engine and workbook directly
                    var excelEngine = new ExcelEngine();
                    var workBook = excelEngine.Excel.Workbooks.Create();
                    var worksheet = workBook.Worksheets.Create("Municipal Enterprises");

                    // Add header information
                    worksheet.Range["A1"].Text = "Municipal Enterprises Export";
                    worksheet.Range["A1"].CellStyle.Font.Bold = true;
                    worksheet.Range["A1"].CellStyle.Font.Size = 14;

                    worksheet.Range["A2"].Text = $"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    worksheet.Range["A3"].Text = $"Total Enterprises: {enterprises.Count}";
                    worksheet.Range["A4"].Text = $"Total Revenue: ${enterprises.Sum(e => e.MonthlyRevenue):N2}";
                    worksheet.Range["A5"].Text = $"Total Expenses: ${enterprises.Sum(e => e.MonthlyExpenses):N2}";

                    // Add data headers
                    worksheet.Range["A7"].Text = "Name";
                    worksheet.Range["B7"].Text = "Type";
                    worksheet.Range["C7"].Text = "Status";
                    worksheet.Range["D7"].Text = "Citizen Count";
                    worksheet.Range["E7"].Text = "Current Rate";
                    worksheet.Range["F7"].Text = "Monthly Revenue";
                    worksheet.Range["G7"].Text = "Monthly Expenses";
                    worksheet.Range["H7"].Text = "Monthly Balance";
                    worksheet.Range["I7"].Text = "Break Even Rate";
                    worksheet.Range["J7"].Text = "Last Updated";

                    // Make headers bold
                    worksheet.Range["A7:J7"].CellStyle.Font.Bold = true;

                    // Add data rows
                    for (int i = 0; i < enterprises.Count; i++)
                    {
                        var enterprise = enterprises[i];
                        var row = i + 8; // Start from row 8
                        worksheet.Range[$"A{row}"].Text = enterprise.Name;
                        worksheet.Range[$"B{row}"].Text = enterprise.Type;
                        worksheet.Range[$"C{row}"].Text = enterprise.Status.ToString();
                        worksheet.Range[$"D{row}"].Number = enterprise.CitizenCount;
                        worksheet.Range[$"E{row}"].Number = (double)enterprise.CurrentRate;
                        worksheet.Range[$"F{row}"].Number = (double)enterprise.MonthlyRevenue;
                        worksheet.Range[$"G{row}"].Number = (double)enterprise.MonthlyExpenses;
                        worksheet.Range[$"H{row}"].Number = (double)enterprise.MonthlyBalance;
                        worksheet.Range[$"I{row}"].Number = (double)enterprise.BreakEvenRate;
                        worksheet.Range[$"J{row}"].Text = enterprise.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss");
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();

                    // Save with file dialog
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx|Excel 97-2003 Files (*.xls)|*.xls",
                        FilterIndex = 1,
                        DefaultExt = "xlsx",
                        FileName = $"Municipal_Enterprises_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        workBook.SaveAs(saveFileDialog.FileName);
                        Logger.LogInformation("Excel export completed: {FileName}", saveFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error exporting to Excel");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Exporting to Excel...");
    }

    /// <summary>
    /// Exports enterprises to advanced Excel with templates, charts, and executive summary
    /// </summary>
    [RelayCommand]
    private async Task ExportToExcelAdvancedAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Create Excel engine and workbook directly
                    var excelEngine = new ExcelEngine();
                    var workBook = excelEngine.Excel.Workbooks.Create();

                    // Create main data sheet
                    var mainSheet = workBook.Worksheets.Create("Enterprise Data");

                    // Add header information
                    mainSheet.Range["A1"].Text = "Enterprise Analysis Report";
                    mainSheet.Range["A1"].CellStyle.Font.Bold = true;
                    mainSheet.Range["A1"].CellStyle.Font.Size = 16;

                    mainSheet.Range["A2"].Text = $"Report Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    mainSheet.Range["A3"].Text = $"Total Enterprises: {enterprises.Count}";

                    // Add data headers
                    mainSheet.Range["A5"].Text = "Name";
                    mainSheet.Range["B5"].Text = "Type";
                    mainSheet.Range["C5"].Text = "Status";
                    mainSheet.Range["D5"].Text = "Citizen Count";
                    mainSheet.Range["E5"].Text = "Current Rate";
                    mainSheet.Range["F5"].Text = "Monthly Revenue";
                    mainSheet.Range["G5"].Text = "Monthly Expenses";
                    mainSheet.Range["H5"].Text = "Monthly Balance";
                    mainSheet.Range["I5"].Text = "Break Even Rate";

                    // Make headers bold
                    mainSheet.Range["A5:I5"].CellStyle.Font.Bold = true;
                    mainSheet.Range["A5:I5"].CellStyle.Color = System.Drawing.Color.LightBlue;

                    // Add data rows
                    for (int i = 0; i < enterprises.Count; i++)
                    {
                        var enterprise = enterprises[i];
                        var row = i + 6; // Start from row 6
                        mainSheet.Range[$"A{row}"].Text = enterprise.Name;
                        mainSheet.Range[$"B{row}"].Text = enterprise.Type;
                        mainSheet.Range[$"C{row}"].Text = enterprise.Status.ToString();
                        mainSheet.Range[$"D{row}"].Number = enterprise.CitizenCount;
                        mainSheet.Range[$"E{row}"].Number = (double)enterprise.CurrentRate;
                        mainSheet.Range[$"F{row}"].Number = (double)enterprise.MonthlyRevenue;
                        mainSheet.Range[$"G{row}"].Number = (double)enterprise.MonthlyExpenses;
                        mainSheet.Range[$"H{row}"].Number = (double)enterprise.MonthlyBalance;
                        mainSheet.Range[$"I{row}"].Number = (double)enterprise.BreakEvenRate;
                    }

                    // Create summary sheet
                    var summarySheet = workBook.Worksheets.Create("Summary");
                    summarySheet.Range["A1"].Text = "Executive Summary";
                    summarySheet.Range["A1"].CellStyle.Font.Bold = true;
                    summarySheet.Range["A1"].CellStyle.Font.Size = 14;

                    summarySheet.Range["A3"].Text = "Total Enterprises:";
                    summarySheet.Range["B3"].Number = enterprises.Count;

                    summarySheet.Range["A4"].Text = "Total Revenue:";
                    summarySheet.Range["B4"].Number = (double)enterprises.Sum(e => e.MonthlyRevenue);

                    summarySheet.Range["A5"].Text = "Total Expenses:";
                    summarySheet.Range["B5"].Number = (double)enterprises.Sum(e => e.MonthlyExpenses);

                    summarySheet.Range["A6"].Text = "Net Balance:";
                    summarySheet.Range["B6"].Number = (double)enterprises.Sum(e => e.MonthlyBalance);

                    // Auto-fit columns
                    mainSheet.UsedRange.AutofitColumns();
                    summarySheet.UsedRange.AutofitColumns();

                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx",
                        FileName = $"Enterprise_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        workBook.SaveAs(saveFileDialog.FileName);
                        Logger.LogInformation("Advanced Excel export completed: {FileName}",
                                             saveFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in advanced Excel export");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Creating advanced Excel report...");
    }

    /// <summary>
    /// Creates an enhanced TreeGrid with all columns for export
    /// </summary>
    private SfTreeGrid CreateEnhancedTreeGrid()
    {
        var treeGrid = new SfTreeGrid
        {
            ItemsSource = HierarchicalEnterprises,
            ChildPropertyName = "Children",
            AutoGenerateColumns = false
        };

        // Add all columns for comprehensive export
        treeGrid.Columns.Add(new TreeGridTextColumn { HeaderText = "Name", MappingName = "Name", Width = 250 });
        treeGrid.Columns.Add(new TreeGridTextColumn { HeaderText = "Type", MappingName = "Enterprise.Type", Width = 120 });
        treeGrid.Columns.Add(new TreeGridTextColumn { HeaderText = "Status", MappingName = "Enterprise.Status", Width = 100 });
        treeGrid.Columns.Add(new TreeGridNumericColumn { HeaderText = "Citizens", MappingName = "Enterprise.CitizenCount", Width = 80 });
        treeGrid.Columns.Add(new TreeGridCurrencyColumn { HeaderText = "Rate", MappingName = "Enterprise.CurrentRate", Width = 100 });
        treeGrid.Columns.Add(new TreeGridCurrencyColumn { HeaderText = "Revenue", MappingName = "Enterprise.MonthlyRevenue", Width = 120 });
        treeGrid.Columns.Add(new TreeGridCurrencyColumn { HeaderText = "Expenses", MappingName = "Enterprise.MonthlyExpenses", Width = 120 });
        treeGrid.Columns.Add(new TreeGridCurrencyColumn { HeaderText = "Balance", MappingName = "Enterprise.MonthlyBalance", Width = 120 });
        treeGrid.Columns.Add(new TreeGridDateTimeColumn { HeaderText = "Last Updated", MappingName = "Enterprise.LastUpdated", Width = 150 });

        return treeGrid;
    }

    /// <summary>
    /// Adds executive summary to Excel worksheet
    /// </summary>
    private void AddExecutiveSummary(IWorksheet worksheet, List<Enterprise> enterprises)
    {
        worksheet.Name = "Executive Summary";

        // Title
        worksheet.Range["A1"].Text = "Municipal Enterprise Financial Analysis";
        worksheet.Range["A1"].CellStyle.Font.Bold = true;
        worksheet.Range["A1"].CellStyle.Font.Size = 16;

        // Summary statistics
        worksheet.Range["A3"].Text = "Summary Statistics";
        worksheet.Range["A3"].CellStyle.Font.Bold = true;

        worksheet.Range["A4"].Text = "Total Enterprises:";
        worksheet.Range["B4"].Number = enterprises.Count;

        worksheet.Range["A5"].Text = "Active Enterprises:";
        worksheet.Range["B5"].Number = enterprises.Count(e => e.Status == EnterpriseStatus.Active);

        worksheet.Range["A6"].Text = "Total Citizens Served:";
        worksheet.Range["B6"].Number = enterprises.Sum(e => e.CitizenCount);

        worksheet.Range["A7"].Text = "Total Monthly Revenue:";
        worksheet.Range["B7"].Number = enterprises.Sum(e => (double)e.MonthlyRevenue);

        worksheet.Range["A8"].Text = "Total Monthly Expenses:";
        worksheet.Range["B8"].Number = enterprises.Sum(e => (double)e.MonthlyExpenses);

        worksheet.Range["A9"].Text = "Net Monthly Balance:";
        worksheet.Range["B9"].Number = enterprises.Sum(e => (double)e.MonthlyBalance);

        // Format currency columns
        worksheet.Range["B7:B9"].NumberFormat = "$#,##0.00";

        // Auto-fit columns
        worksheet.UsedRange.AutofitColumns();
    }

    /// <summary>
    /// Adds financial charts to Excel worksheet
    /// </summary>
    private void AddFinancialCharts(IWorksheet worksheet, List<Enterprise> enterprises)
    {
        worksheet.Name = "Financial Charts";

        // Revenue vs Expenses by Type
        var revenueByType = enterprises.GroupBy(e => e.Type)
            .Select(g => new { Type = g.Key, Revenue = g.Sum(e => e.MonthlyRevenue) })
            .ToList();

        // Add data for charts
        worksheet.Range["A1"].Text = "Enterprise Type";
        worksheet.Range["B1"].Text = "Revenue";
        worksheet.Range["C1"].Text = "Expenses";

        for (int i = 0; i < revenueByType.Count; i++)
        {
            worksheet.Range[$"A{i + 2}"].Text = revenueByType[i].Type.ToString();
            worksheet.Range[$"B{i + 2}"].Number = (double)revenueByType[i].Revenue;
            worksheet.Range[$"C{i + 2}"].Number = enterprises
                .Where(e => e.Type == revenueByType[i].Type)
                .Sum(e => (double)e.MonthlyExpenses);
        }

        // Format as currency
        worksheet.Range[$"B2:C{revenueByType.Count + 1}"].NumberFormat = "$#,##0.00";

        // Auto-fit columns
        worksheet.UsedRange.AutofitColumns();
    }

    /// <summary>
    /// Exports enterprises to CSV
    /// </summary>
    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "CSV Files (*.csv)|*.csv",
                        DefaultExt = "csv",
                        FileName = $"Municipal_Enterprises_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        using var writer = new StreamWriter(saveFileDialog.FileName);
                        // Write header
                        writer.WriteLine("ID,Name,Type,Status,CitizenCount,CurrentRate,MonthlyRevenue,MonthlyExpenses,MonthlyBalance,BreakEvenRate,LastUpdated,Description,Notes");

                        // Write data
                        foreach (var enterprise in enterprises)
                        {
                            writer.WriteLine($"{enterprise.Id},\"{enterprise.Name}\",\"{enterprise.Type}\",\"{enterprise.Status}\"," +
                                          $"{enterprise.CitizenCount},{enterprise.CurrentRate},{enterprise.MonthlyRevenue}," +
                                          $"{enterprise.MonthlyExpenses},{enterprise.MonthlyBalance},{enterprise.BreakEvenRate}," +
                                          $"{enterprise.LastUpdated:yyyy-MM-dd HH:mm:ss},\"{enterprise.Description}\",\"{enterprise.Notes}\"");
                        }

                        Logger.LogInformation("CSV export completed: {FileName}", saveFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error exporting to CSV");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Exporting to CSV...");
    }

    /// <summary>
    /// Exports enterprises to PDF
    /// </summary>
    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Create PDF document directly
                    var document = new PdfDocument();
                    var page = document.Pages.Add();

                    // Add header information
                    var font = new PdfStandardFont(PdfFontFamily.TimesRoman, 16, PdfFontStyle.Bold);
                    page.Graphics.DrawString("Municipal Enterprises Report", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 10));

                    font = new PdfStandardFont(PdfFontFamily.TimesRoman, 12);
                    page.Graphics.DrawString($"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 35));
                    page.Graphics.DrawString($"Total Enterprises: {enterprises.Count}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 55));
                    page.Graphics.DrawString($"Total Revenue: ${enterprises.Sum(e => e.MonthlyRevenue):N2}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 75));
                    page.Graphics.DrawString($"Total Expenses: ${enterprises.Sum(e => e.MonthlyExpenses):N2}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 95));

                    // Create table for enterprise data
                    var table = new Syncfusion.Pdf.Grid.PdfGrid();
                    table.Columns.Add(5);

                    // Add headers
                    var headerRow = table.Rows.Add();
                    headerRow.Cells[0].Value = "Name";
                    headerRow.Cells[1].Value = "Type";
                    headerRow.Cells[2].Value = "Status";
                    headerRow.Cells[3].Value = "Revenue";
                    headerRow.Cells[4].Value = "Expenses";

                    // Make headers bold
                    foreach (var cell in headerRow.Cells)
                    {
                        ((PdfGridCell)cell).Style.Font = new PdfStandardFont(PdfFontFamily.TimesRoman, 10, PdfFontStyle.Bold);
                    }

                    // Add data rows
                    foreach (var enterprise in enterprises)
                    {
                        var row = table.Rows.Add();
                        row.Cells[0].Value = enterprise.Name;
                        row.Cells[1].Value = enterprise.Type;
                        row.Cells[2].Value = enterprise.Status.ToString();
                        row.Cells[3].Value = $"${enterprise.MonthlyRevenue:N2}";
                        row.Cells[4].Value = $"${enterprise.MonthlyExpenses:N2}";
                    }

                    // Draw table on page
                    table.Draw(page, new Syncfusion.Drawing.PointF(10, 120));

                    // Save with file dialog
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "PDF Files (*.pdf)|*.pdf",
                        DefaultExt = "pdf",
                        FileName = $"Municipal_Enterprises_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        document.Save(saveFileDialog.FileName);
                        document.Close();
                        Logger.LogInformation("PDF export completed: {FileName}", saveFileDialog.FileName);
                    }
                    else
                    {
                        document.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error exporting to PDF");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Exporting to PDF...");
    }

    /// <summary>
    /// Exports enterprises to professional PDF report with headers, footers, and executive summary
    /// </summary>
    [RelayCommand]
    private async Task ExportToPdfReportAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Create PDF document directly
                    var document = new PdfDocument();

                    // Add executive summary page first
                    AddExecutiveSummaryPage(document, enterprises);

                    // Add data page
                    var dataPage = document.Pages.Add();

                    // Add header
                    var font = new PdfStandardFont(PdfFontFamily.TimesRoman, 14, PdfFontStyle.Bold);
                    dataPage.Graphics.DrawString("Enterprise Data Report", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 10));

                    font = new PdfStandardFont(PdfFontFamily.TimesRoman, 10);
                    dataPage.Graphics.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 30));
                    dataPage.Graphics.DrawString($"Total Enterprises: {enterprises.Count}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 45));

                    // Create table for enterprise data
                    var table = new Syncfusion.Pdf.Grid.PdfGrid();
                    table.Columns.Add(6);

                    // Add headers
                    var headerRow = table.Rows.Add();
                    headerRow.Cells[0].Value = "Name";
                    headerRow.Cells[1].Value = "Type";
                    headerRow.Cells[2].Value = "Status";
                    headerRow.Cells[3].Value = "Citizens";
                    headerRow.Cells[4].Value = "Revenue";
                    headerRow.Cells[5].Value = "Expenses";

                    // Make headers bold
                    foreach (var cell in headerRow.Cells)
                    {
                        ((PdfGridCell)cell).Style.Font = new PdfStandardFont(PdfFontFamily.TimesRoman, 10, PdfFontStyle.Bold);
                    }

                    // Add data rows (limit to prevent page overflow)
                    var itemsToShow = enterprises.Take(50); // Limit for single page
                    foreach (var enterprise in itemsToShow)
                    {
                        var row = table.Rows.Add();
                        row.Cells[0].Value = enterprise.Name;
                        row.Cells[1].Value = enterprise.Type;
                        row.Cells[2].Value = enterprise.Status.ToString();
                        row.Cells[3].Value = enterprise.CitizenCount.ToString();
                        row.Cells[4].Value = $"${enterprise.MonthlyRevenue:N2}";
                        row.Cells[5].Value = $"${enterprise.MonthlyExpenses:N2}";
                    }

                    // Draw table on page
                    table.Draw(dataPage, new Syncfusion.Drawing.PointF(10, 65));

                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "PDF Files (*.pdf)|*.pdf",
                        FileName = $"Enterprise_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        document.Save(saveFileDialog.FileName);
                        document.Close();
                        Logger.LogInformation("Professional PDF report completed: {FileName}",
                                             saveFileDialog.FileName);
                    }
                    else
                    {
                        document.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error creating professional PDF report");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Generating professional PDF report...");
    }

    /// <summary>
    /// Adds executive summary page to PDF document
    /// </summary>
    private void AddExecutiveSummaryPage(PdfDocument document, List<Enterprise> enterprises)
    {
        var page = document.Pages.Add();
        var font = new PdfStandardFont(PdfFontFamily.TimesRoman, 18, PdfFontStyle.Bold);
        var subFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 14, PdfFontStyle.Bold);
        var normalFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 12);

        float yPosition = 50;

        // Title
        page.Graphics.DrawString("Executive Summary", font, PdfBrushes.Black,
                               new Syncfusion.Drawing.PointF(50, yPosition));
        yPosition += 40;

        // Key Metrics
        page.Graphics.DrawString("Key Financial Metrics", subFont, PdfBrushes.Black,
                               new Syncfusion.Drawing.PointF(50, yPosition));
        yPosition += 30;

        var metrics = new[]
        {
            $"Total Enterprises: {enterprises.Count}",
            $"Active Enterprises: {enterprises.Count(e => e.Status == EnterpriseStatus.Active)}",
            $"Total Citizens Served: {enterprises.Sum(e => e.CitizenCount):N0}",
            $"Total Monthly Revenue: ${enterprises.Sum(e => e.MonthlyRevenue):N2}",
            $"Total Monthly Expenses: ${enterprises.Sum(e => e.MonthlyExpenses):N2}",
            $"Net Monthly Balance: ${enterprises.Sum(e => e.MonthlyBalance):N2}",
            $"Average Rate: ${enterprises.Average(e => e.CurrentRate):N2}"
        };

        foreach (var metric in metrics)
        {
            page.Graphics.DrawString(metric, normalFont, PdfBrushes.Black,
                                   new Syncfusion.Drawing.PointF(70, yPosition));
            yPosition += 20;
        }

        yPosition += 20;

        // Enterprise Type Breakdown
        page.Graphics.DrawString("Enterprise Type Breakdown", subFont, PdfBrushes.Black,
                               new Syncfusion.Drawing.PointF(50, yPosition));
        yPosition += 30;

        var typeBreakdown = enterprises.GroupBy(e => e.Type)
            .Select(g => $"{g.Key}: {g.Count()} enterprises")
            .ToArray();

        foreach (var breakdown in typeBreakdown)
        {
            page.Graphics.DrawString(breakdown, normalFont, PdfBrushes.Black,
                                   new Syncfusion.Drawing.PointF(70, yPosition));
            yPosition += 20;
        }
    }

    /// <summary>
    /// Groups enterprises by type
    /// </summary>
    [RelayCommand]
    private void GroupByType()
    {
        // TODO: Implement grouping by type
    }

    /// <summary>
    /// Groups enterprises by status
    /// </summary>
    [RelayCommand]
    private void GroupByStatus()
    {
        // TODO: Implement grouping by status
    }

    /// <summary>
    /// Clears all grouping
    /// </summary>
    [RelayCommand]
    private void ClearGrouping()
    {
        // TODO: Implement clear grouping
    }

    /// <summary>
    /// Performs rate analysis
    /// </summary>
    [RelayCommand]
    private async Task RateAnalysisAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // TODO: Implement rate analysis
            await Task.Delay(1000, cancellationToken); // Placeholder
        }, statusMessage: "Performing rate analysis...");
    }

    /// <summary>
    /// Edits the selected enterprise
    /// </summary>
    [RelayCommand]
    private void EditEnterprise()
    {
        // TODO: Implement edit enterprise dialog/logic
    }

    /// <summary>
    /// Exports selected enterprises
    /// </summary>
    [RelayCommand]
    private async Task ExportSelectionAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // TODO: Implement export selection
            await Task.Delay(1000, cancellationToken); // Placeholder
        }, statusMessage: "Exporting selection...");
    }

    /// <summary>
    /// Copies data to clipboard
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard()
    {
        // TODO: Implement copy to clipboard
    }

    /// <summary>
    /// Generates enterprise report
    /// </summary>
    [RelayCommand]
    private async Task GenerateEnterpriseReportAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // TODO: Implement report generation
            await Task.Delay(1000, cancellationToken); // Placeholder
        }, statusMessage: "Generating report...");
    }

    /// <summary>
    /// Views enterprise history
    /// </summary>
    [RelayCommand]
    private void ViewEnterpriseHistory()
    {
        // TODO: Implement view history
    }

    /// <summary>
    /// Applies current filters to the enterprise list
    /// </summary>
    private void ApplyFilters()
    {
        var filtered = Enterprises.Where(e =>
        {
            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !e.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !e.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !e.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Status filter
            if (SelectedStatusFilter.HasValue && e.Status != SelectedStatusFilter.Value)
            {
                return false;
            }

            return true;
        });

        // Apply advanced filters
        foreach (var advancedFilter in AdvancedFilters.Where(f => f.IsEnabled))
        {
            filtered = filtered.Where(e => advancedFilter.Matches(e));
        }

        _filteredEnterprises.Clear();
        foreach (var item in filtered)
        {
            _filteredEnterprises.Add(item);
        }
        BuildHierarchicalStructure(filtered);
    }

    /// <summary>
    /// Builds hierarchical structure grouped by enterprise type
    /// </summary>
    private void BuildHierarchicalStructure(IEnumerable<Enterprise> enterprises)
    {
        var groupedByType = enterprises.GroupBy(e => e.Type ?? "Unspecified");

        var hierarchicalNodes = new ObservableCollection<EnterpriseNode>();

        foreach (var group in groupedByType.OrderBy(g => g.Key))
        {
            var typeNode = new EnterpriseNode
            {
                Name = $"{group.Key} ({group.Count()} enterprises)",
                Children = new ObservableCollection<EnterpriseNode>(
                    group.OrderBy(e => e.Name).Select(e => new EnterpriseNode
                    {
                        Name = e.Name,
                        Enterprise = e
                    })
                )
            };
            hierarchicalNodes.Add(typeNode);
        }

        HierarchicalEnterprises.Clear();
        foreach (var node in hierarchicalNodes)
        {
            HierarchicalEnterprises.Add(node);
        }

        // Notify that paging properties may have changed
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(TotalItemCount));
    }

    /// <summary>
    /// Updates the paged data collection based on current page and page size
    /// </summary>
    private void UpdatePagedData()
    {
        PagedHierarchicalEnterprises.Clear();

        if (!HierarchicalEnterprises.Any())
            return;

        var startIndex = CurrentPageIndex * PageSize;
        var endIndex = Math.Min(startIndex + PageSize, HierarchicalEnterprises.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            PagedHierarchicalEnterprises.Add(HierarchicalEnterprises[i]);
        }

        OnPropertyChanged(nameof(TotalItemCount));
    }

    /// <summary>
    /// Adds a new advanced filter
    /// </summary>
    [RelayCommand]
    private void AddAdvancedFilter()
    {
        var filter = new AdvancedFilter
        {
            PropertyName = "MonthlyRevenue",
            Operator = FilterOperator.GreaterThan,
            Value = "50000"
        };
        AdvancedFilters.Add(filter);
        ApplyAdvancedFilters();
    }

    /// <summary>
    /// Removes an advanced filter
    /// </summary>
    [RelayCommand]
    private void RemoveAdvancedFilter(AdvancedFilter filter)
    {
        if (filter != null)
        {
            AdvancedFilters.Remove(filter);
            ApplyAdvancedFilters();
        }
    }

    /// <summary>
    /// Applies advanced filters to the enterprise data
    /// </summary>
    private void ApplyAdvancedFilters()
    {
        var filtered = Enterprises.AsEnumerable();

        foreach (var filter in AdvancedFilters.Where(f => f.IsEnabled))
        {
            filtered = filtered.Where(e => filter.Matches(e));
        }

        _filteredEnterprises.Clear();
        foreach (var item in filtered)
        {
            _filteredEnterprises.Add(item);
        }
        BuildHierarchicalStructure(filtered);
    }

    /// <summary>
    /// Performs bulk update operations on selected enterprises
    /// </summary>
    [RelayCommand]
    private async Task BulkUpdateAsync()
    {
        var selectedEnterprises = HierarchicalEnterprises
            .SelectMany(node => node.Children)
            .Where(node => node.Enterprise?.IsSelected == true)
            .Select(node => node.Enterprise)
            .Where(e => e != null)
            .ToList();

        if (!selectedEnterprises.Any())
        {
            Logger.LogWarning("No enterprises selected for bulk update");
            return;
        }

        // TODO: Create and show BulkUpdateDialog
        // For now, just log the operation
        await ExecuteAsyncOperation((cancellationToken) =>
        {
            Logger.LogInformation("Bulk updating {Count} enterprises", selectedEnterprises.Count);
            // Placeholder for bulk update logic
            return Task.CompletedTask;
        }, statusMessage: $"Bulk updating {selectedEnterprises.Count} enterprises...");
    }

    /// <summary>
    /// Views audit history for the selected enterprise
    /// </summary>
    [RelayCommand]
    private async Task ViewAuditHistoryAsync()
    {
        if (SelectedEnterprise == null)
        {
            Logger.LogWarning("No enterprise selected for audit history");
            return;
        }

        await ExecuteAsyncOperation((cancellationToken) =>
        {
            // TODO: Implement audit history retrieval
            // For now, just log the operation
            Logger.LogInformation("Viewing audit history for enterprise {Id}: {Name}",
                                 SelectedEnterprise.Id, SelectedEnterprise.Name);
            return Task.CompletedTask;
        }, statusMessage: "Loading audit history...");
    }

    /// <summary>
    /// Calculates and displays budget summary
    /// </summary>
    public string GetBudgetSummary()
    {
        if (!Enterprises.Any())
            return "No enterprises loaded";

        var totalRevenue = Enterprises.Sum(e => e.MonthlyRevenue);
        var totalExpenses = Enterprises.Sum(e => e.MonthlyExpenses);
        var totalBalance = totalRevenue - totalExpenses;
        var totalCitizens = Enterprises.Sum(e => e.CitizenCount);

        return $"Total Revenue: ${totalRevenue.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Total Expenses: ${totalExpenses.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Monthly Balance: ${totalBalance.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Citizens Served: {totalCitizens}\n" +
               $"Status: {(totalBalance >= 0 ? "Surplus" : "Deficit")}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
