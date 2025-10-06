"""
Database Test Fixtures and Utilities

Provides fixtures, utilities, and helpers for comprehensive database testing
across different environments and connection types.
"""

import pytest
import asyncio
import os
from pathlib import Path
from unittest.mock import MagicMock, AsyncMock
from typing import Dict, Any
import sys

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))


class DatabaseTestEnvironment:
    """Manages test database environment setup and teardown"""

    def __init__(self, environment: str = "Development"):
        self.environment = environment
        self.original_env = dict(os.environ)
        self.test_db_name = f"WileyWidgetTest_{environment}"

    def setup(self):
        """Set up test environment variables"""
        os.environ["DOTNET_ENVIRONMENT"] = self.environment
        os.environ["TEST_DATABASE_NAME"] = self.test_db_name

        if self.environment == "Development":
            os.environ["CONNECTIONSTRINGS_DEFAULTCONNECTION"] = (
                f"Server=localhost\\SQLEXPRESS01;Database={self.test_db_name};"
                "Trusted_Connection=True;TrustServerCertificate=True;"
            )
        else:
            os.environ["AZURE_SQL_SERVER"] = "test-server.database.windows.net"
            os.environ["AZURE_SQL_DATABASE"] = self.test_db_name

    def teardown(self):
        """Restore original environment"""
        os.environ.clear()
        os.environ.update(self.original_env)

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

    def create_test_database(self) -> bool:
        """Create test database if SQL Server is available"""
        if not self.is_sql_server_available():
            return False

        try:
            import pyodbc
            conn_str = (
                "DRIVER={ODBC Driver 17 for SQL Server};"
                "SERVER=localhost\\SQLEXPRESS01;"
                "DATABASE=master;"
                "Trusted_Connection=yes;"
                "TrustServerCertificate=yes;"
            )
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            # Drop test database if it exists
            cursor.execute(f"""
                IF EXISTS (SELECT name FROM sys.databases WHERE name = '{self.test_db_name}')
                BEGIN
                    ALTER DATABASE [{self.test_db_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{self.test_db_name}];
                END
            """)

            # Create fresh test database
            cursor.execute(f"CREATE DATABASE [{self.test_db_name}]")
            conn.commit()

            cursor.close()
            conn.close()
            return True
        except Exception:
            return False

    def cleanup_test_database(self) -> bool:
        """Clean up test database"""
        if not self.is_sql_server_available():
            return False

        try:
            import pyodbc
            conn_str = (
                "DRIVER={ODBC Driver 17 for SQL Server};"
                "SERVER=localhost\\SQLEXPRESS01;"
                "DATABASE=master;"
                "Trusted_Connection=yes;"
                "TrustServerCertificate=yes;"
            )
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            cursor.execute(f"""
                IF EXISTS (SELECT name FROM sys.databases WHERE name = '{self.test_db_name}')
                BEGIN
                    ALTER DATABASE [{self.test_db_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{self.test_db_name}];
                END
            """)
            conn.commit()

            cursor.close()
            conn.close()
            return True
        except Exception:
            return False


@pytest.fixture(scope="session")
def test_env_manager():
    """Session-scoped test environment manager"""
    manager = DatabaseTestEnvironment()
    manager.setup()
    yield manager
    manager.teardown()


@pytest.fixture
def dev_test_env():
    """Development test environment"""
    env = DatabaseTestEnvironment("Development")
    env.setup()
    yield env
    env.teardown()


@pytest.fixture
def prod_test_env():
    """Production test environment"""
    env = DatabaseTestEnvironment("Production")
    env.setup()
    yield env
    env.teardown()


@pytest.fixture
def mock_azure_credential():
    """Mock Azure credential for testing"""
    from azure.identity import DefaultAzureCredential
    mock_cred = MagicMock(spec=DefaultAzureCredential)
    mock_token = MagicMock()
    mock_token.token = "mock-azure-token-12345"
    mock_cred.get_token = AsyncMock(return_value=mock_token)
    return mock_cred


@pytest.fixture
def mock_db_context():
    """Mock database context for testing"""
    mock_context = MagicMock()
    mock_context.Database = MagicMock()
    mock_context.Database.CanConnectAsync = AsyncMock(return_value=True)
    mock_context.Database.GetPendingMigrations = MagicMock(return_value=[])
    mock_context.SaveChangesAsync = AsyncMock(return_value=1)
    return mock_context


@pytest.fixture
def mock_health_check_service():
    """Mock health check service"""
    service = MagicMock()
    service.CheckHealthAsync = AsyncMock()
    return service


@pytest.fixture
def sample_enterprise_data():
    """Sample enterprise data for testing"""
    return {
        "enterprises": [
            {
                "Id": 1,
                "Name": "Water Department",
                "CurrentRate": 45.50,
                "MonthlyExpenses": 12500.00,
                "CitizenCount": 15000,
                "Notes": "Municipal water utility"
            },
            {
                "Id": 2,
                "Name": "Sewer Department",
                "CurrentRate": 38.75,
                "MonthlyExpenses": 8750.00,
                "CitizenCount": 14800,
                "Notes": "Wastewater management"
            }
        ],
        "budget_interactions": [
            {
                "Id": 1,
                "PrimaryEnterpriseId": 1,
                "SecondaryEnterpriseId": 2,
                "InteractionType": "Cross-subsidy",
                "Description": "Water department supports sewer infrastructure",
                "MonthlyAmount": 2500.00,
                "IsCost": False
            }
        ]
    }


@pytest.fixture
def mock_configuration():
    """Mock configuration for testing"""
    config = {
        "ConnectionStrings": {
            "DefaultConnection": "Server=localhost\\SQLEXPRESS01;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;"
        },
        "Azure": {
            "SqlServer": "test-server.database.windows.net",
            "Database": "test-db"
        },
        "Logging": {
            "LogLevel": {
                "Default": "Information",
                "Microsoft.EntityFrameworkCore": "Warning",
                "Azure.Identity": "Warning",
                "Azure.Core": "Warning"
            }
        },
        "HealthChecks": {
            "Database": {
                "Enabled": True,
                "Timeout": 30,
                "Interval": 60
            }
        }
    }
    return config


@pytest.fixture
def mock_logger():
    """Mock logger for testing"""
    logger = MagicMock()
    logger.LogInformation = MagicMock()
    logger.LogWarning = MagicMock()
    logger.LogError = MagicMock()
    logger.LogDebug = MagicMock()
    return logger


class DatabaseConnectionTester:
    """Utility class for testing database connections"""

    def __init__(self, environment: str = "Development"):
        self.environment = environment
        self.test_env = DatabaseTestEnvironment(environment)

    async def test_connection_async(self) -> Dict[str, Any]:
        """Test database connection asynchronously"""
        result = {
            "connected": False,
            "connection_time": 0.0,
            "error": None,
            "database_name": None,
            "server_version": None
        }

        start_time = asyncio.get_event_loop().time()

        try:
            if self.environment == "Development":
                # Test local SQL Server connection
                if not self.test_env.is_sql_server_available():
                    result["error"] = "SQL Server not available"
                    return result

                import pyodbc
                conn_str = (
                    "DRIVER={ODBC Driver 17 for SQL Server};"
                    "SERVER=localhost\\SQLEXPRESS01;"
                    f"DATABASE={self.test_env.test_db_name};"
                    "Trusted_Connection=yes;"
                    "TrustServerCertificate=yes;"
                    "Connection Timeout=10;"
                )

                conn = await asyncio.get_event_loop().run_in_executor(None, pyodbc.connect, conn_str)
                cursor = conn.cursor()

                # Get database info
                cursor.execute("SELECT DB_NAME() as db_name, @@VERSION as version")
                db_info = cursor.fetchone()

                if db_info and len(db_info) >= 2:
                    result["connected"] = True
                    result["database_name"] = db_info[0] if db_info[0] else "Unknown"
                    result["server_version"] = (db_info[1][:50] + "..." if db_info[1] and len(db_info[1]) > 50 else db_info[1]) if db_info[1] else "Unknown"
                else:
                    result["connected"] = False
                    result["database_name"] = "Unknown"
                    result["server_version"] = "Unknown"

                cursor.close()
                conn.close()

            else:
                # Simulate Azure SQL connection test
                result["connected"] = True
                result["database_name"] = self.test_env.test_db_name
                result["server_version"] = "Azure SQL Database (simulated)"

        except Exception as e:
            result["error"] = str(e)
        finally:
            result["connection_time"] = asyncio.get_event_loop().time() - start_time

        return result

    def test_connection_pooling(self) -> Dict[str, Any]:
        """Test connection pooling configuration"""
        if self.environment == "Development":
            config = {
                "MaxPoolSize": 10,
                "MinPoolSize": 1,
                "ConnectionTimeout": 30
            }
        else:
            config = {
                "MaxPoolSize": 100,
                "MinPoolSize": 5,
                "ConnectionTimeout": 60,
                "ConnectionIdleTimeout": 300
            }

        return {
            "pool_config": config,
            "environment": self.environment,
            "is_valid": all(isinstance(v, int) and v > 0 for v in config.values())
        }


class HealthCheckTester:
    """Utility class for testing health check functionality"""

    def __init__(self):
        self.check_results = []

    async def perform_health_check(self, service_name: str) -> Dict[str, Any]:
        """Perform a health check for a service"""
        result = {
            "service": service_name,
            "status": "Healthy",
            "description": f"{service_name} is operating normally",
            "duration": 0.15,
            "timestamp": asyncio.get_event_loop().time(),
            "error": None
        }

        # Simulate health check logic
        if service_name == "Database":
            # Simulate database connectivity check
            try:
                # In real implementation, this would test actual database connection
                await asyncio.sleep(0.1)  # Simulate connection test
                result["status"] = "Healthy"
                result["description"] = "Database connection successful"
            except Exception as e:
                result["status"] = "Unhealthy"
                result["description"] = f"Database connection failed: {e}"
                result["error"] = str(e)

        elif service_name == "ConnectionPool":
            # Simulate connection pool health check
            result["status"] = "Healthy"
            result["description"] = "Connection pool operating within limits"

        self.check_results.append(result)
        return result

    def get_health_summary(self) -> Dict[str, Any]:
        """Get summary of all health checks"""
        total_checks = len(self.check_results)
        healthy_checks = len([r for r in self.check_results if r["status"] == "Healthy"])
        unhealthy_checks = total_checks - healthy_checks

        return {
            "total_checks": total_checks,
            "healthy": healthy_checks,
            "unhealthy": unhealthy_checks,
            "overall_status": "Healthy" if unhealthy_checks == 0 else "Degraded",
            "average_duration": sum(r["duration"] for r in self.check_results) / total_checks if total_checks > 0 else 0
        }


# Test data generators
class TestDataGenerator:
    """Generate test data for database testing"""

    @staticmethod
    def generate_enterprise_data(count: int = 5) -> list:
        """Generate sample enterprise data"""
        enterprises = []
        departments = ["Water", "Sewer", "Trash", "Electric", "Gas"]

        for i in range(count):
            enterprises.append({
                "Id": i + 1,
                "Name": f"{departments[i % len(departments)]} Department",
                "CurrentRate": round(30.0 + (i * 5.5), 2),
                "MonthlyExpenses": 8000 + (i * 1000),
                "CitizenCount": 12000 + (i * 500),
                "Notes": f"Test enterprise {i + 1}"
            })

        return enterprises

    @staticmethod
    def generate_customer_data(count: int = 10) -> list:
        """Generate sample customer data"""
        customers = []
        first_names = ["John", "Jane", "Bob", "Alice", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry"]
        last_names = ["Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez"]

        for i in range(count):
            customers.append({
                "Id": i + 1,
                "AccountNumber": f"ACC{i+1:04d}",
                "FirstName": first_names[i % len(first_names)],
                "LastName": last_names[i % len(last_names)],
                "ServiceAddress": f"{i+1} Main St",
                "ServiceCity": "Test City",
                "ServiceState": "TS",
                "ServiceZip": f"1234{i}",
                "CustomerType": "Residential" if i % 2 == 0 else "Commercial",
                "Status": "Active",
                "CurrentBalance": round((i * 10.5), 2)
            })

        return customers


# Performance testing utilities
class PerformanceTester:
    """Utilities for performance testing database operations"""

    def __init__(self):
        self.metrics = []

    def measure_operation(self, operation_name: str, operation_func, *args, **kwargs) -> Dict[str, Any]:
        """Measure the performance of a database operation"""
        import time

        start_time = time.perf_counter()
        try:
            operation_func(*args, **kwargs)
            success = True
            error = None
        except Exception as e:
            success = False
            error = str(e)
        finally:
            end_time = time.perf_counter()

        metric = {
            "operation": operation_name,
            "duration": end_time - start_time,
            "success": success,
            "error": error,
            "timestamp": time.time()
        }

        self.metrics.append(metric)
        return metric

    def get_performance_summary(self) -> Dict[str, Any]:
        """Get performance summary statistics"""
        if not self.metrics:
            return {"total_operations": 0, "average_duration": 0, "success_rate": 0}

        total_ops = len(self.metrics)
        successful_ops = len([m for m in self.metrics if m["success"]])
        total_duration = sum(m["duration"] for m in self.metrics)

        return {
            "total_operations": total_ops,
            "successful_operations": successful_ops,
            "failed_operations": total_ops - successful_ops,
            "success_rate": successful_ops / total_ops if total_ops > 0 else 0,
            "average_duration": total_duration / total_ops if total_ops > 0 else 0,
            "total_duration": total_duration,
            "min_duration": min(m["duration"] for m in self.metrics),
            "max_duration": max(m["duration"] for m in self.metrics)
        }


# Export utilities for external use
__all__ = [
    "DatabaseTestEnvironment",
    "DatabaseConnectionTester",
    "HealthCheckTester",
    "TestDataGenerator",
    "PerformanceTester",
    "test_env_manager",
    "dev_test_env",
    "prod_test_env",
    "mock_azure_credential",
    "mock_db_context",
    "mock_health_check_service",
    "sample_enterprise_data",
    "mock_configuration",
    "mock_logger"
]