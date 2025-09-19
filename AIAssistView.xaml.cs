using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Globalization;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget;

/// <summary>
/// AI Assistant window providing xAI integration through custom chat interface
/// </summary>
public partial class AIAssistView : Window
{
    public AIAssistView()
    {
        InitializeComponent();
        DataContext = new ViewModels.AIAssistViewModel();

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        Log.Information("AI Assist View initialized");
    }

    /// <summary>
    /// Handle Enter key in message input
    /// </summary>
    private void OnMessageInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
        {
            e.Handled = true;
            SendMessage();
        }
    }

    /// <summary>
    /// Send message to AI assistant
    /// </summary>
    private void SendMessage()
    {
        var message = MessageInput.Text?.Trim();
        if (string.IsNullOrEmpty(message)) return;

        // Add user message to chat
        AddMessageToChat(message, true);

        // Clear input
        MessageInput.Text = string.Empty;

        // Process AI response (placeholder for now)
        ProcessAIResponse(message);
    }

    /// <summary>
    /// Add a message to the chat history
    /// </summary>
    private void AddMessageToChat(string message, bool isUser)
    {
        var messageBorder = new Border
        {
            Background = isUser ? new SolidColorBrush(Color.FromRgb(25, 118, 210)) : new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 400
        };

        var messageText = new TextBlock
        {
            Text = message,
            Foreground = isUser ? Brushes.White : Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };

        messageBorder.Child = messageText;
        ChatHistory.Children.Add(messageBorder);

        // Scroll to bottom
        var scrollViewer = VisualTreeHelper.GetParent(ChatHistory) as ScrollViewer;
        scrollViewer?.ScrollToBottom();
    }

    /// <summary>
    /// Process AI response (placeholder implementation)
    /// </summary>
    private async void ProcessAIResponse(string userMessage)
    {
        // Show typing indicator
        var typingBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 400
        };

        var typingText = new TextBlock
        {
            Text = "🤖 AI is thinking...",
            Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
            FontSize = 12,
            FontStyle = FontStyles.Italic
        };

        typingBorder.Child = typingText;
        ChatHistory.Children.Add(typingBorder);

        // Simulate AI processing delay
        await Task.Delay(1500);

        // Remove typing indicator
        ChatHistory.Children.Remove(typingBorder);

        // Add AI response
        var aiResponse = GenerateAIResponse(userMessage);
        AddMessageToChat(aiResponse, false);
    }

    /// <summary>
    /// Generate AI response based on user message (placeholder logic)
    /// </summary>
    private string GenerateAIResponse(string userMessage)
    {
        var message = userMessage.ToLower(CultureInfo.InvariantCulture);

        if (message.Contains("hello") || message.Contains("hi"))
        {
            return "Hello! I'm your Wiley Widget AI assistant. How can I help you today?";
        }
        else if (message.Contains("data") || message.Contains("database"))
        {
            return "I can help you with data analysis, database queries, and insights from your Wiley Widget application. What specific data are you working with?";
        }
        else if (message.Contains("quickbooks") || message.Contains("integration"))
        {
            return "I can assist with QuickBooks integration, customer synchronization, invoice processing, and troubleshooting connection issues.";
        }
        else if (message.Contains("enterprise") || message.Contains("management"))
        {
            return "For enterprise management, I can help with organization setup, user permissions, reporting, and workflow optimization.";
        }
        else if (message.Contains("error") || message.Contains("problem"))
        {
            return "I'd be happy to help troubleshoot any issues you're experiencing. Please describe the problem in detail, including any error messages.";
        }
        else
        {
            return "I'm here to help with your Wiley Widget application! I can assist with data analysis, QuickBooks integration, enterprise management, and general application guidance. What would you like to know?";
        }
    }

    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        try
        {
            var canonical = NormalizeTheme(themeName);
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
            SfSkinManager.SetTheme(this, new Theme(canonical));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
        catch
        {
            if (themeName != "FluentLight")
            {
                // Fallback
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                try { SfSkinManager.SetTheme(this, new Theme("FluentLight")); } catch { /* ignore */ }
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }
    }

    private string NormalizeTheme(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "FluentDark";
        raw = raw.Replace(" ", string.Empty);
        return raw switch
        {
            "FluentDark" => "FluentDark",
            "FluentLight" => "FluentLight",
            _ => "FluentDark"
        };
    }

    /// <summary>
    /// Static method to show the AI Assist window (following existing pattern)
    /// </summary>
    public static void ShowAIAssistWindow()
    {
        try
        {
            var aiWindow = new AIAssistView();
            aiWindow.Show();
            Log.Information("AI Assist window opened successfully");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open AI Assistant: {ex.Message}",
                          "Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
            Log.Error(ex, "Failed to open AI Assist window");
        }
    }
}