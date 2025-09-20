<#
.SYNOPSIS
    Generates a comprehensive index of the remote GitHub repository

.DESCRIPTION
    This script creates a detailed index of the remote GitHub repository including:
    - Repository metadata
    - File/directory structure
    - File sizes and types
    - Branch information
    - Recent commits
    - Contributors
    - Languages used

.PARAMETER Owner
    Repository owner/organization name

.PARAMETER Repo
    Repository name

.PARAMETER OutputPath
    Path where the index file will be created

.PARAMETER IncludeContents
    Include detailed file contents (for small repos only)

.EXAMPLE
    .\Index-RemoteRepository.ps1 -Owner "Bigessfour" -Repo "Wiley-Widget"

.EXAMPLE
    .\Index-RemoteRepository.ps1 -Owner "Bigessfour" -Repo "Wiley-Widget" -IncludeContents
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
    [switch]$IncludeContents
)

Write-Host "🔍 Indexing Remote Repository: $Owner/$Repo" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

$index = @{
    metadata = @{
        repository = "$Owner/$Repo"
        generated_at = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
        tool_version = "1.0.0"
    }
    repository = @{}
    branches = @()
    structure = @{}
    statistics = @{}
    recent_activity = @{}
}

# Get repository metadata
Write-Host "📋 Getting repository metadata..." -ForegroundColor Yellow
try {
    $repoData = gh api "repos/$Owner/$Repo" | ConvertFrom-Json
    $index.repository = @{
        name = $repoData.name
        full_name = $repoData.full_name
        description = $repoData.description
        language = $repoData.language
        size_kb = $repoData.size
        created_at = $repoData.created_at
        updated_at = $repoData.updated_at
        pushed_at = $repoData.pushed_at
        default_branch = $repoData.default_branch
        is_private = $repoData.private
        has_issues = $repoData.has_issues
        has_projects = $repoData.has_projects
        has_wiki = $repoData.has_wiki
        has_pages = $repoData.has_pages
        forks_count = $repoData.forks_count
        network_count = $repoData.network_count
        subscribers_count = $repoData.subscribers_count
        stargazers_count = $repoData.stargazers_count
        watchers_count = $repoData.watchers_count
    }
    Write-Host "✅ Repository metadata retrieved" -ForegroundColor Green
} catch {
    Write-Warning "Failed to get repository metadata: $_"
}

# Get branches
Write-Host "🌿 Getting branch information..." -ForegroundColor Yellow
try {
    $branchesData = gh api "repos/$Owner/$Repo/branches" | ConvertFrom-Json
    $index.branches = $branchesData | ForEach-Object {
        @{
            name = $_.name
            protected = $_.protected
            commit_sha = $_.commit.sha
            commit_url = $_.commit.url
        }
    }
    Write-Host "✅ Found $($index.branches.Count) branches" -ForegroundColor Green
} catch {
    Write-Warning "Failed to get branch information: $_"
}

# Get repository structure
Write-Host "📁 Analyzing repository structure..." -ForegroundColor Yellow
try {
    $contentsData = gh api "repos/$Owner/$Repo/contents" | ConvertFrom-Json

    $fileCount = 0
    $dirCount = 0
    $totalSize = 0

    $index.structure = @{
        root_items = @()
        directories = @{}
        files = @{}
    }

    foreach ($item in $contentsData) {
        $itemInfo = @{
            name = $item.name
            type = $item.type
            size = $item.size
            path = $item.path
            url = $item.url
        }

        $index.structure.root_items += $itemInfo

        if ($item.type -eq "dir") {
            $dirCount++
            $index.structure.directories[$item.name] = @{
                path = $item.path
                url = $item.url
            }
        } else {
            $fileCount++
            $totalSize += $item.size
            $index.structure.files[$item.name] = $itemInfo
        }
    }

    $index.statistics = @{
        total_files = $fileCount
        total_directories = $dirCount
        total_size_bytes = $totalSize
        total_size_kb = [math]::Round($totalSize / 1024, 2)
        root_items_count = $contentsData.Count
    }

    Write-Host "✅ Structure analyzed: $fileCount files, $dirCount directories" -ForegroundColor Green
} catch {
    Write-Warning "Failed to analyze repository structure: $_"
}

# Get recent commits
Write-Host "📝 Getting recent commits..." -ForegroundColor Yellow
try {
    $commitsData = gh api "repos/$Owner/$Repo/commits?per_page=10" | ConvertFrom-Json
    $index.recent_activity = @{
        recent_commits = @()
        last_commit = @{}
    }

    $index.recent_activity.recent_commits = $commitsData | ForEach-Object {
        @{
            sha = $_.sha
            message = $_.commit.message
            author = $_.commit.author.name
            date = $_.commit.author.date
            url = $_.html_url
        }
    }

    if ($commitsData.Count -gt 0) {
        $index.recent_activity.last_commit = @{
            sha = $commitsData[0].sha
            message = $commitsData[0].commit.message
            author = $commitsData[0].commit.author.name
            date = $commitsData[0].commit.author.date
            url = $commitsData[0].html_url
        }
    }

    Write-Host "✅ Retrieved $($commitsData.Count) recent commits" -ForegroundColor Green
} catch {
    Write-Warning "Failed to get recent commits: $_"
}

# Get languages
Write-Host "💻 Getting language statistics..." -ForegroundColor Yellow
try {
    $languagesData = gh api "repos/$Owner/$Repo/languages" | ConvertFrom-Json
    $index.repository.languages = $languagesData
    Write-Host "✅ Language statistics retrieved" -ForegroundColor Green
} catch {
    Write-Warning "Failed to get language statistics: $_"
}

# Optional: Get file contents for small files (if requested)
if ($IncludeContents) {
    Write-Host "📄 Getting file contents (small files only)..." -ForegroundColor Yellow
    $index.structure.file_contents = @{}

    foreach ($file in $index.structure.files.GetEnumerator()) {
        if ($file.Value.size -lt 10000) { # Only get contents for files under 10KB
            try {
                $contentData = gh api "repos/$Owner/$Repo/contents/$($file.Value.path)" | ConvertFrom-Json
                if ($contentData.encoding -eq "base64") {
                    $decodedContent = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($contentData.content))
                    $index.structure.file_contents[$file.Name] = @{
                        content = $decodedContent
                        encoding = $contentData.encoding
                        size = $contentData.size
                    }
                }
            } catch {
                Write-Warning "Failed to get content for $($file.Name): $_"
            }
        }
    }
    Write-Host "✅ File contents retrieved for small files" -ForegroundColor Green
}

# Save the index
Write-Host "💾 Saving repository index..." -ForegroundColor Yellow
try {
    $index | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Host "✅ Repository index saved to: $OutputPath" -ForegroundColor Green

    # Display summary
    Write-Host "`n📊 Repository Index Summary:" -ForegroundColor Cyan
    Write-Host "   Repository: $($index.repository.full_name)" -ForegroundColor White
    Write-Host "   Size: $($index.repository.size_kb) KB" -ForegroundColor White
    Write-Host "   Files: $($index.statistics.total_files)" -ForegroundColor White
    Write-Host "   Directories: $($index.statistics.total_directories)" -ForegroundColor White
    Write-Host "   Branches: $($index.branches.Count)" -ForegroundColor White
    Write-Host "   Primary Language: $($index.repository.language)" -ForegroundColor White
    Write-Host "   Last Updated: $($index.repository.updated_at)" -ForegroundColor White

} catch {
    Write-Error "Failed to save repository index: $_"
    exit 1
}

Write-Host "`n🎉 Remote repository indexing completed!" -ForegroundColor Green