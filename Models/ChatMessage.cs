using System;

namespace WileyWidget.Models;

/// <summary>
/// Chat message model for AI conversations
/// </summary>
public class ChatMessage
{
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
}