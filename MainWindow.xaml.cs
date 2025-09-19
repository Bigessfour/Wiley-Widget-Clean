using System.Windows;
using Syncfusion.SfSkinManager; // Theme manager
using Syncfusion.Windows.Shared; // Theme names (if needed)
using Syncfusion.UI.Xaml.Grid; // Grid controls
using WileyWidget.Services;
using Serilog;
using System.Linq;

namespace WileyWidget;

/// <summary>
/// Primary shell window: applies persisted theme + window geometry, wires basic commands (theme switch, about),
/// and persists size/state on close. Keeps logic minimal—heavy operations belong in view models/services.
/// </summary>
public partial class MainWindow : Window
{
    // Runtime toggle for dynamic column generation (kept non-const to avoid CS0162 unreachable warning when false).
    private static readonly bool UseDynamicColumns = true; // set true to enable runtime column build

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.MainViewModel();
        // Apply persisted theme or default
        TryApplyTheme(SettingsService.Instance.Current.Theme);
        if (UseDynamicColumns)
            BuildDynamicColumns();
        RestoreWindowState();
        Loaded += (_, _) => ApplyMaximized();
        Closing += (_, _) => PersistWindowState();
    UpdateThemeToggleVisuals();
    }

    /// <summary>
    /// Dynamically builds columns for each public property of the widget model when enabled.
    /// Supports different column types based on property types and includes proper formatting.
    /// </summary>
    private void BuildDynamicColumns()
    {
        try
        {
            var vm = DataContext as ViewModels.MainViewModel;
            var items = vm?.Widgets;
            if (items == null || items.Count == 0) return;

            Grid.AutoGenerateColumns = false;
            Grid.Columns.Clear();

            var type = items[0].GetType();
            foreach (var prop in type.GetProperties())
            {
                if (!prop.CanRead) continue;

                var mappingName = prop.Name;
                var headerText = SplitCamelCase(prop.Name);

                // Create appropriate column type based on property type
                var column = CreateColumnForProperty(prop, mappingName, headerText);
                if (column != null)
                {
                    Grid.Columns.Add(column);
                }
            }

            Log.Information("Dynamic columns built successfully for {TypeName}", type.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to build dynamic columns");
            // Fallback to basic auto-generated columns
            Grid.AutoGenerateColumns = true;
        }
    }

    /// <summary>
    /// Creates the appropriate Syncfusion column type based on the property type
    /// </summary>
    private Syncfusion.UI.Xaml.Grid.GridColumn CreateColumnForProperty(System.Reflection.PropertyInfo prop, string mappingName, string headerText)
    {
        var propType = prop.PropertyType;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;

        if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
            underlyingType == typeof(short) || underlyingType == typeof(byte))
        {
            return new GridNumericColumn
            {
                MappingName = mappingName,
                HeaderText = headerText,
                NumberDecimalDigits = 0,
                Width = 100
            };
        }
        else if (underlyingType == typeof(decimal) || underlyingType == typeof(double) ||
                 underlyingType == typeof(float))
        {
            return new GridNumericColumn
            {
                MappingName = mappingName,
                HeaderText = headerText,
                NumberDecimalDigits = 2,
                Width = 120
            };
        }
        else if (underlyingType == typeof(DateTime))
        {
            return new GridDateTimeColumn
            {
                MappingName = mappingName,
                HeaderText = headerText,
                Width = 140
            };
        }
        else if (underlyingType == typeof(bool))
        {
            return new GridCheckBoxColumn
            {
                MappingName = mappingName,
                HeaderText = headerText,
                Width = 80
            };
        }
        else
        {
            // Default to text column for strings and other types
            return new GridTextColumn
            {
                MappingName = mappingName,
                HeaderText = headerText,
                Width = 150
            };
        }
    }

    /// <summary>
    /// Converts camelCase or PascalCase to readable text with spaces
    /// </summary>
    private string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = System.Text.RegularExpressions.Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        return result;
    }

    /// <summary>
    /// Copy selected items from the data grid to clipboard
    /// </summary>
    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Grid.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more items to copy.",
                              "No Selection",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            var selectedItems = Grid.SelectedItems;
            var stringBuilder = new System.Text.StringBuilder();

            // Add header row
            if (selectedItems.Count > 0 && selectedItems[0] != null)
            {
                var type = selectedItems[0].GetType();
                var properties = type.GetProperties()
                    .Where(p => p.CanRead)
                    .Select(p => SplitCamelCase(p.Name))
                    .ToArray();

                stringBuilder.AppendLine(string.Join("\t", properties));
            }

            // Add data rows
            foreach (var item in selectedItems)
            {
                if (item == null) continue;

                var type = item.GetType();
                var values = type.GetProperties()
                    .Where(p => p.CanRead)
                    .Select(p =>
                    {
                        try
                        {
                            var value = p.GetValue(item);
                            return value?.ToString() ?? "";
                        }
                        catch
                        {
                            return "";
                        }
                    })
                    .ToArray();

                stringBuilder.AppendLine(string.Join("\t", values));
            }

            Clipboard.SetText(stringBuilder.ToString());
            Log.Information("Copied {Count} items to clipboard", selectedItems.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy items to clipboard");
            MessageBox.Show($"Failed to copy items: {ex.Message}",
                          "Copy Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Paste items from clipboard to the data grid
    /// </summary>
    private void OnPaste(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show("Clipboard does not contain text data.",
                              "No Text Data",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            var clipboardText = Clipboard.GetText();
            var lines = clipboardText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2) // Need at least header + 1 data row
            {
                MessageBox.Show("Clipboard data must contain headers and at least one data row.",
                              "Invalid Format",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            var vm = DataContext as ViewModels.MainViewModel;
            if (vm == null) return;

            // Parse header to understand column mapping
            var headers = lines[0].Split('\t').Select(h => h.Trim()).ToArray();

            // Create new items from data rows
            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split('\t').Select(v => v.Trim()).ToArray();
                if (values.Length != headers.Length) continue;

                try
                {
                    vm.AddWidgetCommand.Execute(null); // This will add a new widget
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to add widget during paste operation");
                }
            }

            Log.Information("Pasted {Count} items from clipboard", lines.Length - 1);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to paste items from clipboard");
            MessageBox.Show($"Failed to paste items: {ex.Message}",
                          "Paste Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails (e.g., renamed or removed).
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
        raw = raw.Replace(" ", string.Empty); // allow "Fluent Dark" legacy
        return raw switch
        {
            "FluentDark" => "FluentDark",
            "FluentLight" => "FluentLight",
            _ => "FluentDark" // default
        };
    }

    /// <summary>Switch to Fluent Dark theme and persist choice.</summary>
    private void OnFluentDark(object sender, RoutedEventArgs e)
    {
    TryApplyTheme("FluentDark");
    SettingsService.Instance.Current.Theme = "FluentDark";
        SettingsService.Instance.Save();
    Log.Information("Theme changed to {Theme}", "Fluent Dark");
    UpdateThemeToggleVisuals();
    }
    /// <summary>Switch to Fluent Light theme and persist choice.</summary>
    private void OnFluentLight(object sender, RoutedEventArgs e)
    {
    TryApplyTheme("FluentLight");
    SettingsService.Instance.Current.Theme = "FluentLight";
        SettingsService.Instance.Save();
    Log.Information("Theme changed to {Theme}", "Fluent Light");
        UpdateThemeToggleVisuals();
    }

    private void UpdateThemeToggleVisuals()
    {
        var current = NormalizeTheme(SettingsService.Instance.Current.Theme);
        if (BtnFluentDark != null)
        {
            BtnFluentDark.IsEnabled = current != "FluentDark";
            BtnFluentDark.Label = current == "FluentDark" ? "✔ Fluent Dark" : "Fluent Dark";
        }
        if (BtnFluentLight != null)
        {
            BtnFluentLight.IsEnabled = current != "FluentLight";
            BtnFluentLight.Label = current == "FluentLight" ? "✔ Fluent Light" : "Fluent Light";
        }
    }
    /// <summary>Display modal About dialog with version information.</summary>
    private void OnAbout(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    /// <summary>
    /// Restores last known window bounds (only if previously saved). Maximized state is applied after window is loaded
    /// to avoid layout measurement issues during construction.
    /// </summary>
    private void RestoreWindowState()
    {
        var s = SettingsService.Instance.Current;
        if (s.WindowWidth.HasValue) Width = s.WindowWidth.Value;
        if (s.WindowHeight.HasValue) Height = s.WindowHeight.Value;
        if (s.WindowLeft.HasValue) Left = s.WindowLeft.Value;
        if (s.WindowTop.HasValue) Top = s.WindowTop.Value;
    }

    /// <summary>
    /// Applies persisted maximized state post-load. Separated for clarity and potential future animation hooks.
    /// </summary>
    private void ApplyMaximized()
    {
        var s = SettingsService.Instance.Current;
        if (s.WindowMaximized == true)
            WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Persists window bounds only when in Normal state to avoid capturing the restored size of a maximized window.
    /// </summary>
    private void PersistWindowState()
    {
        var s = SettingsService.Instance.Current;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowLeft = Left;
            s.WindowTop = Top;
        }
        SettingsService.Instance.Save();
    }

    /// <summary>
    /// Opens the Enterprise Management window
    /// </summary>
    private void OnEnterpriseManagement(object sender, RoutedEventArgs e)
    {
        try
        {
            EnterpriseView.ShowEnterpriseWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open Enterprise Management: {ex.Message}",
                          "Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the Budget Analysis window
    /// </summary>
    private void OnBudgetAnalysis(object sender, RoutedEventArgs e)
    {
        try
        {
            BudgetView.ShowBudgetWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open Budget Analysis: {ex.Message}",
                          "Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the AI Assistant window
    /// </summary>
    private void OnAIAssist(object sender, RoutedEventArgs e)
    {
        try
        {
            AIAssistView.ShowAIAssistWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open AI Assistant: {ex.Message}",
                          "Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the Settings window
    /// </summary>
    private void OnSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = new SettingsView
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open Settings: {ex.Message}",
                          "Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the Dashboard window
    /// </summary>
    private void OnDashboard(object sender, RoutedEventArgs e)
    {
        try
        {
            DashboardView.ShowDashboardWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open Dashboard: {ex.Message}",
                          "Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles keyboard shortcuts by delegating to existing event handlers
    /// </summary>
    private void HandleKeyboardShortcut(string action)
    {
        try
        {
            switch (action)
            {
                case "Settings":
                    OnSettings(null, null);
                    break;
                case "Dashboard":
                    OnDashboard(null, null);
                    break;
                case "AIAssist":
                    OnAIAssist(null, null);
                    break;
                case "Enterprise":
                    OnEnterpriseManagement(null, null);
                    break;
                case "Budget":
                    OnBudgetAnalysis(null, null);
                    break;
                case "About":
                    OnAbout(null, null);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle keyboard shortcut for {Action}", action);
        }
    }
}
