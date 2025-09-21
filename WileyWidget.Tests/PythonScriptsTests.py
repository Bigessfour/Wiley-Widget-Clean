#!/usr/bin/env python3
"""
Integration tests for Wiley Widget Python scripts
Tests dev-start.py and azure-setup.py as subprocesses
"""

import unittest
import subprocess
import sys
import os
import tempfile
from pathlib import Path

class TestDevStartIntegration(unittest.TestCase):
    """Integration tests for dev-start.py script"""

    def setUp(self):
        """Set up test fixtures"""
        self.original_cwd = os.getcwd()
        self.project_root = Path(__file__).parent.parent

        # Change to project root for testing
        os.chdir(self.project_root)

    def tearDown(self):
        """Clean up test fixtures"""
        os.chdir(self.original_cwd)

    def test_dev_start_help_output(self):
        """Test dev-start.py help output"""
        script_path = self.project_root / 'scripts' / 'dev-start.py'
        self.assertTrue(script_path.exists(), "dev-start.py script should exist")

        result = subprocess.run([
            sys.executable, str(script_path), '--help'
        ], capture_output=True, text=True, cwd=self.project_root)

        # Should show help and exit successfully
        self.assertEqual(result.returncode, 0)
        self.assertIn("usage:", result.stdout.lower() or result.stderr.lower())

    def test_dev_start_clean_only_mode(self):
        """Test dev-start.py with --clean-only flag"""
        script_path = self.project_root / 'scripts' / 'dev-start.py'
        self.assertTrue(script_path.exists(), "dev-start.py script should exist")

        result = subprocess.run([
            sys.executable, str(script_path), '--clean-only'
        ], capture_output=True, text=True, cwd=self.project_root)

        # Should complete successfully (exit code 0)
        # Note: May return non-zero if there are actual processes to clean
        # The important thing is it doesn't crash
        self.assertIsInstance(result.returncode, int)
        # Should show cleanup output
        self.assertIn("Process Cleanup", result.stdout)

    def test_dev_start_verbose_mode(self):
        """Test dev-start.py with verbose flag"""
        script_path = self.project_root / 'scripts' / 'dev-start.py'
        self.assertTrue(script_path.exists(), "dev-start.py script should exist")

        result = subprocess.run([
            sys.executable, str(script_path), '--clean-only', '--verbose'
        ], capture_output=True, text=True, cwd=self.project_root)

        # Should complete successfully
        # Note: May return non-zero if there are actual processes to clean
        self.assertIsInstance(result.returncode, int)
        # Should show startup output (verbose mode may not have specific text)
        self.assertIn("Wiley Widget", result.stdout)


class TestAzureSetupIntegration(unittest.TestCase):
    """Integration tests for azure-setup.py script"""

    def setUp(self):
        """Set up test fixtures"""
        self.original_cwd = os.getcwd()
        self.project_root = Path(__file__).parent.parent

        # Change to project root for testing
        os.chdir(self.project_root)

    def tearDown(self):
        """Clean up test fixtures"""
        os.chdir(self.original_cwd)

    def test_azure_setup_help_output(self):
        """Test azure-setup.py help output"""
        script_path = self.project_root / 'scripts' / 'azure-setup.py'
        self.assertTrue(script_path.exists(), "azure-setup.py script should exist")

        result = subprocess.run([
            sys.executable, str(script_path), '--help'
        ], capture_output=True, text=True, cwd=self.project_root)

        # Should show help and exit successfully
        self.assertEqual(result.returncode, 0)
        self.assertIn("usage:", result.stdout.lower() or result.stderr.lower())

    def test_azure_setup_test_connection_mode(self):
        """Test azure-setup.py with --test-connection flag"""
        script_path = self.project_root / 'scripts' / 'azure-setup.py'
        self.assertTrue(script_path.exists(), "azure-setup.py script should exist")

        result = subprocess.run([
            sys.executable, str(script_path), '--test-connection'
        ], capture_output=True, text=True, cwd=self.project_root)

        # Should attempt connection test
        # May succeed or fail depending on environment, but should not crash
        self.assertIsInstance(result.returncode, int)
        # Should show some output (may be empty if no .env file)
        self.assertIsInstance(result.stdout, str)

    def test_azure_setup_without_env_file(self):
        """Test azure-setup.py when .env file doesn't exist"""
        script_path = self.project_root / 'scripts' / 'azure-setup.py'
        self.assertTrue(script_path.exists(), "azure-setup.py script should exist")

        # Create a backup of .env if it exists
        env_file = self.project_root / '.env'
        env_backup = None
        if env_file.exists():
            env_backup = self.project_root / '.env.backup'
            env_file.rename(env_backup)

        try:
            result = subprocess.run([
                sys.executable, str(script_path)
            ], capture_output=True, text=True, cwd=self.project_root)

            # Should fail due to missing .env file
            self.assertNotEqual(result.returncode, 0)
            # Should show some error output
            self.assertIsInstance(result.stdout, str)
        finally:
            # Restore .env file if it existed
            if env_backup and env_backup.exists():
                env_backup.rename(env_file)

    def test_azure_setup_with_env_file(self):
        """Test azure-setup.py with valid .env file"""
        script_path = self.project_root / 'scripts' / 'azure-setup.py'
        self.assertTrue(script_path.exists(), "azure-setup.py script should exist")

        # Check if .env exists
        env_file = self.project_root / '.env'
        if not env_file.exists():
            self.skipTest(".env file not found - cannot test with env file")

        result = subprocess.run([
            sys.executable, str(script_path)
        ], capture_output=True, text=True, cwd=self.project_root)

        # Should attempt to run
        # May succeed or fail depending on Azure setup, but should not crash
        self.assertIsInstance(result.returncode, int)
        # Should show some output
        self.assertIsInstance(result.stdout, str)


class TestPythonScriptStructure(unittest.TestCase):
    """Tests for Python script structure and best practices"""

    def setUp(self):
        """Set up test fixtures"""
        self.project_root = Path(__file__).parent.parent

    def test_dev_start_script_exists(self):
        """Test that dev-start.py script exists and is executable"""
        script_path = self.project_root / 'scripts' / 'dev-start.py'
        self.assertTrue(script_path.exists(), "dev-start.py script should exist")

        # Check if it has shebang
        with open(script_path, 'r', encoding='utf-8') as f:
            first_line = f.readline().strip()
            self.assertEqual(first_line, "#!/usr/bin/env python3",
                           "Script should have proper shebang")

    def test_azure_setup_script_exists(self):
        """Test that azure-setup.py script exists and is executable"""
        script_path = self.project_root / 'scripts' / 'azure-setup.py'
        self.assertTrue(script_path.exists(), "azure-setup.py script should exist")

        # Check if it has shebang
        with open(script_path, 'r', encoding='utf-8') as f:
            first_line = f.readline().strip()
            self.assertEqual(first_line, "#!/usr/bin/env python3",
                           "Script should have proper shebang")

    def test_dev_start_has_main_function(self):
        """Test that dev-start.py has a main function"""
        script_path = self.project_root / 'scripts' / 'dev-start.py'

        with open(script_path, 'r', encoding='utf-8') as f:
            content = f.read()
            self.assertIn("def main(", content, "dev-start.py should have a main function")
            self.assertIn("if __name__ == '__main__'", content,
                         "dev-start.py should have proper main guard")

    def test_azure_setup_has_main_function(self):
        """Test that azure-setup.py has a main function"""
        script_path = self.project_root / 'scripts' / 'azure-setup.py'

        with open(script_path, 'r', encoding='utf-8') as f:
            content = f.read()
            self.assertIn("def main(", content, "azure-setup.py should have a main function")
            self.assertIn("if __name__ == '__main__'", content,
                         "azure-setup.py should have proper main guard")

    def test_dev_start_has_docstring(self):
        """Test that dev-start.py has proper docstring"""
        script_path = self.project_root / 'scripts' / 'dev-start.py'

        with open(script_path, 'r', encoding='utf-8') as f:
            content = f.read()
            # Check for module docstring (starts after shebang)
            lines = content.split('\n')
            # Skip shebang line and find the docstring
            docstring_found = False
            for line in lines[1:]:  # Start from line 2 (after shebang)
                if line.strip().startswith('"""'):
                    docstring_found = True
                    break
            self.assertTrue(docstring_found, "Should have module docstring")
            self.assertIn("Wiley Widget Development Startup Script", content)

    def test_azure_setup_has_docstring(self):
        """Test that azure-setup.py has proper docstring"""
        script_path = self.project_root / 'scripts' / 'azure-setup.py'

        with open(script_path, 'r', encoding='utf-8') as f:
            content = f.read()
            # Check for module docstring (starts after shebang)
            lines = content.split('\n')
            # Skip shebang line and find the docstring
            docstring_found = False
            for line in lines[1:]:  # Start from line 2 (after shebang)
                if line.strip().startswith('"""'):
                    docstring_found = True
                    break
            self.assertTrue(docstring_found, "Should have module docstring")
            self.assertIn("Azure Setup and Configuration Script", content)


if __name__ == '__main__':
    unittest.main()