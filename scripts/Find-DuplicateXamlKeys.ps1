# Find-DuplicateXamlKeys.ps1
# Microsoft WPF Best Practice: Detect duplicate resource keys to prevent XamlParseException at runtime

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RootPath = (Get-Location),

    [Parameter(Mandatory = $false)]
    [switch]$ExportToFile
)

Write-Information "Scanning XAML files for duplicate resource keys..." -InformationAction Continue

# Find all XAML files
$xamlFiles = Get-ChildItem -Path $RootPath -Filter "*.xaml" -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\'
}

Write-Information "Found $($xamlFiles.Count) XAML files to analyze" -InformationAction Continue

# Dictionary to track keys: Key = ResourceKey, Value = List of files that define it
$resourceKeys = @{}

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw

    # Extract all x:Key="..." declarations
    $keyMatches = [regex]::Matches($content, 'x:Key\s*=\s*"([^"]+)"')

    foreach ($match in $keyMatches) {
        $key = $match.Groups[1].Value

        if (-not $resourceKeys.ContainsKey($key)) {
            $resourceKeys[$key] = @()
        }

        $resourceKeys[$key] += [PSCustomObject]@{
            File       = $file.FullName.Replace($RootPath, '.')
            LineNumber = ($content.Substring(0, $match.Index) -split "`n").Count
        }
    }
}

# Find duplicates (keys defined in multiple locations)
$duplicates = @($resourceKeys.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 })

if ($duplicates.Count -eq 0) {
    Write-Information "✅ No duplicate resource keys found - XAML resource management is clean!" -InformationAction Continue
    exit 0
}

Write-Warning "❌ Found $($duplicates.Count) duplicate resource keys:"

$report = @()

foreach ($duplicate in $duplicates | Sort-Object Name) {
    $key = $duplicate.Key
    $locations = $duplicate.Value

    Write-Warning "`n🔴 Duplicate Key: '$key' (defined in $($locations.Count) locations)"

    foreach ($location in $locations) {
        Write-Warning "   - $($location.File):$($location.LineNumber)"

        $report += [PSCustomObject]@{
            ResourceKey = $key
            File        = $location.File
            LineNumber  = $location.LineNumber
        }
    }
}

if ($ExportToFile) {
    $reportPath = Join-Path $RootPath "duplicate-xaml-keys-report.csv"
    $report | Export-Csv -Path $reportPath -NoTypeInformation
    Write-Information "`n📄 Full report exported to: $reportPath" -InformationAction Continue
}

Write-Warning "`n⚠️ MICROSOFT WPF COMPLIANCE ISSUE:"
Write-Warning "Duplicate resource keys cause XamlParseException at runtime."
Write-Warning "Each x:Key must be unique within the merged resource dictionary scope."
Write-Warning "`nRECOMMENDATION:"
Write-Warning "1. Move shared resources to a single central dictionary (e.g., Generic.xaml)"
Write-Warning "2. Remove duplicate definitions from window/view-specific resources"
Write-Warning "3. Use StaticResource references to centralized keys"

exit 1
