using System.Windows.Controls;
using System.Windows.Input;
using WileyWidget.ViewModels;

namespace WileyWidget.Views;

/// <summary>
/// AI Assistant panel view for embedding in docking layout
/// </summary>
public partial class AIAssistPanelView : UserControl
{
    public AIAssistPanelView()
    {
        InitializeComponent();
    }

    private AIAssistViewModel? ViewModel
    {
        get => DataContext as AIAssistViewModel;
    }

    /// <summary>
    /// Handle Enter key in message input
    /// </summary>
    private void OnMessageInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel != null)
        {
            ViewModel.SendMessageCommand.Execute();
            e.Handled = true;
        }
    }
}