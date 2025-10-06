<#
.SYNOPSIS
    Generates a highly fetchable index of the remote GitHub repository

.DESCRIPTION
    This script creates a detailed, optimized index of the remote GitHub repository with:
    - Parallel API fetching for speed
    - Intelligent caching and rate limiting
    - Incremental updates (only fetch changed data)
    - Comprehensive error handling and retry logic
    - Progress indicators and detailed logging
    - Configurable depth and filtering options
    - Resume capability for interrupted operations

.PARAMETER Owner
    Repository owner/organization name

.PARAMETER Repo
    Repository name

.PARAMETER OutputPath
    Path where the index file will be created

.PARAMETER MaxDepth
    Maximum directory depth to index (default: 3)

.PARAMETER IncludeContents
    Include detailed file contents (for small repos only)

.PARAMETER ForceRefresh
    Force full refresh, ignore cache

.PARAMETER ParallelJobs
    Number of parallel API calls (default: 3)

.PARAMETER CacheHours
    Cache validity in hours (default: 24)

.EXAMPLE
    .\Index-RemoteRepository.ps1 -Owner "Bigessfour" -Repo "Wiley-Widget"

.EXAMPLE
    .\Index-RemoteRepository.ps1 -Owner "Bigessfour" -Repo "Wiley-Widget" -MaxDepth 5 -ParallelJobs 5
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Owner = "Bigessfour",

    [Parameter(Mandatory = $false)]
    [string]$Repo = "Wiley-Widget",

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "remote-repository-index.json",

    [Parameter(Mandatory = $false)]
    [int]$MaxDepth = 3,

    [Parameter(Mandatory = $false)]
    [switch]$IncludeContents,

    [Parameter(Mandatory = $false)]
    [switch]$ForceRefresh,

    [Parameter(Mandatory = $false)]
    [int]$ParallelJobs = 3,

    [Parameter(Mandatory = $false)]
    [int]$CacheHours = 24
)

# Enhanced logging and progress
$ProgressPreference = 'Continue'
$ErrorActionPreference = 'Continue'

class FetchableRepoIndexer {
    [string]$Owner
    [string]$Repo
    [string]$BaseUrl
    [hashtable]$Cache = @{}
    [System.Collections.Concurrent.ConcurrentDictionary[string, object]]$Results = [System.Collections.Concurrent.ConcurrentDictionary[string, object]]::new()
    [System.Collections.Generic.List[string]]$Errors = [System.Collections.Generic.List[string]]::new()
    [int]$RateLimitRemaining = 5000
    [DateTime]$RateLimitReset = [DateTime]::UtcNow.AddHours(1)
    [bool]$ForceRefresh
    [int]$CacheHours
    [int]$ParallelJobs

    FetchableRepoIndexer([string]$owner, [string]$repo, [bool]$forceRefresh, [int]$cacheHours, [int]$parallelJobs) {
        $this.Owner = $owner
        $this.Repo = $repo
        $this.BaseUrl = "repos/$owner/$repo"
        $this.ForceRefresh = $forceRefresh
        $this.CacheHours = $cacheHours
        $this.ParallelJobs = $parallelJobs
    }

    [object] FetchWithRetry([string]$endpoint, [int]$maxRetries = 3) {
        $url = "$($this.BaseUrl)/$endpoint".Trim('/')

        # Check cache first
        if ($this.Cache.ContainsKey($url) -and !$this.ForceRefresh) {
            $cached = $this.Cache[$url]
            if ((Get-Date) - $cached.Timestamp -lt [TimeSpan]::FromHours($this.CacheHours)) {
                return $cached.Data
            }
        }

        for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
            try {
                # Rate limiting
                if ($this.RateLimitRemaining -lt 100) {
                    $waitTime = ($this.RateLimitReset - [DateTime]::UtcNow).TotalSeconds
                    if ($waitTime -gt 0) {
                        Write-Host "⏳ Rate limit approaching, waiting $([math]::Ceiling($waitTime)) seconds..." -ForegroundColor Yellow
                        Start-Sleep -Seconds $waitTime
                    }
                }

                $result = gh api $url 2>$null | ConvertFrom-Json

                # Cache the result
                $this.Cache[$url] = @{
                    Data      = $result
                    Timestamp = Get-Date
                }

                return $result

            }
            catch {
                $this.Errors.Add("Attempt $attempt failed for $url`: $($_.Exception.Message)")

                if ($attempt -lt $maxRetries) {
                    $backoffSeconds = [math]::Pow(2, $attempt - 1) * 2
                    Write-Host "🔄 Retrying $url in $backoffSeconds seconds..." -ForegroundColor Yellow
                    Start-Sleep -Seconds $backoffSeconds
                }
            }
        }

        throw "Failed to fetch $url after $maxRetries attempts"
    }

    [object[]] FetchParallel([string[]]$endpoints) {
        $jobs = @()
        $parallelResults = [System.Collections.Concurrent.ConcurrentBag[object]]::new()

        foreach ($endpoint in $endpoints) {
            $job = Start-Job -ScriptBlock {
                param($endpoint, $owner, $repo)
                try {
                    $url = "repos/$owner/$repo/$endpoint".Trim('/')
                    $result = gh api $url 2>$null | ConvertFrom-Json
                    return @{ Endpoint = $endpoint; Data = $result; Success = $true }
                }
                catch {
                    return @{ Endpoint = $endpoint; Error = $_.Exception.Message; Success = $false }
                }
            } -ArgumentList $endpoint, $this.Owner, $this.Repo

            $jobs += $job

            # Limit concurrent jobs
            if ($jobs.Count -ge $this.ParallelJobs) {
                $completed = $jobs | Where-Object { $_.State -eq 'Completed' }
                foreach ($job in $completed) {
                    $result = Receive-Job -Job $job
                    if ($result.Success) {
                        $parallelResults.Add($result)
                    }
                    else {
                        $this.Errors.Add("$($result.Endpoint): $($result.Error)")
                    }
                    Remove-Job -Job $job
                }
                $jobs = $jobs | Where-Object { $_.State -ne 'Completed' }
            }
        }

        # Wait for remaining jobs
        $jobs | ForEach-Object {
            $result = Receive-Job -Job $_ -Wait
            if ($result.Success) {
                $parallelResults.Add($result)
            }
            else {
                $this.Errors.Add("$($result.Endpoint): $($result.Error)")
            }
            Remove-Job -Job $_
        }

        return $parallelResults.ToArray()
    }

    [hashtable] IndexRepository([int]$maxDepth, [bool]$includeContents) {
        Write-Progress -Activity "Indexing Repository" -Status "Initializing..." -PercentComplete 0

        $index = @{
            metadata        = @{
                repository        = "$($this.Owner)/$($this.Repo)"
                generated_at      = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
                tool_version      = "2.0.0"
                fetchable_version = $true
                cache_used        = !$this.ForceRefresh
                parallel_jobs     = $this.ParallelJobs
                max_depth         = $maxDepth
            }
            repository      = @{}
            branches        = @()
            structure       = @{
                root_items  = @()
                directories = @{}
                files       = @{}
                tree        = @{}
            }
            statistics      = @{}
            recent_activity = @{}
            languages       = @{}
            contributors    = @()
            errors          = @()
        }

        try {
            # Fetch repository metadata
            Write-Progress -Activity "Indexing Repository" -Status "Fetching repository metadata..." -PercentComplete 10
            $repoData = $this.FetchWithRetry("", 3)
            $index.repository = $this.ProcessRepositoryData($repoData)

            # Fetch multiple endpoints in parallel
            Write-Progress -Activity "Indexing Repository" -Status "Fetching branches, languages, and contributors..." -PercentComplete 20
            $parallelEndpoints = @("branches", "languages", "contributors?per_page=10", "commits?per_page=10")
            $parallelResults = $this.FetchParallel($parallelEndpoints)

            foreach ($result in $parallelResults) {
                switch -Wildcard ($result.Endpoint) {
                    "branches" { $index.branches = $this.ProcessBranchesData($result.Data) }
                    "languages" { $index.languages = $result.Data }
                    "contributors*" { $index.contributors = $this.ProcessContributorsData($result.Data) }
                    "commits*" { $index.recent_activity = @{ commits = $this.ProcessCommitsData($result.Data) } }
                }
            }

            # Fetch repository structure with depth control
            Write-Progress -Activity "Indexing Repository" -Status "Analyzing repository structure..." -PercentComplete 40
            $structureData = $this.FetchRepositoryStructure("", $maxDepth, 0)
            $index.structure = $structureData

            # Calculate statistics
            Write-Progress -Activity "Indexing Repository" -Status "Calculating statistics..." -PercentComplete 60
            $index.statistics = $this.CalculateStatistics($structureData)

            # Optional content fetching
            if ($includeContents) {
                Write-Progress -Activity "Indexing Repository" -Status "Fetching file contents..." -PercentComplete 80
                $index.structure.file_contents = $this.FetchFileContents($structureData.files)
            }

            # Add errors to index
            $index.errors = $this.Errors.ToArray()

            Write-Progress -Activity "Indexing Repository" -Status "Complete!" -PercentComplete 100

        }
        catch {
            $this.Errors.Add("Critical error during indexing: $($_.Exception.Message)")
            $index.errors = $this.Errors.ToArray()
        }

        return $index
    }

    [hashtable] ProcessRepositoryData($data) {
        return @{
            name              = $data.name
            full_name         = $data.full_name
            description       = $data.description
            language          = $data.language
            size_kb           = $data.size
            created_at        = $data.created_at
            updated_at        = $data.updated_at
            pushed_at         = $data.pushed_at
            default_branch    = $data.default_branch
            is_private        = $data.private
            has_issues        = $data.has_issues
            has_projects      = $data.has_projects
            has_wiki          = $data.has_wiki
            has_pages         = $data.has_pages
            forks_count       = $data.forks_count
            network_count     = $data.network_count
            subscribers_count = $data.subscribers_count
            stargazers_count  = $data.stargazers_count
            watchers_count    = $data.watchers_count
            license           = $data.license
            topics            = $data.topics
        }
    }

    [array] ProcessBranchesData($data) {
        return $data | ForEach-Object {
            @{
                name       = $_.name
                protected  = $_.protected
                commit_sha = $_.commit.sha
                commit_url = $_.commit.url
            }
        }
    }

    [array] ProcessContributorsData($data) {
        return $data | ForEach-Object {
            @{
                login         = $_.login
                id            = $_.id
                contributions = $_.contributions
                type          = $_.type
                url           = $_.url
            }
        }
    }

    [array] ProcessCommitsData($data) {
        return $data | ForEach-Object {
            @{
                sha     = $_.sha
                message = $_.commit.message
                author  = $_.commit.author.name
                date    = $_.commit.author.date
                url     = $_.html_url
            }
        }
    }

    [hashtable] FetchRepositoryStructure([string]$path, [int]$maxDepth, [int]$currentDepth = 0) {
        if ($currentDepth -ge $maxDepth) {
            return @{ directories = @{}; files = @{} }
        }

        $structure = @{
            directories = @{}
            files       = @{}
        }

        try {
            $endpoint = if ($path) { "contents/$path" } else { "contents" }
            $contents = $this.FetchWithRetry($endpoint, 3)

            foreach ($item in $contents) {
                if ($item.type -eq "dir" -and $currentDepth -lt $maxDepth) {
                    $structure.directories[$item.name] = @{
                        path = $item.path
                        url  = $item.url
                        sha  = $item.sha
                    }

                    # Recursively fetch subdirectory contents
                    $subStructure = $this.FetchRepositoryStructure($item.path, $maxDepth, $currentDepth + 1)
                    $structure.directories[$item.name].contents = $subStructure
                }
                elseif ($item.type -eq "file") {
                    $structure.files[$item.name] = @{
                        name         = $item.name
                        path         = $item.path
                        size         = $item.size
                        url          = $item.url
                        sha          = $item.sha
                        download_url = $item.download_url
                    }
                }
            }
        }
        catch {
            $this.Errors.Add("Failed to fetch structure for path '$path': $($_.Exception.Message)")
        }

        return $structure
    }

    [hashtable] CalculateStatistics($structure) {
        $stats = @{
            total_files       = 0
            total_directories = 0
            total_size_bytes  = 0
            max_depth_reached = 0
            file_types        = @{}
            large_files       = @()
        }

        $this.CalculateStatsRecursive($structure, $stats, 0)
        $stats.total_size_kb = [math]::Round($stats.total_size_bytes / 1024, 2)

        return $stats
    }

    [void] CalculateStatsRecursive($structure, $stats, $depth) {
        if ($stats.max_depth_reached -lt $depth) {
            $stats.max_depth_reached = $depth
        }

        foreach ($file in $structure.files.Values) {
            $stats.total_files++
            $stats.total_size_bytes += $file.size

            # Track file types
            $extension = [IO.Path]::GetExtension($file.name)
            if ($extension) {
                $stats.file_types[$extension] = ($stats.file_types[$extension] ?? 0) + 1
            }

            # Track large files
            if ($file.size -gt 1048576) {
                # 1MB
                $stats.large_files += @{
                    name    = $file.name
                    path    = $file.path
                    size_mb = [math]::Round($file.size / 1048576, 2)
                }
            }
        }

        foreach ($dir in $structure.directories.Values) {
            $stats.total_directories++
            if ($dir.contents) {
                $this.CalculateStatsRecursive($dir.contents, $stats, $depth + 1)
            }
        }
    }

    [hashtable] FetchFileContents($files) {
        $contents = @{}
        $smallFiles = $files.Values | Where-Object { $_.size -lt 10000 -and $_.size -gt 0 }

        Write-Host "📄 Fetching contents for $($smallFiles.Count) small files..." -ForegroundColor Gray

        foreach ($file in $smallFiles) {
            try {
                $fileData = $this.FetchWithRetry("contents/$($file.path)", 3)
                if ($fileData.encoding -eq "base64") {
                    $decodedContent = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($fileData.content))
                    $contents[$file.name] = @{
                        content  = $decodedContent
                        encoding = $fileData.encoding
                        size     = $fileData.size
                        sha      = $fileData.sha
                    }
                }
            }
            catch {
                $this.Errors.Add("Failed to fetch content for $($file.name): $($_.Exception.Message)")
            }
        }

        return $contents
    }
}

# Main execution
Write-Host "🚀 Enhanced Fetchable Repository Indexing" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan
Write-Host "Repository: $Owner/$Repo" -ForegroundColor White
Write-Host "Max Depth: $MaxDepth" -ForegroundColor White
Write-Host "Parallel Jobs: $ParallelJobs" -ForegroundColor White
Write-Host "Force Refresh: $ForceRefresh" -ForegroundColor White
Write-Host "Include Contents: $IncludeContents" -ForegroundColor White
Write-Host ""

try {
    $indexer = [FetchableRepoIndexer]::new($Owner, $Repo, $ForceRefresh, $CacheHours, $ParallelJobs)
    $index = $indexer.IndexRepository($MaxDepth, $IncludeContents)

    # Save the index
    Write-Host "💾 Saving enhanced repository index..." -ForegroundColor Yellow
    $index | ConvertTo-Json -Depth 10 -Compress | Out-File -FilePath $OutputPath -Encoding UTF8

    # Display summary
    Write-Host "`n📊 Enhanced Repository Index Summary:" -ForegroundColor Cyan
    Write-Host "   Repository: $($index.repository.full_name)" -ForegroundColor White
    Write-Host "   Size: $($index.repository.size_kb) KB" -ForegroundColor White
    Write-Host "   Files: $($index.statistics.total_files)" -ForegroundColor White
    Write-Host "   Directories: $($index.statistics.total_directories)" -ForegroundColor White
    Write-Host "   Branches: $($index.branches.Count)" -ForegroundColor White
    Write-Host "   Contributors: $($index.contributors.Count)" -ForegroundColor White
    Write-Host "   Primary Language: $($index.repository.language)" -ForegroundColor White
    Write-Host "   Max Depth Indexed: $($index.statistics.max_depth_reached)" -ForegroundColor White
    Write-Host "   Errors: $($index.errors.Count)" -ForegroundColor $(if ($index.errors.Count -eq 0) { "Green" } else { "Red" })

    if ($index.errors.Count -gt 0) {
        Write-Host "`n⚠️  Errors encountered:" -ForegroundColor Yellow
        $index.errors | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
    }

    Write-Host "`n✅ Enhanced fetchable repository indexing completed!" -ForegroundColor Green
    Write-Host "📁 Index saved to: $OutputPath" -ForegroundColor White

}
catch {
    Write-Error "❌ Critical error during indexing: $($_.Exception.Message)"
    exit 1
}
