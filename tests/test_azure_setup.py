#!/usr/bin/env python3
"""
Unit tests for azure-setup.py
Run with: pytest tests/test_azure_setup.py
"""

from unittest.mock import patch, MagicMock, mock_open
import sys
import os
import importlib.util

# Import the script as a module
script_path = os.path.join(os.path.dirname(__file__), '..', 'scripts', 'azure-setup.py')
spec = importlib.util.spec_from_file_location("azure_setup", script_path)
if spec is None or spec.loader is None:
    raise ImportError(f"Could not load script from {script_path}")

azure_setup = importlib.util.module_from_spec(spec)
sys.modules["azure_setup"] = azure_setup
spec.loader.exec_module(azure_setup)

# Now import the functions
load_env_file = azure_setup.load_env_file  # type: ignore
check_azure_cli = azure_setup.check_azure_cli  # type: ignore
sql_connection_func = azure_setup.test_sql_connection  # type: ignore
create_resources = azure_setup.create_resources  # type: ignore
deploy_database = azure_setup.deploy_database  # type: ignore


class TestLoadEnvFile:
    """Test the load_env_file function"""

    @patch('azure_setup.Path')
    @patch.dict(os.environ, {}, clear=True)
    def test_load_env_file_success(self, mock_path_class):
        """Test successful loading of environment variables"""
        # Create a mock Path instance
        mock_env_file = MagicMock()
        mock_env_file.exists.return_value = True
        mock_path_class.return_value = mock_env_file

        test_content = '''# Comment
AZURE_SQL_SERVER=test-server.database.windows.net
AZURE_SQL_DATABASE=test-db
AZURE_SQL_USER=test-user
AZURE_SQL_PASSWORD="test-password"
EMPTY_VAR=
'''

        with patch('builtins.open', mock_open(read_data=test_content)):
            result = load_env_file()

            assert result is True
            assert os.environ['AZURE_SQL_SERVER'] == 'test-server.database.windows.net'
            assert os.environ['AZURE_SQL_DATABASE'] == 'test-db'
            assert os.environ['AZURE_SQL_USER'] == 'test-user'
            assert os.environ['AZURE_SQL_PASSWORD'] == 'test-password'
            assert os.environ['EMPTY_VAR'] == ''  # Empty values are set to empty string

    @patch('azure_setup.Path')
    def test_load_env_file_not_found(self, mock_path_class, capsys):
        """Test when .env file doesn't exist"""
        mock_env_file = MagicMock()
        mock_env_file.exists.return_value = False
        mock_path_class.return_value = mock_env_file

        result = load_env_file()

        assert result is False
        captured = capsys.readouterr()
        assert "❌ .env file not found!" in captured.out

    @patch('azure_setup.Path')
    @patch.dict(os.environ, {}, clear=True)
    def test_load_env_file_with_quotes(self, mock_path_class):
        """Test loading variables with quotes"""
        mock_env_file = MagicMock()
        mock_env_file.exists.return_value = True
        mock_path_class.return_value = mock_env_file

        test_content = '''QUOTED_VAR="quoted value"
SINGLE_QUOTED='single quoted'
'''

        with patch('builtins.open', mock_open(read_data=test_content)):
            result = load_env_file()

            assert result is True
            assert os.environ['QUOTED_VAR'] == 'quoted value'
            assert os.environ['SINGLE_QUOTED'] == 'single quoted'

    @patch('azure_setup.Path')
    def test_load_env_file_read_error(self, mock_path_class, capsys):
        """Test handling of file read errors"""
        mock_env_file = MagicMock()
        mock_env_file.exists.return_value = True
        mock_path_class.return_value = mock_env_file

        with patch('builtins.open', side_effect=Exception("File read error")):
            result = load_env_file()

            assert result is False
            captured = capsys.readouterr()
            assert "❌ Error loading .env file: File read error" in captured.out


class TestCheckAzureCli:
    """Test the check_azure_cli function"""

    @patch('subprocess.run')
    def test_check_azure_cli_success(self, mock_run, capsys):
        """Test successful Azure CLI authentication check"""
        mock_result = MagicMock()
        mock_result.stdout = '{"user": {"name": "test@example.com"}, "name": "Test Subscription"}'
        mock_result.returncode = 0
        mock_run.return_value = mock_result

        result = check_azure_cli()

        assert result is True
        captured = capsys.readouterr()
        assert "✓ Signed in as: test@example.com" in captured.out
        assert "✓ Subscription: Test Subscription" in captured.out

    @patch('subprocess.run')
    def test_check_azure_cli_not_signed_in(self, mock_run, capsys):
        """Test when not signed in to Azure CLI"""
        mock_run.side_effect = Exception("Command failed")

        result = check_azure_cli()

        assert result is False
        captured = capsys.readouterr()
        assert "❌ Error checking Azure CLI: Command failed" in captured.out


class TestTestSqlConnection:
    """Test the test_sql_connection function"""

    @patch.dict(os.environ, {
        'AZURE_SQL_SERVER': 'test-server.database.windows.net',
        'AZURE_SQL_DATABASE': 'test-db',
        'AZURE_SQL_USER': 'test-user',
        'AZURE_SQL_PASSWORD': 'test-password'
    }, clear=True)
    @patch('subprocess.run')
    def test_test_sql_connection_success(self, mock_run, capsys):
        """Test successful SQL connection"""
        mock_result = MagicMock()
        mock_result.returncode = 0
        mock_result.stdout = "TestConnection\n1"
        mock_run.return_value = mock_result

        result = sql_connection_func()

        assert result is True
        captured = capsys.readouterr()
        assert "✓ Azure SQL connection successful" in captured.out

    @patch.dict(os.environ, {}, clear=True)
    def test_test_sql_connection_missing_vars(self, capsys):
        """Test when required environment variables are missing"""
        result = sql_connection_func()

        assert result is False
        captured = capsys.readouterr()
        assert "❌ Missing environment variables:" in captured.out
        assert "AZURE_SQL_SERVER" in captured.out

    @patch.dict(os.environ, {
        'AZURE_SQL_SERVER': 'test-server.database.windows.net',
        'AZURE_SQL_DATABASE': 'test-db',
        'AZURE_SQL_USER': 'test-user',
        'AZURE_SQL_PASSWORD': 'test-password'
    }, clear=True)
    @patch('subprocess.run')
    def test_test_sql_connection_sqlcmd_not_found(self, mock_run, capsys):
        """Test when sqlcmd is not available"""
        mock_run.side_effect = FileNotFoundError("sqlcmd not found")

        result = sql_connection_func()

        assert result is True  # Returns True when sqlcmd not found (graceful degradation)
        captured = capsys.readouterr()
        assert "⚠️  sqlcmd not found, skipping SQL connection test" in captured.out

    @patch.dict(os.environ, {
        'AZURE_SQL_SERVER': 'test-server.database.windows.net',
        'AZURE_SQL_DATABASE': 'test-db',
        'AZURE_SQL_USER': 'test-user',
        'AZURE_SQL_PASSWORD': 'test-password'
    }, clear=True)
    @patch('subprocess.run')
    def test_test_sql_connection_failure(self, mock_run, capsys):
        """Test SQL connection failure"""
        mock_result = MagicMock()
        mock_result.returncode = 1
        mock_result.stderr = "Connection failed"
        mock_run.return_value = mock_result

        result = sql_connection_func()

        assert result is False
        captured = capsys.readouterr()
        assert "❌ SQL connection failed:" in captured.out


class TestCreateResources:
    """Test the create_resources function"""

    def test_create_resources_not_implemented(self, capsys):
        """Test that create_resources shows not implemented message"""
        result = create_resources()

        assert result is True  # Returns True even though not implemented
        captured = capsys.readouterr()
        assert "⚠️  Resource creation not implemented in this script" in captured.out


class TestDeployDatabase:
    """Test the deploy_database function"""

    @patch('subprocess.run')
    @patch('os.getcwd')
    def test_deploy_database_success(self, mock_getcwd, mock_run, capsys):
        """Test successful database deployment"""
        mock_getcwd.return_value = '/test/path'
        mock_result = MagicMock()
        mock_result.returncode = 0
        mock_run.return_value = mock_result

        result = deploy_database()

        assert result is True
        captured = capsys.readouterr()
        assert "✓ Database deployed successfully" in captured.out

    @patch('subprocess.run')
    @patch('os.getcwd')
    def test_deploy_database_failure(self, mock_getcwd, mock_run, capsys):
        """Test database deployment failure"""
        mock_getcwd.return_value = '/test/path'
        mock_result = MagicMock()
        mock_result.returncode = 1
        mock_result.stderr = "Migration failed"
        mock_run.return_value = mock_result

        result = deploy_database()

        assert result is False
        captured = capsys.readouterr()
        assert "❌ Database deployment failed:" in captured.out


class TestMainFunction:
    """Test the main function"""

    @patch('azure_setup.load_env_file')  # type: ignore
    @patch('azure_setup.check_azure_cli')  # type: ignore
    def test_main_success_no_args(self, mock_check_cli, mock_load_env, capsys):
        """Test main function with no arguments (should show help)"""
        mock_load_env.return_value = True
        mock_check_cli.return_value = True

        with patch('sys.argv', ['azure-setup.py']):
            result = azure_setup.main()

            assert result == 0  # Should return 0 when basic checks pass
            captured = capsys.readouterr()
            assert "✅ Azure setup completed successfully!" in captured.out

    @patch('azure_setup.load_env_file')  # type: ignore
    @patch('azure_setup.check_azure_cli')  # type: ignore
    @patch('azure_setup.test_sql_connection')  # type: ignore
    def test_main_test_connection_success(self, mock_test_sql, mock_check_cli, mock_load_env, capsys):
        """Test main function with --test-connection flag"""
        mock_load_env.return_value = True
        mock_check_cli.return_value = True
        mock_test_sql.return_value = True

        with patch('sys.argv', ['azure-setup.py', '--test-connection']):
            result = azure_setup.main()

            assert result == 0
            captured = capsys.readouterr()
            assert "✅ Azure setup completed successfully!" in captured.out

    @patch('azure_setup.load_env_file')  # type: ignore
    def test_main_env_file_failure(self, mock_load_env, capsys):
        """Test main function when env file loading fails"""
        mock_load_env.return_value = False

        with patch('sys.argv', ['azure-setup.py', '--test-connection']):
            result = azure_setup.main()

            assert result == 1
            captured = capsys.readouterr()
            assert "🔧 WileyWidget Azure Setup Script (Python)" in captured.out

    @patch('azure_setup.load_env_file')  # type: ignore
    @patch('azure_setup.check_azure_cli')  # type: ignore
    def test_main_azure_cli_failure(self, mock_check_cli, mock_load_env, capsys):
        """Test main function when Azure CLI check fails"""
        mock_load_env.return_value = True
        mock_check_cli.return_value = False

        with patch('sys.argv', ['azure-setup.py', '--test-connection']):
            result = azure_setup.main()

            assert result == 1
            captured = capsys.readouterr()
            assert "🔧 WileyWidget Azure Setup Script (Python)" in captured.out
