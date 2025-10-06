using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WileyWidget.Services;
using WileyWidget.Services.Excel;

namespace WileyWidget.Views;

/// <summary>
/// Represents a sort option for the preview data.
/// </summary>
public class SortOption
{
    public string DisplayName { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public bool IsDescending { get; set; }

    public SortOption(string displayName, string propertyName, bool isDescending = false)
    {
        DisplayName = displayName;
        PropertyName = propertyName;
        IsDescending = isDescending;
    }
}

/// <summary>
/// Represents a hierarchical preview item for Excel import data.
/// </summary>
public class PreviewItem : ObservableObject
{
    private bool _isExpanded = true;

    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Fund { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsParent { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<PreviewItem> Children { get; } = new();

    public bool HasChildren => Children.Any();

    // Computed properties for display
    public string DisplayName => string.IsNullOrEmpty(AccountName) ? AccountNumber : $"{AccountNumber} - {AccountName}";
    public string FormattedBudget => BudgetAmount.ToString("C0");
    public string FormattedActual => ActualAmount.ToString("C0");
    public string FormattedVariance => Variance.ToString("C0");

    public PreviewItem(dynamic data)
    {
        // Extract data from the dynamic object
        AccountNumber = GetStringValue(data, "AccountNumber") ?? GetStringValue(data, "Account") ?? "";
        AccountName = GetStringValue(data, "AccountName") ?? GetStringValue(data, "Name") ?? GetStringValue(data, "Description") ?? "";
        Department = GetStringValue(data, "Department") ?? "";
        Fund = GetStringValue(data, "Fund") ?? "";
        BudgetAmount = GetDecimalValue(data, "Budget") ?? 0;
        ActualAmount = GetDecimalValue(data, "Actual") ?? GetDecimalValue(data, "Balance") ?? 0;
        Variance = GetDecimalValue(data, "Variance") ?? 0;
        AccountType = GetStringValue(data, "Type") ?? "";

        // Determine hierarchy level based on account number
        Level = AccountNumber.Contains('.') ? AccountNumber.Split('.').Length : 1;
        IsParent = AccountNumber.Contains('.') && AccountNumber.Split('.').Length < 3;
    }

    private static string? GetStringValue(dynamic data, string propertyName)
    {
        try
        {
            var dict = data as IDictionary<string, object>;
            if (dict != null && dict.TryGetValue(propertyName, out var value))
            {
                return value?.ToString();
            }

            var expando = data as ExpandoObject;
            if (expando != null)
            {
                return ((IDictionary<string, object?>)expando).TryGetValue(propertyName, out var val) ? val?.ToString() : null;
            }
        }
        catch
        {
            // Ignore conversion errors
        }
        return null;
    }

    private static decimal? GetDecimalValue(dynamic data, string propertyName)
    {
        try
        {
            var dict = data as IDictionary<string, object>;
            if (dict != null && dict.TryGetValue(propertyName, out var value))
            {
                return Convert.ToDecimal(value);
            }

            var expando = data as ExpandoObject;
            if (expando != null && ((IDictionary<string, object?>)expando).TryGetValue(propertyName, out var val))
            {
                return Convert.ToDecimal(val);
            }
        }
        catch
        {
            // Ignore conversion errors
        }
        return null;
    }
}

/// <summary>
/// ViewModel for the Excel import functionality.
/// </summary>
public partial class ExcelImportViewModel : ObservableObject
{
    private readonly IBudgetImporter _budgetImporter;
    private readonly BudgetImportService _importService;
    private readonly GASBValidator _gasbValidator;
    private readonly MunicipalAccountingService _accountingService;
    private readonly ILogger<ExcelImportViewModel> _logger;

    [ObservableProperty]
    private string selectedFilePath = string.Empty;

    [ObservableProperty]
    private bool validateGASBCompliance = true;

    [ObservableProperty]
    private bool createNewBudgetPeriod = true;

    [ObservableProperty]
    private bool overwriteExistingAccounts = false;

    [ObservableProperty]
    private int budgetYear = DateTime.Now.Year;

    [ObservableProperty]
    private ObservableCollection<PreviewItem> hierarchicalPreviewData = new();

    [ObservableProperty]
    private ObservableCollection<dynamic> previewData = new();

    [ObservableProperty]
    private int previewRowCount;

    [ObservableProperty]
    private ObservableCollection<string> statusMessages = new();

    [ObservableProperty]
    private bool isImporting;

    [ObservableProperty]
    private double importProgress;

    [ObservableProperty]
    private ImportStatistics importStats = new();

    [ObservableProperty]
    private bool showImportStats;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SortOption> sortOptions = new();

    [ObservableProperty]
    private SortOption? selectedSortOption;

    /// <summary>
    /// Initializes a new instance of the ExcelImportViewModel class.
    /// </summary>
    /// <param name="budgetImporter">The budget importer service.</param>
    /// <param name="importService">The budget import service.</param>
    /// <param name="gasbValidator">The GASB validator.</param>
    /// <param name="accountingService">The accounting service.</param>
    /// <param name="logger">Logger instance.</param>
    public ExcelImportViewModel(
        IBudgetImporter budgetImporter,
        BudgetImportService importService,
        GASBValidator gasbValidator,
        MunicipalAccountingService accountingService,
        ILogger<ExcelImportViewModel> logger)
    {
        _budgetImporter = budgetImporter ?? throw new ArgumentNullException(nameof(budgetImporter));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _gasbValidator = gasbValidator ?? throw new ArgumentNullException(nameof(gasbValidator));
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        BudgetYear = DateTime.Now.Year;
        InitializeSortOptions();
    }

    /// <summary>
    /// Initializes the sort options.
    /// </summary>
    private void InitializeSortOptions()
    {
        SortOptions = new ObservableCollection<SortOption>
        {
            new SortOption("Account Number (A-Z)", "AccountNumber"),
            new SortOption("Account Number (Z-A)", "AccountNumber", true),
            new SortOption("Account Name (A-Z)", "AccountName"),
            new SortOption("Account Name (Z-A)", "AccountName", true),
            new SortOption("Department (A-Z)", "Department"),
            new SortOption("Department (Z-A)", "Department", true),
            new SortOption("Budget Amount (Low-High)", "BudgetAmount"),
            new SortOption("Budget Amount (High-Low)", "BudgetAmount", true),
            new SortOption("Variance (Low-High)", "Variance"),
            new SortOption("Variance (High-Low)", "Variance", true)
        };

        SelectedSortOption = SortOptions.FirstOrDefault();
    }

    /// <summary>
    /// Gets whether the preview command can execute.
    /// </summary>
    public bool CanPreview => !string.IsNullOrWhiteSpace(SelectedFilePath) && !IsImporting;

    /// <summary>
    /// Gets whether the import command can execute.
    /// </summary>
    public bool CanImport => !string.IsNullOrWhiteSpace(SelectedFilePath) && (PreviewData.Any() || HierarchicalPreviewData.Any()) && !IsImporting;

    /// <summary>
    /// Gets the browse file command.
    /// </summary>
    [RelayCommand]
    private void BrowseFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Budget Excel File",
            Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SelectedFilePath = openFileDialog.FileName;
            ClearPreview();
            AddStatusMessage($"Selected file: {System.IO.Path.GetFileName(SelectedFilePath)}");
            OnPropertyChanged(nameof(CanPreview));
        }
    }

    /// <summary>
    /// Gets the preview command.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private async Task Preview()
    {
        try
        {
            ClearStatusMessages();
            AddStatusMessage("Analyzing Excel file...");

            var options = new BudgetImportOptions
            {
                ValidateGASBCompliance = ValidateGASBCompliance,
                PreviewOnly = true
            };

            var result = await _budgetImporter.ImportBudgetAsync(SelectedFilePath, options);

            if (result.Success)
            {
                // Convert preview data to dynamic objects for DataGrid
                var previewItems = result.PreviewData?.Select(row => new PreviewRow(row)) ?? Enumerable.Empty<PreviewRow>();
                PreviewData = new ObservableCollection<dynamic>(previewItems);

                // Build hierarchical structure for TreeView
                var hierarchicalItems = result.PreviewData?.Select(row => new PreviewItem(row)) ?? Enumerable.Empty<PreviewItem>();
                HierarchicalPreviewData = new ObservableCollection<PreviewItem>(BuildHierarchy(hierarchicalItems));

                PreviewRowCount = PreviewData.Count;

                AddStatusMessage($"Preview complete: {PreviewRowCount} rows found");
                AddStatusMessage($"Detected format: {result.DetectedFormat}");

                if (result.Warnings.Any())
                {
                    foreach (var warning in result.Warnings)
                        AddStatusMessage($"Warning: {warning}", MessageType.Warning);
                }
            }
            else
            {
                AddStatusMessage("Preview failed", MessageType.Error);
                foreach (var error in result.Errors)
                    AddStatusMessage($"Error: {error}", MessageType.Error);
            }

            OnPropertyChanged(nameof(CanImport));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during preview");
            AddStatusMessage($"Preview error: {ex.Message}", MessageType.Error);
        }
    }

    /// <summary>
    /// Builds a hierarchical structure from flat preview items.
    /// </summary>
    /// <param name="items">The flat list of preview items.</param>
    /// <returns>Hierarchical collection of preview items.</returns>
    private IEnumerable<PreviewItem> BuildHierarchy(IEnumerable<PreviewItem> items)
    {
        var itemList = items.ToList();

        // Apply filtering
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            itemList = itemList.Where(item =>
                item.AccountNumber.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                item.AccountName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                item.Department.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                item.Fund.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // Apply sorting
        if (SelectedSortOption != null)
        {
            itemList = SelectedSortOption.PropertyName switch
            {
                "AccountNumber" => SelectedSortOption.IsDescending
                    ? itemList.OrderByDescending(x => x.AccountNumber).ToList()
                    : itemList.OrderBy(x => x.AccountNumber).ToList(),
                "AccountName" => SelectedSortOption.IsDescending
                    ? itemList.OrderByDescending(x => x.AccountName).ToList()
                    : itemList.OrderBy(x => x.AccountName).ToList(),
                "Department" => SelectedSortOption.IsDescending
                    ? itemList.OrderByDescending(x => x.Department).ToList()
                    : itemList.OrderBy(x => x.Department).ToList(),
                "BudgetAmount" => SelectedSortOption.IsDescending
                    ? itemList.OrderByDescending(x => x.BudgetAmount).ToList()
                    : itemList.OrderBy(x => x.BudgetAmount).ToList(),
                "Variance" => SelectedSortOption.IsDescending
                    ? itemList.OrderByDescending(x => x.Variance).ToList()
                    : itemList.OrderBy(x => x.Variance).ToList(),
                _ => itemList.OrderBy(x => x.AccountNumber).ToList()
            };
        }

        var rootItems = new List<PreviewItem>();

        // Group items by their hierarchical level
        var groupedByLevel = itemList.GroupBy(x => x.Level).OrderBy(g => g.Key);

        foreach (var levelGroup in groupedByLevel)
        {
            foreach (var item in levelGroup)
            {
                if (item.Level == 1)
                {
                    // Root level items
                    rootItems.Add(item);
                }
                else
                {
                    // Find parent based on account number
                    var parentNumber = item.Level > 1 ? string.Join(".", item.AccountNumber.Split('.').Take(item.Level - 1)) : null;
                    var parent = FindItemByAccountNumber(rootItems, parentNumber);

                    if (parent != null)
                    {
                        parent.Children.Add(item);
                    }
                    else
                    {
                        // If parent not found, add as root
                        rootItems.Add(item);
                    }
                }
            }
        }

        return rootItems.OrderBy(x => x.AccountNumber);
    }

    /// <summary>
    /// Recursively finds an item by account number in the hierarchy.
    /// </summary>
    /// <param name="items">The items to search.</param>
    /// <param name="accountNumber">The account number to find.</param>
    /// <returns>The found item or null.</returns>
    private PreviewItem? FindItemByAccountNumber(IEnumerable<PreviewItem> items, string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber))
            return null;

        foreach (var item in items)
        {
            if (item.AccountNumber == accountNumber)
                return item;

            var found = FindItemByAccountNumber(item.Children, accountNumber);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Gets the import command.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task Import()
    {
        try
        {
            IsImporting = true;
            ImportProgress = 0;
            ShowImportStats = false;
            ClearStatusMessages();

            AddStatusMessage("Starting budget import...");

            var options = new BudgetImportOptions
            {
                ValidateGASBCompliance = ValidateGASBCompliance,
                CreateNewBudgetPeriod = CreateNewBudgetPeriod,
                OverwriteExistingAccounts = OverwriteExistingAccounts,
                BudgetYear = BudgetYear
            };

            // Import with progress reporting
            var progress = new Progress<ImportProgress>(p => ImportProgress = p.PercentComplete);
            var result = await _budgetImporter.ImportBudgetAsync(SelectedFilePath, options, progress);

            if (result.Success)
            {
                AddStatusMessage("Import completed successfully!", MessageType.Success);

                // Update statistics
                ImportStats = new ImportStatistics
                {
                    AccountsImported = result.RowsImported,
                    Errors = result.Errors.Count,
                    Warnings = result.Warnings.Count
                };
                ShowImportStats = true;

                // Show detailed results
                AddStatusMessage($"Imported {result.RowsImported} accounts");
                AddStatusMessage($"Budget Period: {result.BudgetPeriod?.Name ?? "N/A"}");

                if (result.Warnings.Any())
                {
                    AddStatusMessage($"Warnings: {result.Warnings.Count}");
                    foreach (var warning in result.Warnings.Take(5)) // Show first 5 warnings
                        AddStatusMessage($"Warning: {warning}", MessageType.Warning);
                    if (result.Warnings.Count > 5)
                        AddStatusMessage($"... and {result.Warnings.Count - 5} more warnings", MessageType.Warning);
                }

                if (result.Errors.Any())
                {
                    AddStatusMessage($"Errors: {result.Errors.Count}", MessageType.Error);
                    foreach (var error in result.Errors.Take(3)) // Show first 3 errors
                        AddStatusMessage($"Error: {error}", MessageType.Error);
                }

                // Clear preview after successful import
                ClearPreview();
            }
            else
            {
                AddStatusMessage("Import failed", MessageType.Error);
                foreach (var error in result.Errors)
                    AddStatusMessage($"Error: {error}", MessageType.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during import");
            AddStatusMessage($"Import error: {ex.Message}", MessageType.Error);
        }
        finally
        {
            IsImporting = false;
            ImportProgress = 0;
        }
    }

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        IsImporting = false;
        AddStatusMessage("Import cancelled by user", MessageType.Warning);
    }

    /// <summary>
    /// Adds a status message.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <param name="type">The message type.</param>
    private void AddStatusMessage(string message, MessageType type = MessageType.Info)
    {
        var timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        StatusMessages.Add(timestampedMessage);

        // Keep only last 50 messages
        while (StatusMessages.Count > 50)
            StatusMessages.RemoveAt(0);
    }

    /// <summary>
    /// Clears all status messages.
    /// </summary>
    private void ClearStatusMessages()
    {
        StatusMessages.Clear();
    }

    /// <summary>
    /// Clears the preview data.
    /// </summary>
    private void ClearPreview()
    {
        PreviewData.Clear();
        HierarchicalPreviewData.Clear();
        PreviewRowCount = 0;
        OnPropertyChanged(nameof(CanImport));
    }

    /// <summary>
    /// Refreshes the hierarchical preview data when filter or sort options change.
    /// </summary>
    private void RefreshHierarchy()
    {
        if (PreviewData.Any())
        {
            var hierarchicalItems = PreviewData.Select(row => new PreviewItem(row));
            HierarchicalPreviewData = new ObservableCollection<PreviewItem>(BuildHierarchy(hierarchicalItems));
        }
    }

    /// <summary>
    /// Handles property changed events to update command states.
    /// </summary>
    /// <param name="e">The property changed event args.</param>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(SelectedFilePath) || e.PropertyName == nameof(IsImporting))
        {
            PreviewCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(SelectedFilePath) || e.PropertyName == nameof(PreviewData) || e.PropertyName == nameof(IsImporting))
        {
            ImportCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(FilterText) || e.PropertyName == nameof(SelectedSortOption))
        {
            RefreshHierarchy();
        }
    }
}

/// <summary>
/// Preview row for DataGrid display.
/// </summary>
public class PreviewRow : DynamicObject
{
    private readonly Dictionary<string, object> _properties = new();

    /// <summary>
    /// Initializes a new instance of the PreviewRow class.
    /// </summary>
    /// <param name="data">The row data.</param>
    public PreviewRow(IDictionary<string, object> data)
    {
        foreach (var kvp in data)
        {
            _properties[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Gets the property value.
    /// </summary>
    /// <param name="binder">The binder.</param>
    /// <param name="result">The result.</param>
    /// <returns>True if successful.</returns>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        return _properties.TryGetValue(binder.Name, out result);
    }

    /// <summary>
    /// Gets the dynamic member names.
    /// </summary>
    /// <returns>The member names.</returns>
    public override IEnumerable<string> GetDynamicMemberNames()
    {
        return _properties.Keys;
    }
}

/// <summary>
/// Import statistics.
/// </summary>
public class ImportStatistics
{
    /// <summary>
    /// Gets or sets the number of accounts imported.
    /// </summary>
    public int AccountsImported { get; set; }

    /// <summary>
    /// Gets or sets the number of errors.
    /// </summary>
    public int Errors { get; set; }

    /// <summary>
    /// Gets or sets the number of warnings.
    /// </summary>
    public int Warnings { get; set; }
}

/// <summary>
/// Message type enumeration.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Information message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning message.
    /// </summary>
    Warning,

    /// <summary>
    /// Error message.
    /// </summary>
    Error,

    /// <summary>
    /// Success message.
    /// </summary>
    Success
}