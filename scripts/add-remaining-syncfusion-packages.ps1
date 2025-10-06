#!/usr/bin/env pwsh

$packages = @(
    'Syncfusion.Compression.NET',
    'Syncfusion.Data.WPF',
    'Syncfusion.DocIO.NET',
    'Syncfusion.DocIORenderer.NET',
    'Syncfusion.Pdf.NET',
    'Syncfusion.SfBusyIndicator.WPF',
    'Syncfusion.SfChart.WPF',
    'Syncfusion.SfChat.WPF',
    'Syncfusion.SfGridConverter.WPF',
    'Syncfusion.SfInput.WPF',
    'Syncfusion.SfProgressBar.WPF',
    'Syncfusion.SfShared.WPF',
    'Syncfusion.SfSpreadsheet.WPF',
    'Syncfusion.Shared.WPF',
    'Syncfusion.Themes.FluentDark.WPF',
    'Syncfusion.Themes.FluentLight.WPF',
    'Syncfusion.Tools.WPF',
    'Syncfusion.XlsIO.Base'
)

$version = '27.1.48'

Write-Output "Adding $($packages.Count) NuGet packages...`n"

foreach ($pkg in $packages) {
    Write-Output "Adding: $pkg..."
    dotnet add WileyWidget.csproj package $pkg --version $version 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Output "  ✅ $pkg"
    }
    else {
        Write-Output "  ❌ $pkg (failed)"
    }
}

Write-Output "`n✅ Package addition complete!"
