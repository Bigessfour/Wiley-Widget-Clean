using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for the Tools and Utilities panel
/// </summary>
public partial class ToolsViewModel : INotifyPropertyChanged
{
    private readonly ILogger<ToolsViewModel> _logger;

    public ToolsViewModel(ILogger<ToolsViewModel> logger)
    {
        _logger = logger;
        InitializeCalculator();
        InitializeUnitConverter();
        InitializeDateCalculator();
        InitializeNotes();

        _logger.LogInformation("ToolsViewModel initialized");
    }

    #region Calculator

    private string _calculatorDisplay = "0";
    private double _calculatorMemory = 0;
    private double _calculatorCurrentValue = 0;
    private double _calculatorPreviousValue = 0;
    private string _calculatorOperation = "";
    private bool _calculatorIsNewValue = true;

    public string CalculatorDisplay
    {
        get => _calculatorDisplay;
        set
        {
            _calculatorDisplay = value;
            OnPropertyChanged();
        }
    }

    public string CalculatorMemory => _calculatorMemory.ToString("N2");

    [RelayCommand]
    private void CalculatorNumber(string number)
    {
        if (_calculatorIsNewValue)
        {
            CalculatorDisplay = number;
            _calculatorIsNewValue = false;
        }
        else
        {
            CalculatorDisplay += number;
        }
    }

    [RelayCommand]
    private void CalculatorOperation(string operation)
    {
        if (!string.IsNullOrEmpty(_calculatorOperation))
        {
            CalculatorEquals();
        }

        if (double.TryParse(CalculatorDisplay, out _calculatorPreviousValue))
        {
            _calculatorOperation = operation;
            _calculatorIsNewValue = true;
        }
    }

    [RelayCommand]
    private void CalculatorEquals()
    {
        if (double.TryParse(CalculatorDisplay, out _calculatorCurrentValue) && !string.IsNullOrEmpty(_calculatorOperation))
        {
            double result = _calculatorOperation switch
            {
                "+" => _calculatorPreviousValue + _calculatorCurrentValue,
                "-" => _calculatorPreviousValue - _calculatorCurrentValue,
                "*" => _calculatorPreviousValue * _calculatorCurrentValue,
                "/" => _calculatorCurrentValue != 0 ? _calculatorPreviousValue / _calculatorCurrentValue : 0,
                _ => _calculatorCurrentValue
            };

            CalculatorDisplay = result.ToString();
            _calculatorOperation = "";
            _calculatorIsNewValue = true;
        }
    }

    [RelayCommand]
    private void CalculatorDecimal()
    {
        if (_calculatorIsNewValue)
        {
            CalculatorDisplay = "0.";
            _calculatorIsNewValue = false;
        }
        else if (!CalculatorDisplay.Contains("."))
        {
            CalculatorDisplay += ".";
        }
    }

    [RelayCommand]
    private void CalculatorClear()
    {
        CalculatorDisplay = "0";
        _calculatorCurrentValue = 0;
        _calculatorPreviousValue = 0;
        _calculatorOperation = "";
        _calculatorIsNewValue = true;
    }

    [RelayCommand]
    private void CalculatorClearEntry()
    {
        CalculatorDisplay = "0";
        _calculatorIsNewValue = true;
    }

    [RelayCommand]
    private void CalculatorMemoryStore()
    {
        if (double.TryParse(CalculatorDisplay, out double value))
        {
            _calculatorMemory = value;
            OnPropertyChanged(nameof(CalculatorMemory));
        }
    }

    [RelayCommand]
    private void CalculatorMemoryRecall()
    {
        CalculatorDisplay = _calculatorMemory.ToString();
        _calculatorIsNewValue = true;
    }

    [RelayCommand]
    private void CalculatorMemoryAdd()
    {
        if (double.TryParse(CalculatorDisplay, out double value))
        {
            _calculatorMemory += value;
            OnPropertyChanged(nameof(CalculatorMemory));
        }
    }

    [RelayCommand]
    private void CalculatorMemoryClear()
    {
        _calculatorMemory = 0;
        OnPropertyChanged(nameof(CalculatorMemory));
    }

    #endregion

    #region Unit Converter

    private ObservableCollection<string> _unitCategories = new();
    private string _selectedUnitCategory = "";
    private ObservableCollection<string> _fromUnits = new();
    private ObservableCollection<string> _toUnits = new();
    private string _selectedFromUnit = "";
    private string _selectedToUnit = "";
    private double _fromValue = 0;
    private string _toValue = "0";

    public ObservableCollection<string> UnitCategories => _unitCategories;
    public ObservableCollection<string> FromUnits => _fromUnits;
    public ObservableCollection<string> ToUnits => _toUnits;

    public string SelectedUnitCategory
    {
        get => _selectedUnitCategory;
        set
        {
            _selectedUnitCategory = value;
            OnPropertyChanged();
            UpdateUnitLists();
        }
    }

    public string SelectedFromUnit
    {
        get => _selectedFromUnit;
        set
        {
            _selectedFromUnit = value;
            OnPropertyChanged();
            ConvertUnits();
        }
    }

    public string SelectedToUnit
    {
        get => _selectedToUnit;
        set
        {
            _selectedToUnit = value;
            OnPropertyChanged();
            ConvertUnits();
        }
    }

    public double FromValue
    {
        get => _fromValue;
        set
        {
            _fromValue = value;
            OnPropertyChanged();
            ConvertUnits();
        }
    }

    public string ToValue
    {
        get => _toValue;
        set
        {
            _toValue = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void ConvertUnits()
    {
        if (string.IsNullOrEmpty(_selectedFromUnit) || string.IsNullOrEmpty(_selectedToUnit) || _fromValue == 0)
            return;

        double result = ConvertValue(_fromValue, _selectedFromUnit, _selectedToUnit, _selectedUnitCategory);
        ToValue = result.ToString("N4");
    }

    private double ConvertValue(double value, string fromUnit, string toUnit, string category)
    {
        // Length conversions (base: meters)
        if (category == "Length")
        {
            double meters = fromUnit switch
            {
                "Millimeters" => value / 1000,
                "Centimeters" => value / 100,
                "Meters" => value,
                "Kilometers" => value * 1000,
                "Inches" => value * 0.0254,
                "Feet" => value * 0.3048,
                "Yards" => value * 0.9144,
                "Miles" => value * 1609.344,
                _ => value
            };

            return toUnit switch
            {
                "Millimeters" => meters * 1000,
                "Centimeters" => meters * 100,
                "Meters" => meters,
                "Kilometers" => meters / 1000,
                "Inches" => meters / 0.0254,
                "Feet" => meters / 0.3048,
                "Yards" => meters / 0.9144,
                "Miles" => meters / 1609.344,
                _ => meters
            };
        }

        // Weight conversions (base: kilograms)
        if (category == "Weight")
        {
            double kg = fromUnit switch
            {
                "Grams" => value / 1000,
                "Kilograms" => value,
                "Ounces" => value * 0.0283495,
                "Pounds" => value * 0.453592,
                "Stones" => value * 6.35029,
                "Tons" => value * 1000,
                _ => value
            };

            return toUnit switch
            {
                "Grams" => kg * 1000,
                "Kilograms" => kg,
                "Ounces" => kg / 0.0283495,
                "Pounds" => kg / 0.453592,
                "Stones" => kg / 6.35029,
                "Tons" => kg / 1000,
                _ => kg
            };
        }

        // Temperature conversions
        if (category == "Temperature")
        {
            if (fromUnit == "Celsius" && toUnit == "Fahrenheit")
                return (value * 9/5) + 32;
            if (fromUnit == "Fahrenheit" && toUnit == "Celsius")
                return (value - 32) * 5/9;
            if (fromUnit == "Celsius" && toUnit == "Kelvin")
                return value + 273.15;
            if (fromUnit == "Kelvin" && toUnit == "Celsius")
                return value - 273.15;
            if (fromUnit == "Fahrenheit" && toUnit == "Kelvin")
                return (value - 32) * 5/9 + 273.15;
            if (fromUnit == "Kelvin" && toUnit == "Fahrenheit")
                return (value - 273.15) * 9/5 + 32;
        }

        return value;
    }

    #endregion

    #region Date Calculator

    private DateTime _startDate = DateTime.Today;
    private ObservableCollection<string> _dateOperations = new();
    private string _selectedDateOperation = "";
    private int _dateValue = 0;
    private string _dateResult = "";

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            _startDate = value;
            OnPropertyChanged();
            CalculateDate();
        }
    }

    public ObservableCollection<string> DateOperations => _dateOperations;

    public string SelectedDateOperation
    {
        get => _selectedDateOperation;
        set
        {
            _selectedDateOperation = value;
            OnPropertyChanged();
            CalculateDate();
        }
    }

    public int DateValue
    {
        get => _dateValue;
        set
        {
            _dateValue = value;
            OnPropertyChanged();
            CalculateDate();
        }
    }

    public string DateResult
    {
        get => _dateResult;
        set
        {
            _dateResult = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void CalculateDate()
    {
        if (_dateValue == 0 || string.IsNullOrEmpty(_selectedDateOperation))
        {
            DateResult = _startDate.ToString("D");
            return;
        }

        DateTime result = _selectedDateOperation switch
        {
            "Add Days" => _startDate.AddDays(_dateValue),
            "Subtract Days" => _startDate.AddDays(-_dateValue),
            "Add Weeks" => _startDate.AddDays(_dateValue * 7),
            "Subtract Weeks" => _startDate.AddDays(-_dateValue * 7),
            "Add Months" => _startDate.AddMonths(_dateValue),
            "Subtract Months" => _startDate.AddMonths(-_dateValue),
            "Add Years" => _startDate.AddYears(_dateValue),
            "Subtract Years" => _startDate.AddYears(-_dateValue),
            _ => _startDate
        };

        DateResult = result.ToString("D");
    }

    #endregion

    #region Notes

    private string _notesText = "";

    public string NotesText
    {
        get => _notesText;
        set
        {
            _notesText = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void SaveNotes()
    {
        // In a real app, this would save to a file or database
        _logger.LogInformation("Notes saved: {Length} characters", _notesText.Length);
        // For now, just log that notes were saved
    }

    [RelayCommand]
    private void ClearNotes()
    {
        NotesText = "";
        _logger.LogInformation("Notes cleared");
    }

    #endregion

    #region Initialization

    private void InitializeCalculator()
    {
        CalculatorDisplay = "0";
    }

    private void InitializeUnitConverter()
    {
        _unitCategories.Add("Length");
        _unitCategories.Add("Weight");
        _unitCategories.Add("Temperature");

        SelectedUnitCategory = "Length";
    }

    private void UpdateUnitLists()
    {
        _fromUnits.Clear();
        _toUnits.Clear();

        switch (_selectedUnitCategory)
        {
            case "Length":
                _fromUnits.Add("Millimeters");
                _fromUnits.Add("Centimeters");
                _fromUnits.Add("Meters");
                _fromUnits.Add("Kilometers");
                _fromUnits.Add("Inches");
                _fromUnits.Add("Feet");
                _fromUnits.Add("Yards");
                _fromUnits.Add("Miles");
                break;
            case "Weight":
                _fromUnits.Add("Grams");
                _fromUnits.Add("Kilograms");
                _fromUnits.Add("Ounces");
                _fromUnits.Add("Pounds");
                _fromUnits.Add("Stones");
                _fromUnits.Add("Tons");
                break;
            case "Temperature":
                _fromUnits.Add("Celsius");
                _fromUnits.Add("Fahrenheit");
                _fromUnits.Add("Kelvin");
                break;
        }

        // Copy to ToUnits
        foreach (var unit in _fromUnits)
        {
            _toUnits.Add(unit);
        }

        SelectedFromUnit = _fromUnits.Count > 0 ? _fromUnits[0] : "";
        SelectedToUnit = _toUnits.Count > 1 ? _toUnits[1] : _toUnits[0];
    }

    private void InitializeDateCalculator()
    {
        _dateOperations.Add("Add Days");
        _dateOperations.Add("Subtract Days");
        _dateOperations.Add("Add Weeks");
        _dateOperations.Add("Subtract Weeks");
        _dateOperations.Add("Add Months");
        _dateOperations.Add("Subtract Months");
        _dateOperations.Add("Add Years");
        _dateOperations.Add("Subtract Years");

        SelectedDateOperation = "Add Days";
    }

    private void InitializeNotes()
    {
        NotesText = "Welcome to the Notes tool!\n\nUse this space for quick notes and reminders.";
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}