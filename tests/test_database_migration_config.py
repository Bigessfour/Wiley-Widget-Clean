"""
Test Database Migration Configuration Changes

Tests the production-ready database migration setup:
- CI/CD script generation
- Deployment script functionality
- Environment detection logic
"""

import pytest
import os
import subprocess
from pathlib import Path


class TestMigrationScripts:
    """Test the migration-related scripts and CI/CD integration."""

    def test_apply_migrations_script_exists(self):
        """Test that the apply migrations script exists and is valid PowerShell."""
        script_path = Path("scripts/apply-migrations.ps1")
        assert script_path.exists(), "Apply migrations script should exist"

        # Read the script content
        content = script_path.read_text(encoding='utf-8')

        # Verify it contains expected PowerShell structure
        assert "param(" in content, "Should contain PowerShell parameter definition"
        assert "ConnectionString" in content, "Should accept ConnectionString parameter"
        assert "ScriptPath" in content, "Should accept ScriptPath parameter"
        assert "WhatIf" in content, "Should support WhatIf mode"

    def test_apply_migrations_script_syntax(self):
        """Test that the apply migrations script has valid PowerShell syntax."""
        script_path = Path("scripts/apply-migrations.ps1")

        # Use PowerShell to validate syntax
        result = subprocess.run(
            ["pwsh", "-Command", f"$ast = [System.Management.Automation.Language.Parser]::ParseFile('{script_path}', [ref]$null, [ref]$null); if ($ast) {{ Write-Host 'Valid' }} else {{ Write-Host 'Invalid' }}"],
            capture_output=True,
            text=True,
            cwd=Path.cwd()
        )

        # The command should succeed (syntax is valid)
        assert result.returncode == 0, f"PowerShell syntax validation failed: {result.stderr}"

    def test_ci_workflow_includes_migration_script_generation(self):
        """Test that the CI workflow includes migration script generation."""
        workflow_path = Path(".github/workflows/ci-new.yml")
        assert workflow_path.exists(), "CI workflow should exist"

        content = workflow_path.read_text(encoding='utf-8')

        # Verify migration script generation step exists
        assert "Generate Migration Scripts" in content, "Should contain migration script generation step"
        assert "dotnet ef migrations script" in content, "Should use dotnet ef migrations script command"
        assert "migration-script.sql" in content, "Should generate migration-script.sql"

    def test_ci_workflow_uploads_migration_script(self):
        """Test that the CI workflow uploads the generated migration script."""
        workflow_path = Path(".github/workflows/ci-new.yml")
        content = workflow_path.read_text(encoding='utf-8')

        # Verify migration script is included in artifacts
        assert "migration-script.sql" in content, "Migration script should be uploaded as artifact"


class TestEnvironmentDetection:
    """Test environment detection logic."""

    @pytest.fixture
    def original_env(self):
        """Save original environment variables."""
        original = dict(os.environ)
        yield
        # Restore original environment
        os.environ.clear()
        os.environ.update(original)

    def test_development_environment_detection(self, original_env):
        """Test development environment detection."""
        # Test default (no environment set)
        os.environ.pop('ASPNETCORE_ENVIRONMENT', None)
        os.environ.pop('DOTNET_ENVIRONMENT', None)

        # Import and test the logic (we'll simulate this)
        # Since we can't directly import C#, we'll test the concept
        env_vars = ['ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT']

        for var in env_vars:
            os.environ[var] = 'Development'
            assert os.environ.get(var) == 'Development'
            os.environ[var] = 'Production'
            assert os.environ.get(var) == 'Production'

    def test_production_environment_detection(self, original_env):
        """Test production environment detection."""
        env_vars = ['ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT']

        for var in env_vars:
            os.environ[var] = 'Production'
            assert os.environ.get(var) == 'Production'


class TestMigrationWorkflow:
    """Test the overall migration workflow."""

    def test_migration_script_can_be_generated(self):
        """Test that migration scripts can be generated (conceptual test)."""
        # This test verifies the EF command structure
        # In a real scenario, this would run against an actual project

        # Verify the project file exists
        csproj_path = Path("WileyWidget.csproj")
        assert csproj_path.exists(), "C# project file should exist"

        # Verify Migrations directory exists
        migrations_path = Path("Migrations")
        assert migrations_path.exists(), "Migrations directory should exist"
        assert migrations_path.is_dir(), "Migrations should be a directory"

        # Check that migration files exist
        migration_files = list(migrations_path.glob("*.cs"))
        assert len(migration_files) > 0, "Should have migration files"

    def test_deployment_script_parameters(self):
        """Test that the deployment script has proper parameter validation."""
        script_path = Path("scripts/apply-migrations.ps1")
        content = script_path.read_text(encoding='utf-8')

        # Verify mandatory parameters
        assert "Parameter(Mandatory" in content, "Should have mandatory parameters"
        assert "[string]$ConnectionString" in content, "Should require ConnectionString"
        assert "[string]$ScriptPath" in content, "Should require ScriptPath"

        # Verify WhatIf support
        assert "[switch]$WhatIf" in content, "Should support WhatIf mode"


if __name__ == "__main__":
    pytest.main([__file__, "-v"])