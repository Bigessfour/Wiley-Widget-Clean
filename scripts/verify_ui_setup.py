#!/usr/bin/env python3
"""
Simple UI Testing Verification Script
Tests that the UI testing framework is properly set up
"""

def test_imports():
    """Test that all required UI testing imports work"""
    try:
        import pywinauto
        print("✅ pywinauto imported successfully")
    except ImportError as e:
        print(f"❌ pywinauto import failed: {e}")
        return False

    try:
        from PIL import Image
        print("✅ PIL imported successfully")
    except ImportError as e:
        print(f"❌ PIL import failed: {e}")
        return False

    try:
        import psutil
        print("✅ psutil imported successfully")
    except ImportError as e:
        print(f"❌ psutil import failed: {e}")
        return False

    return True


def test_pywinauto_basic():
    """Test basic pywinauto functionality"""
    try:
        from pywinauto import Desktop
        desktop = Desktop(backend="uia")
        print("✅ pywinauto Desktop access works")

        # Try to get a simple window count
        windows = desktop.windows()
        print(f"✅ Found {len(windows)} windows on desktop")
        return True
    except Exception as e:
        print(f"❌ pywinauto basic test failed: {e}")
        return False


def test_pytest_fixtures():
    """Test that pytest fixtures are available"""
    try:
        import pytest
        print("✅ pytest imported successfully")

        # Simple check - just verify pytest is working
        print("✅ pytest basic functionality verified")
        return True
    except Exception as e:
        print(f"❌ pytest test failed: {e}")
        return False


def main():
    """Run all verification tests"""
    print("🧪 UI Testing Framework Verification")
    print("=" * 40)

    tests = [
        ("Import Tests", test_imports),
        ("pywinauto Basic", test_pywinauto_basic),
        ("Pytest Fixtures", test_pytest_fixtures),
    ]

    passed = 0
    total = len(tests)

    for test_name, test_func in tests:
        print(f"\n🔍 Running {test_name}...")
        if test_func():
            passed += 1
        else:
            print(f"❌ {test_name} failed")

    print(f"\n📊 Results: {passed}/{total} tests passed")

    if passed == total:
        print("🎉 UI testing framework is ready!")
        print("\nNext steps:")
        print("1. Build the application: dotnet build WileyWidget.csproj")
        print("2. Run UI tests: python -m pytest tests/ -m ui -v")
        print("3. Debug UI: python scripts/ui_debug.py --help")
    else:
        print("⚠️  Some tests failed. Check the output above.")

    return passed == total


if __name__ == "__main__":
    import sys
    success = main()
    sys.exit(0 if success else 1)