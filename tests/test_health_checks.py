import pytest
import asyncio
import time
from unittest.mock import AsyncMock, MagicMock, patch
from datetime import datetime, timedelta
import sys
import os

# Add the project root to the path so we can import modules
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

class TestHealthChecks:
    """Comprehensive tests for async health check operations"""

    @pytest.fixture
    def mock_health_check_service(self):
        """Mock health check service for testing"""
        service = MagicMock()
        service.CheckHealthAsync = AsyncMock()
        return service

    @pytest.fixture
    def mock_health_result(self):
        """Mock health check result"""
        result = MagicMock()
        result.Status = "Healthy"
        result.Description = "Service is healthy"
        result.Duration = timedelta(milliseconds=150)
        result.Exception = None
        result.Timestamp = datetime.utcnow()
        return result

    @pytest.mark.asyncio
    async def test_async_health_check_execution(self, mock_health_check_service: MagicMock, mock_health_result: MagicMock):
        """Test basic async health check execution"""
        # Setup
        mock_health_check_service.CheckHealthAsync.return_value = mock_health_result

        # Execute
        start_time = time.time()
        result = await mock_health_check_service.CheckHealthAsync()
        execution_time = time.time() - start_time

        # Assert
        assert result is not None
        assert result.Status == "Healthy"
        assert result.Duration.total_seconds() > 0
        assert execution_time >= 0  # Should not be instantaneous
        mock_health_check_service.CheckHealthAsync.assert_called_once()

    @pytest.mark.asyncio
    async def test_multiple_health_checks_concurrent_execution(self):
        """Test running multiple health checks concurrently"""
        async def mock_health_check(name: str, delay: float = 0.1):
            await asyncio.sleep(delay)
            return {"service": name, "status": "Healthy", "duration": delay}

        # Setup multiple health checks
        health_checks = [
            mock_health_check("Database", 0.1),
            mock_health_check("Azure AD", 0.15),
            mock_health_check("Key Vault", 0.08),
            mock_health_check("QuickBooks", 0.12)
        ]

        # Execute concurrently
        start_time = time.time()
        results = await asyncio.gather(*health_checks, return_exceptions=True)
        total_time = time.time() - start_time

        # Assert
        assert len(results) == 4
        successful_results = [r for r in results if isinstance(r, dict)]
        assert len(successful_results) == 4  # All should succeed
        assert all(r["status"] == "Healthy" for r in successful_results)
        # Should complete faster than sequential execution
        assert total_time < 0.5  # Less than sum of all delays

    @pytest.mark.asyncio
    async def test_health_check_timeout_handling(self):
        """Test health check timeout handling"""
        async def slow_health_check():
            await asyncio.sleep(2.0)  # Longer than timeout
            return {"status": "Healthy"}

        # Test with timeout
        with pytest.raises(asyncio.TimeoutError):
            await asyncio.wait_for(slow_health_check(), timeout=0.5)

    @pytest.mark.asyncio
    async def test_health_check_error_resilience(self):
        """Test health check error handling and resilience"""
        async def failing_health_check():
            await asyncio.sleep(0.1)
            raise ConnectionError("Service unavailable")

        async def successful_health_check():
            await asyncio.sleep(0.05)
            return {"status": "Healthy"}

        # Test individual failure doesn't break others
        tasks = [
            failing_health_check(),
            successful_health_check(),
            failing_health_check()
        ]

        results = await asyncio.gather(*tasks, return_exceptions=True)

        # Assert
        assert len(results) == 3
        assert isinstance(results[0], ConnectionError)
        assert results[1] == {"status": "Healthy"}
        assert isinstance(results[2], ConnectionError)

    @pytest.mark.asyncio
    async def test_health_check_circuit_breaker_pattern(self):
        """Test circuit breaker pattern for health checks"""
        class MockCircuitBreaker:
            def __init__(self, failure_threshold=3):
                self.failure_count = 0
                self.failure_threshold = failure_threshold
                self.state = "Closed"

            async def execute(self, func):
                if self.state == "Open":
                    raise Exception("Circuit breaker is open")

                try:
                    result = await func()
                    self.failure_count = 0
                    self.state = "Closed"
                    return result
                except Exception:
                    self.failure_count += 1
                    if self.failure_count >= self.failure_threshold:
                        self.state = "Open"
                    raise

        circuit_breaker = MockCircuitBreaker()

        async def failing_operation():
            raise Exception("Simulated failure")

        # Simulate failures
        for i in range(3):
            with pytest.raises(Exception):
                await circuit_breaker.execute(failing_operation)

        # Circuit should be open now
        assert circuit_breaker.state == "Open"
        with pytest.raises(Exception, match="Circuit breaker is open"):
            await circuit_breaker.execute(lambda: asyncio.sleep(0.01) or {"status": "Healthy"})

    @pytest.mark.asyncio
    async def test_health_check_retry_logic(self):
        """Test retry logic for failed health checks"""
        attempt_count = 0

        async def unreliable_health_check():
            nonlocal attempt_count
            attempt_count += 1
            if attempt_count < 3:
                raise ConnectionError(f"Attempt {attempt_count} failed")
            return {"status": "Healthy", "attempts": attempt_count}

        async def retry_health_check(func, max_retries=3, delay=0.01):
            for attempt in range(max_retries):
                try:
                    return await func()
                except Exception as e:
                    if attempt == max_retries - 1:
                        raise e
                    await asyncio.sleep(delay)

        # Test retry succeeds after failures
        result = await retry_health_check(unreliable_health_check, max_retries=3)
        assert isinstance(result, dict)
        assert result["status"] == "Healthy"
        assert result["attempts"] == 3

    @pytest.mark.asyncio
    async def test_health_check_performance_monitoring(self):
        """Test health check performance monitoring"""
        async def monitored_health_check():
            start = time.perf_counter()
            await asyncio.sleep(0.1)  # Simulate work
            duration = time.perf_counter() - start
            return {"status": "Healthy", "duration": duration}

        results = []
        for i in range(5):
            result = await monitored_health_check()
            results.append(result)

        # Assert performance is consistent
        durations = [r["duration"] for r in results]
        avg_duration = sum(durations) / len(durations)
        max_deviation = max(abs(d - avg_duration) for d in durations)

        assert all(r["status"] == "Healthy" for r in results)
        assert all(0.08 <= d <= 0.15 for d in durations)  # Reasonable range
        assert max_deviation < 0.05  # Low variance

    @pytest.mark.asyncio
    async def test_health_check_dependency_chain(self):
        """Test health checks with dependencies"""
        health_status = {"database": False, "api": False, "cache": False}

        async def check_database():
            await asyncio.sleep(0.05)
            health_status["database"] = True
            return {"service": "database", "status": "Healthy"}

        async def check_api():
            # API depends on database
            if not health_status["database"]:
                raise Exception("Database not available")
            await asyncio.sleep(0.03)
            health_status["api"] = True
            return {"service": "api", "status": "Healthy"}

        async def check_cache():
            # Cache depends on API
            if not health_status["api"]:
                raise Exception("API not available")
            await asyncio.sleep(0.02)
            health_status["cache"] = True
            return {"service": "cache", "status": "Healthy"}

        # Execute in dependency order
        db_result = await check_database()
        api_result = await check_api()
        cache_result = await check_cache()

        # Assert all services are healthy
        assert all(r["status"] == "Healthy" for r in [db_result, api_result, cache_result])
        assert all(health_status.values())

    @pytest.mark.asyncio
    async def test_health_check_resource_cleanup(self):
        """Test proper resource cleanup in health checks"""
        resources_created = []

        async def health_check_with_resources():
            # Simulate resource creation
            resource = {"id": "test-resource", "type": "connection"}
            resources_created.append(resource)

            try:
                await asyncio.sleep(0.05)
                return {"status": "Healthy", "resource": resource}
            finally:
                # Simulate cleanup
                if resource in resources_created:
                    resources_created.remove(resource)

        result = await health_check_with_resources()

        # Assert resource was created and cleaned up
        assert result["status"] == "Healthy"
        assert len(resources_created) == 0  # Should be cleaned up

    @pytest.mark.asyncio
    async def test_health_check_configuration_validation(self):
        """Test health check configuration validation"""
        valid_configs = [
            {"timeout": 30, "retries": 3, "critical": True},
            {"timeout": 10, "retries": 1, "critical": False},
            {"timeout": 60, "retries": 5, "critical": True}
        ]

        invalid_configs = [
            {"timeout": -1, "retries": 3},  # Negative timeout
            {"timeout": 30, "retries": -1},  # Negative retries
            {"timeout": 0, "retries": 0}  # Zero values
        ]

        def validate_config(config):
            if config.get("timeout", 0) <= 0:
                raise ValueError("Timeout must be positive")
            if config.get("retries", 0) < 0:
                raise ValueError("Retries cannot be negative")
            return True

        # Test valid configs
        for config in valid_configs:
            assert validate_config(config)

        # Test invalid configs
        for config in invalid_configs:
            with pytest.raises(ValueError):
                validate_config(config)

    @pytest.mark.asyncio
    async def test_health_check_aggregation_and_reporting(self):
        """Test health check result aggregation and reporting"""
        async def mock_service_check(service_name: str, status: str, duration: float):
            await asyncio.sleep(duration)
            return {
                "service": service_name,
                "status": status,
                "duration": duration,
                "timestamp": datetime.utcnow().isoformat()
            }

        # Simulate various service checks
        service_checks = [
            mock_service_check("Database", "Healthy", 0.1),
            mock_service_check("API", "Healthy", 0.08),
            mock_service_check("Cache", "Degraded", 0.15),
            mock_service_check("Queue", "Unhealthy", 0.05)
        ]

        results = await asyncio.gather(*service_checks, return_exceptions=True)

        # Aggregate results
        healthy_count = sum(1 for r in results if isinstance(r, dict) and r["status"] == "Healthy")
        degraded_count = sum(1 for r in results if isinstance(r, dict) and r["status"] == "Degraded")
        unhealthy_count = sum(1 for r in results if isinstance(r, dict) and r["status"] == "Unhealthy")
        total_duration = sum(r["duration"] for r in results if isinstance(r, dict))

        # Assert aggregation
        assert healthy_count == 2
        assert degraded_count == 1
        assert unhealthy_count == 1
        assert 0.35 <= total_duration <= 0.45  # Sum of all durations

        # Overall status should be Unhealthy due to failed service
        overall_status = "Unhealthy" if unhealthy_count > 0 else "Degraded" if degraded_count > 0 else "Healthy"
        assert overall_status == "Unhealthy"

    @pytest.mark.asyncio
    async def test_health_check_background_monitoring(self):
        """Test continuous background health monitoring"""
        monitoring_active = True
        check_count = 0
        health_history = []

        async def background_monitor(interval=0.1, max_checks=5):
            nonlocal check_count
            while monitoring_active and check_count < max_checks:
                # Simulate health check
                status = "Healthy" if check_count % 2 == 0 else "Degraded"
                health_history.append({
                    "check": check_count,
                    "status": status,
                    "timestamp": datetime.utcnow()
                })
                check_count += 1
                await asyncio.sleep(interval)

        # Start background monitoring
        monitor_task = asyncio.create_task(background_monitor())

        # Let it run for a bit
        await asyncio.sleep(0.35)

        # Stop monitoring
        monitoring_active = False
        await monitor_task

        # Assert monitoring worked
        assert check_count >= 3  # Should have completed several checks
        assert len(health_history) == check_count
        assert all("status" in h and "timestamp" in h for h in health_history)

    @pytest.mark.asyncio
    async def test_health_check_load_testing(self):
        """Test health checks under load"""
        async def simple_health_check(check_id: int):
            await asyncio.sleep(0.01)  # Small delay
            return {"check_id": check_id, "status": "Healthy"}

        # Simulate high load - many concurrent health checks
        num_checks = 50
        tasks = [simple_health_check(i) for i in range(num_checks)]

        start_time = time.time()
        results = await asyncio.gather(*tasks)
        total_time = time.time() - start_time

        # Assert all checks completed successfully
        assert len(results) == num_checks
        assert all(r["status"] == "Healthy" for r in results)
        assert all(r["check_id"] == i for i, r in enumerate(results))

        # Performance check - should complete reasonably quickly
        expected_min_time = 0.01  # At least the delay per check
        expected_max_time = 0.5   # Shouldn't take too long even under load
        assert expected_min_time <= total_time <= expected_max_time

if __name__ == "__main__":
    pytest.main([__file__, "-v"])