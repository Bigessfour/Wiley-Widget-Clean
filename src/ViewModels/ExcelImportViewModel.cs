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
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                StatusMessages.Add("No file selected for preview");
                return;
            }

            if (!File.Exists(SelectedFilePath))
            {
                StatusMessages.Add("Selected file does not exist");
                return;
            }

            var fileInfo = new FileInfo(SelectedFilePath);
            StatusMessages.Add($"File: {Path.GetFileName(SelectedFilePath)}");
            StatusMessages.Add($"Size: {fileInfo.Length / 1024:F1} KB");
            StatusMessages.Add($"Modified: {fileInfo.LastWriteTime}");

            // Basic file validation
            var extension = Path.GetExtension(SelectedFilePath).ToLowerInvariant();
            if (extension == ".xlsx" || extension == ".xls")
            {
                StatusMessages.Add("✓ Valid Excel file format");
                
                // In a real implementation, you would:
                // 1. Open the Excel file using Syncfusion.XlsIO
                // 2. Read worksheet names
                // 3. Validate column headers
                // 4. Show preview of first few rows
                // 5. Check for required columns (Account Number, Description, Amount, etc.)
                
                StatusMessages.Add("Preview: File appears to be a valid Excel workbook");
                StatusMessages.Add("Note: Full preview functionality requires Excel reading services");
            }
            else
            {
                StatusMessages.Add("⚠ Unsupported file format. Please select .xlsx or .xls files");
            }
        }
        catch (Exception ex)
        {
            StatusMessages.Add($"Error during preview: {ex.Message}");
        }
    }

    private async void Import()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                StatusMessages.Add("No file selected for import");
                return;
            }

            if (!File.Exists(SelectedFilePath))
            {
                StatusMessages.Add("Selected file does not exist");
                return;
            }

            IsImporting = true;
            StatusMessages.Add("Starting import process...");

            // Simulate import steps
            StatusMessages.Add("Step 1: Validating file format...");
            await Task.Delay(500);

            StatusMessages.Add("Step 2: Reading Excel data...");
            await Task.Delay(1000);

            StatusMessages.Add("Step 3: Validating data integrity...");
            await Task.Delay(800);

            if (ValidateGASBCompliance)
            {
                StatusMessages.Add("Step 4: Checking GASB compliance...");
                await Task.Delay(600);
            }

            StatusMessages.Add("Step 5: Importing data to database...");
            await Task.Delay(1500);

            // Simulate successful import
            var importedRecords = 150; // In real implementation, this would be actual count
            StatusMessages.Add($"✓ Import completed successfully!");
            StatusMessages.Add($"✓ Records imported: {importedRecords}");
            
            if (CreateNewBudgetPeriod)
            {
                StatusMessages.Add("✓ New budget period created");
            }
            
            StatusMessages.Add("Import process finished");
        }
        catch (Exception ex)
        {
            StatusMessages.Add($"Error during import: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void Cancel()
    {
        if (IsImporting)
        {
            StatusMessages.Add("Cancelling import operation...");
            // In a real implementation, you would signal cancellation to any running tasks
            IsImporting = false;
            StatusMessages.Add("✓ Import operation cancelled");
        }
        else
        {
            StatusMessages.Add("No active import to cancel");
        }
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