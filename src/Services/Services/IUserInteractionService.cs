using System.Windows;

namespace WileyWidget.Services;

/// <summary>
/// Service for handling user interactions like showing messages and dialogs.
/// This replaces direct MessageBox.Show() calls to maintain testability and MVVM separation.
/// </summary>
public interface IUserInteractionService
{
    /// <summary>
    /// Shows a message dialog to the user.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="buttons">The buttons to show on the dialog.</param>
    /// <param name="icon">The icon to display.</param>
    /// <returns>The result of the dialog.</returns>
    MessageBoxResult ShowMessage(string message, string title = "", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None);

    /// <summary>
    /// Shows an information message dialog.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog.</param>
    void ShowInformation(string message, string title = "Information");

    /// <summary>
    /// Shows a warning message dialog.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog.</param>
    void ShowWarning(string message, string title = "Warning");

    /// <summary>
    /// Shows an error message dialog.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog.</param>
    void ShowError(string message, string title = "Error");

    /// <summary>
    /// Shows a confirmation dialog.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the dialog.</param>
    /// <returns>True if the user confirmed, false otherwise.</returns>
    bool ShowConfirmation(string message, string title = "Confirm");
}