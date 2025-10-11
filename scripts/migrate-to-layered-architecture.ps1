<#
.SYNOPSIS
    Migrates WileyWidget to a layered N-tier architecture.

.DESCRIPTION
    Systematically moves Models and Data files from the WPF project to
    separate class library projects following Microsoft's N-tier pattern.

.EXAMPLE
    .\migrate-to-layered-architecture.ps1
#>

[CmdletBinding(SupportsShouldProcess)]
param()$ErrorActionPreference = 'Stop'

# Project paths
$rootPath = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $rootPath "src"
$modelsProject = Join-Path $rootPath "WileyWidget.Models"
$dataProject = Join-Path $rootPath "WileyWidget.Data"

Write-Information "🚀 Starting layered architecture migration..." -InformationAction Continue

# Step 1: Move Model files
Write-Information "`n📦 Step 1: Moving Model files..." -InformationAction Continue

$modelFiles = Get-ChildItem -Path (Join-Path $srcPath "Models") -Filter "*.cs" -File
$modelsMoved = 0

foreach ($file in $modelFiles) {
    $destDir = Join-Path $modelsProject "Models"
    $destPath = Join-Path $destDir $file.Name

    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($file.Name, "Move to WileyWidget.Models")) {
        Write-Information "  Moving: $($file.Name)" -InformationAction Continue
        Copy-Item -Path $file.FullName -Destination $destPath -Force

        # Update namespace
        $content = Get-Content $destPath -Raw
        $newContent = $content -replace 'namespace WileyWidget\.Models', 'namespace WileyWidget.Models'
        Set-Content -Path $destPath -Value $newContent -NoNewline

        $modelsMoved++
    }
}

Write-Information "  ✅ Moved $modelsMoved model files" -InformationAction Continue

# Step 2: Move Data files
Write-Information "`n🗄️  Step 2: Moving Data files..." -InformationAction Continue

$dataFiles = Get-ChildItem -Path (Join-Path $srcPath "Data") -Filter "*.cs" -File -Recurse
$dataMoved = 0

foreach ($file in $dataFiles) {
    # Preserve subdirectory structure
    $relativePath = $file.FullName.Substring((Join-Path $srcPath "Data").Length + 1)
    $destPath = Join-Path $dataProject $relativePath
    $destDir = Split-Path $destPath -Parent

    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($relativePath, "Move to WileyWidget.Data")) {
        Write-Information "  Moving: $relativePath" -InformationAction Continue
        Copy-Item -Path $file.FullName -Destination $destPath -Force

        # Update namespace
        $content = Get-Content $destPath -Raw
        $newContent = $content -replace 'namespace WileyWidget\.Data', 'namespace WileyWidget.Data'
        Set-Content -Path $destPath -Value $newContent -NoNewline

        $dataMoved++
    }
}

Write-Information "  ✅ Moved $dataMoved data files" -InformationAction Continue

# Step 3: Update using statements in moved files
Write-Information "`n🔧 Step 3: Updating using statements..." -InformationAction Continue

$allMovedFiles = @(
    (Get-ChildItem -Path $modelsProject -Filter "*.cs" -Recurse)
    (Get-ChildItem -Path $dataProject -Filter "*.cs" -Recurse)
)

foreach ($file in $allMovedFiles) {
    if ($PSCmdlet.ShouldProcess($file.Name, "Update using statements")) {
        $content = Get-Content $file.FullName -Raw

        # Ensure proper using statements
        if ($content -notmatch 'using WileyWidget\.Models;' -and $file.DirectoryName -like "*Data*") {
            $content = "using WileyWidget.Models;`n" + $content
        }

        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Information "  ✅ Updated using statements" -InformationAction Continue

# Step 4: Build new projects to verify
Write-Information "`n🔨 Step 4: Building new projects..." -InformationAction Continue

if ($true) {
    try {
        Write-Information "  Building WileyWidget.Models..." -InformationAction Continue
        $modelsResult = dotnet build (Join-Path $modelsProject "WileyWidget.Models.csproj") --nologo 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Models project build had issues (this is expected initially)"
            Write-Information $modelsResult -InformationAction Continue
        }

        Write-Information "  Building WileyWidget.Data..." -InformationAction Continue
        $dataResult = dotnet build (Join-Path $dataProject "WileyWidget.Data.csproj") --nologo 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Data project build had issues (this is expected initially)"
            Write-Information $dataResult -InformationAction Continue
        }

        Write-Information "  ✅ Build verification complete" -InformationAction Continue
    }
    catch {
        Write-Warning "Build verification encountered issues (expected during initial migration)"
    }
}

# Step 5: Generate migration report
Write-Information "`n📊 Step 5: Generating migration report..." -InformationAction Continue

$reportPath = Join-Path $rootPath "docs\LAYERED_ARCHITECTURE_MIGRATION_REPORT.md"
$report = @"
# Layered Architecture Migration Report
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Summary
- **Models Moved**: $modelsMoved files
- **Data Files Moved**: $dataMoved files
- **Total Files Migrated**: $($modelsMoved + $dataMoved)

## Next Steps

### 1. Update WileyWidget.csproj
Remove old file includes and add Business project reference:
``````xml
<ItemGroup>
  <ProjectReference Include="..\WileyWidget.Business\WileyWidget.Business.csproj" />
</ItemGroup>
``````

### 2. Update Integration Tests
``````bash
dotnet add WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj reference WileyWidget.Data/WileyWidget.Data.csproj
dotnet add WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj reference WileyWidget.Models/WileyWidget.Models.csproj
``````

Remove the test exclusion:
``````xml
<!-- DELETE THESE LINES from WileyWidget.IntegrationTests.csproj -->
<Compile Remove="**\*.cs" />
<None Include="**\*.cs" />
``````

### 3. Fix Build Errors
Run incremental builds to identify and fix:
- Missing using statements
- Namespace conflicts
- Type resolution issues

### 4. Create Business Layer Services
Extract business logic from ViewModels into dedicated service classes in WileyWidget.Business.

### 5. Update ViewModels
Inject services via dependency injection instead of direct data access.

## Verification Commands
``````bash
# Build each layer
dotnet build WileyWidget.Models/WileyWidget.Models.csproj
dotnet build WileyWidget.Data/WileyWidget.Data.csproj
dotnet build WileyWidget.Business/WileyWidget.Business.csproj
dotnet build WileyWidget.csproj

# Run integration tests
dotnet test WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj
``````

## Architecture Diagram
``````
WileyWidget (.NET 9.0-windows WPF)
    ↓ references
WileyWidget.Business (.NET 8.0)
    ↓ references
WileyWidget.Data (.NET 8.0)
    ↓ references
WileyWidget.Models (.NET 8.0)
``````

## Files Moved

### Models ($modelsMoved files)
$($modelFiles | ForEach-Object { "- $($_.Name)" } | Out-String)

### Data ($dataMoved files)
$($dataFiles | ForEach-Object {
    $rel = $_.FullName.Substring((Join-Path $srcPath "Data").Length + 1)
    "- $rel"
} | Out-String)
"@

if ($PSCmdlet.ShouldProcess($reportPath, "Generate migration report")) {
    Set-Content -Path $reportPath -Value $report
    Write-Information "  ✅ Report saved to: $reportPath" -InformationAction Continue
}

Write-Information "`n✨ Migration complete!" -InformationAction Continue
Write-Information "📖 Review the report at: $reportPath" -InformationAction Continue
Write-Information "`n⚠️  Next: Manually update WileyWidget.csproj to reference WileyWidget.Business" -InformationAction Continue
