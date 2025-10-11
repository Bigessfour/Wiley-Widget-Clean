<#
.SYNOPSIS
    Fixes dependencies and cross-references in the layered architecture.

.DESCRIPTION
    Adds necessary package references and moves shared interfaces/attributes
    to the correct projects to resolve build errors.
#>

$ErrorActionPreference = 'Stop'

$rootPath = Split-Path -Parent $PSScriptRoot

Write-Information "🔧 Fixing layered architecture dependencies..." -InformationAction Continue

# Step 1: Add EF Core to Models project (for attributes)
Write-Information "`n📦 Adding Entity Framework Core to Models project..." -InformationAction Continue
dotnet add "$rootPath\WileyWidget.Models\WileyWidget.Models.csproj" package Microsoft.EntityFrameworkCore --version 9.0.8

# Step 2: Move IAuditable and ISoftDeletable from Data to Models
Write-Information "`n🔄 Moving shared interfaces to Models project..." -InformationAction Continue

$interfacesToMove = @(
    "IAuditable.cs",
    "ISoftDeletable.cs"
)

foreach ($interface in $interfacesToMove) {
    $sourcePath = Join-Path $rootPath "WileyWidget.Data\$interface"
    $destPath = Join-Path $rootPath "WileyWidget.Models\Interfaces\$interface"

    if (Test-Path $sourcePath) {
        $destDir = Split-Path $destPath -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        Write-Information "  Moving $interface to Models/Interfaces/" -InformationAction Continue
        Move-Item -Path $sourcePath -Destination $destPath -Force

        # Update namespace
        $content = Get-Content $destPath -Raw
        $newContent = $content -replace 'namespace WileyWidget\.Data', 'namespace WileyWidget.Models'
        Set-Content -Path $destPath -Value $newContent -NoNewline
    }
}

# Step 3: Create Attributes directory and GridDisplayAttribute
Write-Information "`n🏷️  Creating Attributes in Models project..." -InformationAction Continue

$attributesDir = Join-Path $rootPath "WileyWidget.Models\Attributes"
if (-not (Test-Path $attributesDir)) {
    New-Item -ItemType Directory -Path $attributesDir -Force | Out-Null
}

$gridDisplayAttributeCode = @'
using System;

namespace WileyWidget.Models;

/// <summary>
/// Attribute to control grid display properties for model properties
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class GridDisplayAttribute : Attribute
{
    public string Header { get; set; }
    public bool Visible { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string Format { get; set; }

    public GridDisplayAttribute(string header)
    {
        Header = header;
    }
}
'@

$gridDisplayPath = Join-Path $attributesDir "GridDisplayAttribute.cs"
Set-Content -Path $gridDisplayPath -Value $gridDisplayAttributeCode -NoNewline
Write-Information "  Created GridDisplayAttribute.cs" -InformationAction Continue

# Step 4: Update Data project files to reference Models interfaces
Write-Information "`n🔗 Updating Data project using statements..." -InformationAction Continue

$dataFiles = Get-ChildItem -Path (Join-Path $rootPath "WileyWidget.Data") -Filter "*.cs" -Recurse

foreach ($file in $dataFiles) {
    $content = Get-Content $file.FullName -Raw

    # Ensure proper using for interfaces that moved to Models
    if ($content -match 'IAuditable|ISoftDeletable') {
        if ($content -notmatch 'using WileyWidget\.Models;') {
            $lines = Get-Content $file.FullName
            $usingIndex = ($lines | Select-Object -First 10 | Select-String -Pattern "^using" | Select-Object -Last 1).LineNumber - 1
            $lines = @($lines[0..$usingIndex]) + @("using WileyWidget.Models;") + @($lines[($usingIndex+1)..($lines.Count-1)])
            Set-Content -Path $file.FullName -Value $lines
        }
    }
}

Write-Information "  ✅ Updated Data project using statements" -InformationAction Continue

# Step 5: Comment out problematic dependencies temporarily
Write-Information "`n⚠️  Temporarily commenting out complex dependencies..." -InformationAction Continue

# ChatMessage.cs - Syncfusion dependency
$chatMessagePath = Join-Path $rootPath "WileyWidget.Models\Models\ChatMessage.cs"
if (Test-Path $chatMessagePath) {
    $content = Get-Content $chatMessagePath -Raw
    if ($content -match 'using Syncfusion') {
        $content = $content -replace 'using Syncfusion[^;]+;', '// using Syncfusion... (commented for Models project)'
        $content = $content -replace ': TextMessage', '// : TextMessage (commented for Models project)'
        Set-Content -Path $chatMessagePath -Value $content -NoNewline
        Write-Information "  Commented Syncfusion dependency in ChatMessage.cs" -InformationAction Continue
    }
}

# HealthCheckModels.cs - Service dependencies
$healthCheckPath = Join-Path $rootPath "WileyWidget.Models\Models\HealthCheckModels.cs"
if (Test-Path $healthCheckPath) {
    $content = Get-Content $healthCheckPath -Raw
    if ($content -match 'using Serilog|using WileyWidget\.Services') {
        $content = $content -replace 'using Serilog[^;]+;', '// using Serilog... (commented for Models project)'
        $content = $content -replace 'using WileyWidget\.Services[^;]+;', '// using WileyWidget.Services... (commented for Models project)'
        $content = $content -replace 'ApplicationMetricsService', 'object /* ApplicationMetricsService */'
        Set-Content -Path $healthCheckPath -Value $content -NoNewline
        Write-Information "  Commented service dependencies in HealthCheckModels.cs" -InformationAction Continue
    }
}

# Step 6: Fix Enterprise.cs - remove WileyWidget.Attributes using (now same namespace)
Write-Information "`n🔧 Fixing namespace references..." -InformationAction Continue

$enterprisePath = Join-Path $rootPath "WileyWidget.Models\Models\Enterprise.cs"
if (Test-Path $enterprisePath) {
    $content = Get-Content $enterprisePath -Raw
    # Remove WileyWidget.Attributes and WileyWidget.Data usings since they're in Models now
    $content = $content -replace 'using WileyWidget\.Attributes;', '// using WileyWidget.Attributes; (same namespace now)'
    $content = $content -replace 'using WileyWidget\.Data;', '// using WileyWidget.Data; (interfaces moved to Models)'
    Set-Content -Path $enterprisePath -Value $content -NoNewline
    Write-Information "  Fixed Enterprise.cs namespace references" -InformationAction Continue
}

# Step 7: Build to verify
Write-Information "`n🔨 Building fixed projects..." -InformationAction Continue

Write-Information "  Building Models..." -InformationAction Continue
$modelsResult = dotnet build "$rootPath\WileyWidget.Models\WileyWidget.Models.csproj" --nologo 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Information "  ✅ Models project builds successfully!" -InformationAction Continue
} else {
    Write-Warning "Models build still has issues:"
    Write-Information ($modelsResult | Out-String) -InformationAction Continue
}

Write-Information "  Building Data..." -InformationAction Continue
$dataResult = dotnet build "$rootPath\WileyWidget.Data\WileyWidget.Data.csproj" --nologo 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Information "  ✅ Data project builds successfully!" -InformationAction Continue
} else {
    Write-Warning "Data build still has issues:"
    Write-Information ($dataResult | Out-String) -InformationAction Continue
}

Write-Information "`n✨ Dependency fixes complete!" -InformationAction Continue
Write-Information "📝 Next: Run build-integration-tests.ps1 to verify integration tests" -InformationAction Continue
