#!/usr/bin/env python3
"""
Debug version of Wiley Widget Development Startup Script
This version includes debugging hooks and detailed logging
"""

import os
import sys
import time
import subprocess
import argparse
import pdb
from pathlib import Path

# Enable debugging if requested
DEBUG_MODE = os.environ.get('DEBUG_STARTUP', '').lower() in ('true', '1', 'yes')

def debug_breakpoint(name):
    """Insert a debug breakpoint if in debug mode"""
    if DEBUG_MODE:
        print(f"\n🔍 DEBUG BREAKPOINT: {name}")
        print("Press 'c' to continue, 'n' for next line, 's' to step into")
        pdb.set_trace()

def cleanup_dotnet_processes():
    """Clean up orphaned .NET processes"""
    debug_breakpoint("cleanup_start")

    print("=== .NET Process Cleanup ===")

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

        if not processes:
            print("✅ No orphaned .NET processes found")
            return True

        print(f"Found {len(processes)} .NET-related process(es):")
        for name, pid in processes:
            print(f"  - {name} (PID: {pid})")

        # Kill processes
        killed_count = 0
        for name, pid in processes:
            try:
                subprocess.run(['taskkill', '/PID', pid, '/F'],
                             capture_output=True, check=True)
                print(f"✅ Killed {name} (PID: {pid})")
                killed_count += 1
            except subprocess.CalledProcessError as e:
                print(f"❌ Failed to kill {name} (PID: {pid}): {e}")

        print(f"Cleaned up {killed_count} process(es)")

        # Clean up temporary files
        print("Cleaning up temporary files...")
        for dir_name in ['bin', 'obj']:
            if os.path.exists(dir_name):
                import shutil
                shutil.rmtree(dir_name, ignore_errors=True)
                print(f"✅ Removed {dir_name}/")

        print("✅ Temporary build files cleaned")

        debug_breakpoint("cleanup_complete")
        return True

    except Exception as e:
        print(f"❌ Error during cleanup: {e}")
        return False

def check_conflicts():
    """Check for conflicting WileyWidget processes"""
    debug_breakpoint("conflict_check_start")

    print("Checking for conflicts...")

    try:
        result = subprocess.run(['tasklist', '/FI', 'IMAGENAME eq WileyWidget.exe', '/FO', 'CSV', '/NH'],
                              capture_output=True, text=True)

        lines = result.stdout.strip().split('\n')
        conflicts = [line for line in lines if line.strip() and 'WileyWidget.exe' in line]

        if conflicts:
            print(f"⚠️  Found {len(conflicts)} WileyWidget process(es) still running")
            for conflict in conflicts:
                parts = conflict.split(',')
                if len(parts) >= 2:
                    pid = parts[1].strip('"')
                    print(f"  - PID: {pid}")

            response = input("Kill these processes? (y/N): ").lower().strip()
            if response == 'y':
                for conflict in conflicts:
                    parts = conflict.split(',')
                    if len(parts) >= 2:
                        pid = parts[1].strip('"')
                        try:
                            subprocess.run(['taskkill', '/PID', pid, '/F'],
                                         capture_output=True, check=True)
                            print(f"✅ Killed process (PID: {pid})")
                        except subprocess.CalledProcessError as e:
                            print(f"❌ Failed to kill process (PID: {pid}): {e}")
                debug_breakpoint("conflict_check_complete")
                return True

        print("✅ No conflicting processes found")
        debug_breakpoint("conflict_check_complete")
        return True

    except Exception as e:
        print(f"❌ Error checking conflicts: {e}")
        return False

def clean_build_artifacts():
    """Clean build artifacts"""
    debug_breakpoint("build_clean_start")

    print("Cleaning build artifacts...")

    try:
        result = subprocess.run(['dotnet', 'clean', 'WileyWidget.csproj'],
                              capture_output=True, text=True, cwd=os.getcwd())

        if result.returncode == 0:
            print("✅ Build artifacts cleaned")
            debug_breakpoint("build_clean_complete")
            return True
        else:
            print(f"⚠️  Clean failed: {result.stderr}")
            debug_breakpoint("build_clean_failed")
            return False

    except Exception as e:
        print(f"❌ Error cleaning build: {e}")
        return False

def start_development(no_watch=False):
    """Start development environment"""
    debug_breakpoint("dev_start_begin")

    print("Starting development...")

    if no_watch:
        print("Running without watch mode...")
        cmd = ['dotnet', 'run', '--project', 'WileyWidget.csproj']
    else:
        print("Starting dotnet watch...")
        print("Press Ctrl+C to stop, then run cleanup script if needed")
        cmd = ['dotnet', 'watch', 'run', '--project', 'WileyWidget.csproj']

    debug_breakpoint("before_dotnet_command")

    try:
        subprocess.run(cmd, cwd=os.getcwd())
        debug_breakpoint("dev_start_complete")
        return True
    except KeyboardInterrupt:
        print("Development session interrupted")
        debug_breakpoint("dev_interrupted")
        return True
    except Exception as e:
        print(f"❌ Error starting development: {e}")
        debug_breakpoint("dev_start_error")
        return False

def main():
    parser = argparse.ArgumentParser(description='Wiley Widget Development Startup (Debug Version)')
    parser.add_argument('--clean-only', action='store_true',
                       help='Only clean up and exit')
    parser.add_argument('--no-watch', action='store_true',
                       help='Start without watch mode')
    parser.add_argument('--verbose', action='store_true',
                       help='Verbose output')
    parser.add_argument('--debug', action='store_true',
                       help='Enable debug breakpoints')

    args = parser.parse_args()

    # Set debug mode
    global DEBUG_MODE
    DEBUG_MODE = DEBUG_MODE or args.debug

    if DEBUG_MODE:
        print("🐛 DEBUG MODE ENABLED")
        print("Use 'c' to continue, 'n' for next, 's' to step, 'q' to quit")

    debug_breakpoint("main_start")

    print("=== Wiley Widget Development Startup (Debug Version) ===")

    # Step 1: Clean up orphaned processes
    print("Step 1: Cleaning up orphaned processes...")
    if not cleanup_dotnet_processes():
        print("❌ Cleanup failed")
        return 1

    if args.clean_only:
        print("Clean-only mode - exiting")
        debug_breakpoint("clean_only_exit")
        return 0

    # Step 2: Check for conflicts
    print("Step 2: Checking for conflicts...")
    if not check_conflicts():
        print("❌ Conflict check failed")
        return 1

    # Step 3: Clean build artifacts
    print("Step 3: Cleaning build artifacts...")
    if not clean_build_artifacts():
        print("⚠️  Build clean failed, continuing...")

    # Step 4: Start development
    print("Step 4: Starting development...")
    if not start_development(args.no_watch):
        print("❌ Failed to start development")
        return 1

    debug_breakpoint("main_complete")
    print("Development session ended")
    return 0

if __name__ == '__main__':
    sys.exit(main())