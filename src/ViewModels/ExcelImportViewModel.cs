#nullable enable

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for Excel import functionality
/// </summary>
public partial class ExcelImportViewModel : AsyncViewModelBase
{
    /// <summary>
    /// Self-reference for data binding
    /// </summary>
    public ExcelImportViewModel ViewModel => this;

    /// <summary>
    /// Selected file path
    /// </summary>
    [ObservableProperty]
    private string selectedFilePath = string.Empty;

    /// <summary>
    /// Browse file command
    /// </summary>
    public ICommand BrowseFileCommand { get; }

    /// <summary>
    /// Validate GASB compliance
    /// </summary>
    [ObservableProperty]
    private bool validateGASBCompliance = true;

    /// <summary>
    /// Create new budget period
    /// </summary>
    [ObservableProperty]
    private bool createNewBudgetPeriod;

    /// <summary>
    /// Overwrite existing accounts
    /// </summary>
    [ObservableProperty]
    private bool overwriteExistingAccounts;

    /// <summary>
    /// Budget year
    /// </summary>
    [ObservableProperty]
    private string budgetYear = DateTime.Now.Year.ToString();

    /// <summary>
    /// Preview row count
    /// </summary>
    [ObservableProperty]
    private int previewRowCount;

    /// <summary>
    /// Filter text
    /// </summary>
    [ObservableProperty]
    private string filterText = string.Empty;

    /// <summary>
    /// Sort options
    /// </summary>
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "Account Number",
        "Account Name",
        "Budget Amount",
        "Actual Amount"
    };

    /// <summary>
    /// Selected sort option
    /// </summary>
    [ObservableProperty]
    private string selectedSortOption = "Account Number";

    /// <summary>
    /// Import progress
    /// </summary>
    [ObservableProperty]
    private double importProgress;

    /// <summary>
    /// Is importing
    /// </summary>
    [ObservableProperty]
    private bool isImporting;

    /// <summary>
    /// Status messages
    /// </summary>
    public ObservableCollection<string> StatusMessages { get; } = new();

    /// <summary>
    /// Show import stats
    /// </summary>
    [ObservableProperty]
    private bool showImportStats;

    /// <summary>
    /// Import stats
    /// </summary>
    [ObservableProperty]
    private ImportStatistics importStats = new();

    /// <summary>
    /// Preview command
    /// </summary>
    public ICommand PreviewCommand { get; }

    /// <summary>
    /// Can preview
    /// </summary>
    [ObservableProperty]
    private bool canPreview = true;

    /// <summary>
    /// Import command
    /// </summary>
    public ICommand ImportCommand { get; }

    /// <summary>
    /// Can import
    /// </summary>
    [ObservableProperty]
    private bool canImport;

    /// <summary>
    /// Cancel command
    /// </summary>
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ExcelImportViewModel(IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<ExcelImportViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        BrowseFileCommand = new RelayCommand(BrowseFile);
        PreviewCommand = new RelayCommand(Preview, () => CanPreview);
        ImportCommand = new RelayCommand(Import, () => CanImport);
        CancelCommand = new RelayCommand(Cancel, () => IsImporting);
    }

    private void BrowseFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Excel File",
            Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SelectedFilePath = openFileDialog.FileName;
            CanImport = File.Exists(SelectedFilePath);
            StatusMessages.Add($"Selected file: {Path.GetFileName(SelectedFilePath)}");
        }
    }

    private void Preview()
    {
        // TODO: Implement preview functionality
        StatusMessages.Add("Preview functionality not yet implemented");
    }

    private void Import()
    {
        // TODO: Implement import functionality
        StatusMessages.Add("Import functionality not yet implemented");
    }

    private void Cancel()
    {
        // TODO: Implement cancel functionality
        IsImporting = false;
        StatusMessages.Add("Import cancelled");
    }
}

/// <summary>
/// Import statistics
/// </summary>
public class ImportStatistics
{
    /// <summary>
    /// Accounts imported
    /// </summary>
    public int AccountsImported { get; set; }

    /// <summary>
    /// Errors
    /// </summary>
    public int Errors { get; set; }

    /// <summary>
    /// Warnings
    /// </summary>
    public int Warnings { get; set; }
}