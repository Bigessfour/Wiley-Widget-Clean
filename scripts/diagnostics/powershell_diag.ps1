# PowerShell Diagnostic Script
Write-Host "PowerShell Diagnostic:"
Write-Host "PowerShell version: $($PSVersionTable.PSVersion)"
Write-Host "Current location: $(Get-Location)"
Write-Host "Execution policy: $(Get-ExecutionPolicy)"
Write-Host "PowerShell execution works correctly!"
