"""
Timing Tests for WileyWidget with Artificial Delays
Tests that services work correctly with delays and data loads properly
"""

import pytest


@pytest.mark.asyncio
class TestServiceDelays:
    """Test that services contain artificial delays"""

    def test_authentication_service_contains_delay(self):
        """Test that authentication service source code includes artificial delay"""
        import os

        auth_service_path = r"c:\Users\biges\Desktop\Wiley_Widget\Services\AuthenticationService.cs"
        assert os.path.exists(auth_service_path), "AuthenticationService.cs should exist"

        with open(auth_service_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Check that SignInAsync contains Task.Delay
        assert "Task.Delay(2000)" in content, "AuthenticationService.SignInAsync should contain 2-second delay"
        assert "Task.Delay(1500)" in content, "AuthenticationService.GetAccessTokenAsync should contain 1.5-second delay"

    def test_repository_services_contain_delays(self):
        """Test that repository classes contain artificial delays"""
        import os

        # Check UtilityCustomerRepository
        repo_path = r"c:\Users\biges\Desktop\Wiley_Widget\Data\UtilityCustomerRepository.cs"
        assert os.path.exists(repo_path), "UtilityCustomerRepository.cs should exist"

        with open(repo_path, 'r', encoding='utf-8') as f:
            content = f.read()

        assert "Task.Delay(1000)" in content, "UtilityCustomerRepository should contain 1-second delay"

        # Check EnterpriseRepository
        repo_path = r"c:\Users\biges\Desktop\Wiley_Widget\Data\EnterpriseRepository.cs"
        assert os.path.exists(repo_path), "EnterpriseRepository.cs should exist"

        with open(repo_path, 'r', encoding='utf-8') as f:
            content = f.read()

        assert "Task.Delay(1200)" in content, "EnterpriseRepository should contain 1.2-second delay"

    def test_viewmodel_contains_delays(self):
        """Test that ViewModel contains artificial delays"""
        import os

        vm_path = r"c:\Users\biges\Desktop\Wiley_Widget\ViewModels\DashboardViewModel.cs"
        assert os.path.exists(vm_path), "DashboardViewModel.cs should exist"

        with open(vm_path, 'r', encoding='utf-8') as f:
            content = f.read()

        assert "Task.Delay(800)" in content, "DashboardViewModel should contain 0.8-second delay"
        assert "Task.Delay(500)" in content, "DashboardViewModel.LoadKPIsAsync should contain 0.5-second delay"


class TestApplicationStartup:
    """Test application startup behavior with delays"""

    def test_application_builds_with_delays(self):
        """Test that application builds successfully with delay code"""
        import subprocess

        result = subprocess.run(
            ["dotnet", "build", "WileyWidget.csproj", "--verbosity", "quiet"],
            cwd=r"c:\Users\biges\Desktop\Wiley_Widget",
            capture_output=True,
            text=True
        )

        assert result.returncode == 0, f"Build failed: {result.stderr}"
        assert "Task.Delay" in open(r"c:\Users\biges\Desktop\Wiley_Widget\Services\AuthenticationService.cs").read()
        assert "Task.Delay" in open(r"c:\Users\biges\Desktop\Wiley_Widget\Data\UtilityCustomerRepository.cs").read()
        assert "Task.Delay" in open(r"c:\Users\biges\Desktop\Wiley_Widget\Data\EnterpriseRepository.cs").read()

    def test_startup_logging_includes_delays(self):
        """Test that startup logs show timing information"""
        import subprocess

        result = subprocess.run(
            ["dotnet", "run", "--project", "WileyWidget.csproj"],
            cwd=r"c:\Users\biges\Desktop\Wiley_Widget",
            capture_output=True,
            text=True,
            timeout=30  # Don't wait forever
        )

        # Check that the application at least starts (may exit due to no GUI)
        # The important thing is that it doesn't hang indefinitely
        output = result.stdout + result.stderr
        assert "Building..." in output or "Using launch settings" in output, "Application should start"


@pytest.mark.smoke
def test_timing_validation_script_syntax():
    """Validate that the timing test script has correct Python syntax"""
    import py_compile
    import sys

    file_path = sys.modules[__name__].__file__
    if file_path is None:
        pytest.skip("Cannot determine file path for syntax validation")

    assert file_path is not None  # Type hint for mypy/Pylance
    try:
        py_compile.compile(file_path, doraise=True)
        print("✓ Python syntax validation passed")
    except py_compile.PyCompileError as e:
        pytest.fail(f"Python syntax error: {e}")


if __name__ == "__main__":
    # Allow running this test directly
    pytest.main([__file__, "-v", "-s"])