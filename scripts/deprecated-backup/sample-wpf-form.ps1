# Sample WPF Form using PowerShell Pro Tools
# This demonstrates how to create a simple WPF window in PowerShell

Add-Type -AssemblyName PresentationFramework

# Create a WPF Window
$window = New-Object System.Windows.Window
$window.Title = "Wiley Widget - PowerShell WPF Demo"
$window.Width = 400
$window.Height = 300
$window.WindowStartupLocation = "CenterScreen"

# Create a StackPanel for layout
$stackPanel = New-Object System.Windows.Controls.StackPanel
$stackPanel.Margin = "10"

# Add a TextBlock
$textBlock = New-Object System.Windows.Controls.TextBlock
$textBlock.Text = "Hello from PowerShell WPF!"
$textBlock.FontSize = 16
$textBlock.HorizontalAlignment = "Center"
$textBlock.Margin = "0,0,0,20"
$stackPanel.Children.Add($textBlock)

# Add a Button
$button = New-Object System.Windows.Controls.Button
$button.Content = "Click Me!"
$button.Width = 100
$button.HorizontalAlignment = "Center"
$button.Add_Click({
        [System.Windows.MessageBox]::Show("Button clicked!", "Info")
    })
$stackPanel.Children.Add($button)

# Set the content and show the window
$window.Content = $stackPanel
$window.ShowDialog()
