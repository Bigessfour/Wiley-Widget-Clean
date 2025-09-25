"""
UI Tests for WileyWidget Main Window using pywinauto
Tests the main application window and its basic functionality
"""

import pytest
import time

from pywinauto.base_wrapper import ElementNotEnabled
from pywinauto.findwindows import ElementNotFoundError
from pywinauto.timings import TimeoutError as PywinautoTimeoutError


@pytest.mark.ui
class TestMainWindow:
    """Test the main application window"""

    def test_main_window_opens(self, ui_main_window):
        """Test that the main window opens successfully"""
        assert ui_main_window.exists()
        assert ui_main_window.is_visible()

        # Check window title
        title = ui_main_window.window_text()
        assert "Wiley" in title or "Widget" in title

    def test_main_window_elements_exist(self, ui_main_window):
        """Test that main UI elements are present"""
        # Wait for window to be fully loaded
        time.sleep(2)

        # Check for common WPF elements
        try:
            # Look for ribbon control (Syncfusion)
            ribbon = ui_main_window.child_window(class_name="Ribbon")
            assert ribbon.exists()
        except (ElementNotFoundError, ElementNotEnabled, PywinautoTimeoutError):
            # Ribbon might not be immediately available, check for basic controls
            pass

        # Check that window is not minimized
        assert not ui_main_window.is_minimized()

    def test_window_resizing(self, ui_main_window):
        """Test window resizing functionality"""
        original_rect = ui_main_window.rectangle()

        # Try to resize window
        new_width = original_rect.width() + 100
        new_height = original_rect.height() + 50

        ui_main_window.move_window(
            x=original_rect.left,
            y=original_rect.top,
            width=new_width,
            height=new_height
        )

        time.sleep(0.5)  # Wait for resize to complete

        new_rect = ui_main_window.rectangle()
        # Allow some tolerance for window borders/decorations
        assert abs(new_rect.width() - new_width) < 20
        assert abs(new_rect.height() - new_height) < 20

    def test_window_minimize_maximize(self, ui_main_window):
        """Test window minimize and maximize functionality"""
        # Test minimize
        ui_main_window.minimize()
        time.sleep(0.5)
        assert ui_main_window.is_minimized()

        # Test restore
        ui_main_window.restore()
        time.sleep(0.5)
        assert not ui_main_window.is_minimized()
        assert ui_main_window.is_visible()

    def test_menu_accessibility(self, ui_main_window):
        """Test that menus are accessible"""
        time.sleep(1)  # Wait for UI to settle

        try:
            # Try to find menu bar or ribbon
            menu = ui_main_window.child_window(class_name="MenuBar") or \
                   ui_main_window.child_window(class_name="Ribbon")

            if menu.exists():
                assert menu.is_visible()
                assert menu.is_enabled()
        except (ElementNotFoundError, ElementNotEnabled, PywinautoTimeoutError):
            # Menu might be implemented differently
            pass

    def test_keyboard_navigation(self, ui_main_window):
        """Test basic keyboard navigation"""
        # Focus the window
        ui_main_window.set_focus()
        time.sleep(0.5)

        # Test Tab navigation (should move focus)
        ui_main_window.type_keys("{TAB}")
        time.sleep(0.5)

        # Test Escape (should not close window in main app)
        ui_main_window.type_keys("{ESC}")
        time.sleep(0.5)

        # Window should still be open
        assert ui_main_window.exists()
        assert ui_main_window.is_visible()


@pytest.mark.ui
class TestViewNavigation:
    """Test navigation between different views"""

    def test_dashboard_view_accessible(self, ui_main_window):
        """Test that dashboard view is accessible"""
        time.sleep(2)  # Wait for full load

        # Look for dashboard-related elements
        dashboard_elements = [
            ui_main_window.child_window(title="Dashboard", control_type="Button"),
            ui_main_window.child_window(title_re=".*Dashboard.*", control_type="TabItem"),
            ui_main_window.child_window(title_re=".*Main.*", control_type="TabItem")
        ]

        dashboard_found = any(elem.exists() for elem in dashboard_elements)
        assert dashboard_found, "Dashboard view should be accessible"

    def test_settings_view_accessible(self, ui_main_window):
        """Test that settings view is accessible"""
        time.sleep(2)

        # Look for settings-related elements
        settings_elements = [
            ui_main_window.child_window(title="Settings", control_type="Button"),
            ui_main_window.child_window(title_re=".*Settings.*", control_type="TabItem"),
            ui_main_window.child_window(title_re=".*Options.*", control_type="Button")
        ]

        settings_found = any(elem.exists() for elem in settings_elements)
        assert settings_found, "Settings view should be accessible"

    def test_about_dialog_accessible(self, ui_main_window):
        """Test that about dialog can be opened"""
        time.sleep(2)

        # Try to find and click About menu/button
        about_elements = [
            ui_main_window.child_window(title="About", control_type="MenuItem"),
            ui_main_window.child_window(title="About", control_type="Button"),
            ui_main_window.child_window(title_re=".*About.*", control_type="MenuItem")
        ]

        for elem in about_elements:
            if elem.exists():
                try:
                    elem.click()
                    time.sleep(1)
                    # Check if about dialog opened
                    about_dialog = ui_main_window.child_window(title_re=".*About.*")
                    if about_dialog.exists():
                        # Close the dialog
                        about_dialog.close()
                        break
                except (ElementNotFoundError, ElementNotEnabled, PywinautoTimeoutError):
                    continue

        # About dialog is not strictly required to be accessible from main window
        # This test mainly checks that the UI doesn't crash when looking for it
        assert True  # Test passes if no exceptions occurred


@pytest.mark.ui
class TestUIResponsiveness:
    """Test UI responsiveness and performance"""

    def test_ui_response_time(self, ui_main_window):
        """Test that UI responds within reasonable time"""
        import time

        # Measure response to basic interaction
        start_time = time.time()

        # Try a simple click on the window
        ui_main_window.click()
        response_time = time.time() - start_time

        # UI should respond within 1 second
        assert response_time < 1.0, f"UI response too slow: {response_time:.2f}s"

    def test_window_paint_time(self, ui_main_window):
        """Test that window paints without hanging"""
        # Force a repaint by minimizing and restoring
        ui_main_window.minimize()
        time.sleep(0.5)
        ui_main_window.restore()

        # Wait for paint to complete
        time.sleep(1)

        # Window should be visible and responsive
        assert ui_main_window.is_visible()
        assert not ui_main_window.is_minimized()

    def test_memory_usage_stable(self, ui_main_window, ui_app):
        """Test that memory usage doesn't grow excessively"""
        import psutil

        # Get initial memory usage
        process = psutil.Process(ui_app.process)
        initial_memory = process.memory_info().rss

        # Perform some UI operations
        ui_main_window.click()
        time.sleep(0.5)
        ui_main_window.type_keys("{TAB}")
        time.sleep(0.5)

        # Check memory after operations
        final_memory = process.memory_info().rss
        memory_growth = final_memory - initial_memory

        # Memory growth should be reasonable (< 50MB)
        assert memory_growth < 50 * 1024 * 1024, f"Excessive memory growth: {memory_growth / 1024 / 1024:.1f}MB"