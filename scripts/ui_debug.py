#!/usr/bin/env python3
"""
UI Debugging and Inspection Tool for WileyWidget
Provides utilities to inspect, debug, and test UI elements
"""

import argparse
import time
import json
import sys
from pathlib import Path
from typing import Dict, List, Any, Optional

def inspect_ui_elements(app_path: str, output_file: Optional[str] = None):
    """Inspect and catalog all UI elements in the application"""
    try:
        from pywinauto import Application
    except ImportError:
        print("❌ pywinauto not installed. Run: pip install pywinauto")
        return

    print(f"🔍 Inspecting UI elements in: {app_path}")

    # Start the application
    app = Application(backend="uia").start(app_path)

    # Wait for main window
    main_window = None
    for i in range(30):
        try:
            # Try different title patterns
            for pattern in [".*Wiley.*", ".*Widget.*", "MainWindow"]:
                try:
                    main_window = app.window(title_re=pattern)
                    if main_window.exists():
                        break
                except:
                    continue
            if main_window and main_window.exists():
                break
        except:
            pass
        print(f"⏳ Waiting for window... ({i+1}/30)")
        time.sleep(1)

    if not main_window or not main_window.exists():
        print("❌ Could not find main application window")
        app.kill()
        return

    print("✅ Main window found, inspecting elements...")

    # Inspect all elements
    elements = inspect_window_elements(main_window)

    # Save to file if requested
    if output_file:
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(elements, f, indent=2, ensure_ascii=False)
        print(f"💾 Element inspection saved to: {output_file}")

    # Print summary
    print(f"\n📊 UI Inspection Summary:")
    print(f"   Total elements found: {len(elements)}")

    # Count by control type
    control_types = {}
    for elem in elements:
        ctrl_type = elem.get('control_type', 'Unknown')
        control_types[ctrl_type] = control_types.get(ctrl_type, 0) + 1

    print("   Elements by type:")
    for ctrl_type, count in sorted(control_types.items()):
        print(f"     {ctrl_type}: {count}")

    app.kill()


def inspect_window_elements(window, max_depth: int = 3, current_depth: int = 0) -> List[Dict[str, Any]]:
    """Recursively inspect all elements in a window"""
    elements = []

    if current_depth > max_depth:
        return elements

    try:
        # Get all child elements
        children = window.find_elements()

        for child in children:
            try:
                element_info = {
                    'control_type': child.control_type,
                    'class_name': child.class_name,
                    'title': child.window_text(),
                    'automation_id': getattr(child, 'automation_id', ''),
                    'is_visible': child.is_visible(),
                    'is_enabled': child.is_enabled(),
                    'rectangle': {
                        'left': child.rectangle.left,
                        'top': child.rectangle.top,
                        'right': child.rectangle.right,
                        'bottom': child.rectangle.bottom,
                        'width': child.rectangle.width(),
                        'height': child.rectangle.height()
                    },
                    'depth': current_depth
                }

                elements.append(element_info)

                # Recursively inspect children if not too deep
                if current_depth < max_depth:
                    try:
                        child_elements = inspect_window_elements(child, max_depth, current_depth + 1)
                        elements.extend(child_elements)
                    except:
                        pass  # Skip if can't inspect children

            except Exception as e:
                # Skip elements that can't be inspected
                continue

    except Exception as e:
        print(f"⚠️  Error inspecting window: {e}")

    return elements


def take_ui_screenshot(app_path: str, output_file: str):
    """Take a screenshot of the UI for visual debugging"""
    try:
        from pywinauto import Application
        from PIL import ImageGrab
    except ImportError:
        print("❌ Required packages not installed. Run: pip install pywinauto pillow")
        return

    print(f"📸 Taking UI screenshot: {app_path}")

    # Start the application
    app = Application(backend="uia").start(app_path)

    # Wait for main window
    main_window = None
    for i in range(30):
        try:
            main_window = app.window(title_re=".*Wiley.*Widget.*")
            if main_window.exists():
                break
        except:
            pass
        time.sleep(1)

    if not main_window or not main_window.exists():
        print("❌ Could not find main application window")
        app.kill()
        return

    # Wait a bit for UI to settle
    time.sleep(3)

    # Take screenshot
    try:
        screenshot = ImageGrab.grab()
        screenshot.save(output_file)
        print(f"💾 Screenshot saved to: {output_file}")
    except Exception as e:
        print(f"❌ Failed to take screenshot: {e}")

    app.kill()


def test_ui_interactions(app_path: str, interaction_file: Optional[str] = None):
    """Test various UI interactions for debugging"""
    try:
        from pywinauto import Application
    except ImportError:
        print("❌ pywinauto not installed. Run: pip install pywinauto")
        return

    print(f"🧪 Testing UI interactions: {app_path}")

    # Start the application
    app = Application(backend="uia").start(app_path)

    # Wait for main window
    main_window = None
    for i in range(30):
        try:
            main_window = app.window(title_re=".*Wiley.*Widget.*")
            if main_window.exists():
                break
        except:
            pass
        time.sleep(1)

    if not main_window or not main_window.exists():
        print("❌ Could not find main application window")
        app.kill()
        return

    print("✅ Application started, running interaction tests...")

    # Basic interaction tests
    test_results = {}

    # Test 1: Window responsiveness
    try:
        start_time = time.time()
        main_window.click()
        response_time = time.time() - start_time
        test_results['window_click_response'] = f"{response_time:.3f}s"
        print(f"✅ Window click response: {response_time:.3f}s")
    except Exception as e:
        test_results['window_click_response'] = f"FAILED: {e}"
        print(f"❌ Window click failed: {e}")

    # Test 2: Keyboard navigation
    try:
        main_window.set_focus()
        time.sleep(0.5)
        main_window.type_keys("{TAB}")
        time.sleep(0.5)
        main_window.type_keys("{TAB}")
        test_results['keyboard_navigation'] = "PASSED"
        print("✅ Keyboard navigation: PASSED")
    except Exception as e:
        test_results['keyboard_navigation'] = f"FAILED: {e}"
        print(f"❌ Keyboard navigation failed: {e}")

    # Test 3: Find interactive elements
    try:
        buttons = main_window.find_elements(control_type="Button")
        test_results['buttons_found'] = len(buttons)
        print(f"ℹ️  Buttons found: {len(buttons)}")

        tabs = main_window.find_elements(control_type="TabItem")
        test_results['tabs_found'] = len(tabs)
        print(f"ℹ️  Tabs found: {len(tabs)}")

        grids = main_window.find_elements(control_type="DataGrid")
        test_results['grids_found'] = len(grids)
        print(f"ℹ️  Data grids found: {len(grids)}")

    except Exception as e:
        test_results['element_discovery'] = f"FAILED: {e}"
        print(f"❌ Element discovery failed: {e}")

    # Test 4: Window operations
    try:
        original_rect = main_window.rectangle()
        main_window.move_window(
            x=original_rect.left + 10,
            y=original_rect.top + 10,
            width=original_rect.width(),
            height=original_rect.height()
        )
        time.sleep(0.5)
        test_results['window_move'] = "PASSED"
        print("✅ Window move: PASSED")
    except Exception as e:
        test_results['window_move'] = f"FAILED: {e}"
        print(f"❌ Window move failed: {e}")

    # Save results if requested
    if interaction_file:
        with open(interaction_file, 'w', encoding='utf-8') as f:
            json.dump(test_results, f, indent=2, ensure_ascii=False)
        print(f"💾 Interaction test results saved to: {interaction_file}")

    app.kill()
    print("🏁 UI interaction testing completed")


def main():
    """Main entry point for UI debugging tool"""
    parser = argparse.ArgumentParser(description='UI Debugging Tool for WileyWidget')
    parser.add_argument('--app', required=True,
                       help='Path to WileyWidget.exe')
    parser.add_argument('--inspect', action='store_true',
                       help='Inspect and catalog UI elements')
    parser.add_argument('--screenshot', type=str,
                       help='Take UI screenshot and save to file')
    parser.add_argument('--test-interactions', action='store_true',
                       help='Test basic UI interactions')
    parser.add_argument('--output', type=str,
                       help='Output file for inspection/test results')

    args = parser.parse_args()

    if not Path(args.app).exists():
        print(f"❌ Application not found: {args.app}")
        sys.exit(1)

    if args.inspect:
        inspect_ui_elements(args.app, args.output)
    elif args.screenshot:
        take_ui_screenshot(args.app, args.screenshot)
    elif args.test_interactions:
        test_ui_interactions(args.app, args.output)
    else:
        print("❌ Please specify an action: --inspect, --screenshot, or --test-interactions")
        sys.exit(1)


if __name__ == "__main__":
    main()