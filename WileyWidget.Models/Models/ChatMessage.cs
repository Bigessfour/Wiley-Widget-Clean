using System;
// using Syncfusion... (commented for Models project)

namespace WileyWidget.Models;

/// <summary>
/// Chat message model for AI conversations - compatible with SfAIAssistView
/// </summary>
public class ChatMessage // : TextMessage (commented for Models project)
{
    public ChatMessage()
    {
        // Set default author for AI messages
        if (!IsUser)
        {
            Author = new Author { Name = "Wiley AI Assistant" };
        }
    }

    /// <summary>
    /// Indicates if this message was sent by the user
    /// </summary>
    public bool IsUser { get; set; }

    /// <summary>
    /// Timestamp when the message was sent
    /// </summary>
    public DateTime Timestamp { get; set; }
    public string Message { get; internal set; }

    /// <summary>
    /// Creates a user message
    /// </summary>
    public static ChatMessage CreateUserMessage(string content)
    {
        return new ChatMessage
        {
            Text = content,
            Author = new Author { Name = "You" },
            IsUser = true,
            Timestamp = DateTime.Now,
            DateTime = DateTime.Now
        };
    }

    /// <summary>
    /// Creates an AI assistant message
    /// </summary>
    public static ChatMessage CreateAIMessage(string content)
    {
        return new ChatMessage
        {
            Text = content,
            Author = new Author { Name = "Wiley AI Assistant" },
            IsUser = false,
            Timestamp = DateTime.Now,
            DateTime = DateTime.Now
        };
    }
}