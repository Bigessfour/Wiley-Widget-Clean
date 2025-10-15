#!/usr/bin/env python3
"""
Wiley Widget Development Startup Script with debugpy Remote Debugging
This version includes debugpy for advanced debugging capabilities
"""

import argparse
import logging
import os
import subprocess
import time

# Import debugpy for remote debugging
import debugpy

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)

def setup_debugpy(port=5678, wait_for_client=True):
    """Setup debugpy for remote debugging with port conflict handling"""
    print(f"🔍 Setting up debugpy on port {port}")

    try:
        # Check if debugpy is already listening
        if debugpy.is_client_connected():
            print("✅ debugpy already connected to client")
            return

        # Try to listen on the specified port, with fallback ports
        ports_to_try = [port, port + 1, port + 2, 5679, 5680]
        listening_port = None

        for try_port in ports_to_try:
            try:
                debugpy.listen(try_port)
                listening_port = try_port
                print(f"✅ debugpy listening on port {try_port}")
                break
            except Exception:
                continue

        if listening_port is None:
            print("❌ Failed to setup debugpy on any available port")
            print("   Continuing without debugpy...")
            return

        if wait_for_client:
            print("⏳ Waiting for debugger to attach...")
            print(f"   In VS Code: Run 'Python: Attach' debug configuration (port {listening_port})")
            debugpy.wait_for_client()
            print("✅ Debugger attached!")

        # Set breakpoint right after setup
        debugpy.breakpoint()

    except Exception as e:
        print(f"❌ Failed to setup debugpy: {e}")
        print("   Continuing without debugpy...")

def cleanup_dotnet_processes():
    """Clean up orphaned .NET processes with debugging"""
    print("=== .NET Process Cleanup ===")
    debugpy.breakpoint()  # Debug breakpoint

    try:
        # Get all processes
        result = subprocess.run(['tasklist', '/FO', 'CSV', '/NH'],
                              capture_output=True, text=True, check=True)

        processes = []
        for line in result.stdout.strip().split('\n'):
            if line.strip():
                parts = line.split(',')
                if len(parts) >= 2:
                    name = parts[0].strip('"')
                    pid = parts[1].strip('"')
                    if 'dotnet' in name.lower() or name == 'WileyWidget.exe':
                        processes.append((name, pid))

        if processes:
            print(f"Found {len(processes)} .NET processes to clean up:")
            for name, pid in processes:
                print(f"  - {name} (PID: {pid})")

            # Debug breakpoint before killing processes
            debugpy.breakpoint()

            for name, pid in processes:
                try:
                    subprocess.run(['taskkill', '/F', '/PID', pid],
                                 capture_output=True, check=True)
                    print(f"  ✅ Killed {name} (PID: {pid})")
                except subprocess.CalledProcessError as e:
                    print(f"  ❌ Failed to kill {name} (PID: {pid}): {e}")
        else:
            print("  ✅ No .NET processes found to clean up")

    except subprocess.CalledProcessError as e:
        print(f"  ❌ Failed to list processes: {e}")

def cleanup_dotnet_artifacts():
    """Clean up .NET build artifacts with debugging"""
    print("\n=== .NET Artifact Cleanup ===")
    debugpy.breakpoint()  # Debug breakpoint

    cleanup_dirs = ['bin', 'obj', 'TestResults']

    for dir_name in cleanup_dirs:
        if os.path.exists(dir_name):
            print(f"  🗑️  Removing {dir_name}/")
            try:
                # Debug breakpoint before deletion
                debugpy.breakpoint()

                if os.name == 'nt':  # Windows
                    subprocess.run(['rmdir', '/S', '/Q', dir_name],
                                 shell=True, check=True)
                else:  # Unix-like
                    subprocess.run(['rm', '-rf', dir_name], check=True)
                print(f"  ✅ Removed {dir_name}/")
            except subprocess.CalledProcessError as e:
                print(f"  ❌ Failed to remove {dir_name}/: {e}")
        else:
            print(f"  ℹ️  {dir_name}/ not found")

def build_application(incremental=True):
    """Build the WileyWidget application with debugging"""
    print("\n=== Building Application ===")
    debugpy.breakpoint()  # Debug breakpoint

    try:
        if not incremental:
            # Clean first (only for full rebuilds)
            print("  🧹 Cleaning project...")
            try:
                result = subprocess.run(['dotnet', 'clean', 'WileyWidget.csproj'],
                                      capture_output=True, text=True, check=True)
                print("  ✅ Project cleaned")
            except subprocess.CalledProcessError as e:
                logging.error(f"Failed to clean WileyWidget.csproj: {e}", exc_info=True)
                raise

        # For incremental builds, ensure packages are restored if needed
        if incremental and not os.path.exists('obj/project.assets.json'):
            print("  📦 Restoring packages for incremental build...")
            try:
                result = subprocess.run(['dotnet', 'restore', 'WileyWidget.csproj'],
                                      capture_output=True, text=True, check=True)
                print("  ✅ Packages restored")
            except subprocess.CalledProcessError as e:
                logging.error(f"Failed to restore packages for WileyWidget.csproj: {e}", exc_info=True)
                raise

        # Debug breakpoint before build
        debugpy.breakpoint()

        # Build (incremental by default)
        build_type = "incrementally" if incremental else "from scratch"
        print(f"  🔨 Building project {build_type}...")
        try:
            result = subprocess.run(['dotnet', 'build', 'WileyWidget.csproj', '--no-restore'],
                                  capture_output=True, text=True, check=True)
            print("  ✅ Build successful")

            if result.stdout:
                print(f"Build output:\n{result.stdout}")
        except subprocess.CalledProcessError as e:
            logging.error(f"Failed to build WileyWidget.csproj: {e}", exc_info=True)
            raise

    except subprocess.CalledProcessError as e:
        print(f"  ❌ Build failed: {e}")
        if e.stdout:
            print(f"STDOUT:\n{e.stdout}")
        if e.stderr:
            print(f"STDERR:\n{e.stderr}")
        return False

    return True

def run_application(debug_mode=False):
    """Run the WileyWidget application with debugging"""
    print("\n=== Running Application ===")
    debugpy.breakpoint()  # Debug breakpoint

    try:
        cmd = ['dotnet', 'run', '--project', 'WileyWidget.csproj']

        if debug_mode:
            # Add debug configuration if needed
            print("  🐛 Running in debug mode...")
        else:
            print("  🚀 Running application...")

        # Debug breakpoint before execution
        debugpy.breakpoint()

        # Run the application
        try:
            process = subprocess.Popen(cmd, stdout=subprocess.PIPE,
                                     stderr=subprocess.PIPE, text=True)
        except Exception as e:
            logging.error(f"Failed to launch WileyWidget.exe: {e}", exc_info=True)
            raise

        print(f"  ✅ Application started (PID: {process.pid})")
        print("     Press Ctrl+C to stop")

        # Monitor the process
        try:
            stdout, stderr = process.communicate(timeout=5)
            if stdout:
                print(f"Application output:\n{stdout}")
            if stderr:
                print(f"Application errors:\n{stderr}")
        except subprocess.TimeoutExpired:
            print("  ⏳ Application is running...")
            return process

    except Exception as e:
        print(f"  ❌ Failed to run application: {e}")
        return None

def monitor_startup_timing(skip_cleanup=False):
    """Monitor startup timing with debugging"""
    print("\n=== Startup Timing Analysis ===")
    debugpy.breakpoint()  # Debug breakpoint

    start_time = time.time()

    # Measure each phase
    phases = []

    # Phase 1: Process cleanup (optional)
    if not skip_cleanup:
        phase_start = time.time()
        cleanup_dotnet_processes()
        phases.append(("Process Cleanup", time.time() - phase_start))

    # Phase 2: Artifact cleanup (optional)
    if not skip_cleanup:
        phase_start = time.time()
        cleanup_dotnet_artifacts()
        phases.append(("Artifact Cleanup", time.time() - phase_start))

    # Phase 3: Build
    phase_start = time.time()
    build_success = build_application(incremental=True)
    phases.append(("Build", time.time() - phase_start))

    if build_success:
        # Phase 4: Application startup
        phase_start = time.time()
        run_application()
        phases.append(("Application Start", time.time() - phase_start))

    total_time = time.time() - start_time

    # Debug breakpoint for timing analysis
    debugpy.breakpoint()

    print("\n=== Timing Report ===")
    for phase, duration in phases:
        print(f"  {phase}: {duration:.2f}s")
    print(f"  Total: {total_time:.2f}s")

def main():
    """Main startup function with debugpy integration"""
    parser = argparse.ArgumentParser(description='Wiley Widget Startup with debugpy')
    parser.add_argument('--debug-port', type=int, default=5678,
                       help='Port for debugpy (default: 5678)')
    parser.add_argument('--no-wait', action='store_true',
                       help='Don\'t wait for debugger to attach')
    parser.add_argument('--timing', action='store_true',
                       help='Run with timing analysis')
    parser.add_argument('--skip-cleanup', action='store_true',
                       help='Skip cleanup steps for faster development iterations')
    parser.add_argument('--skip-debugpy', action='store_true',
                       help='Skip debugpy setup and run normally')

    args = parser.parse_args()

    print("🚀 Wiley Widget Development Startup (debugpy enabled)")
    print("=" * 50)

    # Setup debugpy unless skipped
    if not args.skip_debugpy:
        setup_debugpy(port=args.debug_port, wait_for_client=not args.no_wait)

    # Main startup logic with debugging
    debugpy.breakpoint()  # Main entry breakpoint

    if args.timing:
        monitor_startup_timing(skip_cleanup=args.skip_cleanup)
    else:
        # Standard startup sequence
        if not args.skip_cleanup:
            cleanup_dotnet_processes()
            cleanup_dotnet_artifacts()

        if build_application(incremental=not args.skip_cleanup):
            process = run_application()
            if process:
                try:
                    process.wait()
                except KeyboardInterrupt:
                    print("\n⏹️  Stopping application...")
                    process.terminate()
                    process.wait()

if __name__ == "__main__":
    main()
