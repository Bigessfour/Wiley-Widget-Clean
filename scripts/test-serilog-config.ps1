# Test Serilog Configuration Loading
param(
    [string]$ConfigPath = "config\appsettings.json"
)

Write-Information "Testing Serilog Configuration Loading..." -InformationAction Continue

# Check if config file exists
$configFile = Join-Path $PWD $ConfigPath
if (Test-Path $configFile) {
    Write-Information "✓ Config file found: $configFile" -InformationAction Continue

    # Test JSON parsing
    try {
        $config = Get-Content $configFile | ConvertFrom-Json
        if ($config.Serilog) {
            Write-Information "✓ Serilog section found in configuration" -InformationAction Continue
            Write-Information "  - MinimumLevel: $($config.Serilog.MinimumLevel.Default)" -InformationAction Continue
            Write-Information "  - WriteTo count: $($config.Serilog.WriteTo.Count)" -InformationAction Continue

            # Check log paths
            $logPaths = @()
            foreach ($writeTo in $config.Serilog.WriteTo) {
                if ($writeTo.Name -eq "Async") {
                    foreach ($innerWriteTo in $writeTo.Args.configure.WriteTo) {
                        if ($innerWriteTo.Name -eq "File") {
                            $logPaths += $innerWriteTo.Args.path
                        }
                    }
                }
            }

            Write-Information "  - Log paths configured:" -InformationAction Continue
            foreach ($path in $logPaths) {
                Write-Information "    $path" -InformationAction Continue

                # Check if logs directory exists
                $logDir = Split-Path $path
                if (!(Test-Path $logDir)) {
                    Write-Information "    ⚠ Directory does not exist: $logDir" -InformationAction Continue
                }
                else {
                    Write-Information "    ✓ Directory exists: $logDir" -InformationAction Continue
                }
            }
        }
        else {
            Write-Information "✗ No Serilog section found in configuration" -InformationAction Continue
        }
    }
    catch {
        Write-Information "✗ Failed to parse JSON: $_" -InformationAction Continue
    }
}
else {
    Write-Information "✗ Config file not found: $configFile" -InformationAction Continue
}

# Test working directory
Write-Information "`nCurrent working directory: $PWD" -InformationAction Continue

# Check for any existing log files
Write-Information "`nExisting log files:" -InformationAction Continue
$logFiles = Get-ChildItem -Path "logs" -Filter "*.log" -ErrorAction SilentlyContinue
if ($logFiles) {
    foreach ($file in $logFiles) {
        Write-Information "  $($file.Name) - $($file.LastWriteTime) - $($file.Length) bytes" -InformationAction Continue
    }
}
else {
    Write-Information "  No log files found in logs directory" -InformationAction Continue
}

Write-Information "`nTest completed." -InformationAction Continue
