#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.ViewModels.Base;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for the Tools section of the application
/// </summary>
public class ToolsViewModel : AsyncViewModelBase
{
    private const string DefaultCalculatorDisplay = "0";

    private string? _selectedTool;
    private string? _toolOutput;
    private string _calculatorDisplay = DefaultCalculatorDisplay;
    private double _calculatorMemory;
    private double _accumulator;
    private string? _pendingOperator;
    private bool _isNewCalculatorEntry = true;
    private double _fromValue;
    private double _toValue;
    private string? _selectedUnitCategory;
    private string? _selectedFromUnit;
    private string? _selectedToUnit;
    private DateTime _startDate = DateTime.Today;
    private int _dateValue = 1;
    private string? _selectedDateOperation;
    private string? _dateResult;
    private string _notesText = string.Empty;

    private readonly Dictionary<string, List<UnitConversionDefinition>> _unitConversions;

    /// <summary>
    /// Gets the collection of available tools
    /// </summary>
    public ObservableCollection<string> AvailableTools { get; } = new()
    {
        "Database Cleanup",
        "Cache Management",
        "Log Analysis",
        "Performance Diagnostics",
        "Configuration Validator"
    };

    /// <summary>
    /// Gets or sets the currently selected tool
    /// </summary>
    public string? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetProperty(ref _selectedTool, value))
            {
                ExecuteToolCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the output from the currently running tool
    /// </summary>
    public string? ToolOutput
    {
        get => _toolOutput;
        set => SetProperty(ref _toolOutput, value);
    }

    /// <summary>
    /// Gets the command to execute the selected tool
    /// </summary>
    public IAsyncRelayCommand ExecuteToolCommand { get; }

    /// <summary>
    /// Gets the command to clear the tool output
    /// </summary>
    public ICommand ClearOutputCommand { get; }

    /// <summary>
    /// Gets or sets the calculator display value
    /// </summary>
    public string CalculatorDisplay
    {
        get => _calculatorDisplay;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? DefaultCalculatorDisplay : value;
            SetProperty(ref _calculatorDisplay, sanitized);
        }
    }

    /// <summary>
    /// Gets or sets the calculator memory value
    /// </summary>
    public double CalculatorMemory
    {
        get => _calculatorMemory;
        private set => SetProperty(ref _calculatorMemory, value);
    }

    /// <summary>
    /// Gets the command for entering calculator numbers
    /// </summary>
    public IRelayCommand<string> CalculatorNumberCommand { get; }

    /// <summary>
    /// Gets the command for calculator decimal entry
    /// </summary>
    public IRelayCommand CalculatorDecimalCommand { get; }

    /// <summary>
    /// Gets the command for calculator operations (+, -, *, /)
    /// </summary>
    public IRelayCommand<string> CalculatorOperationCommand { get; }

    /// <summary>
    /// Gets the command to evaluate the calculator expression
    /// </summary>
    public IRelayCommand CalculatorEqualsCommand { get; }

    /// <summary>
    /// Gets the command to clear the calculator
    /// </summary>
    public IRelayCommand CalculatorClearCommand { get; }

    /// <summary>
    /// Gets the command to clear the current calculator entry
    /// </summary>
    public IRelayCommand CalculatorClearEntryCommand { get; }

    /// <summary>
    /// Gets the command to clear calculator memory
    /// </summary>
    public IRelayCommand CalculatorMemoryClearCommand { get; }

    /// <summary>
    /// Gets the command to recall calculator memory
    /// </summary>
    public IRelayCommand CalculatorMemoryRecallCommand { get; }

    /// <summary>
    /// Gets the command to store calculator memory
    /// </summary>
    public IRelayCommand CalculatorMemoryStoreCommand { get; }

    /// <summary>
    /// Gets the command to add the current value to memory
    /// </summary>
    public IRelayCommand CalculatorMemoryAddCommand { get; }

    /// <summary>
    /// Gets the collection of unit categories
    /// </summary>
    public ObservableCollection<string> UnitCategories { get; } = new();

    /// <summary>
    /// Gets the units available for the "from" selection
    /// </summary>
    public ObservableCollection<string> FromUnits { get; } = new();

    /// <summary>
    /// Gets the units available for the "to" selection
    /// </summary>
    public ObservableCollection<string> ToUnits { get; } = new();

    /// <summary>
    /// Gets or sets the selected unit category
    /// </summary>
    public string? SelectedUnitCategory
    {
        get => _selectedUnitCategory;
        set
        {
            if (SetProperty(ref _selectedUnitCategory, value))
            {
                UpdateUnitsForCategory();
                ConvertUnitsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the numeric value to convert
    /// </summary>
    public double FromValue
    {
        get => _fromValue;
        set
        {
            if (SetProperty(ref _fromValue, value))
            {
                ConvertUnitsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the converted value
    /// </summary>
    public double ToValue
    {
        get => _toValue;
    }

    /// <summary>
    /// Gets or sets the selected "from" unit
    /// </summary>
    public string? SelectedFromUnit
    {
        get => _selectedFromUnit;
        set
        {
            if (SetProperty(ref _selectedFromUnit, value))
            {
                ConvertUnitsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected "to" unit
    /// </summary>
    public string? SelectedToUnit
    {
        get => _selectedToUnit;
        set
        {
            if (SetProperty(ref _selectedToUnit, value))
            {
                ConvertUnitsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the command used to convert units
    /// </summary>
    public IRelayCommand ConvertUnitsCommand { get; }

    /// <summary>
    /// Gets the collection of date calculator operations
    /// </summary>
    public ObservableCollection<string> DateOperations { get; } = new();

    /// <summary>
    /// Gets or sets the selected start date
    /// </summary>
    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                CalculateDateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the value used in date calculations
    /// </summary>
    public int DateValue
    {
        get => _dateValue;
        set
        {
            if (SetProperty(ref _dateValue, value))
            {
                CalculateDateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected date operation
    /// </summary>
    public string? SelectedDateOperation
    {
        get => _selectedDateOperation;
        set
        {
            if (SetProperty(ref _selectedDateOperation, value))
            {
                CalculateDateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the formatted result of the date calculation
    /// </summary>
    public string? DateResult
    {
        get => _dateResult;
        private set => SetProperty(ref _dateResult, value);
    }

    /// <summary>
    /// Gets the command to execute the date calculation
    /// </summary>
    public IRelayCommand CalculateDateCommand { get; }

    /// <summary>
    /// Gets or sets the notes text
    /// </summary>
    public string NotesText
    {
        get => _notesText;
        set => SetProperty(ref _notesText, value);
    }

    /// <summary>
    /// Gets the command to save notes
    /// </summary>
    public IRelayCommand SaveNotesCommand { get; }

    /// <summary>
    /// Gets the command to clear notes
    /// </summary>
    public IRelayCommand ClearNotesCommand { get; }

    /// <summary>
    /// Initializes a new instance of the ToolsViewModel class
    /// </summary>
    public ToolsViewModel(Services.Threading.IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<ToolsViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        ExecuteToolCommand = new AsyncRelayCommand(ExecuteSelectedToolAsync, CanExecuteTool);
        ClearOutputCommand = new RelayCommand(ClearOutput);

        CalculatorNumberCommand = new RelayCommand<string>(AppendNumber);
        CalculatorDecimalCommand = new RelayCommand(AppendDecimal);
        CalculatorOperationCommand = new RelayCommand<string>(SetOperation, op => !string.IsNullOrWhiteSpace(op));
        CalculatorEqualsCommand = new RelayCommand(EvaluateCalculator);
        CalculatorClearCommand = new RelayCommand(ClearCalculator);
        CalculatorClearEntryCommand = new RelayCommand(ClearEntry);
        CalculatorMemoryClearCommand = new RelayCommand(() => CalculatorMemory = 0);
        CalculatorMemoryRecallCommand = new RelayCommand(RecallMemory);
        CalculatorMemoryStoreCommand = new RelayCommand(StoreMemory);
        CalculatorMemoryAddCommand = new RelayCommand(AddToMemory);

        ConvertUnitsCommand = new RelayCommand(ConvertUnits, CanConvertUnits);
        CalculateDateCommand = new RelayCommand(CalculateDate, CanCalculateDate);
        SaveNotesCommand = new RelayCommand(SaveNotes);
        ClearNotesCommand = new RelayCommand(() => NotesText = string.Empty);

        _unitConversions = CreateUnitConversionDefinitions();
        InitializeUnitConverter();
        InitializeDateCalculator();
    }

    private bool CanExecuteTool()
    {
        return !string.IsNullOrWhiteSpace(SelectedTool) && !IsBusy;
    }

    private async Task ExecuteSelectedToolAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTool))
            return;

        await ExecuteAsync(async () =>
        {
            ToolOutput = $"Executing {SelectedTool}...\n";

            // Simulate tool execution based on selected tool
            switch (SelectedTool)
            {
                case "Database Cleanup":
                    await ExecuteDatabaseCleanupAsync();
                    break;
                case "Cache Management":
                    await ExecuteCacheManagementAsync();
                    break;
                case "Log Analysis":
                    await ExecuteLogAnalysisAsync();
                    break;
                case "Performance Diagnostics":
                    await ExecutePerformanceDiagnosticsAsync();
                    break;
                case "Configuration Validator":
                    await ExecuteConfigurationValidationAsync();
                    break;
                default:
                    ToolOutput += "Unknown tool selected.\n";
                    break;
            }

            ToolOutput += $"Completed execution of {SelectedTool}.\n";
        }, $"Running {SelectedTool}...");
    }

    private async Task ExecuteDatabaseCleanupAsync()
    {
        // TODO: Implement actual database cleanup logic
        await Task.Delay(1000); // Simulate work
        ToolOutput += "Database cleanup completed. Removed 0 temporary records.\n";
    }

    private async Task ExecuteCacheManagementAsync()
    {
        // TODO: Implement actual cache management logic
        await Task.Delay(500); // Simulate work
        ToolOutput += "Cache management completed. Cache size: 0 MB\n";
    }

    private async Task ExecuteLogAnalysisAsync()
    {
        // TODO: Implement actual log analysis logic
        await Task.Delay(1500); // Simulate work
        ToolOutput += "Log analysis completed. Found 0 errors, 0 warnings.\n";
    }

    private async Task ExecutePerformanceDiagnosticsAsync()
    {
        // TODO: Implement actual performance diagnostics logic
        await Task.Delay(2000); // Simulate work
        ToolOutput += "Performance diagnostics completed. All systems operating normally.\n";
    }

    private async Task ExecuteConfigurationValidationAsync()
    {
        // TODO: Implement actual configuration validation logic
        await Task.Delay(800); // Simulate work
        ToolOutput += "Configuration validation completed. All settings are valid.\n";
    }

    private void ClearOutput()
    {
        ToolOutput = string.Empty;
    }

    private void AppendNumber(string? digit)
    {
        if (string.IsNullOrWhiteSpace(digit))
        {
            return;
        }

        if (_isNewCalculatorEntry || CalculatorDisplay.Equals(DefaultCalculatorDisplay, StringComparison.Ordinal))
        {
            CalculatorDisplay = digit;
        }
        else
        {
            CalculatorDisplay += digit;
        }

        _isNewCalculatorEntry = false;
    }

    private void AppendDecimal()
    {
        if (_isNewCalculatorEntry)
        {
            CalculatorDisplay = "0.";
            _isNewCalculatorEntry = false;
            return;
        }

        if (!CalculatorDisplay.Contains('.', StringComparison.Ordinal))
        {
            CalculatorDisplay += CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        }
    }

    private void SetOperation(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return;
        }

        if (double.TryParse(CalculatorDisplay, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            if (_pendingOperator is null)
            {
                _accumulator = value;
            }
            else
            {
                _accumulator = ApplyPendingOperation(_accumulator, value, _pendingOperator);
                CalculatorDisplay = _accumulator.ToString(CultureInfo.CurrentCulture);
            }
        }

        _pendingOperator = operation;
        _isNewCalculatorEntry = true;
    }

    private void EvaluateCalculator()
    {
        if (_pendingOperator is null)
        {
            return;
        }

        if (double.TryParse(CalculatorDisplay, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            _accumulator = ApplyPendingOperation(_accumulator, value, _pendingOperator);
            CalculatorDisplay = _accumulator.ToString(CultureInfo.CurrentCulture);
        }

        _pendingOperator = null;
        _isNewCalculatorEntry = true;
    }

    private static double ApplyPendingOperation(double left, double right, string operation)
    {
        return operation switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" when Math.Abs(right) > double.Epsilon => left / right,
            "/" => 0,
            _ => right
        };
    }

    private void ClearCalculator()
    {
        _accumulator = 0;
        _pendingOperator = null;
        _isNewCalculatorEntry = true;
        CalculatorDisplay = DefaultCalculatorDisplay;
    }

    private void ClearEntry()
    {
        CalculatorDisplay = DefaultCalculatorDisplay;
        _isNewCalculatorEntry = true;
    }

    private void RecallMemory()
    {
        CalculatorDisplay = CalculatorMemory.ToString(CultureInfo.CurrentCulture);
        _isNewCalculatorEntry = true;
    }

    private void StoreMemory()
    {
        if (double.TryParse(CalculatorDisplay, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            CalculatorMemory = value;
        }
    }

    private void AddToMemory()
    {
        if (double.TryParse(CalculatorDisplay, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            CalculatorMemory += value;
        }
    }

    private static Dictionary<string, List<UnitConversionDefinition>> CreateUnitConversionDefinitions()
    {
        return new Dictionary<string, List<UnitConversionDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Length"] = new()
            {
                new UnitConversionDefinition("Meters", value => value, value => value),
                new UnitConversionDefinition("Kilometers", value => value * 1000d, value => value / 1000d),
                new UnitConversionDefinition("Feet", value => value * 0.3048d, value => value / 0.3048d),
                new UnitConversionDefinition("Miles", value => value * 1609.344d, value => value / 1609.344d)
            },
            ["Weight"] = new()
            {
                new UnitConversionDefinition("Kilograms", value => value, value => value),
                new UnitConversionDefinition("Grams", value => value / 1000d, value => value * 1000d),
                new UnitConversionDefinition("Pounds", value => value * 0.45359237d, value => value / 0.45359237d),
                new UnitConversionDefinition("Ounces", value => value * 0.0283495231d, value => value / 0.0283495231d)
            },
            ["Temperature"] = new()
            {
                new UnitConversionDefinition("Celsius", value => value, value => value),
                new UnitConversionDefinition("Fahrenheit", value => (value - 32d) * (5d / 9d), value => (value * (9d / 5d)) + 32d),
                new UnitConversionDefinition("Kelvin", value => value - 273.15d, value => value + 273.15d)
            }
        };
    }

    private void InitializeUnitConverter()
    {
        UnitCategories.Clear();
        foreach (var category in _unitConversions.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            UnitCategories.Add(category);
        }

        SelectedUnitCategory = UnitCategories.FirstOrDefault();
        if (FromUnits.Count > 0)
        {
            SelectedFromUnit = FromUnits.First();
        }

        if (ToUnits.Count > 0)
        {
            SelectedToUnit = ToUnits.Skip(1).FirstOrDefault() ?? ToUnits.First();
        }
    }

    private void UpdateUnitsForCategory()
    {
        FromUnits.Clear();
        ToUnits.Clear();

        if (string.IsNullOrWhiteSpace(SelectedUnitCategory))
        {
            return;
        }

        if (_unitConversions.TryGetValue(SelectedUnitCategory, out var definitions))
        {
            foreach (var definition in definitions)
            {
                FromUnits.Add(definition.Name);
                ToUnits.Add(definition.Name);
            }
        }

        SelectedFromUnit = FromUnits.FirstOrDefault();
        SelectedToUnit = ToUnits.Skip(1).FirstOrDefault() ?? ToUnits.FirstOrDefault();
    }

    private bool CanConvertUnits()
    {
        return !string.IsNullOrWhiteSpace(SelectedUnitCategory)
               && !string.IsNullOrWhiteSpace(SelectedFromUnit)
               && !string.IsNullOrWhiteSpace(SelectedToUnit);
    }

    private void ConvertUnits()
    {
        if (string.IsNullOrWhiteSpace(SelectedUnitCategory)
            || string.IsNullOrWhiteSpace(SelectedFromUnit)
            || string.IsNullOrWhiteSpace(SelectedToUnit))
        {
            return;
        }

        if (!_unitConversions.TryGetValue(SelectedUnitCategory, out var conversions))
        {
            return;
        }

        var fromDefinition = conversions.FirstOrDefault(definition => string.Equals(definition.Name, SelectedFromUnit, StringComparison.OrdinalIgnoreCase));
        var toDefinition = conversions.FirstOrDefault(definition => string.Equals(definition.Name, SelectedToUnit, StringComparison.OrdinalIgnoreCase));

        if (fromDefinition is null || toDefinition is null)
        {
            return;
        }

        var baseValue = fromDefinition.ToBase(FromValue);
        var convertedValue = toDefinition.FromBase(baseValue);
        SetProperty(ref _toValue, Math.Round(convertedValue, 4), nameof(ToValue));
    }

    private void InitializeDateCalculator()
    {
        DateOperations.Clear();
        DateOperations.Add("Add Days");
        DateOperations.Add("Subtract Days");
        DateOperations.Add("Add Weeks");
        DateOperations.Add("Add Months");
        DateOperations.Add("Add Years");

        SelectedDateOperation = DateOperations.FirstOrDefault();
    }

    private bool CanCalculateDate()
    {
        return !string.IsNullOrWhiteSpace(SelectedDateOperation);
    }

    private void CalculateDate()
    {
        if (string.IsNullOrWhiteSpace(SelectedDateOperation))
        {
            DateResult = null;
            return;
        }

        DateTime result = SelectedDateOperation switch
        {
            "Add Days" => StartDate.AddDays(DateValue),
            "Subtract Days" => StartDate.AddDays(-DateValue),
            "Add Weeks" => StartDate.AddDays(DateValue * 7),
            "Add Months" => StartDate.AddMonths(DateValue),
            "Add Years" => StartDate.AddYears(DateValue),
            _ => StartDate
        };

        DateResult = result.ToString("D", CultureInfo.CurrentCulture);
    }

    private void SaveNotes()
    {
        Logger.LogInformation("Notes saved at {Timestamp}", DateTimeOffset.Now);
    }

    private sealed record UnitConversionDefinition(string Name, Func<double, double> ToBase, Func<double, double> FromBase);
}