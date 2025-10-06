using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using System.ComponentModel;
using System.Threading;
using System.Linq;
using System.Runtime.CompilerServices;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for managing enterprise data filtering, searching, and grouping operations
/// Handles search text, status filters, advanced filters, and hierarchical data presentation
/// </summary>
public partial class EnterpriseFilterViewModel : AsyncViewModelBase
{
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly Timer _searchDebounceTimer;

    /// <summary>
    /// Collection of all enterprises for filtering
    /// </summary>
    public ThreadSafeObservableCollection<Enterprise> Enterprises { get; } = new();

    /// <summary>
    /// Search text for filtering enterprises
    /// </summary>
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();

                // Debounce search to avoid hanging on every keystroke
                _searchDebounceTimer.Change(TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>
    /// Available status options for filtering
    /// </summary>
    public ObservableCollection<EnterpriseStatus> StatusOptions { get; } = new()
    {
        EnterpriseStatus.Active,
        EnterpriseStatus.Inactive,
        EnterpriseStatus.Suspended
    };

    /// <summary>
    /// Selected status filter
    /// </summary>
    private EnterpriseStatus? _selectedStatusFilter;
    public EnterpriseStatus? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (_selectedStatusFilter != value)
            {
                _selectedStatusFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Hierarchical enterprise node for tree structure
    /// </summary>
    public class EnterpriseNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        private Enterprise? _enterprise;
        public Enterprise? Enterprise
        {
            get => _enterprise;
            set
            {
                if (_enterprise != value)
                {
                    _enterprise = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<EnterpriseNode> _children = new();
        public ObservableCollection<EnterpriseNode> Children
        {
            get => _children;
            set
            {
                if (_children != value)
                {
                    _children = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsExpanded { get; set; } = true;
        public bool HasChildren => Children.Any();

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Advanced filter for enterprise data
    /// </summary>
    public class AdvancedFilter : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _propertyName = string.Empty;
        public string PropertyName
        {
            get => _propertyName;
            set
            {
                if (_propertyName != value)
                {
                    _propertyName = value;
                    OnPropertyChanged();
                }
            }
        }

        private FilterOperator _operator = FilterOperator.Equals;
        public FilterOperator Operator
        {
            get => _operator;
            set
            {
                if (_operator != value)
                {
                    _operator = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Matches(Enterprise enterprise)
        {
            if (!IsEnabled) return true;

            var property = enterprise.GetType().GetProperty(PropertyName);
            if (property == null) return false;

            var propertyValue = property.GetValue(enterprise);
            if (propertyValue == null) return false;

            return Operator switch
            {
                FilterOperator.Equals => propertyValue.ToString() == Value,
                FilterOperator.NotEquals => propertyValue.ToString() != Value,
                FilterOperator.Contains => propertyValue.ToString()?.Contains(Value, StringComparison.OrdinalIgnoreCase) == true,
                FilterOperator.GreaterThan => CompareValues(propertyValue, Value) > 0,
                FilterOperator.LessThan => CompareValues(propertyValue, Value) < 0,
                FilterOperator.GreaterThanOrEqual => CompareValues(propertyValue, Value) >= 0,
                FilterOperator.LessThanOrEqual => CompareValues(propertyValue, Value) <= 0,
                _ => false
            };
        }

        private int CompareValues(object propertyValue, string filterValue)
        {
            if (propertyValue is decimal dec && decimal.TryParse(filterValue, out var filterDec))
                return dec.CompareTo(filterDec);
            if (propertyValue is int intVal && int.TryParse(filterValue, out var filterInt))
                return intVal.CompareTo(filterInt);
            if (propertyValue is DateTime date && DateTime.TryParse(filterValue, out var filterDate))
                return date.CompareTo(filterDate);

            return string.Compare(propertyValue.ToString(), filterValue, StringComparison.OrdinalIgnoreCase);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Filter operators for advanced filtering
    /// </summary>
    public enum FilterOperator
    {
        Equals,
        NotEquals,
        Contains,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    /// <summary>
    /// Selected node in the tree view
    /// </summary>
    private EnterpriseNode? _selectedNode;
    public EnterpriseNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
                _selectedNode = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Hierarchical enterprises collection for tree view binding
    /// </summary>
    public ObservableCollection<EnterpriseNode> HierarchicalEnterprises { get; } = new();

    /// <summary>
    /// Paged hierarchical enterprises collection for SfTreeGrid binding
    /// </summary>
    public ObservableCollection<EnterpriseNode> PagedHierarchicalEnterprises { get; } = new();

    /// <summary>
    /// Collection of advanced filters for enterprise data
    /// </summary>
    public ObservableCollection<AdvancedFilter> AdvancedFilters { get; } = new();

    /// <summary>
    /// Filtered enterprises collection for advanced filtering
    /// </summary>
    private ObservableCollection<Enterprise> _filteredEnterprises = new();
    public ObservableCollection<Enterprise> FilteredEnterprises
    {
        get => _filteredEnterprises;
        set
        {
            if (_filteredEnterprises != value)
            {
                _filteredEnterprises = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Page size for the data pager
    /// </summary>
    private int _pageSize = 50;
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (_pageSize != value)
            {
                _pageSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageCount));
                UpdatePagedData();
            }
        }
    }

    /// <summary>
    /// Current page index for the data pager
    /// </summary>
    private int _currentPageIndex = 0;
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (_currentPageIndex != value)
            {
                _currentPageIndex = value;
                OnPropertyChanged();
                UpdatePagedData();
            }
        }
    }

    /// <summary>
    /// Total number of pages based on item count and page size
    /// </summary>
    public int PageCount => HierarchicalEnterprises.Any() ? (int)Math.Ceiling(HierarchicalEnterprises.Count / (double)PageSize) : 0;

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalItemCount => HierarchicalEnterprises.Count;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseFilterViewModel(
        IEnterpriseRepository enterpriseRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<EnterpriseViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));

        // Search debounce timer - one-time timer for debouncing search input
        _searchDebounceTimer = new Timer(async _ => await DispatcherHelper.InvokeAsync(() => ApplyFilters()), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Handles SfTreeGrid selection changes
    /// </summary>
    [RelayCommand]
    public void SelectionChanged()
    {
        if (SelectedNode?.Enterprise != null)
        {
            StatusMessage = $"Selected: {SelectedNode.Enterprise.Name} - Balance: ${SelectedNode.Enterprise.MonthlyBalance:F2}";
            Logger.LogInformation("Enterprise selected: {EnterpriseName}", SelectedNode.Enterprise.Name);
        }
        else
        {
            StatusMessage = "Ready";
        }
    }

    /// <summary>
    /// Clears all filters
    /// </summary>
    [RelayCommand]
    public void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = null;
        AdvancedFilters.Clear();
        ApplyFilters();
    }

    /// <summary>
    /// Groups enterprises by type
    /// </summary>
    [RelayCommand]
    public void GroupByType()
    {
        // TODO: Implement grouping by type
        Logger.LogInformation("GroupByType requested but not yet implemented");
    }

    /// <summary>
    /// Groups enterprises by status
    /// </summary>
    [RelayCommand]
    public void GroupByStatus()
    {
        // TODO: Implement grouping by status
        Logger.LogInformation("GroupByStatus requested but not yet implemented");
    }

    /// <summary>
    /// Clears all grouping
    /// </summary>
    [RelayCommand]
    public void ClearGrouping()
    {
        // TODO: Implement clear grouping
        Logger.LogInformation("ClearGrouping requested but not yet implemented");
    }

    /// <summary>
    /// Applies current filters to the enterprise list
    /// </summary>
    public void ApplyFilters()
    {
        var filtered = Enterprises.Where(e =>
        {
            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !e.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !e.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !e.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Status filter
            if (SelectedStatusFilter.HasValue && e.Status != SelectedStatusFilter.Value)
            {
                return false;
            }

            return true;
        });

        // Apply advanced filters
        foreach (var advancedFilter in AdvancedFilters.Where(f => f.IsEnabled))
        {
            filtered = filtered.Where(e => advancedFilter.Matches(e));
        }

        _filteredEnterprises.Clear();
        foreach (var item in filtered)
        {
            _filteredEnterprises.Add(item);
        }
        BuildHierarchicalStructure(filtered);
    }

    /// <summary>
    /// Builds hierarchical structure grouped by enterprise type
    /// </summary>
    private void BuildHierarchicalStructure(IEnumerable<Enterprise> enterprises)
    {
        var groupedByType = enterprises.GroupBy(e => e.Type ?? "Unspecified");

        var hierarchicalNodes = new ObservableCollection<EnterpriseNode>();

        foreach (var group in groupedByType.OrderBy(g => g.Key))
        {
            var typeNode = new EnterpriseNode
            {
                Name = $"{group.Key} ({group.Count()} enterprises)",
                Children = new ObservableCollection<EnterpriseNode>(
                    group.OrderBy(e => e.Name).Select(e => new EnterpriseNode
                    {
                        Name = e.Name,
                        Enterprise = e
                    })
                )
            };
            hierarchicalNodes.Add(typeNode);
        }

        HierarchicalEnterprises.Clear();
        foreach (var node in hierarchicalNodes)
        {
            HierarchicalEnterprises.Add(node);
        }

        // Notify that paging properties may have changed
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(TotalItemCount));
    }

    /// <summary>
    /// Updates the paged data collection based on current page and page size
    /// </summary>
    private void UpdatePagedData()
    {
        PagedHierarchicalEnterprises.Clear();

        if (!HierarchicalEnterprises.Any())
            return;

        var startIndex = CurrentPageIndex * PageSize;
        var endIndex = Math.Min(startIndex + PageSize, HierarchicalEnterprises.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            PagedHierarchicalEnterprises.Add(HierarchicalEnterprises[i]);
        }

        OnPropertyChanged(nameof(TotalItemCount));
    }

    /// <summary>
    /// Adds a new advanced filter
    /// </summary>
    [RelayCommand]
    public void AddAdvancedFilter()
    {
        var filter = new AdvancedFilter
        {
            PropertyName = "MonthlyRevenue",
            Operator = FilterOperator.GreaterThan,
            Value = "50000"
        };
        AdvancedFilters.Add(filter);
        ApplyAdvancedFilters();
    }

    /// <summary>
    /// Removes an advanced filter
    /// </summary>
    [RelayCommand]
    public void RemoveAdvancedFilter(AdvancedFilter filter)
    {
        if (filter != null)
        {
            AdvancedFilters.Remove(filter);
            ApplyAdvancedFilters();
        }
    }

    /// <summary>
    /// Applies advanced filters to the enterprise data
    /// </summary>
    private void ApplyAdvancedFilters()
    {
        var filtered = Enterprises.AsEnumerable();

        foreach (var filter in AdvancedFilters.Where(f => f.IsEnabled))
        {
            filtered = filtered.Where(e => filter.Matches(e));
        }

        _filteredEnterprises.Clear();
        foreach (var item in filtered)
        {
            _filteredEnterprises.Add(item);
        }
        BuildHierarchicalStructure(filtered);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchDebounceTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}