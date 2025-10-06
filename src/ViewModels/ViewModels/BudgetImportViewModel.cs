#nullable enable
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.Services.Excel;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for budget import operations with hierarchical account management
/// </summary>
public partial class BudgetImportViewModel : AsyncViewModelBase
{
    private readonly IMunicipalAccountRepository _accountRepository;
    private readonly BudgetImportService _importService;
    private readonly AppDbContext _context;

    /// <summary>
    /// Collection of departments for the current budget import
    /// </summary>
    public ObservableCollection<Department> Departments { get; } = new();

    /// <summary>
    /// Collection of root-level accounts (hierarchical structure)
    /// </summary>
    public ObservableCollection<MunicipalAccount> RootAccounts { get; } = new();

    /// <summary>
    /// Collection of all accounts (flat list for data binding)
    /// </summary>
    public ObservableCollection<MunicipalAccount> AllAccounts { get; } = new();

    /// <summary>
    /// Currently selected department for filtering
    /// </summary>
    [ObservableProperty]
    private Department? selectedDepartment;

    /// <summary>
    /// Currently selected account in the hierarchy
    /// </summary>
    [ObservableProperty]
    private MunicipalAccount? selectedAccount;

    /// <summary>
    /// Budget year for the import
    /// </summary>
    [ObservableProperty]
    private int budgetYear = DateTime.Now.Year;

    /// <summary>
    /// Budget period name
    /// </summary>
    [ObservableProperty]
    private string budgetPeriodName = $"Budget {DateTime.Now.Year}";

    /// <summary>
    /// Whether to validate GASB compliance during import
    /// </summary>
    [ObservableProperty]
    private bool validateGASBCompliance = true;

    /// <summary>
    /// Whether to create a new budget period or update existing
    /// </summary>
    [ObservableProperty]
    private bool createNewBudgetPeriod = true;

    /// <summary>
    /// Whether to overwrite existing accounts
    /// </summary>
    [ObservableProperty]
    private bool overwriteExistingAccounts;

    /// <summary>
    /// Import progress percentage (0-100)
    /// </summary>
    [ObservableProperty]
    private double importProgress;

    /// <summary>
    /// Number of accounts imported
    /// </summary>
    [ObservableProperty]
    private int accountsImported;

    /// <summary>
    /// Number of departments imported
    /// </summary>
    [ObservableProperty]
    private int departmentsImported;

    /// <summary>
    /// Import statistics
    /// </summary>
    [ObservableProperty]
    private ImportStatistics importStatistics = new();

    /// <summary>
    /// Whether import statistics are visible
    /// </summary>
    [ObservableProperty]
    private bool showImportStatistics;

    /// <summary>
    /// Collection of import messages (errors, warnings, info)
    /// </summary>
    public ObservableCollection<ImportMessage> ImportMessages { get; } = new();

    /// <summary>
    /// Initializes a new instance of the BudgetImportViewModel class.
    /// </summary>
    public BudgetImportViewModel(
        IMunicipalAccountRepository accountRepository,
        BudgetImportService importService,
        AppDbContext context,
        IDispatcherHelper dispatcherHelper,
        ILogger<BudgetImportViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Loads departments and existing accounts for the current budget year
    /// </summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // Load departments
            var departments = await _context.Departments
                .OrderBy(d => d.Name)
                .ToListAsync(cancellationToken);

            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                Departments.Clear();
                foreach (var dept in departments)
                {
                    Departments.Add(dept);
                }
            });

            // Load existing accounts for the current budget year
            var budgetPeriod = await _context.BudgetPeriods
                .FirstOrDefaultAsync(bp => bp.Year == BudgetYear, cancellationToken);

            if (budgetPeriod != null)
            {
                var accounts = await _context.MunicipalAccounts
                    .Include(a => a.Department)
                    .Include(a => a.ChildAccounts)
                    .Where(a => a.BudgetPeriodId == budgetPeriod.Id)
                    .OrderBy(a => a.AccountNumber.Value)
                    .ToListAsync(cancellationToken);

                await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
                {
                    AllAccounts.Clear();
                    RootAccounts.Clear();

                    foreach (var account in accounts)
                    {
                        AllAccounts.Add(account);
                        if (account.ParentAccountId == null)
                        {
                            RootAccounts.Add(account);
                        }
                    }
                });
            }
        }, statusMessage: "Loading budget data...");
    }

    /// <summary>
    /// Imports budget data from Excel file
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportBudgetAsync()
    {
        // This would integrate with Excel import functionality
        // For now, this is a placeholder for the import logic
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            ImportProgress = 0;
            ShowImportStatistics = false;
            ImportMessages.Clear();

            // Simulate import progress
            for (int i = 0; i <= 100; i += 10)
            {
                ImportProgress = i;
                await Task.Delay(100, cancellationToken);
            }

            // Update statistics
            ImportStatistics = new ImportStatistics
            {
                AccountsImported = 150,
                DepartmentsImported = 12,
                Errors = 2,
                Warnings = 5
            };

            ShowImportStatistics = true;
            AccountsImported = 150;
            DepartmentsImported = 12;

            // Add sample messages
            ImportMessages.Add(new ImportMessage("Import completed successfully", MessageType.Success));
            ImportMessages.Add(new ImportMessage("2 validation errors found", MessageType.Warning));
            ImportMessages.Add(new ImportMessage("5 accounts had warnings", MessageType.Info));

        }, statusMessage: "Importing budget data...");
    }

    /// <summary>
    /// Validates the current import configuration
    /// </summary>
    [RelayCommand]
    private async Task ValidateImportAsync()
    {
        await ExecuteAsyncOperation((cancellationToken) =>
        {
            ImportMessages.Clear();

            // Basic validation
            if (string.IsNullOrWhiteSpace(BudgetPeriodName))
            {
                ImportMessages.Add(new ImportMessage("Budget period name is required", MessageType.Error));
            }

            if (BudgetYear < DateTime.Now.Year - 1 || BudgetYear > DateTime.Now.Year + 5)
            {
                ImportMessages.Add(new ImportMessage("Budget year seems invalid", MessageType.Warning));
            }

            if (!Departments.Any())
            {
                ImportMessages.Add(new ImportMessage("No departments loaded - run Load Data first", MessageType.Warning));
            }

            if (ImportMessages.All(m => m.Type != MessageType.Error))
            {
                ImportMessages.Add(new ImportMessage("Validation passed", MessageType.Success));
            }

            return Task.CompletedTask;
        }, statusMessage: "Validating import configuration...");
    }

    /// <summary>
    /// Clears all import data and resets the view model
    /// </summary>
    [RelayCommand]
    private void ClearData()
    {
        Departments.Clear();
        RootAccounts.Clear();
        AllAccounts.Clear();
        ImportMessages.Clear();
        ImportProgress = 0;
        ShowImportStatistics = false;
        SelectedDepartment = null;
        SelectedAccount = null;
        ImportStatistics = new ImportStatistics();
        AccountsImported = 0;
        DepartmentsImported = 0;
    }

    /// <summary>
    /// Gets whether the import command can execute
    /// </summary>
    public bool CanImport => !IsLoading && !string.IsNullOrWhiteSpace(BudgetPeriodName) && Departments.Any();

    /// <summary>
    /// Gets child accounts for a given parent account
    /// </summary>
    public IEnumerable<MunicipalAccount> GetChildAccounts(MunicipalAccount parentAccount)
    {
        return AllAccounts.Where(a => a.ParentAccountId == parentAccount.Id);
    }

    /// <summary>
    /// Gets accounts for a specific department
    /// </summary>
    public IEnumerable<MunicipalAccount> GetAccountsForDepartment(Department department)
    {
        return AllAccounts.Where(a => a.DepartmentId == department.Id);
    }

    /// <summary>
    /// Handles property changes to update command states
    /// </summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(BudgetPeriodName) or nameof(IsLoading))
        {
            ImportBudgetCommand.NotifyCanExecuteChanged();
        }
    }
}

/// <summary>
/// Import statistics for display
/// </summary>
public class ImportStatistics
{
    /// <summary>
    /// Number of accounts imported
    /// </summary>
    public int AccountsImported { get; set; }

    /// <summary>
    /// Number of departments imported
    /// </summary>
    public int DepartmentsImported { get; set; }

    /// <summary>
    /// Number of errors encountered
    /// </summary>
    public int Errors { get; set; }

    /// <summary>
    /// Number of warnings generated
    /// </summary>
    public int Warnings { get; set; }
}

/// <summary>
/// Message for import operations
/// </summary>
public class ImportMessage
{
    /// <summary>
    /// The message text
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// The message type
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// Timestamp of the message
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Initializes a new instance of the ImportMessage class
    /// </summary>
    public ImportMessage(string message, MessageType type)
    {
        Message = message;
        Type = type;
    }
}

/// <summary>
/// Message type enumeration
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Information message
    /// </summary>
    Info,

    /// <summary>
    /// Warning message
    /// </summary>
    Warning,

    /// <summary>
    /// Error message
    /// </summary>
    Error,

    /// <summary>
    /// Success message
    /// </summary>
    Success
}