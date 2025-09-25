#!/usr/bin/env python3
"""
Quick UI Inspection Test - Simplified version for debugging
"""

import time
import json
from pathlib import Path

def quick_inspect():
    """Quick inspection of any windows that appear"""
    try:
        from pywinauto import Application, Desktop
    except ImportError:
        print("❌ pywinauto not installed")
        return

    print("🔍 Quick UI inspection starting...")

    # Get initial window count
    desktop = Desktop(backend="uia")
    initial_windows = desktop.windows()
    initial_count = len(initial_windows)
    print(f"📊 Initial windows: {initial_count}")

    # Start the application
    app_path = str(Path(__file__).parent.parent / "bin" / "Debug" / "net9.0-windows" / "WileyWidget.exe")
    print(f"🚀 Starting: {app_path}")

    app = Application(backend="uia").start(app_path)

    # Wait and monitor for new windows
    max_wait = 30
    for i in range(max_wait):
        current_windows = desktop.windows()
        current_count = len(current_windows)

        if current_count > initial_count:
            print(f"✅ New windows detected! ({current_count - initial_count} new)")

            # List new windows
            for j, window in enumerate(current_windows):
                title = window.window_text()
                class_name = window.class_name()
                if title or class_name:
                    print(f"   Window {j}: '{title}' (class: {class_name})")

            # Try to inspect the first new window
            new_windows = current_windows[initial_count:]
            if new_windows:
                target_window = new_windows[0]
                print(f"🔍 Inspecting window: '{target_window.window_text()}'")

                try:
                    # Basic element inspection
                    elements = []
                    for elem in target_window.find_elements():
                        try:
                            elements.append({
                                'control_type': elem.control_type,
                                'title': elem.window_text(),
                                'class_name': elem.class_name,
                                'visible': elem.is_visible(),
                                'enabled': elem.is_enabled()
                            })
                        except Exception:
                            continue

                    print(f"📊 Found {len(elements)} elements")

                    # Save results
                    with open('quick_ui_inspect.json', 'w') as f:
                        json.dump(elements, f, indent=2)

                    print("💾 Results saved to quick_ui_inspect.json")
                    break

                except Exception as e:
                    print(f"❌ Inspection failed: {e}")

        time.sleep(1)
        print(f"⏳ Waiting... ({i+1}/{max_wait})")

    if current_count == initial_count:
        print("❌ No new windows appeared")

    app.kill()

if __name__ == "__main__":
    quick_inspect()