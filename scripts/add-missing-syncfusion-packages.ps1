#!/usr/bin/env pwsh

# Missing packages identified from build errors
$missingPackages = @(
    'Syncfusion.SfBusyIndicator.WPF',      # SfBusyIndicator control
    'Syncfusion.SfChart.WPF',              # SfChart control
    'Syncfusion.SfChat.WPF',               # SfAIAssistView control
    'Syncfusion.SfInput.WPF',              # SfDatePicker control
    'Syncfusion.SfSpreadsheet.WPF',        # SfSpreadsheet control
    'Syncfusion.Tools.WPF',                # Ribbon, RibbonWindow controls
    'Syncfusion.DocIORenderer.NET',        # Document rendering
    'Syncfusion.Pdf.NET',                  # PDF export
    'Syncfusion.SfProgressBar.WPF',        # Progress indicators
    'Syncfusion.SfShared.WPF',             # Shared utilities
    'Syncfusion.Shared.WPF',               # Legacy shared
    'Syncfusion.Themes.FluentDark.WPF',    # Theme
    'Syncfusion.Themes.FluentLight.WPF',   # Theme
    'Syncfusion.XlsIO.Base'                # Excel I/O
)

$version = '27.1.48'

Write-Output "Adding $($missingPackages.Count) missing Syncfusion packages...`n"

foreach ($pkg in $missingPackages) {
    Write-Output "Adding: $pkg..."
    dotnet add WileyWidget.csproj package $pkg --version $version 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Output "  ✅ $pkg"
    }
    else {
        Write-Output "  ⚠️ $pkg (may already exist or version mismatch)"
    }
}

Write-Output "`n✅ Package addition complete!"
Write-Output "Run: dotnet restore && dotnet build"
