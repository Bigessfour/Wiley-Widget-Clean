using System;

namespace WileyWidget.UiTests;

/// <summary>
/// Centralized constants for UI tests to reduce brittleness and improve maintainability.
/// All hardcoded values should be defined here instead of scattered across test files.
/// </summary>
public static class UiTestConstants
{
    /// <summary>
    /// Navigation menu item names with fallback aliases.
    /// </summary>
    public static class NavigationItems
    {
        public const string Dashboard = "Dashboard";
        public const string Enterprise = "Enterprise";
        public const string Budget = "Budget";
        public const string Analytics = "Analytics";
        public const string Settings = "Settings";
        public const string AiAssist = "AI Assist";
        public const string MunicipalAccount = "Municipal Account";
        public const string UtilityCustomer = "Utility Customer";
        
        // Alternate names for robustness
        public static readonly string[] DashboardAliases = { "Dashboard", "Home" };
        public static readonly string[] EnterpriseAliases = { "Enterprise", "EnterpriseView" };
        public static readonly string[] BudgetAliases = { "Budget", "BudgetView" };
        public static readonly string[] AnalyticsAliases = { "Analytics", "AnalyticsView" };
        public static readonly string[] SettingsAliases = { "Settings", "SettingsView", "Options" };
        public static readonly string[] AiAssistAliases = { "AI Assist", "AIAssist", "AI" };
    }
    
    /// <summary>
    /// View automation IDs used for element searches.
    /// </summary>
    public static class ViewIds
    {
        public const string DashboardView = "DashboardView";
        public const string EnterpriseView = "EnterpriseView";
        public const string BudgetView = "BudgetView";
        public const string AnalyticsView = "AnalyticsView";
        public const string SettingsView = "SettingsView";
        public const string AiAssistView = "AIAssistView";
        public const string MunicipalAccountView = "MunicipalAccountView";
        public const string UtilityCustomerView = "UtilityCustomerView";
    }
    
    /// <summary>
    /// Region automation IDs for layout verification.
    /// </summary>
    public static class RegionIds
    {
        public const string MainRegion = "MainRegion";
        public const string ContentRegion = "ContentRegion";
        public const string NavigationRegion = "NavigationRegion";
        public const string HeaderRegion = "HeaderRegion";
        public const string FooterRegion = "FooterRegion";
    }
    
    /// <summary>
    /// Keywords for detecting error dialogs and exceptions.
    /// </summary>
    public static class ErrorDialogKeywords
    {
        public static readonly string[] Keywords = 
        { 
            "Exception", 
            "Error", 
            "XamlParseException", 
            "has stopped working", 
            "stopped responding",
            "Unhandled",
            "Fatal",
            "Critical"
        };
    }
    
    /// <summary>
    /// Data indicators for verifying content is loaded.
    /// </summary>
    public static class DataIndicators
    {
        public static readonly string[] Keywords = 
        { 
            "Enterprises", 
            "Budget", 
            "Total", 
            "Count", 
            "Loading", 
            "Data" 
        };
    }
    
    /// <summary>
    /// Empty state indicators for initial/no-data scenarios.
    /// </summary>
    public static class EmptyStateIndicators
    {
        public static readonly string[] Keywords = 
        {
            "No data", 
            "No enterprises", 
            "No records", 
            "Empty", 
            "0 enterprises", 
            "No items", 
            "Getting started"
        };
    }
    
    /// <summary>
    /// Loading state indicators.
    /// </summary>
    public static class LoadingIndicators
    {
        public static readonly string[] Keywords = 
        {
            "Loading", 
            "Please wait", 
            "Progress",
            "Initializing"
        };
    }
    
    /// <summary>
    /// Timeout configurations for various test scenarios.
    /// </summary>
    public static class Timeouts
    {
        /// <summary>App launch timeout (seconds)</summary>
        public static readonly TimeSpan AppLaunch = TimeSpan.FromSeconds(30);
        
        /// <summary>Main window detection timeout (seconds)</summary>
        public static readonly TimeSpan MainWindow = TimeSpan.FromSeconds(30);
        
        /// <summary>Element search timeout (seconds)</summary>
        public static readonly TimeSpan ElementSearch = TimeSpan.FromSeconds(10);
        
        /// <summary>Element responsive check (seconds)</summary>
        public static readonly TimeSpan ElementResponsive = TimeSpan.FromSeconds(5);
        
        /// <summary>Element clickable check (seconds)</summary>
        public static readonly TimeSpan ElementClickable = TimeSpan.FromSeconds(5);
        
        /// <summary>Navigation transition (seconds)</summary>
        public static readonly TimeSpan Navigation = TimeSpan.FromSeconds(10);
        
        /// <summary>Data loading timeout (seconds)</summary>
        public static readonly TimeSpan DataLoad = TimeSpan.FromSeconds(15);
        
        /// <summary>View rendering timeout (seconds)</summary>
        public static readonly TimeSpan ViewRender = TimeSpan.FromSeconds(10);
        
        /// <summary>Short delay for UI updates (milliseconds)</summary>
        public static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(500);
        
        /// <summary>Medium delay for animations (milliseconds)</summary>
        public static readonly TimeSpan MediumDelay = TimeSpan.FromSeconds(1);
        
        /// <summary>Retry interval for polling (milliseconds)</summary>
        public static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(500);
        
        /// <summary>CI environment multiplier for timeouts</summary>
        public static readonly double CiMultiplier = 2.0;
        
        /// <summary>
        /// Gets a timeout adjusted for CI environment.
        /// </summary>
        public static TimeSpan AdjustForCi(TimeSpan timeout)
        {
            var isCi = Environment.GetEnvironmentVariable("CI") == "true" ||
                      Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            return isCi ? TimeSpan.FromMilliseconds(timeout.TotalMilliseconds * CiMultiplier) : timeout;
        }
    }
    
    /// <summary>
    /// Window title patterns for identification.
    /// </summary>
    public static class WindowTitles
    {
        public const string MainWindow = "Wiley Widget";
        public const string Pattern = "Wiley";
    }
    
    /// <summary>
    /// Syncfusion control identifiers.
    /// </summary>
    public static class SyncfusionControls
    {
        public const string Prefix = "Syncfusion";
        public const string DataGrid = "SfDataGrid";
        public const string Chart = "SfChart";
        public const string RibbonControl = "RibbonControlAdv";
        public const string DockingManager = "DockingManager";
    }
}
