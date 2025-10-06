#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a municipal department for organizing accounts
/// </summary>
public class Department : INotifyPropertyChanged
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
    /// Unique identifier for the department
    /// </summary>
    [Key]
    public int Id { get; set; }

    private string _code = string.Empty;

    /// <summary>
    /// Department code (e.g., "GEN GOVT", "HWY&ST", "WATER")
    /// </summary>
    [Required(ErrorMessage = "Department code is required")]
    [StringLength(20, ErrorMessage = "Department code cannot exceed 20 characters")]
    public string Code
    {
        get => _code;
        set
        {
            if (_code != value)
            {
                _code = value;
                OnPropertyChanged(nameof(Code));
            }
        }
    }

    private string _name = string.Empty;

    /// <summary>
    /// Department name (e.g., "General Government", "Highways & Streets")
    /// </summary>
    [Required(ErrorMessage = "Department name is required")]
    [StringLength(100, ErrorMessage = "Department name cannot exceed 100 characters")]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Fund type this department belongs to
    /// </summary>
    [Required]
    public FundType Fund { get; set; }

    /// <summary>
    /// Parent department for hierarchical organization (null for root departments)
    /// </summary>
    public int? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }

    /// <summary>
    /// Child departments in the hierarchy
    /// </summary>
    public ICollection<Department> ChildDepartments { get; set; } = new List<Department>();

    /// <summary>
    /// Accounts belonging to this department
    /// </summary>
    public ICollection<MunicipalAccount> Accounts { get; set; } = new List<MunicipalAccount>();

    /// <summary>
    /// Display name combining code and name
    /// </summary>
    [NotMapped]
    public string DisplayName => $"{Code} - {Name}";
}