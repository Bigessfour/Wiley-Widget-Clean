#!/usr/bin/env python3
"""
UI Testing Runner for WileyWidget
Installs dependencies and runs UI tests using Python
"""

import subprocess
import sys
import os
from pathlib import Path


def install_ui_dependencies():
    """Install UI testing dependencies"""
    print("📦 Installing UI testing dependencies...")

    requirements = [
        "pywinauto>=0.6.8",
        "pillow>=10.0.0",
        "opencv-python>=4.8.0",
        "psutil>=5.9.0"
    ]

    for req in requirements:
        print(f"  Installing {req}...")
        result = subprocess.run([
            sys.executable, "-m", "pip", "install", req
        ], capture_output=True, text=True)

        if result.returncode != 0:
            print(f"❌ Failed to install {req}: {result.stderr}")
            return False

    print("✅ UI testing dependencies installed")
    return True


def run_ui_tests(test_filter: str = "ui", verbose: bool = False):
    """Run UI tests with pytest"""
    print(f"🧪 Running UI tests (filter: {test_filter})...")

    project_root = Path(__file__).parent.parent
    tests_dir = project_root / "tests"

    cmd = [
        sys.executable, "-m", "pytest",
        str(tests_dir),
        f"-m {test_filter}",
        "--tb=short",
        "-v" if verbose else "--quiet"
    ]

    if verbose:
        cmd.append("--capture=no")

    result = subprocess.run(cmd, cwd=project_root)

    return result.returncode == 0


def run_ui_inspection(app_path: str, output_file: str = None):
    """Run UI inspection using the debug script"""
    print("🔍 Running UI inspection...")

    project_root = Path(__file__).parent.parent
    script_path = project_root / "scripts" / "ui_debug.py"

    cmd = [
        sys.executable, str(script_path),
        "--app", app_path,
        "--inspect"
    ]

    if output_file:
        cmd.extend(["--output", output_file])

    result = subprocess.run(cmd, cwd=project_root)
    return result.returncode == 0


def run_ui_screenshot(app_path: str, output_file: str):
    """Take UI screenshot"""
    print("📸 Taking UI screenshot...")

    project_root = Path(__file__).parent.parent
    script_path = project_root / "scripts" / "ui_debug.py"

    cmd = [
        sys.executable, str(script_path),
        "--app", app_path,
        "--screenshot", output_file
    ]

    result = subprocess.run(cmd, cwd=project_root)
    return result.returncode == 0


def main():
    """Main entry point"""
    import argparse

    parser = argparse.ArgumentParser(description='UI Testing Runner for WileyWidget')
    parser.add_argument('--install-deps', action='store_true',
                       help='Install UI testing dependencies')
    parser.add_argument('--run-tests', action='store_true',
                       help='Run UI tests')
    parser.add_argument('--inspect-ui', type=str,
                       help='Inspect UI elements (provide app path)')
    parser.add_argument('--screenshot', type=str,
                       help='Take UI screenshot (provide output file)')
    parser.add_argument('--app-path', type=str,
                       help='Path to WileyWidget.exe for inspection/screenshot')
    parser.add_argument('--verbose', action='store_true',
                       help='Verbose output')
    parser.add_argument('--output', type=str,
                       help='Output file for inspection results')

    args = parser.parse_args()

    success = True

    if args.install_deps:
        success &= install_ui_dependencies()

    if args.run_tests:
        success &= run_ui_tests(verbose=args.verbose)

    if args.inspect_ui:
        if not args.app_path:
            print("❌ --app-path required for --inspect-ui")
            success = False
        else:
            output_file = args.output or "ui_inspection.json"
            success &= run_ui_inspection(args.app_path, output_file)

    if args.screenshot:
        if not args.app_path:
            print("❌ --app-path required for --screenshot")
            success = False
        else:
            success &= run_ui_screenshot(args.app_path, args.screenshot)

    if not any([args.install_deps, args.run_tests, args.inspect_ui, args.screenshot]):
        print("ℹ️  No action specified. Use --help for options.")
        print("\nExample usage:")
        print("  python ui_test_runner.py --install-deps --run-tests")
        print("  python ui_test_runner.py --inspect-ui --app-path bin/Debug/net9.0-windows/WileyWidget.exe")
        print("  python ui_test_runner.py --screenshot ui_screenshot.png --app-path bin/Debug/net9.0-windows/WileyWidget.exe")

    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()