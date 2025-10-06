"""Shared helpers for startup reliability tests."""

from __future__ import annotations

import asyncio
import time
from dataclasses import dataclass
from typing import Any, Awaitable, Callable, Dict, List, Optional


StepFunc = Callable[[], Awaitable[None]]


@dataclass
class StepMetrics:
    """Timing details captured for each background initialization step."""

    name: str
    duration_ms: float


class BackgroundInitializationServiceSimulator:
    """Async simulator mirroring the C# BackgroundInitializationService workflow."""

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


class ErrorReportingRecorder:
    """Captures errors reported by the hosted application harness."""

    def __init__(self) -> None:
        self.records: List[Dict[str, Any]] = []

    def report_error(
        self,
        exception: Exception,
        category: str,
        *,
        show_to_user: bool,
        level: str,
        correlation_id: str,
    ) -> None:
        self.records.append(
            {
                "exception": exception,
                "category": category,
                "show_to_user": show_to_user,
                "level": level,
                "correlation_id": correlation_id,
            }
        )


class HostedWpfApplicationHarness:
    """Recreates the orchestration logic of HostedWpfApplication.StartAsync for testing."""

    def __init__(
        self,
        background_service: BackgroundInitializationServiceSimulator,
        *,
        show_splash: Callable[[], Awaitable[None]],
        show_main_window: Callable[[], Awaitable[None]],
        close_splash: Callable[[], Awaitable[None]],
        error_reporter: Optional[ErrorReportingRecorder] = None,
        observation_window_seconds: float = 5.0,
        fallback_close_seconds: float = 10.0,
    ) -> None:
        self._background_service = background_service
        self._show_splash = show_splash
        self._show_main_window = show_main_window
        self._close_splash = close_splash
        self._error_reporter = error_reporter or ErrorReportingRecorder()
        self._observation_window_seconds = observation_window_seconds
        self._fallback_close_seconds = fallback_close_seconds
        self._lock = asyncio.Lock()
        self.events: List[str] = []
        self._monitor_tasks: List[asyncio.Task] = []

    @property
    def error_records(self) -> List[Dict[str, Any]]:
        return list(self._error_reporter.records)

    async def start_async(self, cancellation_event: Optional[asyncio.Event] = None) -> None:
        cancellation_event = cancellation_event or asyncio.Event()
        if cancellation_event.is_set():
            raise asyncio.CancelledError

        async with self._lock:
            background_future = self._background_service.initialization_completed

            observation_task = asyncio.create_task(asyncio.sleep(self._observation_window_seconds))
            try:
                done, _ = await asyncio.wait(
                    {background_future, observation_task},
                    return_when=asyncio.FIRST_COMPLETED,
                )
            except asyncio.CancelledError:
                observation_task.cancel()
                raise

            if background_future in done:
                await self._observe_background_completion(background_future)
            else:
                observation_task.cancel()
                self._monitor_background_completion(background_future)
                self.events.append("background_pending")

            await self._invoke_async(self._show_splash)
            self.events.append("splash_shown")

            main_window_task = asyncio.create_task(self._invoke_async(self._show_main_window))
            fallback_task = asyncio.create_task(asyncio.sleep(self._fallback_close_seconds))

            done, _ = await asyncio.wait(
                {main_window_task, fallback_task},
                return_when=asyncio.FIRST_COMPLETED,
            )

            if fallback_task in done and not main_window_task.done():
                self.events.append("fallback_close")
                await self._invoke_async(self._close_splash)
                self.events.append("splash_closed")
                main_window_task.cancel()
            else:
                await main_window_task
                await self._invoke_async(self._close_splash)
                self.events.append("splash_closed")

            if self._monitor_tasks:
                await asyncio.gather(*self._monitor_tasks)

    async def _observe_background_completion(self, task: asyncio.Future) -> None:
        try:
            await task
            self.events.append("background_complete")
        except Exception as exc:  # noqa: BLE001
            self._handle_background_failure(exc)

    def _monitor_background_completion(self, task: asyncio.Future) -> None:
        def _callback(fut: asyncio.Future) -> None:
            try:
                fut.result()
                self.events.append("background_complete_async")
            except Exception as exc:  # noqa: BLE001
                self._handle_background_failure(exc)

        monitor_task = asyncio.create_task(self._wait_for_future(task, _callback))
        self._monitor_tasks.append(monitor_task)

    async def _wait_for_future(self, fut: asyncio.Future, callback: Callable[[asyncio.Future], None]) -> None:
        try:
            await fut
        finally:
            callback(fut)

    async def _invoke_async(self, func: Callable[[], Awaitable[None]]) -> None:
        await func()

    def _handle_background_failure(self, exception: Exception) -> None:
        correlation_id = "deadbeef"
        self._error_reporter.report_error(
            exception,
            "BackgroundInitialization",
            show_to_user=True,
            level="Error",
            correlation_id=correlation_id,
        )
        self.events.append("background_failure")


class TestEnvironmentManager:
    """Test environment manager for database testing"""

    def __init__(self):
        self.is_setup = False
        self.environment = "Test"

    def setup(self):
        """Setup test environment"""
        self.is_setup = True

    def teardown(self):
        """Teardown test environment"""
        self.is_setup = False

    def is_sql_server_available(self) -> bool:
        """Check if SQL Server is available for testing"""
        try:
            import pyodbc
            conn_str = (
                "DRIVER={ODBC Driver 17 for SQL Server};"
                "SERVER=localhost\\SQLEXPRESS01;"
                "DATABASE=master;"
                "Trusted_Connection=yes;"
                "TrustServerCertificate=yes;"
                "Connection Timeout=5;"
            )
            conn = pyodbc.connect(conn_str)
            conn.close()
            return True
        except (ImportError, Exception):
            return False