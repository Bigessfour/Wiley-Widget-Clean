"""
Pytest configuration and fixtures for Wiley Widget tests
"""
import pytest
import time
import subprocess
from pathlib import Path


# Register custom markers
def pytest_configure(config):
    """Register custom pytest markers"""
    config.addinivalue_line("markers", "unit: Fast unit tests (< 100ms)")
    config.addinivalue_line("markers", "integration: Integration tests (< 5s)")
    config.addinivalue_line("markers", "slow: Slow tests (> 5s) - skipped by default")
    config.addinivalue_line("markers", "ui: UI tests (require UI)")
    config.addinivalue_line("markers", "smoke: Critical path tests only")
    config.addinivalue_line("markers", "azure: Tests requiring Azure resources")


# UI Testing fixtures
@pytest.fixture(scope="session")
def ui_app_path():
    """Path to the WileyWidget executable"""
    project_root = Path(__file__).parent.parent
    exe_path = project_root / "bin" / "Debug" / "net9.0-windows" / "WileyWidget.exe"

    if not exe_path.exists():
        # Try to build the application first
        build_result = subprocess.run(
            ["dotnet", "build", str(project_root / "WileyWidget.csproj")],
            capture_output=True,
            text=True,
            cwd=project_root
        )
        if build_result.returncode != 0:
            pytest.skip(f"Could not build application: {build_result.stderr}")

    return str(exe_path)


@pytest.fixture
def ui_app(ui_app_path):
    """Launched WileyWidget application for UI testing"""
    try:
        from pywinauto import Application
    except ImportError:
        pytest.skip("pywinauto not installed. Run: pip install -r requirements-test.txt")

    # Start the application
    app = Application(backend="uia").start(ui_app_path)

    # Wait for main window to appear
    main_window = None
    for _ in range(30):  # Wait up to 30 seconds
        try:
            main_window = app.window(title_re=".*Wiley.*Widget.*", class_name="#32770")
            if main_window.exists():
                break
        except Exception:
            pass
        time.sleep(1)

    if main_window is None or not main_window.exists():
        pytest.fail("Could not find main application window")

    yield app

    # Cleanup
    try:
        app.kill()
    except (ProcessLookupError, OSError):
        pass


@pytest.fixture
def ui_main_window(ui_app):
    """Main window of the WileyWidget application"""
    return ui_app.window(title_re=".*Wiley.*Widget.*")


# Common fixtures
@pytest.fixture
def sample_data():
    """Sample test data"""
    return {
        "annual_expenses": 100000,
        "target_reserve_percentage": 15,
        "pay_raise_percentage": 3,
        "benefits_increase_percentage": 2,
        "equipment_cost": 25000
    }


@pytest.fixture
def mock_ai_service():
    """Mock AI service for testing"""
    from unittest.mock import Mock
    return Mock()


@pytest.fixture
def mock_charge_calculator():
    """Mock service charge calculator for testing"""
    from unittest.mock import Mock
    return Mock()


@pytest.fixture
def mock_scenario_engine():
    """Mock what-if scenario engine for testing"""
    from unittest.mock import Mock
    return Mock()
