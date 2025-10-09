#nullable enable

namespace WileyWidget.ViewModels.Messages;

/// <summary>
/// Message sent when an enterprise is changed (created, updated, deleted)
/// Used for cross-ViewModel communication via WeakReferenceMessenger
/// </summary>
public class EnterpriseChangedMessage
{
    public int EnterpriseId { get; set; }
    public string EnterpriseName { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when a budget is updated
/// </summary>
public class BudgetUpdatedMessage
{
    public string Context { get; set; } = string.Empty;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when data refresh is needed
/// </summary>
public class RefreshDataMessage
{
    public string ViewName { get; set; } = string.Empty;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Type of change made to an entity
/// </summary>
public enum ChangeType
{
    Created,
    Updated,
    Deleted,
    Restored
}
