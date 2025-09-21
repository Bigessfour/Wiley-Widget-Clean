"""
Pytest configuration and fixtures for Wiley Widget tests
"""
import pytest


# Register custom markers
def pytest_configure(config):
    """Register custom pytest markers"""
    config.addinivalue_line("markers", "unit: Fast unit tests (< 100ms)")
    config.addinivalue_line("markers", "integration: Integration tests (< 5s)")
    config.addinivalue_line("markers", "slow: Slow tests (> 5s) - skipped by default")
    config.addinivalue_line("markers", "ui: UI tests (require UI)")
    config.addinivalue_line("markers", "smoke: Critical path tests only")
    config.addinivalue_line("markers", "azure: Tests requiring Azure resources")


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
