using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a budget entry for multi-year tracking
/// </summary>
public class BudgetEntry : INotifyPropertyChanged
{
    private int _id;
    private int _municipalAccountId;
    private YearType _yearType;
    private EntryType _entryType;
    private decimal _amount;
    private DateTime _createdDate = DateTime.UtcNow;
    private string? _notes;

    /// <summary>
    /// Unique identifier for the budget entry
    /// </summary>
    [Key]
    public int Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Reference to the municipal account
    /// </summary>
    [Required]
    public int MunicipalAccountId
    {
        get => _municipalAccountId;
        set
        {
            if (_municipalAccountId != value)
            {
                _municipalAccountId = value;
                OnPropertyChanged();
            }
        }
    }

    public MunicipalAccount? MunicipalAccount { get; set; }

    /// <summary>
    /// Reference to the budget period
    /// </summary>
    [Required]
    public int BudgetPeriodId { get; set; }
    public BudgetPeriod? BudgetPeriod { get; set; }

    /// <summary>
    /// Type of year this entry represents
    /// </summary>
    [Required]
    public YearType YearType
    {
        get => _yearType;
        set
        {
            if (_yearType != value)
            {
                _yearType = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Type of entry (actual, estimate, budget)
    /// </summary>
    [Required]
    public EntryType EntryType
    {
        get => _entryType;
        set
        {
            if (_entryType != value)
            {
                _entryType = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The monetary amount
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount
    {
        get => _amount;
        set
        {
            if (_amount != value)
            {
                _amount = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Date this entry was created/modified
    /// </summary>
    public DateTime CreatedDate
    {
        get => _createdDate;
        set
        {
            if (_createdDate != value)
            {
                _createdDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Notes about this entry
    /// </summary>
    [StringLength(200)]
    public string? Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Types of years for budget tracking
/// </summary>
public enum YearType
{
    /// <summary>
    /// Prior year actual amounts
    /// </summary>
    Prior,

    /// <summary>
    /// Current year amounts
    /// </summary>
    Current,

    /// <summary>
    /// Budget year planned amounts
    /// </summary>
    Budget
}

/// <summary>
/// Types of budget entries
/// </summary>
public enum EntryType
{
    /// <summary>
    /// Actual historical amounts
    /// </summary>
    Actual,

    /// <summary>
    /// Estimated amounts
    /// </summary>
    Estimate,

    /// <summary>
    /// Budgeted/planned amounts
    /// </summary>
    Budget
}