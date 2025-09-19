using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using Serilog;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for AI Assistant functionality
/// </summary>
public partial class AIAssistViewModel : ObservableObject
{
    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

    [ObservableProperty]
    private string currentMessage = string.Empty;

    [ObservableProperty]
    private bool isTyping = false;

    /// <summary>
    /// Send message command
    /// </summary>
    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        var userMessage = CurrentMessage.Trim();
        CurrentMessage = string.Empty;

        // Add user message
        ChatMessages.Add(new ChatMessage
        {
            Content = userMessage,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        // Show typing indicator
        IsTyping = true;

        try
        {
            // Simulate AI processing
            await Task.Delay(1500);

            // Generate AI response
            var aiResponse = GenerateAIResponse(userMessage);

            // Add AI response
            ChatMessages.Add(new ChatMessage
            {
                Content = aiResponse,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating AI response");

            ChatMessages.Add(new ChatMessage
            {
                Content = "Sorry, I encountered an error processing your request. Please try again.",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
        }
    }

    /// <summary>
    /// Clear chat command
    /// </summary>
    [RelayCommand]
    private void ClearChat()
    {
        ChatMessages.Clear();
        Log.Information("Chat history cleared");
    }

    /// <summary>
    /// Export chat command
    /// </summary>
    [RelayCommand]
    private void ExportChat()
    {
        // Placeholder for chat export functionality
        Log.Information("Chat export requested");
    }

    /// <summary>
    /// Configure AI command
    /// </summary>
    [RelayCommand]
    private void ConfigureAI()
    {
        // Placeholder for AI configuration
        Log.Information("AI configuration requested");
    }

    /// <summary>
    /// Generate AI response based on user message
    /// </summary>
    private string GenerateAIResponse(string userMessage)
    {
        // This is a placeholder implementation
        // In a real implementation, this would call xAI API or another AI service

        var message = userMessage.ToLower(System.Globalization.CultureInfo.InvariantCulture);

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
        else if (message.Contains("help"))
        {
            return "I'm here to help with:\n\n• Data analysis and insights\n• QuickBooks integration support\n• Enterprise management guidance\n• Application troubleshooting\n• General application assistance\n\nWhat would you like to know?";
        }
        else
        {
            return "I'm here to help with your Wiley Widget application! I can assist with data analysis, QuickBooks integration, enterprise management, and general application guidance. What would you like to know?";
        }
    }
}

/// <summary>
/// Chat message model
/// </summary>
public class ChatMessage
{
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
}