#!/usr/bin/env pwsh
<#
.SYNOPSIS
Convert Wiley Widget repository to LLM-readable format

.DESCRIPTION
Based on the gitingest approach, this script converts your codebase into a
format that's optimized for LLM analysis. It filters out build artifacts,
dependencies, and other unnecessary files while preserving the essential
code structure and configuration.

.PARAMETER OutputFile
The output file path for the LLM-readable format (default: wiley-widget-llm.txt)

.PARAMETER IncludeImages
Whether to include image descriptions (requires description in comments)

.PARAMETER IncludeBinary
Whether to include binary file listings

.PARAMETER MaxFileSize
Maximum file size to include in bytes (default: 1MB)

.EXAMPLE
./repo-to-llm.ps1 -OutputFile analysis.txt
#>

param(
    [string]$OutputFile = "wiley-widget-llm.txt",
    [switch]$IncludeImages = $false,
    [switch]$IncludeBinary = $false,
    [int]$MaxFileSize = 1048576  # 1MB
)

# Ignore patterns based on the gitingest approach, optimized for .NET/C# projects
$IgnorePatterns = @(
    # Version control
    '.git',
    '.svn',
    '.hg',

    # Build outputs and caches
    'bin',
    'obj',
    'build',
    'dist',
    'target',
    'out',
    'TestResults',
    'coverage',
    '.buildcache',

    # Dependencies and packages
    'node_modules',
    'packages',
    '.venv',
    'venv',
    'env',
    'virtualenv',
    '__pycache__',

    # IDE and editor files
    '.vs',
    '.vscode/settings.json',
    '*.user',
    '*.suo',
    '*.userprefs',
    '.idea',
    '*.swp',
    '*.tmp',

    # Logs and temporary files
    'logs',
    '*.log',
    '*.bak',
    '*.backup',
    '*.old',
    '*.temp',
    '.cache',
    '.DS_Store',
    'Thumbs.db',
    'desktop.ini',

    # Binary files (unless explicitly included)
    '*.exe',
    '*.dll',
    '*.pdb',
    '*.lib',
    '*.obj',
    '*.o',
    '*.a',
    '*.so',
    '*.dylib',

    # Package manager files
    'package-lock.json',
    'yarn.lock',
    'Pipfile.lock',
    'poetry.lock',
    'bun.lock',
    'bun.lockb',
    '*.nupkg',
    '*.snupkg',

    # Document processing artifacts
    '*.min.js',
    '*.min.css',
    '*.map',

    # Environment and secrets
    '.env*',
    'appsettings.secrets.json',
    '*.key',
    '*.token',

    # Documentation that's too verbose for LLM context
    'CHANGELOG.md',
    'LICENSE*',

    # Large data files
    '*.pdf',
    '*.docx',
    '*.xlsx',
    '*.xls'
)

function Test-ShouldIgnore {
    param([string]$RelativePath)

    foreach ($pattern in $IgnorePatterns) {
        if ($pattern.Contains('*')) {
            # Wildcard pattern
            if ($RelativePath -like $pattern) {
                return $true
            }
        }
        else {
            # Exact match or path contains pattern
            if ($RelativePath -eq $pattern -or $RelativePath.Contains("/$pattern/") -or $RelativePath.EndsWith("/$pattern") -or $RelativePath.StartsWith("$pattern/")) {
                return $true
            }
        }
    }
    return $false
}

function Get-FileContent {
    param(
        [string]$FilePath,
        [string]$RelativePath
    )

    $fileInfo = Get-Item $FilePath

    # Skip if file is too large
    if ($fileInfo.Length -gt $MaxFileSize) {
        return "# FILE TOO LARGE: $($fileInfo.Length) bytes"
    }

    $extension = $fileInfo.Extension.ToLower()

    # Handle different file types
    switch ($extension) {
        { $_ -in '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.svg' } {
            if ($IncludeImages) {
                return "# IMAGE FILE: $RelativePath`n# Add image description here if needed"
            }
            else {
                return "# IMAGE FILE: $RelativePath (skipped)"
            }
        }
        { $_ -in '.exe', '.dll', '.pdb', '.lib', '.obj', '.bin' } {
            if ($IncludeBinary) {
                return "# BINARY FILE: $RelativePath (size: $($fileInfo.Length) bytes)"
            }
            else {
                return $null  # Skip binary files
            }
        }
        default {
            try {
                # Try to read as text
                $content = Get-Content $FilePath -Raw -ErrorAction Stop
                if ($null -eq $content) {
                    return "# EMPTY FILE"
                }
                return $content
            }
            catch {
                return "# BINARY OR UNREADABLE FILE: $RelativePath"
            }
        }
    }
}

function Format-FileForLLM {
    param(
        [string]$RelativePath,
        [string]$Content
    )

    $separator = '=' * 50
    $fileName = Split-Path $RelativePath -Leaf

    $output = @"
$separator
FILE: $fileName
PATH: $RelativePath
$separator
$Content

"@
    return $output
}

# Main processing
Write-Information "🔍 Converting Wiley Widget repository to LLM-readable format..." -InformationAction Continue
Write-Information "📁 Analyzing directory structure..." -InformationAction Continue

$rootPath = Get-Location
$allFiles = @()

# Get all files recursively
$files = Get-ChildItem -Path $rootPath -Recurse -File | Where-Object {
    $relativePath = $_.FullName.Substring($rootPath.Path.Length + 1).Replace('\', '/')
    -not (Test-ShouldIgnore $relativePath)
}

Write-Information "📊 Found $($files.Count) files to process..." -InformationAction Continue

# Process each file
$processedFiles = @()
$skippedCount = 0

foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($rootPath.Path.Length + 1).Replace('\', '/')

    Write-Progress -Activity "Processing files" -Status $relativePath -PercentComplete (($processedFiles.Count / $files.Count) * 100)

    $content = Get-FileContent -FilePath $file.FullName -RelativePath $relativePath

    if ($null -ne $content) {
        $formattedContent = Format-FileForLLM -RelativePath $relativePath -Content $content
        $processedFiles += $formattedContent
    }
    else {
        $skippedCount++
    }
}

Write-Progress -Activity "Processing files" -Completed

# Create the final output
$header = @"
# WILEY WIDGET REPOSITORY - LLM READABLE FORMAT
# Generated on: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
# Total files processed: $($processedFiles.Count)
# Files skipped: $skippedCount
# Repository root: $($rootPath.Path)

# OVERVIEW
This is a municipal budget management application built with:
- WPF (Windows Presentation Foundation) for the UI
- .NET 8+ for the backend
- Entity Framework for data access
- Syncfusion controls for enhanced UI components
- Azure integration for cloud services
- PowerShell scripts for automation and tooling

# SECURITY FEATURES
- Environment variable configuration for secrets
- GitLeaks integration for secret scanning
- Pre-commit hooks for security validation
- Comprehensive .gitignore for sensitive files

# TRUNK CONFIGURATION
The project uses Trunk for code quality and security:
- Enabled linters: gitleaks, trufflehog, ruff, dotnet-format, psscriptanalyzer
- Memory-optimized with --jobs=1 to prevent OOM issues
- Infrastructure security scanning with checkov
- Automated formatting and style checking

$('=' * 80)

"@

$finalOutput = $header + ($processedFiles -join "`n")

# Write to output file
$finalOutput | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Information "✅ LLM-readable format created: $OutputFile" -InformationAction Continue
Write-Information "📊 Statistics:" -InformationAction Continue
Write-Information "   - Files processed: $($processedFiles.Count)" -InformationAction Continue
Write-Information "   - Files skipped: $skippedCount" -InformationAction Continue
Write-Information "   - Output size: $([math]::Round((Get-Item $OutputFile).Length / 1KB, 2)) KB" -InformationAction Continue

# Generate analysis insights
Write-Information "`n🔍 Repository Analysis Insights:" -InformationAction Continue

# Count file types
$fileTypes = @{}
foreach ($file in $files) {
    $ext = $file.Extension.ToLower()
    if (-not $ext) { $ext = '(no extension)' }
    $fileTypes[$ext] = ($fileTypes[$ext] ?? 0) + 1
}

Write-Information "📁 File type distribution:" -InformationAction Continue
$fileTypes.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
    Write-Information "   $($_.Key): $($_.Value) files" -InformationAction Continue
}

# Check trunk configuration effectiveness
$trunkYaml = Join-Path $rootPath '.trunk' 'trunk.yaml'
if (Test-Path $trunkYaml) {
    Write-Information "`n🔧 Trunk Configuration Status:" -InformationAction Continue
    Write-Information "   - Configuration file found: ✅" -InformationAction Continue

    $trunkContent = Get-Content $trunkYaml -Raw
    # Fix regex pattern - remove extra closing parenthesis
    $enabledLinters = ([regex]'enabled:\s*\n((?:\s*-[^\n]+\n)*)').Matches($trunkContent)
    if ($enabledLinters.Count -gt 0) {
        Write-Information "   - Security linters active: ✅" -InformationAction Continue
    }
}

Write-Information "`n💡 Recommendations for LLM analysis:" -InformationAction Continue
Write-Information "   - Use this file to understand codebase structure" -InformationAction Continue
Write-Information "   - Focus on configuration files for security review" -InformationAction Continue
Write-Information "   - Analyze trunk.yaml for optimization opportunities" -InformationAction Continue
Write-Information "   - Review ignore patterns to ensure comprehensive coverage" -InformationAction Continue
