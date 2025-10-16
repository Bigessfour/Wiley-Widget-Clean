using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for the Enterprise Dialog
/// </summary>
public partial class EnterpriseDialogViewModel : ObservableObject
{
    private readonly Window _window;
    private Enterprise _enterprise = new();

    /// <summary>
    /// Gets or sets the enterprise being edited
    /// </summary>
    public Enterprise Enterprise
    {
        get => _enterprise;
        set => SetProperty(ref _enterprise, value);
    }

    /// <summary>
    /// Gets the OK command
    /// </summary>
    public RelayCommand OkCommand => new(ExecuteOk, CanExecuteOk);

    /// <summary>
    /// Gets the Cancel command
    /// </summary>
    public RelayCommand CancelCommand => new(ExecuteCancel);

    /// <summary>
    /// Initializes a new instance of the EnterpriseDialogViewModel class
    /// </summary>
    /// <param name="window">The dialog window</param>
    public EnterpriseDialogViewModel(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    private void ExecuteOk()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(Enterprise.Name))
        {
            MessageBox.Show("Enterprise name is required.", "Validation Error",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Enterprise.Type))
        {
            MessageBox.Show("Enterprise type is required.", "Validation Error",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Enterprise.CitizenCount <= 0)
        {
            MessageBox.Show("Citizen count must be greater than zero.", "Validation Error",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _window.DialogResult = true;
    }

    private bool CanExecuteOk()
    {
        return !string.IsNullOrWhiteSpace(Enterprise.Name) &&
               !string.IsNullOrWhiteSpace(Enterprise.Type) &&
               Enterprise.CitizenCount > 0;
    }

    private void ExecuteCancel()
    {
        _window.DialogResult = false;
    }
}