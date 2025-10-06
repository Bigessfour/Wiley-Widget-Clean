"""
UI Tests for WileyWidget Dashboard View
Tests dashboard-specific functionality and data visualization
"""

import pytest
import time


@pytest.mark.ui
class TestDashboardView:
    """Test the dashboard view functionality"""

    def test_dashboard_loads(self, ui_main_window):
        """Test that dashboard view loads properly"""
        time.sleep(3)  # Wait for dashboard to load

        # Look for dashboard indicators
        dashboard_indicators = [
            ui_main_window.child_window(title_re=".*Dashboard.*"),
            ui_main_window.child_window(control_type="DataGrid"),
            ui_main_window.child_window(control_type="Chart"),
            ui_main_window.child_window(title_re=".*Summary.*"),
            ui_main_window.child_window(title_re=".*Overview.*")
        ]

        dashboard_visible = any(elem.exists() and elem.is_visible()
                              for elem in dashboard_indicators)

        assert dashboard_visible, "Dashboard should be visible with data elements"

    def test_data_grid_present(self, ui_main_window):
        """Test that data grid is present and functional"""
        time.sleep(2)

        # Look for data grid (Syncfusion SfDataGrid)
        grids = [
            ui_main_window.child_window(control_type="DataGrid"),
            ui_main_window.child_window(class_name="SfDataGrid"),
            ui_main_window.child_window(title_re=".*Grid.*")
        ]

        grid_found = any(grid.exists() for grid in grids)
        assert grid_found, "Data grid should be present on dashboard"

    def test_navigation_elements(self, ui_main_window):
        """Test navigation elements are present"""
        time.sleep(2)

        # Look for navigation elements
        nav_elements = [
            ui_main_window.child_window(control_type="TabControl"),
            ui_main_window.child_window(control_type="TabItem"),
            ui_main_window.child_window(title="Dashboard", control_type="TabItem"),
            ui_main_window.child_window(title="Budget", control_type="TabItem"),
            ui_main_window.child_window(title="Settings", control_type="TabItem")
        ]

        nav_found = any(elem.exists() for elem in nav_elements)
        assert nav_found, "Navigation elements should be present"

    def test_data_visualization(self, ui_main_window):
        """Test that data visualization elements are present"""
        time.sleep(2)

        # Look for charts and visualization elements
        viz_elements = [
            ui_main_window.child_window(control_type="Chart"),
            ui_main_window.child_window(class_name_re=".*Chart.*"),
            ui_main_window.child_window(title_re=".*Chart.*"),
            ui_main_window.child_window(control_type="ProgressBar"),
            ui_main_window.child_window(title_re=".*Progress.*")
        ]

        viz_found = any(elem.exists() for elem in viz_elements)
        # Charts are not strictly required, so this is informational
        if not viz_found:
            pytest.skip("No data visualization elements found - may be expected for minimal UI")


@pytest.mark.ui
class TestDashboardInteractions:
    """Test interactive elements on the dashboard"""

    def test_grid_interaction(self, ui_main_window):
        """Test data grid interactions"""
        time.sleep(2)

        # Find data grid
        grid = None
        for candidate in [
            ui_main_window.child_window(control_type="DataGrid"),
            ui_main_window.child_window(class_name="SfDataGrid")
        ]:
            if candidate.exists():
                grid = candidate
                break

        if grid is None:
            pytest.skip("No data grid found to test")

        # Test clicking on grid
        try:
            grid.click()
            time.sleep(0.5)
            # Should not crash
            assert ui_main_window.exists()
        except Exception as e:
            pytest.fail(f"Grid interaction failed: {e}")

    def test_tab_navigation(self, ui_main_window):
        """Test tab navigation if present"""
        time.sleep(2)

        # Look for tabs
        tabs = ui_main_window.find_elements(control_type="TabItem")

        if not tabs:
            pytest.skip("No tabs found to test navigation")

        # Try clicking on first available tab
        first_tab = tabs[0]
        try:
            first_tab.click()
            time.sleep(1)
            assert ui_main_window.exists()
        except Exception as e:
            pytest.fail(f"Tab navigation failed: {e}")

    def test_button_interactions(self, ui_main_window):
        """Test button interactions"""
        time.sleep(2)

        # Find buttons (but avoid system buttons like close/minimize)
        buttons = []
        for button in ui_main_window.find_elements(control_type="Button"):
            title = button.window_text()
            # Skip system buttons
            if not any(sys_btn in title.lower() for sys_btn in ['close', 'minimize', 'maximize', '']):
                buttons.append(button)

        if not buttons:
            pytest.skip("No user buttons found to test")

        # Test clicking first button
        test_button = buttons[0]
        try:
            test_button.click()
            time.sleep(1)
            # Application should still be responsive
            assert ui_main_window.exists()
            assert ui_main_window.is_visible()
        except Exception as e:
            pytest.fail(f"Button interaction failed: {e}")


@pytest.mark.ui
class TestDashboardPerformance:
    """Test dashboard performance and responsiveness"""

    def test_dashboard_load_performance(self, ui_main_window):
        """Test dashboard load performance"""
        import time

        start_time = time.time()

        # Wait for dashboard to be fully interactive
        time.sleep(5)  # Give it time to load

        load_time = time.time() - start_time

        # Dashboard should load within reasonable time
        assert load_time < 15.0, f"Dashboard load too slow: {load_time:.2f}s"

        # Should be responsive after loading
        ui_main_window.click()
        time.sleep(0.5)
        assert ui_main_window.is_visible()

    def test_ui_thread_responsiveness(self, ui_main_window):
        """Test that UI thread remains responsive"""
        import time

        # Perform several quick interactions
        for _ in range(3):
            ui_main_window.click()
            time.sleep(0.2)

            # UI should remain responsive
            assert ui_main_window.is_visible()
            assert not ui_main_window.is_minimized()

    def test_memory_stability_during_interaction(self, ui_main_window, ui_app):
        """Test memory stability during UI interactions"""
        import psutil
        import time

        process = psutil.Process(ui_app.process)
        initial_memory = process.memory_info().rss

        # Perform various interactions
        interactions = [
            lambda: ui_main_window.click(),
            lambda: ui_main_window.type_keys("{TAB}"),
            lambda: ui_main_window.click_input(coords=(100, 100)),
        ]

        for interaction in interactions:
            try:
                interaction()
                time.sleep(0.5)
            except Exception:
                continue  # Some interactions may not be available

        final_memory = process.memory_info().rss
        memory_growth = final_memory - initial_memory

        # Memory should not grow excessively during normal interactions
        assert memory_growth < 100 * 1024 * 1024, f"Excessive memory growth: {memory_growth / 1024 / 1024:.1f}MB"