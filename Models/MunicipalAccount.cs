#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a municipal accounting account following GASB standards
/// </summary>
public class MunicipalAccount : INotifyPropertyChanged
{
    /// <summary>
    /// Property changed event for data binding
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises PropertyChanged for a specific property
    /// </summary>
    protected void OnPropertyChanged(params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// Unique identifier for the municipal account
    /// </summary>
    [Key]
    public int Id { get; set; }

    private string _accountNumber = string.Empty;

    /// <summary>
    /// Account number following municipal accounting standards (e.g., "1010-100")
    /// </summary>
    [Required(ErrorMessage = "Account number is required")]
    [StringLength(20, ErrorMessage = "Account number cannot exceed 20 characters")]
    public string AccountNumber
    {
        get => _accountNumber;
        set
        {
            if (_accountNumber != value)
            {
                _accountNumber = value;
                OnPropertyChanged(nameof(AccountNumber), nameof(DisplayName));
            }
        }
    }

    private string _name = string.Empty;

    /// <summary>
    /// Account name/description
    /// </summary>
    [Required(ErrorMessage = "Account name is required")]
    [StringLength(100, ErrorMessage = "Account name cannot exceed 100 characters")]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name), nameof(DisplayName));
            }
        }
    }

    private AccountType _type;

    /// <summary>
    /// Type of account following GASB standards
    /// </summary>
    [Required]
    public AccountType Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged(nameof(Type), nameof(TypeDescription));
            }
        }
    }

    private FundType _fund;

    /// <summary>
    /// Fund type for governmental fund accounting
    /// </summary>
    [Required]
    public FundType Fund
    {
        get => _fund;
        set
        {
            if (_fund != value)
            {
                _fund = value;
                OnPropertyChanged(nameof(Fund), nameof(FundDescription));
            }
        }
    }

    private decimal _balance;

    /// <summary>
    /// Current account balance
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance
    {
        get => _balance;
        set
        {
            if (_balance != value)
            {
                _balance = value;
                OnPropertyChanged(nameof(Balance), nameof(FormattedBalance));
            }
        }
    }

    private decimal _budgetAmount;

    /// <summary>
    /// Budgeted amount for this account
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAmount
    {
        get => _budgetAmount;
        set
        {
            if (_budgetAmount != value)
            {
                _budgetAmount = value;
                OnPropertyChanged(nameof(BudgetAmount), nameof(Variance), nameof(VariancePercent));
            }
        }
    }

    private bool _isActive = true;

    /// <summary>
    /// Whether the account is active
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    /// <summary>
    /// QuickBooks account ID for synchronization
    /// </summary>
    [StringLength(50)]
    public string? QuickBooksId { get; set; }

    /// <summary>
    /// Last synchronized date with QuickBooks
    /// </summary>
    public DateTime? LastSyncDate { get; set; }

    /// <summary>
    /// Additional notes about the account
    /// </summary>
    [StringLength(200)]
    public string? Notes { get; set; }

    /// <summary>
    /// Calculated property: Variance between budget and actual
    /// </summary>
    [NotMapped]
    public decimal Variance => BudgetAmount - Balance;

    /// <summary>
    /// Calculated property: Variance percentage
    /// </summary>
    [NotMapped]
    public decimal VariancePercent => BudgetAmount != 0 ? (Variance / BudgetAmount) * 100 : 0;

    /// <summary>
    /// Formatted balance string
    /// </summary>
    [NotMapped]
    public string FormattedBalance => Balance >= 0
        ? $"${Balance:N2}"
        : $"(${Math.Abs(Balance):N2})";

    /// <summary>
    /// Display name combining account number and name
    /// </summary>
    [NotMapped]
    public string DisplayName => $"{AccountNumber} - {Name}";

    /// <summary>
    /// Human-readable account type description
    /// </summary>
    [NotMapped]
    public string TypeDescription => Type switch
    {
        AccountType.Asset => "Asset",
        AccountType.Liability => "Liability",
        AccountType.Equity => "Equity",
        AccountType.Revenue => "Revenue",
        AccountType.Expense => "Expense",
        _ => "Unknown"
    };

    /// <summary>
    /// Human-readable fund type description
    /// </summary>
    [NotMapped]
    public string FundDescription => Fund switch
    {
        FundType.General => "General Fund",
        FundType.Water => "Water Fund",
        FundType.Sewer => "Sewer Fund",
        FundType.Trash => "Trash Fund",
        FundType.Enterprise => "Enterprise Fund",
        _ => "Unknown"
    };
}

/// <summary>
/// Account types following GASB standards
/// </summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

/// <summary>
/// Fund types for governmental fund accounting
/// </summary>
public enum FundType
{
    General,
    Water,
    Sewer,
    Trash,
    Enterprise
}