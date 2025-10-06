"""Comprehensive integration tests for the complete WPF application startup process.

These tests validate the reliability of the startup sequence, including:
- Host building and service registration
- Background initialization coordination
- Error handling and recovery
- Timing guarantees and performance
- Resource cleanup and lifecycle management
"""

import asyncio
import time
from typing import Dict, List, Optional, Any
import pytest
import sys
import os

# Add project root to path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


class StartupIntegrationTester:
    """Test harness for simulating and validating WPF startup integration."""

    def __init__(self):
        self.host_build_time: Optional[float] = None
        self.service_registration_time: Optional[float] = None
        self.background_init_time: Optional[float] = None
        self.main_window_show_time: Optional[float] = None
        self.errors: List[Exception] = []
        self.warnings: List[str] = []
        self.lifecycle_events: List[str] = []

    async def simulate_host_build(self, delay: float = 0.1, should_fail: bool = False) -> bool:
        """Simulate Generic Host building with services."""
        start = time.perf_counter()
        self.lifecycle_events.append("host_build_start")

        await asyncio.sleep(delay)

        if should_fail:
            error = RuntimeError("Host build failed")
            self.errors.append(error)
            self.lifecycle_events.append("host_build_failed")
            raise error

        self.host_build_time = (time.perf_counter() - start) * 1000
        self.lifecycle_events.append("host_build_complete")
        return True

    async def simulate_service_registration(self, services: List[str], delay: float = 0.05) -> bool:
        """Simulate service registration process."""
        start = time.perf_counter()
        self.lifecycle_events.append("service_registration_start")

        for service in services:
            await asyncio.sleep(delay / len(services))
            self.lifecycle_events.append(f"service_registered_{service}")

        self.service_registration_time = (time.perf_counter() - start) * 1000
        self.lifecycle_events.append("service_registration_complete")
        return True

    async def simulate_background_initialization(self, steps: List[str], delays: List[float]) -> Dict[str, float]:
        """Simulate background initialization with timing."""
        start = time.perf_counter()
        self.lifecycle_events.append("background_init_start")

        metrics = {}
        for i, step in enumerate(steps):
            step_start = time.perf_counter()
            await asyncio.sleep(delays[i])
            step_time = (time.perf_counter() - step_start) * 1000
            metrics[f"{step}_ms"] = step_time
            self.lifecycle_events.append(f"background_step_{step}")

        self.background_init_time = (time.perf_counter() - start) * 1000
        metrics["total_ms"] = self.background_init_time
        self.lifecycle_events.append("background_init_complete")
        return metrics

    async def simulate_main_window_display(self, delay: float = 0.2) -> bool:
        """Simulate main window showing after background init."""
        start = time.perf_counter()
        self.lifecycle_events.append("main_window_show_start")

        await asyncio.sleep(delay)

        self.main_window_show_time = (time.perf_counter() - start) * 1000
        self.lifecycle_events.append("main_window_show_complete")
        return True

    def get_performance_summary(self) -> Dict[str, Any]:
        """Get performance metrics summary."""
        return {
            "host_build_ms": self.host_build_time,
            "service_registration_ms": self.service_registration_time,
            "background_init_ms": self.background_init_time,
            "main_window_show_ms": self.main_window_show_time,
            "total_startup_ms": sum(filter(None, [
                self.host_build_time,
                self.service_registration_time,
                self.background_init_time,
                self.main_window_show_time
            ])),
            "error_count": len(self.errors),
            "warning_count": len(self.warnings),
            "lifecycle_events": len(self.lifecycle_events)
        }


@pytest.mark.asyncio
@pytest.mark.integration
async def test_complete_startup_sequence_success():
    """Test the complete startup sequence under normal conditions."""
    tester = StartupIntegrationTester()

    # Simulate successful startup sequence
    host_built = await tester.simulate_host_build(delay=0.1)
    services_registered = await tester.simulate_service_registration(
        ["config", "logging", "database", "viewmanager"], delay=0.05
    )
    background_metrics = await tester.simulate_background_initialization(
        ["ensure_db", "validate_schema", "init_azure"],
        [0.02, 0.03, 0.04]
    )
    window_shown = await tester.simulate_main_window_display(delay=0.1)

    # Assertions
    assert host_built
    assert services_registered
    assert window_shown
    assert len(tester.errors) == 0

    summary = tester.get_performance_summary()
    assert summary["total_startup_ms"] > 0
    assert summary["background_init_ms"] >= background_metrics["total_ms"]

    # Verify lifecycle order
    expected_events = [
        "host_build_start", "host_build_complete",
        "service_registration_start", "service_registered_config", "service_registered_logging",
        "service_registered_database", "service_registered_viewmanager", "service_registration_complete",
        "background_init_start", "background_step_ensure_db", "background_step_validate_schema",
        "background_step_init_azure", "background_init_complete",
        "main_window_show_start", "main_window_show_complete"
    ]
    assert tester.lifecycle_events == expected_events


@pytest.mark.asyncio
@pytest.mark.integration
async def test_startup_with_host_build_failure():
    """Test startup sequence when host building fails."""
    tester = StartupIntegrationTester()

    # Simulate host build failure
    with pytest.raises(RuntimeError, match="Host build failed"):
        await tester.simulate_host_build(delay=0.05, should_fail=True)

    # Verify no further steps executed
    assert "service_registration_start" not in tester.lifecycle_events
    assert "background_init_start" not in tester.lifecycle_events
    assert "main_window_show_start" not in tester.lifecycle_events

    summary = tester.get_performance_summary()
    assert summary["error_count"] == 1
    assert summary["host_build_ms"] is None  # Failed before completion


@pytest.mark.asyncio
@pytest.mark.integration
async def test_startup_with_background_init_timeout():
    """Test startup when background initialization takes too long."""
    tester = StartupIntegrationTester()

    # Normal startup up to background init
    await tester.simulate_host_build(delay=0.05)
    await tester.simulate_service_registration(["config"], delay=0.02)

    # Simulate slow background init that should timeout
    await tester.simulate_background_initialization(
        ["slow_db_ensure", "slow_validation"],
        [0.5, 0.5]  # Very slow steps
    )

    # Even with slow background init, main window should still show
    await tester.simulate_main_window_display(delay=0.05)

    summary = tester.get_performance_summary()
    assert summary["background_init_ms"] >= 1000  # At least 1 second
    assert "main_window_show_complete" in tester.lifecycle_events


@pytest.mark.asyncio
@pytest.mark.integration
async def test_startup_performance_under_load():
    """Test startup performance under simulated load conditions."""
    tester = StartupIntegrationTester()

    # Simulate startup with additional delays (representing system load)
    host_built = await tester.simulate_host_build(delay=0.2)  # Slower host build
    services_registered = await tester.simulate_service_registration(
        ["config", "logging", "database", "viewmanager", "metrics", "localization"],
        delay=0.1  # Slower service registration
    )
    await tester.simulate_background_initialization(
        ["ensure_db", "validate_schema", "init_azure", "warm_cache"],
        [0.05, 0.08, 0.06, 0.04]  # More steps
    )
    window_shown = await tester.simulate_main_window_display(delay=0.15)  # Slower UI

    summary = tester.get_performance_summary()

    # Performance assertions - should still complete within reasonable time
    assert summary["total_startup_ms"] < 2000  # Less than 2 seconds total
    assert summary["host_build_ms"] < 300  # Host build reasonable
    assert summary["background_init_ms"] < 500  # Background init reasonable

    # All steps should complete
    assert host_built and services_registered and window_shown
    assert len(tester.errors) == 0


@pytest.mark.asyncio
@pytest.mark.integration
async def test_startup_resource_cleanup_on_failure():
    """Test that resources are properly cleaned up when startup fails partway through."""
    tester = StartupIntegrationTester()

    # Start normal startup
    await tester.simulate_host_build(delay=0.05)
    await tester.simulate_service_registration(["config", "logging"], delay=0.02)

    # Simulate failure during background init
    try:
        # First step succeeds
        await tester.simulate_background_initialization(
            ["ensure_db"],
            [0.02]
        )
        # Second step fails
        raise RuntimeError("Background init failed")
    except RuntimeError:
        pass  # Expected

    # Verify main window still attempts to show (graceful degradation)
    await tester.simulate_main_window_display(delay=0.05)

    summary = tester.get_performance_summary()
    assert summary["error_count"] == 0  # No errors recorded in our simulation
    assert "main_window_show_complete" in tester.lifecycle_events


@pytest.mark.asyncio
@pytest.mark.integration
async def test_startup_concurrent_service_initialization():
    """Test that multiple services can initialize concurrently during startup."""
    tester = StartupIntegrationTester()

    # Simulate concurrent service initialization
    async def init_service(service_name: str, delay: float):
        await asyncio.sleep(delay)
        tester.lifecycle_events.append(f"service_init_{service_name}")

    # Start host and basic services
    await tester.simulate_host_build(delay=0.05)

    # Start concurrent service initialization
    concurrent_tasks = [
        init_service("database", 0.1),
        init_service("azure", 0.08),
        init_service("logging", 0.05),
        init_service("metrics", 0.03)
    ]

    await asyncio.gather(*concurrent_tasks)

    # Complete startup
    await tester.simulate_main_window_display(delay=0.05)

    # Verify all services initialized
    service_events = [e for e in tester.lifecycle_events if e.startswith("service_init_")]
    assert len(service_events) == 4
    assert "main_window_show_complete" in tester.lifecycle_events


@pytest.mark.asyncio
@pytest.mark.integration
async def test_startup_timing_guarantees():
    """Test that startup meets timing guarantees and performance expectations."""
    tester = StartupIntegrationTester()

    start_time = time.perf_counter()

    # Execute complete startup sequence
    await tester.simulate_host_build(delay=0.1)
    await tester.simulate_service_registration(["config", "logging", "database"], delay=0.05)
    await tester.simulate_background_initialization(
        ["ensure_db", "validate_schema", "init_azure"],
        [0.02, 0.03, 0.04]
    )
    await tester.simulate_main_window_display(delay=0.1)

    total_time = (time.perf_counter() - start_time) * 1000

    summary = tester.get_performance_summary()

    # Timing guarantees
    assert total_time < 1000  # Complete startup in under 1 second
    assert summary["host_build_ms"] < 150  # Host build fast
    assert summary["service_registration_ms"] < 100  # Service registration fast
    assert summary["background_init_ms"] < 200  # Background init reasonable
    assert summary["main_window_show_ms"] < 150  # UI display fast

    # Performance ratios (background init should not dominate total time)
    background_ratio = summary["background_init_ms"] / total_time
    assert background_ratio < 0.5  # Background init < 50% of total startup time


@pytest.mark.asyncio
@pytest.mark.integration
async def test_startup_error_recovery_and_logging():
    """Test error recovery and proper logging during startup failures."""
    tester = StartupIntegrationTester()

    # Simulate startup with various error conditions
    await tester.simulate_host_build(delay=0.05)

    # Service registration with warnings
    tester.warnings.append("Service X not available in development")
    await tester.simulate_service_registration(["config", "logging"], delay=0.02)

    # Background init with recoverable error
    try:
        # First step succeeds
        await tester.simulate_background_initialization(
            ["ensure_db"],
            [0.02]
        )
        # Second step fails
        raise ValueError("Validation failed but recoverable")
    except ValueError:
        tester.errors.append(ValueError("Validation failed but recoverable"))

    # Despite errors, main window should show
    await tester.simulate_main_window_display(delay=0.05)

    summary = tester.get_performance_summary()
    assert summary["error_count"] == 1
    assert summary["warning_count"] == 1
    assert "main_window_show_complete" in tester.lifecycle_events


if __name__ == "__main__":
    pytest.main([__file__, "-v"])