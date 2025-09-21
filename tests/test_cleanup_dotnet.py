#!/usr/bin/env python3
"""
Unit tests for cleanup-dotnet.py
Run with: pytest tests/test_cleanup_dotnet.py
"""

import pytest
from unittest.mock import patch, MagicMock
import sys
import os
import importlib.util

# Import the script as a module
script_path = os.path.join(os.path.dirname(__file__), '..', 'scripts', 'cleanup-dotnet.py')
spec = importlib.util.spec_from_file_location("cleanup_dotnet", script_path)
if spec is None or spec.loader is None:
    raise ImportError(f"Could not load script from {script_path}")

cleanup_dotnet = importlib.util.module_from_spec(spec)
sys.modules["cleanup_dotnet"] = cleanup_dotnet
spec.loader.exec_module(cleanup_dotnet)

# Now import the functions
get_dotnet_processes = cleanup_dotnet.get_dotnet_processes
cleanup_processes = cleanup_dotnet.cleanup_processes


class TestGetDotnetProcesses:
    """Test the get_dotnet_processes function"""

    @patch('subprocess.run')
    def test_get_dotnet_processes_success(self, mock_run):
        """Test successful process listing"""
        # Mock successful tasklist output
        mock_result = MagicMock()
        mock_result.stdout = ('"dotnet.exe","1234","Console","1","10,000 K"\n'
                              '"WileyWidget.exe","5678","Console","1","15,000 K"\n'
                              '"notepad.exe","9999","Console","1","5,000 K"')
        mock_result.returncode = 0
        mock_run.return_value = mock_result

        processes = get_dotnet_processes()

        assert len(processes) == 2
        assert processes[0] == ('dotnet.exe', '1234')
        assert processes[1] == ('WileyWidget.exe', '5678')

    @patch('subprocess.run')
    def test_get_dotnet_processes_no_matches(self, mock_run):
        """Test when no .NET processes are found"""
        mock_result = MagicMock()
        mock_result.stdout = ('"notepad.exe","9999","Console","1","5,000 K"\n'
                              '"explorer.exe","1111","Console","1","20,000 K"')
        mock_result.returncode = 0
        mock_run.return_value = mock_result

        processes = get_dotnet_processes()

        assert len(processes) == 0

    @patch('subprocess.run')
    def test_get_dotnet_processes_error(self, mock_run):
        """Test error handling"""
        mock_run.side_effect = Exception("Command failed")

        processes = get_dotnet_processes()

        assert processes == []


class TestCleanupProcesses:
    """Test the cleanup_processes function"""

    @patch('cleanup_dotnet.get_dotnet_processes')
    @patch('builtins.input')
    @patch('subprocess.run')
    def test_cleanup_no_processes(self, mock_subprocess, mock_input, mock_get_processes):
        """Test cleanup when no processes found"""
        mock_get_processes.return_value = []

        result = cleanup_processes()

        assert result is True

    @patch('cleanup_dotnet.get_dotnet_processes')
    @patch('builtins.input')
    @patch('subprocess.run')
    def test_cleanup_with_processes_force(self, mock_subprocess, mock_input, mock_get_processes):
        """Test cleanup with processes in force mode"""
        mock_get_processes.return_value = [('dotnet.exe', '1234'), ('WileyWidget.exe', '5678')]
        mock_subprocess.return_value = MagicMock()

        result = cleanup_processes(force=True)

        assert result is True
        # Should call taskkill twice
        assert mock_subprocess.call_count == 2

    @patch('cleanup_dotnet.get_dotnet_processes')
    @patch('builtins.input')
    @patch('subprocess.run')
    def test_cleanup_dry_run(self, mock_subprocess, mock_input, mock_get_processes):
        """Test dry run mode"""
        mock_get_processes.return_value = [('dotnet.exe', '1234')]

        result = cleanup_processes(dry_run=True)

        assert result is True
        # Should not call taskkill in dry run
        mock_subprocess.assert_not_called()


if __name__ == '__main__':
    pytest.main([__file__])
