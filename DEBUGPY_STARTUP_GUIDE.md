# Wiley Widget Startup Debugging with debugpy

## Overview

This guide shows how to debug the Wiley Widget startup procedure using debugpy for advanced debugging capabilities.

## Quick Start

### Method 1: VS Code Debug Configuration (Recommended)

1. **Open VS Code Debug Panel**: Press `Ctrl+Shift+D` or click the Debug icon
2. **Select Configuration**: Choose "Debug Startup with debugpy" from the dropdown
3. **Start Debugging**: Press `F5` or click the green play button
4. **Set Breakpoints**: The script includes automatic breakpoints at key phases

### Method 2: Manual debugpy Session

1. **Start the Script**:
   ```powershell
   python scripts/dev-start-debugpy.py --timing
   ```

2. **Attach Debugger**: 
   - In VS Code, select "Python: Attach to debugpy"
   - Press `F5` to attach
   - The script will continue after attachment

### Method 3: No-Wait Mode (Advanced)

```powershell
python scripts/dev-start-debugpy.py --no-wait --timing
```

## Debugging Features

### Automatic Breakpoints

The script includes strategic breakpoints at:

- **Startup Phase**: Right after debugpy setup
- **Process Cleanup**: Before and after killing .NET processes  
- **Artifact Cleanup**: Before directory deletions
- **Build Phase**: Before clean and build operations
- **Application Launch**: Before starting the WileyWidget application
- **Timing Analysis**: After collecting performance metrics

### Debug Commands

When stopped at a breakpoint, use these debugger commands:

- **`c` (continue)**: Resume execution until next breakpoint
- **`s` (step)**: Step into function calls
- **`n` (next)**: Step over function calls
- **`l` (list)**: Show current code location
- **`p variable_name`**: Print variable value
- **`pp variable_name`**: Pretty-print variable value
- **`locals()`**: Show all local variables
- **`globals()`**: Show all global variables

### Timing Analysis

The `--timing` flag provides detailed performance metrics:

```
=== Timing Report ===
  Process Cleanup: 0.15s
  Artifact Cleanup: 0.32s
  Build: 4.21s
  Application Start: 1.08s
  Total: 5.76s
```

## Configuration Options

### Command Line Arguments

- `--debug-port PORT`: Change debugpy port (default: 5678)
- `--no-wait`: Don't wait for debugger attachment
- `--timing`: Enable detailed timing analysis
- `--skip-debugpy`: Run without debugpy (normal mode)

### Environment Variables

- `DEBUG_STARTUP=true`: Enable additional debug output
- `PYTHONPATH`: Ensure proper module resolution

## Troubleshooting

### Common Issues

1. **Port Already in Use**:
   ```powershell
   python scripts/dev-start-debugpy.py --debug-port 5679
   ```

2. **Debugger Won't Attach**:
   - Check firewall settings
   - Ensure port 5678 is available
   - Try `--no-wait` mode

3. **Script Hangs**:
   - Use `Ctrl+C` to interrupt
   - Check for zombie .NET processes
   - Try manual cleanup first

### Debug Configuration Details

VS Code configurations available:

- **"Python: Attach to debugpy"**: Attach to running debugpy session
- **"Debug Startup with debugpy"**: Full debugging with timing
- **"Debug Startup (No Wait)"**: Start immediately without waiting

## Performance Monitoring

### Identifying Bottlenecks

The timing analysis helps identify slow startup phases:

1. **Process Cleanup** (Should be <0.5s)
   - Many orphaned processes indicate cleanup issues
   - Check for hanging .NET applications

2. **Artifact Cleanup** (Should be <1s)
   - Large bin/obj directories slow cleanup
   - Consider automated cleanup scripts

3. **Build Phase** (Varies by project size)
   - First build after cleanup takes longest
   - Subsequent builds should be faster
   - Watch for dependency download delays

4. **Application Start** (Should be <2s)
   - WPF initialization overhead
   - Database connection delays
   - Syncfusion license validation

### Optimization Tips

1. **Faster Builds**: Use `dotnet build --no-restore` if packages are current
2. **Parallel Cleanup**: Clean artifacts while checking processes
3. **Selective Cleanup**: Only clean when necessary
4. **Precompiled Assets**: Use ReadyToRun images for faster startup

## Integration with CI/CD

The debugpy script integrates with the project's CI/CD workflow:

```powershell
# In CI pipeline
python scripts/dev-start-debugpy.py --skip-debugpy --timing
```

This provides timing metrics without debug overhead in automated environments.

## Examples

### Debug Build Issues

```powershell
# Start with timing to identify slow build
python scripts/dev-start-debugpy.py --timing

# When stopped at build breakpoint, inspect:
# - Build command arguments
# - Environment variables  
# - Project file dependencies
```

### Debug Application Startup

```powershell
# Focus on application launch phase
python scripts/dev-start-debugpy.py --timing

# When stopped at app launch breakpoint:
# - Check WPF initialization
# - Verify database connections
# - Monitor Syncfusion license loading
```

### Performance Comparison

```powershell
# Baseline timing
python scripts/dev-start-debugpy.py --skip-debugpy --timing

# With full debugging overhead
python scripts/dev-start-debugpy.py --timing
```

## Advanced Debugging

### Custom Breakpoints

Add custom breakpoints in the script:

```python
import debugpy

# Add breakpoint anywhere in code
debugpy.breakpoint()

# Conditional breakpoint
if some_condition:
    debugpy.breakpoint()
```

### Variable Inspection

When stopped at breakpoints, inspect key variables:

- `processes`: List of .NET processes found
- `cleanup_dirs`: Directories being cleaned
- `result`: Subprocess execution results
- `phases`: Timing data for each phase

This enables deep analysis of startup behavior and performance optimization.