using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// Base class for ViewModels that require data validation.
/// Implements INotifyDataErrorInfo for WPF data validation support.
/// </summary>
public abstract class ValidatableViewModelBase : AsyncViewModelBase, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    /// <summary>
    /// Event raised when validation errors change.
    /// </summary>
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>
    /// Gets a value indicating whether the ViewModel has validation errors.
    /// </summary>
    public bool HasErrors => _errors.Any();

    /// <summary>
    /// Initializes a new instance of the ValidatableViewModelBase class.
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations.</param>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    protected ValidatableViewModelBase(IDispatcherHelper dispatcherHelper, ILogger logger)
        : base(dispatcherHelper, logger)
    {
    }

    /// <summary>
    /// Gets validation errors for a specific property.
    /// </summary>
    /// <param name="propertyName">The name of the property to get errors for.</param>
    /// <returns>An enumerable of error messages.</returns>
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return _errors.Values.SelectMany(errors => errors);
        }

        return _errors.TryGetValue(propertyName, out var errors) ? errors : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Validates a property and updates the error collection.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <returns>True if the value is valid, false otherwise.</returns>
    protected bool ValidateProperty(object? value, [CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
            return true;

        var errors = new List<string>();

        // Perform validation based on property attributes and custom rules
        ValidatePropertyValue(value, propertyName, errors);

        // Update error collection
        if (errors.Any())
        {
            _errors[propertyName] = errors;
        }
        else
        {
            _errors.Remove(propertyName);
        }

        // Notify UI of validation changes
        OnErrorsChanged(propertyName);

        return !errors.Any();
    }

    /// <summary>
    /// Override this method to implement custom validation logic.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="propertyName">The name of the property being validated.</param>
    /// <param name="errors">The list to add validation errors to.</param>
    protected virtual void ValidatePropertyValue(object? value, string propertyName, List<string> errors)
    {
        // Base implementation - override in derived classes for specific validation
    }

    /// <summary>
    /// Clears all validation errors.
    /// </summary>
    protected void ClearErrors()
    {
        _errors.Clear();
        OnErrorsChanged(string.Empty);
    }

    /// <summary>
    /// Clears validation errors for a specific property.
    /// </summary>
    /// <param name="propertyName">The name of the property to clear errors for.</param>
    protected void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
        {
            OnErrorsChanged(propertyName);
        }
    }

    /// <summary>
    /// Adds a validation error for a property.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="error">The error message.</param>
    protected void AddError(string propertyName, string error)
    {
        if (!_errors.TryGetValue(propertyName, out var errors))
        {
            errors = new List<string>();
            _errors[propertyName] = errors;
        }

        if (!errors.Contains(error))
        {
            errors.Add(error);
            OnErrorsChanged(propertyName);
        }
    }

    /// <summary>
    /// Raises the ErrorsChanged event.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Validates all properties in the ViewModel.
    /// </summary>
    /// <returns>True if all properties are valid, false otherwise.</returns>
    public virtual bool ValidateAll()
    {
        // Override in derived classes to validate all properties
        return !HasErrors;
    }

    /// <summary>
    /// Gets all validation errors as a flat list.
    /// </summary>
    /// <returns>A list of all validation error messages.</returns>
    public IEnumerable<string> GetAllErrors()
    {
        return _errors.Values.SelectMany(errors => errors);
    }
}