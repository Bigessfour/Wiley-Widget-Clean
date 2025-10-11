<#
.SYNOPSIS
    Generates a fetchability resources manifest for CI/CD pipelines

.DESCRIPTION
    This script creates a machine-readable JSON manifest containing SHA256 hashes,
    timestamps, and metadata for all files in the repository (tracked and untracked).
    Used for file integrity verification and CI/CD pipeline automation.

.PARAMETER OutputPath
    Path where the manifest file will be created. Defaults to "fetchability-resources.json"

.PARAMETER OutputFormat
    Output format for the manifest. Options: JSON, CSV. Defaults to JSON

.PARAMETER ExcludePatterns
    Array of directory patterns to exclude from the manifest

.PARAMETER IncludeEmptyDirs
    Include empty directories in the manifest. Defaults to false

.PARAMETER Verbose
    Enable verbose output during processing

.EXAMPLE
    .\Generate-FetchabilityManifest.ps1

.EXAMPLE
    .\Generate-FetchabilityManifest.ps1 -OutputPath "custom-manifest.json" -Verbose

.EXAMPLE
    .\Generate-FetchabilityManifest.ps1 -OutputFormat CSV -OutputPath "manifest.csv"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "fetchability-resources.json",

    [Parameter(Mandatory = $false)]
    [ValidateSet("JSON", "CSV")]
    [string]$OutputFormat = "JSON",

    [Parameter(Mandatory = $false)]
    [string[]]$ExcludePatterns = @(
        ".git",
        "bin",
        "obj",
        "tmp",
        ".tmp",
        ".vs",
        "TestResults",
        "node_modules",
        ".trunk",
        "*.tmp",
        "*.log",
        "*.cache",
        "*.user",
        "*.suo",
        "Thumbs.db",
        "Desktop.ini",
        ".DS_Store",
        "__pycache__",
        ".pytest_cache",
        "*.pyc",
        "*.pyo",
        "*.pyd"
    ),

    [Parameter(Mandatory = $false)]
    [switch]$IncludeEmptyDirs
)

begin {
    Write-Verbose "Starting fetchability manifest generation..."

    # Validate prerequisites
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "Git is not available in PATH. Please install Git and ensure it's accessible."
    }

    if (-not (Test-Path ".git")) {
        throw "This script must be run from the root of a Git repository."
    }

    # Validate output path
    $outputDir = Split-Path -Path $OutputPath -Parent
    if ($outputDir -and -not (Test-Path $outputDir)) {
        Write-Verbose "Creating output directory: $outputDir"
        try {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        }
        catch {
            $errorDetails = "Failed to create output directory '$outputDir'. "
            $errorDetails += "Please check that you have write permissions for this path. "
            $errorDetails += "Error: $($_.Exception.Message)"
            throw $errorDetails
        }
    }

    # Validate output format extension
    $expectedExtension = if ($OutputFormat -eq "JSON") { ".json" } else { ".csv" }
    if (-not $OutputPath.EndsWith($expectedExtension)) {
        Write-Warning "Output path '$OutputPath' does not match expected extension '$expectedExtension' for format '$OutputFormat'"
    }

    # Function to calculate SHA256 hash of a file
    function Get-FileSHA256 {
        param([string]$FilePath)

        try {
            $hasher = [System.Security.Cryptography.SHA256]::Create()
            $stream = [System.IO.File]::OpenRead($FilePath)
            $hash = $hasher.ComputeHash($stream)
            $stream.Close()
            return [BitConverter]::ToString($hash).Replace("-", "").ToLower()
        }
        catch {
            Write-Warning "Could not calculate hash for file: $FilePath. Error: $($_.Exception.Message)"
            return $null
        }
    }

    # Function to check if file is tracked by git
    function Test-GitTracked {
        param([string]$FilePath)

        try {
            $null = & git ls-files --error-unmatch $FilePath 2>$null
            return $LASTEXITCODE -eq 0
        }
        catch {
            return $false
        }
    }

    # Function to get git repository information
    function Get-GitInfo {
        $gitInfo = @{
            CommitHash = $null
            Branch     = $null
            IsDirty    = $false
            RemoteUrl  = $null
        }

        try {
            $gitInfo.CommitHash = & git rev-parse HEAD 2>$null
            $gitInfo.Branch = & git rev-parse --abbrev-ref HEAD 2>$null

            # Check if repository is dirty
            $statusOutput = & git status --porcelain 2>$null
            $gitInfo.IsDirty = ($null -ne $statusOutput -and $statusOutput.Length -gt 0)

            # Get remote URL
            $remoteUrl = & git config --get remote.origin.url 2>$null
            if ($remoteUrl) {
                $gitInfo.RemoteUrl = $remoteUrl
            }
        }
        catch {
            Write-Warning "Could not retrieve git information: $($_.Exception.Message)"
        }

        return $gitInfo
    }

    # Function to validate manifest integrity
    function Test-ManifestIntegrity {
        param([PSCustomObject]$Manifest)

        $errors = @()

        try {
            # Validate metadata
            if (-not $Manifest.metadata) {
                $errors += "Missing metadata section"
            }
            elseif (-not $Manifest.metadata.generatedAt) {
                $errors += "Missing generatedAt timestamp"
            }

            # Validate files array
            if (-not $Manifest.files -or $Manifest.files.Count -eq 0) {
                $errors += "No files found in manifest"
            }
            else {
                # Check for duplicate paths
                $duplicatePaths = $Manifest.files | Group-Object -Property path | Where-Object { $_.Count -gt 1 }
                if ($duplicatePaths) {
                    $errors += "Duplicate file paths found: $($duplicatePaths.Group[0].path)"
                }

                # Validate file entries
                foreach ($file in $Manifest.files) {
                    if (-not $file.path) {
                        $errors += "File entry missing path property"
                        break
                    }
                    if (-not $file.sha256 -or $file.sha256.Length -ne 64) {
                        $errors += "Invalid SHA256 hash for file: $($file.path)"
                    }
                }
            }
        }
        catch {
            $errors += "Validation error: $($_.Exception.Message)"
        }

        return $errors
    }

    # Function to check if path should be excluded
    function Test-ShouldExclude {
        param([string]$Path)

        foreach ($pattern in $ExcludePatterns) {
            if ($Path -like "*$pattern*" -or $Path -match [regex]::Escape($pattern)) {
                return $true
            }
        }
        return $false
    }
}

process {
    Write-Information "🔍 Scanning repository files..." -InformationAction Continue

    # Get git information
    $gitInfo = Get-GitInfo

    # Get all files in the repository
    $allFiles = Get-ChildItem -Path "." -File -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { -not (Test-ShouldExclude $_.FullName) } |
        Select-Object -ExpandProperty FullName

    Write-Information "📁 Found $($allFiles.Count) files to process" -InformationAction Continue

    # Process each file
    $fileManifest = @()
    $processedCount = 0

    foreach ($file in $allFiles) {
        $processedCount++
        Write-Progress -Activity "Processing files" -Status "$processedCount/$($allFiles.Count)" -PercentComplete (($processedCount / $allFiles.Count) * 100)

        try {
            $relativePath = [System.IO.Path]::GetRelativePath((Get-Location), $file)
            $fileInfo = Get-Item $file

            # Calculate SHA256
            $sha256 = Get-FileSHA256 -FilePath $file
            if (-not $sha256) {
                Write-Warning "Skipping file due to hash calculation failure: $relativePath"
                continue
            }

            # Check if file is git tracked
            $isTracked = Test-GitTracked -FilePath $relativePath

            # Create file entry
            $fileEntry = @{
                path         = $relativePath.Replace("\", "/")  # Use forward slashes for consistency
                sha256       = $sha256
                size         = $fileInfo.Length
                lastModified = $fileInfo.LastWriteTimeUtc.ToString("o")  # ISO 8601 format
                tracked      = $isTracked
                extension    = $fileInfo.Extension
            }

            $fileManifest += $fileEntry

        }
        catch {
            Write-Warning "Error processing file '$file': $($_.Exception.Message)"
        }
    }

    Write-Progress -Activity "Processing files" -Completed

    # Add directory information if requested
    $directories = @()
    if ($IncludeEmptyDirs) {
        Write-Verbose "Including empty directories in manifest..."
        $directories = Get-ChildItem -Path "." -Directory -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { -not (Test-ShouldExclude $_.FullName) -and ($_.GetFiles().Count -eq 0) -and ($_.GetDirectories().Count -eq 0) } |
            ForEach-Object {
                $relativePath = [System.IO.Path]::GetRelativePath((Get-Location), $_.FullName)
                @{
                    path         = $relativePath.Replace("\", "/") + "/"
                    type         = "directory"
                    lastModified = $_.LastWriteTimeUtc.ToString("o")
                    tracked      = (Test-GitTracked -FilePath $relativePath)
                }
            }
    }

    # Create manifest object with enhanced metadata
    $generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    $manifest = @{
        metadata = @{
            generatedAt   = $generatedAt
            generator     = @{
                name    = "Generate-FetchabilityManifest.ps1"
                version = "2.0.0"
                format  = $OutputFormat
            }
            repository    = @{
                commitHash   = $gitInfo.CommitHash
                branch       = $gitInfo.Branch
                isDirty      = $gitInfo.IsDirty
                remoteUrl    = $gitInfo.RemoteUrl
                workingDir   = (Get-Location).Path
            }
            statistics    = @{
                totalFiles     = $fileManifest.Count
                trackedFiles   = ($fileManifest | Where-Object { $_.tracked }).Count
                untrackedFiles = ($fileManifest | Where-Object { -not $_.tracked }).Count
                totalSize      = if ($fileManifest.Count -gt 0) { ($fileManifest | Measure-Object -Property size -Sum).Sum } else { 0 }
                directories   = $directories.Count
                generatedIn   = [math]::Round(((Get-Date) - [DateTime]::Parse($generatedAt)).TotalSeconds, 2)
            }
            configuration = @{
                excludePatterns   = $ExcludePatterns
                includeEmptyDirs  = $IncludeEmptyDirs.IsPresent
                outputFormat      = $OutputFormat
            }
        }
        files       = $fileManifest | Sort-Object -Property path
    }

    # Add directories if requested
    if ($directories.Count -gt 0) {
        $manifest.directories = $directories | Sort-Object -Property path
    }

    # Write manifest to file
    Write-Verbose "💾 Writing manifest to $OutputPath..."

    try {
        # Ensure output directory exists
        $outputDir = Split-Path -Path $OutputPath -Parent
        if ($outputDir -and -not (Test-Path $outputDir)) {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        }

        if ($OutputFormat -eq "JSON") {
            $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8
        }
        elseif ($OutputFormat -eq "CSV") {
            # Convert to CSV format for machine readability
            $csvData = $manifest.files | ForEach-Object {
                [PSCustomObject]@{
                    Path         = $_.path
                    SHA256       = $_.sha256
                    Size         = $_.size
                    LastModified = $_.lastModified
                    Tracked      = $_.tracked
                    Extension    = $_.extension
                }
            }
            $csvData | Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8

            # Add metadata as comments at the top of CSV
            $metadataLines = @(
                "# Fetchability Manifest - Generated: $($manifest.metadata.generatedAt)",
                "# Repository: $($manifest.metadata.repository.remoteUrl)",
                "# Branch: $($manifest.metadata.repository.branch)",
                "# Commit: $($manifest.metadata.repository.commitHash)",
                "# Total Files: $($manifest.metadata.statistics.totalFiles)",
                "# Total Size: $([math]::Round($manifest.metadata.statistics.totalSize / 1MB, 2)) MB",
                ""
            )
            $existingContent = Get-Content -Path $OutputPath -Raw
            $newContent = $metadataLines -join "`n" + $existingContent
            Set-Content -Path $OutputPath -Value $newContent -Encoding UTF8
        }

        Write-Information "✅ Manifest successfully created: $OutputPath" -InformationAction Continue

        # Validate manifest integrity
        Write-Verbose "Validating manifest integrity..."
        try {
            $validationErrors = Test-ManifestIntegrity -Manifest $manifest
            if ($validationErrors -and $validationErrors.Count -gt 0) {
                Write-Warning "Manifest validation found $($validationErrors.Count) issues:"
                $validationErrors | ForEach-Object { Write-Warning "  • $_" }
            }
            else {
                Write-Information "✅ Manifest validation passed" -InformationAction Continue
            }
        }
        catch {
            Write-Warning "Manifest validation failed: $($_.Exception.Message)"
        }

        Write-Information "📊 Statistics:" -InformationAction Continue
        Write-Information "   • Total files: $($manifest.metadata.statistics.totalFiles)" -InformationAction Continue
        Write-Information "   • Tracked: $($manifest.metadata.statistics.trackedFiles)" -InformationAction Continue
        Write-Information "   • Untracked: $($manifest.metadata.statistics.untrackedFiles)" -InformationAction Continue
        Write-Information "   • Total size: $([math]::Round($manifest.metadata.statistics.totalSize / 1MB, 2)) MB" -InformationAction Continue
        if ($manifest.metadata.statistics.directories -gt 0) {
            Write-Information "   • Directories: $($manifest.metadata.statistics.directories)" -InformationAction Continue
        }
        Write-Information "   • Generation time: $($manifest.metadata.statistics.generatedIn) seconds" -InformationAction Continue
    }
    catch {
        $errorMessage = if ($_.Exception.Message) { $_.Exception.Message } else { $_.ToString() }
        throw "Failed to write manifest file: $errorMessage"
    }
}

end {
    Write-Information "🎉 Fetchability manifest generation completed!" -InformationAction Continue
}
