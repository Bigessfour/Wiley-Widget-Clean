"""Comprehensive tests for background initialization service behavior.

These tests simulate the sequencing, cancellation, and error handling
patterns used by the C# ``BackgroundInitializationService`` to ensure
we exercise the integration contract thoroughly from Python.
"""

import asyncio
import time
from dataclasses import dataclass
from typing import Awaitable, Callable, Dict, List, Optional

import pytest


StepFunc = Callable[[], Awaitable[None]]


@dataclass
class StepMetrics:
    name: str
    duration_ms: float


class BackgroundInitializationServiceSimulator:
    """Async simulator that mirrors the C# background initialization workflow."""

    def __init__(
        self,
        ensure_database_created: StepFunc,
        validate_database_schema: StepFunc,
        initialize_azure: StepFunc,
    ) -> None:
        loop = asyncio.get_running_loop()
        self._ensure_database_created = ensure_database_created
        self._validate_database_schema = validate_database_schema
        self._initialize_azure = initialize_azure
        self._initialization_completed: asyncio.Future = loop.create_future()
        self._step_order: List[str] = []
        self._step_metrics: List[StepMetrics] = []

    @property
    def initialization_completed(self) -> asyncio.Future:
        return self._initialization_completed

    @property
    def step_order(self) -> List[str]:
        return list(self._step_order)

    @property
    def step_metrics(self) -> List[StepMetrics]:
        return list(self._step_metrics)

    async def execute_async(self, cancellation_event: Optional[asyncio.Event] = None) -> Dict[str, float]:
        start_total = time.perf_counter()

        async def run_step(name: str, func: StepFunc) -> float:
            await self._ensure_not_cancelled(cancellation_event)
            step_start = time.perf_counter()
            self._step_order.append(name)
            await func()
            await self._ensure_not_cancelled(cancellation_event)
            duration = (time.perf_counter() - step_start) * 1000
            self._step_metrics.append(StepMetrics(name=name, duration_ms=duration))
            return duration

        try:
            metrics: Dict[str, float] = {}
            metrics["ensure_db_ms"] = await run_step("ensure_database", self._ensure_database_created)
            metrics["validate_schema_ms"] = await run_step("validate_schema", self._validate_database_schema)
            metrics["initialize_azure_ms"] = await run_step("initialize_azure", self._initialize_azure)
            metrics["total_ms"] = (time.perf_counter() - start_total) * 1000

            if not self._initialization_completed.done():
                self._initialization_completed.set_result(metrics)
            return metrics
        except asyncio.CancelledError:
            if not self._initialization_completed.done():
                self._initialization_completed.cancel()
            raise
        except Exception as exc:  # noqa: BLE001
            if not self._initialization_completed.done():
                self._initialization_completed.set_exception(exc)
            raise

    async def _ensure_not_cancelled(self, cancellation_event: Optional[asyncio.Event]) -> None:
        if cancellation_event and cancellation_event.is_set():
            raise asyncio.CancelledError


@pytest.mark.asyncio
@pytest.mark.integration
async def test_background_initialization_happy_path_sequence():
    """Verify steps run in order and completion future resolves with metrics."""

    call_order: List[str] = []

    async def ensure_db():
        call_order.append("ensure")
        await asyncio.sleep(0.01)

    async def validate_schema():
        call_order.append("validate")
        await asyncio.sleep(0.01)

    async def initialize_azure():
        call_order.append("azure")
        await asyncio.sleep(0.01)

    service = BackgroundInitializationServiceSimulator(ensure_db, validate_schema, initialize_azure)

    metrics = await service.execute_async()

    assert call_order == ["ensure", "validate", "azure"]
    assert service.step_order == ["ensure_database", "validate_schema", "initialize_azure"]
    assert service.initialization_completed.done()
    assert not service.initialization_completed.cancelled()
    assert service.initialization_completed.result() == metrics
    assert {step.name for step in service.step_metrics} == {
        "ensure_database",
        "validate_schema",
        "initialize_azure",
    }
    combined = sum(
        metrics[key] for key in ("ensure_db_ms", "validate_schema_ms", "initialize_azure_ms")
    )
    assert metrics["total_ms"] >= combined
    assert all(step.duration_ms > 0 for step in service.step_metrics)


@pytest.mark.asyncio
@pytest.mark.integration
async def test_background_initialization_propagates_exceptions():
    """Ensure exceptions short-circuit the pipeline and surface via the completion future."""

    async def ensure_db():
        await asyncio.sleep(0.005)

    async def validate_schema():
        raise RuntimeError("schema mismatch")

    async def initialize_azure():
        pytest.fail("Azure initialization should not run after failure")

    service = BackgroundInitializationServiceSimulator(ensure_db, validate_schema, initialize_azure)

    with pytest.raises(RuntimeError, match="schema mismatch"):
        await service.execute_async()

    assert service.initialization_completed.done()
    exception = service.initialization_completed.exception()
    assert isinstance(exception, RuntimeError)
    assert str(exception) == "schema mismatch"
    assert service.step_order == ["ensure_database", "validate_schema"]


@pytest.mark.asyncio
@pytest.mark.integration
async def test_background_initialization_handles_cancellation():
    """Simulate cancellation arriving mid-flight and ensure the future is cancelled."""

    cancel_event = asyncio.Event()
    checkpoint = asyncio.Event()

    async def ensure_db():
        await asyncio.sleep(0.01)
        checkpoint.set()

    async def validate_schema():
        await cancel_event.wait()
        raise asyncio.CancelledError

    async def initialize_azure():
        await asyncio.sleep(0.02)

    service = BackgroundInitializationServiceSimulator(ensure_db, validate_schema, initialize_azure)

    execute_task = asyncio.create_task(service.execute_async(cancel_event))

    await asyncio.wait_for(checkpoint.wait(), timeout=0.5)
    cancel_event.set()

    with pytest.raises(asyncio.CancelledError):
        await execute_task

    assert service.initialization_completed.done()
    assert service.initialization_completed.cancelled()
    assert service.step_order == ["ensure_database", "validate_schema"]
    assert "initialize_azure" not in service.step_order


@pytest.mark.asyncio
@pytest.mark.integration
async def test_background_initialization_allows_multiple_waiters():
    """Multiple waiters should all resolve with identical metrics once initialization finishes."""

    async def ensure_db():
        await asyncio.sleep(0.01)

    async def validate_schema():
        await asyncio.sleep(0.015)

    async def initialize_azure():
        await asyncio.sleep(0.02)

    service = BackgroundInitializationServiceSimulator(ensure_db, validate_schema, initialize_azure)

    waiters = [asyncio.create_task(asyncio.wait_for(service.initialization_completed, timeout=1)) for _ in range(3)]

    metrics = await service.execute_async()
    results = await asyncio.gather(*waiters)

    assert all(result == metrics for result in results)
    assert metrics["total_ms"] >= metrics["initialize_azure_ms"]


@pytest.mark.asyncio
@pytest.mark.integration
async def test_background_initialization_records_step_metrics_precision():
    """Durations should reflect cumulative timing characteristics across steps."""

    durations: Dict[str, float] = {
        "ensure": 0.005,
        "validate": 0.012,
        "azure": 0.02,
    }

    def make_step(name: str) -> StepFunc:
        async def _step() -> None:
            await asyncio.sleep(durations[name])

        return _step

    service = BackgroundInitializationServiceSimulator(
        make_step("ensure"),
        make_step("validate"),
        make_step("azure"),
    )

    metrics = await service.execute_async()

    assert metrics["ensure_db_ms"] >= durations["ensure"] * 1000
    assert metrics["validate_schema_ms"] >= durations["validate"] * 1000
    assert metrics["initialize_azure_ms"] >= durations["azure"] * 1000
    assert metrics["ensure_db_ms"] <= (durations["ensure"] + 0.02) * 1000
    assert metrics["validate_schema_ms"] <= (durations["validate"] + 0.03) * 1000
    assert metrics["initialize_azure_ms"] <= (durations["azure"] + 0.04) * 1000
    computed_total = sum(metrics[key] for key in ("ensure_db_ms", "validate_schema_ms", "initialize_azure_ms"))
    assert metrics["total_ms"] >= computed_total