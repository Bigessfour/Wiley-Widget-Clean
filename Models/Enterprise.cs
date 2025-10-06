#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WileyWidget.Attributes;

namespace WileyWidget.Models;

public enum EnterpriseStatus
{
    Active,
    Inactive,
    Suspended
}

/// <summary>
/// Represents a municipal enterprise (Water, Sewer, Trash, Apartments)
/// </summary>
public class Enterprise : INotifyPropertyChanged
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
    /// Unique identifier for the enterprise
    /// </summary>
    [Key]
    [GridDisplay(99, 80, Visible = true)] // Put ID at the end
    public int Id { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    private string _name = string.Empty;

    /// <summary>
    /// Name of the enterprise (Water, Sewer, Trash, Apartments)
    /// </summary>
    [Required(ErrorMessage = "Enterprise name is required")]
    [StringLength(100, ErrorMessage = "Enterprise name cannot exceed 100 characters")]
    [GridDisplay(1, 150)]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    private string _description = string.Empty;

    /// <summary>
    /// Description of the enterprise
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }
    }

    private decimal _currentRate;

    /// <summary>
    /// Current rate charged per citizen (e.g., $5.00 per month for water)
    /// </summary>
    [Required(ErrorMessage = "Current rate is required")]
    [Range(0.01, 9999.99, ErrorMessage = "Rate must be between 0.01 and 9999.99")]
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(3, 100, DecimalDigits = 2)]
    public decimal CurrentRate
    {
        get => _currentRate;
        set
        {
            if (_currentRate != value)
            {
                _currentRate = value;
                OnPropertyChanged(nameof(CurrentRate), nameof(MonthlyRevenue), nameof(MonthlyBalance), nameof(BreakEvenRate));
            }
        }
    }

    private decimal _monthlyExpenses;

    /// <summary>
    /// Monthly expenses (sum of employee compensation + maintenance + other operational costs)
    /// </summary>
    [Required(ErrorMessage = "Monthly expenses are required")]
    [Range(0, double.MaxValue, ErrorMessage = "Monthly expenses cannot be negative")]
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(5, 120, DecimalDigits = 2)]
    public decimal MonthlyExpenses
    {
        get => _monthlyExpenses;
        set
        {
            if (_monthlyExpenses != value)
            {
                _monthlyExpenses = value;
                OnPropertyChanged(nameof(MonthlyExpenses), nameof(MonthlyBalance));
            }
        }
    }

    /// <summary>
    /// Monthly revenue (calculated as CitizenCount * CurrentRate)
    /// </summary>
    [NotMapped]
    [GridDisplay(6, 120, DecimalDigits = 2)]
    public decimal MonthlyRevenue => CitizenCount * CurrentRate;

    private int _citizenCount;

    /// <summary>
    /// Number of citizens served by this enterprise
    /// </summary>
    [Required(ErrorMessage = "Citizen count is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Citizen count must be at least 1")]
    [GridDisplay(4, 80, DecimalDigits = 0)]
    public int CitizenCount
    {
        get => _citizenCount;
        set
        {
            if (_citizenCount != value)
            {
                _citizenCount = value;
                OnPropertyChanged(nameof(CitizenCount), nameof(MonthlyRevenue), nameof(MonthlyBalance), nameof(BreakEvenRate));
            }
        }
    }

    private decimal _totalBudget;

    /// <summary>
    /// Total budget allocated for this enterprise
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(8, 120, DecimalDigits = 2)]
    public decimal TotalBudget
    {
        get => _totalBudget;
        set
        {
            if (_totalBudget != value)
            {
                _totalBudget = value;
                OnPropertyChanged(nameof(TotalBudget));
            }
        }
    }

    private decimal _budgetAmount;

    /// <summary>
    /// Budget amount for this enterprise
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
                OnPropertyChanged(nameof(BudgetAmount));
            }
        }
    }

    /// <summary>
    /// Last modified date for this enterprise
    /// </summary>
    public DateTime? LastModified { get; set; }

    private string _type = string.Empty;

    /// <summary>
    /// Type/category of the enterprise
    /// </summary>
    [StringLength(50, ErrorMessage = "Type cannot exceed 50 characters")]
    [GridDisplay(2, 100)]
    public string Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
            }
        }
    }

    private string _notes = string.Empty;

    /// <summary>
    /// Additional notes about the enterprise
    /// </summary>
    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    [GridDisplay(9, 200)]
    public string Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes = value;
                OnPropertyChanged(nameof(Notes));
            }
        }
    }

    private EnterpriseStatus _status = EnterpriseStatus.Active;

    /// <summary>
    /// Operational status of the enterprise for grouping and filtering
    /// </summary>
    [GridDisplay(7, 100)]
    public EnterpriseStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    /// <summary>
    /// Convenience: Last updated timestamp (for UI binding)
    /// </summary>
    [NotMapped]
    public DateTime LastUpdated => DateTime.Now;

    /// <summary>
    /// Navigation property for budget interactions
    /// </summary>
    public virtual ICollection<BudgetInteraction> BudgetInteractions { get; set; } = new List<BudgetInteraction>();

    /// <summary>
    /// Calculated property: Monthly deficit/surplus (Revenue - Expenses)
    /// </summary>
    [NotMapped]
    [GridDisplay(7, 120, DecimalDigits = 2)]
    public decimal MonthlyBalance => MonthlyRevenue - MonthlyExpenses;

    /// <summary>
    /// Calculated property: Break-even rate needed to cover expenses
    /// </summary>
    [NotMapped]
    public decimal BreakEvenRate => CitizenCount > 0 ? MonthlyExpenses / CitizenCount : 0;
}
