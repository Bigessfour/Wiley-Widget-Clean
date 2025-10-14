#nullable enable

using Prism.Events;

namespace WileyWidget.ViewModels.Messages;

/// <summary>
/// Message sent when an enterprise is changed (created, updated, deleted)
/// Used for cross-ViewModel communication via Prism EventAggregator
/// </summary>
public class EnterpriseChangedMessage : PubSubEvent<EnterpriseChangedMessage>
{
    public int EnterpriseId { get; set; }
    public string EnterpriseName { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when a budget is updated
/// </summary>
public class BudgetUpdatedMessage : PubSubEvent<BudgetUpdatedMessage>
{
    public string Context { get; set; } = string.Empty;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent when data refresh is needed
/// </summary>
public class RefreshDataMessage : PubSubEvent<RefreshDataMessage>
{
    public string ViewName { get; set; } = string.Empty;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Message sent to navigate to a specific view
/// </summary>
public class NavigationMessage : PubSubEvent<NavigationMessage>
{
    public string TargetView { get; set; } = string.Empty;
    public object? Parameter { get; set; }
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
