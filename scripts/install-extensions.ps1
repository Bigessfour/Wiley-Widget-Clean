# Install Required VS Code Extensions
# This script ensures all necessary extensions are installed and persist across environment resets

param(
    [switch]$Force,
    [switch]$Quiet
)

$extensions = @(
    # SQL Server Extensions
    "ms-mssql.mssql",
    "ms-mssql.data-workspace-vscode",
    "ms-mssql.sql-bindings-vscode",
    "ms-mssql.sql-database-projects-vscode",

    # Azure Extensions
    "ms-azuretools.vscode-azure-github-copilot",
    "ms-azuretools.vscode-azure-mcp-server",
    "ms-azuretools.vscode-azureappservice",
    "ms-azuretools.vscode-azurefunctions",
    "ms-azuretools.vscode-azureresourcegroups",
    "ms-vscode.azure-repos",

    # Development Tools
    "ms-dotnettools.csharp",
    "ms-dotnettools.vscode-dotnet-runtime"
)

Write-Host "🔧 Installing Required VS Code Extensions..." -ForegroundColor Cyan
Write-Host "This will ensure extensions persist across environment resets." -ForegroundColor Yellow
Write-Host ""

$installed = 0
$failed = 0

foreach ($ext in $extensions) {
    try {
        if (!$Quiet) {
            Write-Host "Installing $ext..." -ForegroundColor Gray
        }

        $result = code --install-extension $ext 2>&1

        if ($LASTEXITCODE -eq 0) {
            $installed++
            if (!$Quiet) {
                Write-Host "✅ $ext installed successfully" -ForegroundColor Green
            }
        }
        else {
            $failed++
            if (!$Quiet) {
                Write-Host "❌ Failed to install $ext" -ForegroundColor Red
                Write-Host "  Error: $result" -ForegroundColor Red
            }
        }
    }
    catch {
        $failed++
        if (!$Quiet) {
            Write-Host "❌ Error installing $ext : $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "📊 Installation Summary:" -ForegroundColor Cyan
Write-Host "  ✅ Successfully installed: $installed extensions" -ForegroundColor Green
Write-Host "  ❌ Failed installations: $failed extensions" -ForegroundColor Red
Write-Host ""
Write-Host "🔄 Extension Persistence:" -ForegroundColor Yellow
Write-Host "  - Extensions are now tracked in .vscode/extensions.json"
Write-Host "  - VS Code will recommend these extensions to team members"
Write-Host "  - Extensions will auto-update when available"
Write-Host ""
Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Restart VS Code to ensure all extensions are loaded"
Write-Host "  2. Check that SQL Server extension can connect to Azure SQL"
Write-Host "  3. Verify Azure extensions are working in the sidebar"

if ($failed -gt 0) {
    Write-Host ""
    Write-Host "⚠️  Some extensions failed to install. You may need to:" -ForegroundColor Yellow
    Write-Host "  - Check your internet connection"
    Write-Host "  - Restart VS Code and try again"
    Write-Host "  - Manually install failed extensions from the marketplace"
}
