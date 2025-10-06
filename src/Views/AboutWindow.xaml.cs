using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace WileyWidget;

/// <summary>
/// Enhanced modal dialog showing comprehensive application information, features, and technical details.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // Get the ViewModel from DI container
        if (App.ServiceProvider?.GetService(typeof(ViewModels.AboutViewModel)) is ViewModels.AboutViewModel viewModel)
        {
            DataContext = viewModel;
            viewModel.CloseAction = () => Close();
        }
        else
        {
            // Fallback if DI fails - this should not happen in normal operation
            throw new InvalidOperationException("AboutViewModel could not be resolved from DI container");
        }
    }
}
