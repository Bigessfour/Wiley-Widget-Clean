"""Rigorous tests for HostedWpfApplication startup orchestration."""

import asyncio
import os
import sys
from typing import List

import pytest

sys.path.insert(0, os.path.dirname(__file__))

from startup_test_utils import (
    BackgroundInitializationServiceSimulator,
    ErrorReportingRecorder,
    HostedWpfApplicationHarness,
)


async def _sleep_step(delay: float, marker_list: List[str], marker: str) -> None:
    await asyncio.sleep(delay)
    marker_list.append(marker)


@pytest.mark.asyncio
@pytest.mark.integration
async def test_host_startup_completes_when_background_ready():
    """Happy-path startup completes after background initialization finishes."""

    call_markers: List[str] = []

    service = BackgroundInitializationServiceSimulator(
        ensure_database_created=lambda: _sleep_step(0.01, call_markers, "ensure"),
        validate_database_schema=lambda: _sleep_step(0.01, call_markers, "validate"),
        initialize_azure=lambda: _sleep_step(0.01, call_markers, "azure"),
    )

    async def show_splash() -> None:
        await asyncio.sleep(0)

    async def show_main() -> None:
        await asyncio.sleep(0.01)

    async def close_splash() -> None:
        await asyncio.sleep(0)

    harness = HostedWpfApplicationHarness(
        service,
        show_splash=show_splash,
        show_main_window=show_main,
        close_splash=close_splash,
    )

    background_task = asyncio.create_task(service.execute_async())
    await harness.start_async()
    await background_task

    assert call_markers == ["ensure", "validate", "azure"]
    assert "background_complete" in harness.events
    assert harness.events.count("splash_shown") == 1
    assert harness.events.count("splash_closed") == 1
    assert not harness.error_records


@pytest.mark.asyncio
@pytest.mark.integration
async def test_host_startup_continues_with_pending_background_then_completes():
    """Startup proceeds when background work exceeds observation window and logs completion later."""

    background_gate = asyncio.Event()
    transitions: List[str] = []

    async def gated_step(name: str) -> None:
        transitions.append(f"wait_{name}")
        await background_gate.wait()
        transitions.append(f"run_{name}")

    service = BackgroundInitializationServiceSimulator(
        ensure_database_created=lambda: gated_step("ensure"),
        validate_database_schema=lambda: _sleep_step(0.01, transitions, "validate"),
        initialize_azure=lambda: _sleep_step(0.01, transitions, "azure"),
    )

    async def show_splash() -> None:
        await asyncio.sleep(0)

    async def show_main() -> None:
        await asyncio.sleep(0.01)

    async def close_splash() -> None:
        await asyncio.sleep(0)

    harness = HostedWpfApplicationHarness(
        service,
        show_splash=show_splash,
        show_main_window=show_main,
        close_splash=close_splash,
        observation_window_seconds=0.05,
    )

    background_task = asyncio.create_task(service.execute_async())

    await harness.start_async()
    assert "background_pending" in harness.events

    background_gate.set()
    await background_task
    await asyncio.sleep(0)  # allow monitor callback to record completion

    assert "background_complete_async" in harness.events
    assert not harness.error_records


@pytest.mark.asyncio
@pytest.mark.integration
async def test_host_startup_records_background_failure():
    """Failures bubbling from background initialization trigger error reporting."""

    async def ensure_db() -> None:
        await asyncio.sleep(0.005)

    async def validate_schema() -> None:
        raise RuntimeError("schema corrupt")

    service = BackgroundInitializationServiceSimulator(
        ensure_database_created=ensure_db,
        validate_database_schema=validate_schema,
        initialize_azure=lambda: asyncio.sleep(0.01),
    )

    reporter = ErrorReportingRecorder()

    async def noop() -> None:
        await asyncio.sleep(0)

    harness = HostedWpfApplicationHarness(
        service,
        show_splash=noop,
        show_main_window=noop,
        close_splash=noop,
        error_reporter=reporter,
    )

    background_task = asyncio.create_task(service.execute_async())
    await harness.start_async()

    with pytest.raises(RuntimeError):
        await background_task

    assert any(event == "background_failure" for event in harness.events)
    assert len(reporter.records) == 1
    record = reporter.records[0]
    assert record["category"] == "BackgroundInitialization"
    assert isinstance(record["exception"], RuntimeError)


@pytest.mark.asyncio
@pytest.mark.integration
async def test_host_startup_fallback_closes_splash_when_main_window_hangs():
    """Splash screen should close automatically if the main window never signals readiness."""

    service = BackgroundInitializationServiceSimulator(
        ensure_database_created=lambda: asyncio.sleep(0.01),
        validate_database_schema=lambda: asyncio.sleep(0.01),
        initialize_azure=lambda: asyncio.sleep(0.01),
    )

    async def show_splash() -> None:
        await asyncio.sleep(0)

    async def never_finishing_main() -> None:
        await asyncio.sleep(1)  # much longer than fallback

    async def close_splash() -> None:
        await asyncio.sleep(0)

    harness = HostedWpfApplicationHarness(
        service,
        show_splash=show_splash,
        show_main_window=never_finishing_main,
        close_splash=close_splash,
        fallback_close_seconds=0.05,
    )

    background_task = asyncio.create_task(service.execute_async())
    await harness.start_async()
    await background_task

    assert "fallback_close" in harness.events
    assert harness.events.count("splash_closed") == 1