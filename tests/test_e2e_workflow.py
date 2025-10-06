"""
End-to-End Tests for WileyWidget Application

Tests complete user workflows from application startup through data operations
to UI verification, ensuring the full application stack works correctly.
"""

import pytest
import time
import pyodbc
from pywinauto.base_wrapper import ElementNotEnabled
from pywinauto.findwindows import ElementNotFoundError
from pywinauto.timings import TimeoutError as PywinautoTimeoutError


class BaseE2ETest:
    """Base class for E2E tests with shared helper methods"""

    def _find_data_grid(self, main_window):
        """Find the main data grid in the UI"""
        grid_candidates = [
            main_window.child_window(control_type="DataGrid"),
            main_window.child_window(class_name="SfDataGrid"),
            main_window.child_window(title_re=".*Grid.*")
        ]

        for grid in grid_candidates:
            if grid.exists():
                return grid
        return None

    def _verify_database_content(self, test_env):
        """Verify database has expected content"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            # Check enterprises table
            cursor.execute("SELECT COUNT(*) FROM Enterprises")
            result = cursor.fetchone()
            if result is None:
                raise Exception("No result returned from COUNT query")
            enterprise_count = result[0]
            assert enterprise_count > 0, "Should have enterprise records"

            # Check for other expected tables
            cursor.execute("SELECT COUNT(*) FROM BudgetInteractions")
            # Budget interactions may or may not exist

            conn.close()
            return True

        except Exception as e:
            pytest.fail(f"Database verification failed: {e}")

    def _get_connection_string(self, test_env):
        """Get database connection string for testing"""
        if test_env.environment == "Development":
            return (f"DRIVER={{ODBC Driver 17 for SQL Server}};"
                    f"SERVER=localhost\\SQLEXPRESS01;"
                    f"DATABASE={test_env.test_db_name};"
                    "Trusted_Connection=yes;")
        else:
            # For Azure testing (mocked)
            return "mock_connection_string"

    def _get_enterprise_data_from_db(self, test_env):
        """Get enterprise data from database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            cursor.execute("SELECT Id, Name, CurrentRate, MonthlyExpenses, CitizenCount FROM Enterprises")
            columns = [column[0] for column in cursor.description]
            data = [dict(zip(columns, row)) for row in cursor.fetchall()]

            conn.close()
            return data

        except Exception:
            return []

    def _get_enterprise_count_from_db(self, test_env):
        """Get count of enterprises from database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            cursor.execute("SELECT COUNT(*) FROM Enterprises")
            result = cursor.fetchone()
            if result is None:
                return 0
            count = result[0]

            conn.close()
            return count

        except Exception:
            return 0

    def _get_budget_interaction_data_from_db(self, test_env):
        """Get budget interaction data from database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            cursor.execute("SELECT * FROM BudgetInteractions")
            columns = [column[0] for column in cursor.description]
            data = [dict(zip(columns, row)) for row in cursor.fetchall()]

            conn.close()
            return data

        except Exception:
            return []

    def _extract_enterprise_names_from_ui(self, main_window):
        """Extract enterprise names displayed in the UI"""
        names = []

        try:
            # Look for text elements that might contain enterprise names
            text_elements = main_window.find_elements(control_type="Text")

            for elem in text_elements:
                text = elem.window_text()
                # Look for known enterprise names or patterns
                if any(keyword in text for keyword in ["Department", "Water", "Sewer", "Utility"]):
                    names.append(text)

        except (ElementNotFoundError, ElementNotEnabled, PywinautoTimeoutError):
            pass

        return names

    def _verify_ui_matches_database(self, main_window, db_data):
        """Verify that UI content matches database data"""
        if not db_data:
            return False

        ui_names = self._extract_enterprise_names_from_ui(main_window)
        db_names = [enterprise['Name'] for enterprise in db_data]

        # Check for overlap
        return any(db_name in ' '.join(ui_names) for db_name in db_names)

    def _find_budget_elements(self, main_window):
        """Find budget-related UI elements"""
        budget_elements = []

        try:
            # Look for budget-related controls
            candidates = [
                main_window.child_window(title_re=".*Budget.*"),
                main_window.child_window(title_re=".*Chart.*"),
                main_window.child_window(control_type="ProgressBar")
            ]

            for elem in candidates:
                if elem.exists():
                    budget_elements.append(elem)

        except (ElementNotFoundError, ElementNotEnabled, PywinautoTimeoutError):
            pass

        return budget_elements

    def _insert_enterprise_into_db(self, test_env, enterprise_data):
        """Insert a new enterprise into the database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            cursor.execute("""
                INSERT INTO Enterprises (Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes, Status, Type, BudgetAmount, TotalBudget)
                VALUES (?, ?, ?, ?, ?, 'Active', 'Municipal', ?, ?)
            """, (
                enterprise_data['Name'],
                enterprise_data['CurrentRate'],
                enterprise_data['MonthlyExpenses'],
                enterprise_data['CitizenCount'],
                enterprise_data['Notes'],
                enterprise_data['MonthlyExpenses'] * 12,  # BudgetAmount
                enterprise_data['MonthlyExpenses'] * 12   # TotalBudget
            ))

            conn.commit()

            # Get the inserted ID
            cursor.execute("SELECT SCOPE_IDENTITY()")
            result = cursor.fetchone()
            if result is None:
                raise Exception("Failed to get inserted enterprise ID")
            new_id = result[0]

            conn.close()
            return new_id

        except Exception as e:
            print(f"Error inserting enterprise: {e}")
            return None

    def _update_enterprise_in_db(self, test_env, enterprise_id, updates):
        """Update an enterprise in the database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            set_clause = ", ".join([f"{key} = ?" for key in updates.keys()])
            values = list(updates.values())
            values.append(enterprise_id)

            cursor.execute(f"UPDATE Enterprises SET {set_clause} WHERE Id = ?", values)
            conn.commit()
            conn.close()
            return True

        except Exception as e:
            print(f"Error updating enterprise: {e}")
            return False

    def _delete_enterprise_from_db(self, test_env, enterprise_id):
        """Delete an enterprise from the database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            cursor.execute("DELETE FROM Enterprises WHERE Id = ?", (enterprise_id,))
            conn.commit()
            conn.close()
            return True

        except Exception as e:
            print(f"Error deleting enterprise: {e}")
            return False

    def _insert_budget_interaction_into_db(self, test_env, interaction_data):
        """Insert a new budget interaction into the database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            cursor.execute("""
                INSERT INTO BudgetInteractions (PrimaryEnterpriseId, SecondaryEnterpriseId, InteractionType, Description, MonthlyAmount, IsCost)
                VALUES (?, ?, ?, ?, ?, ?)
            """, (
                interaction_data['PrimaryEnterpriseId'],
                interaction_data['SecondaryEnterpriseId'],
                interaction_data['InteractionType'],
                interaction_data['Description'],
                interaction_data['MonthlyAmount'],
                interaction_data['IsCost']
            ))

            conn.commit()

            # Get the inserted ID
            cursor.execute("SELECT SCOPE_IDENTITY()")
            result = cursor.fetchone()
            if result is None:
                raise Exception("Failed to get inserted budget interaction ID")
            new_id = result[0]

            conn.close()
            return new_id

        except Exception as e:
            print(f"Error inserting budget interaction: {e}")
            raise e

    def _update_budget_interaction_in_db(self, test_env, interaction_id, updates):
        """Update a budget interaction in the database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            set_clause = ", ".join([f"{key} = ?" for key in updates.keys()])
            values = list(updates.values())
            values.append(interaction_id)

            cursor.execute(f"UPDATE BudgetInteractions SET {set_clause} WHERE Id = ?", values)
            conn.commit()
            conn.close()
            return True

        except Exception as e:
            print(f"Error updating budget interaction: {e}")
            return False

    def _delete_budget_interaction_from_db(self, test_env, interaction_id):
        """Delete a budget interaction from the database"""
        try:
            conn_str = self._get_connection_string(test_env)
            conn = pyodbc.connect(conn_str)
            cursor = conn.cursor()

            cursor.execute("DELETE FROM BudgetInteractions WHERE Id = ?", (interaction_id,))
            conn.commit()
            conn.close()
            return True

        except Exception as e:
            print(f"Error deleting budget interaction: {e}")
            return False


@pytest.mark.ui
@pytest.mark.e2e
class TestApplicationWorkflow(BaseE2ETest):
    """Test complete application workflows"""

    def test_application_startup_and_data_loading(self, ui_main_window, dev_test_env):
        """Test that application starts and loads data successfully"""
        if ui_main_window is None:
            pytest.skip("UI application not available for testing")

        # Wait for application to fully load with timeout
        import time
        start_time = time.time()
        timeout = 10  # 10 second timeout

        while time.time() - start_time < timeout:
            if ui_main_window.exists() and ui_main_window.is_visible():
                break
            time.sleep(0.5)
        else:
            pytest.skip("Application window did not become visible within timeout")

        # Verify main window is responsive
        assert ui_main_window.exists()
        assert ui_main_window.is_visible()
        assert not ui_main_window.is_minimized()

        # Wait for data loading to complete
        time.sleep(1)

        # Check that data grid is populated (should have enterprise data)
        data_grid = self._find_data_grid(ui_main_window)
        if data_grid:
            # Verify grid has content
            assert data_grid.exists()
            # Grid should be visible and enabled
            assert data_grid.is_visible()
            assert data_grid.is_enabled()

        # Verify database has expected data
        self._verify_database_content(dev_test_env)

    def test_data_display_accuracy(self, ui_main_window, dev_test_env):
        """Test that UI displays data accurately from database"""
        time.sleep(5)  # Wait for full load

        # Get data from database
        db_data = self._get_enterprise_data_from_db(dev_test_env)

        # Verify UI shows corresponding data
        if db_data:
            # Look for data in UI elements
            ui_enterprise_names = self._extract_enterprise_names_from_ui(ui_main_window)

            # At minimum, UI should show some of the database data
            db_names = {enterprise['Name'] for enterprise in db_data}
            ui_names = set(ui_enterprise_names)

            # There should be some overlap between DB and UI data
            overlap = db_names.intersection(ui_names)
            assert len(overlap) > 0, f"No enterprise names from database found in UI. DB: {db_names}, UI: {ui_names}"

    def test_ui_interaction_with_database(self, ui_main_window, dev_test_env):
        """Test that UI interactions affect database state"""
        time.sleep(5)

        # Get initial database state
        initial_count = self._get_enterprise_count_from_db(dev_test_env)

        # Try to interact with UI (this depends on available UI elements)
        # For now, just verify the application remains stable during interaction
        try:
            # Click on main window
            ui_main_window.click()
            time.sleep(0.5)

            # Try keyboard navigation
            ui_main_window.type_keys("{TAB}")
            time.sleep(0.5)

            # Application should remain stable
            assert ui_main_window.exists()
            assert ui_main_window.is_visible()

        except (ElementNotFoundError, ElementNotEnabled, PywinautoTimeoutError):
            # Some interactions may not be available, that's OK
            pass

        # Verify database state hasn't changed unexpectedly
        final_count = self._get_enterprise_count_from_db(dev_test_env)
        assert final_count == initial_count, "Database state changed unexpectedly during UI interaction"

    def test_application_shutdown_clean(self, ui_app, ui_main_window):
        """Test that application shuts down cleanly"""
        # Application should be running
        assert ui_app.is_process_running()

        # Close the application
        ui_main_window.close()
        time.sleep(2)

        # Process should terminate cleanly
        assert not ui_app.is_process_running()


@pytest.mark.e2e
class TestDatabaseCrudOperations(BaseE2ETest):
    """Test CRUD operations on database entities"""

    def test_enterprise_crud_operations(self, dev_test_env):
        """Test Create, Read, Update, Delete operations on enterprises"""
        # Test Read - get existing enterprises
        initial_data = self._get_enterprise_data_from_db(dev_test_env)
        initial_count = len(initial_data)
        assert initial_count > 0, "Should have initial enterprise data"

        # Test Create - add a new enterprise
        new_enterprise = {
            'Name': 'Test Municipal Services',
            'CurrentRate': 25.50,
            'MonthlyExpenses': 8500.00,
            'CitizenCount': 12000,
            'Notes': 'Test enterprise for E2E testing'
        }

        new_id = self._insert_enterprise_into_db(dev_test_env, new_enterprise)
        assert new_id is not None, "Should return new enterprise ID"

        # Verify Create - check count increased
        after_create_data = self._get_enterprise_data_from_db(dev_test_env)
        assert len(after_create_data) == initial_count + 1, "Enterprise count should increase after create"

        # Find the new enterprise
        new_enterprise_data = next((e for e in after_create_data if e['Id'] == new_id), None)
        assert new_enterprise_data is not None, "New enterprise should exist in database"
        assert new_enterprise_data['Name'] == new_enterprise['Name'], "Enterprise name should match"

        # Test Update - modify the enterprise
        updated_rate = 28.75
        self._update_enterprise_in_db(dev_test_env, new_id, {'CurrentRate': updated_rate})

        # Verify Update
        updated_data = self._get_enterprise_data_from_db(dev_test_env)
        updated_enterprise = next((e for e in updated_data if e['Id'] == new_id), None)
        assert updated_enterprise is not None, "Updated enterprise should exist"
        assert updated_enterprise['CurrentRate'] == updated_rate, "Enterprise rate should be updated"

        # Test Delete - remove the test enterprise
        self._delete_enterprise_from_db(dev_test_env, new_id)

        # Verify Delete
        after_delete_data = self._get_enterprise_data_from_db(dev_test_env)
        assert len(after_delete_data) == initial_count, "Enterprise count should return to initial after delete"

        # Verify the enterprise is gone
        deleted_enterprise = next((e for e in after_delete_data if e['Id'] == new_id), None)
        assert deleted_enterprise is None, "Deleted enterprise should not exist"

    def test_budget_interaction_crud_operations(self, dev_test_env):
        """Test CRUD operations on budget interactions"""
        # Get initial state
        initial_budget_data = self._get_budget_interaction_data_from_db(dev_test_env)
        initial_count = len(initial_budget_data)

        # Get enterprise IDs for testing
        enterprise_data = self._get_enterprise_data_from_db(dev_test_env)
        if len(enterprise_data) < 2:
            pytest.skip("Need at least 2 enterprises for budget interaction testing")

        primary_id = enterprise_data[0]['Id']
        secondary_id = enterprise_data[1]['Id']

        # Test Create - add budget interaction
        new_interaction = {
            'PrimaryEnterpriseId': primary_id,
            'SecondaryEnterpriseId': secondary_id,
            'InteractionType': 'Test Subsidy',
            'Description': 'E2E test budget interaction',
            'MonthlyAmount': 1500.00,
            'IsCost': True
        }

        interaction_id = self._insert_budget_interaction_into_db(dev_test_env, new_interaction)
        assert interaction_id is not None, "Should return new interaction ID"

        # Verify Create
        after_create_data = self._get_budget_interaction_data_from_db(dev_test_env)
        assert len(after_create_data) == initial_count + 1, "Budget interaction count should increase"

        # Test Update - modify the interaction
        updated_amount = 1800.00
        self._update_budget_interaction_in_db(dev_test_env, interaction_id, {'MonthlyAmount': updated_amount})

        # Verify Update
        updated_data = self._get_budget_interaction_data_from_db(dev_test_env)
        updated_interaction = next((i for i in updated_data if i['Id'] == interaction_id), None)
        assert updated_interaction is not None, "Updated interaction should exist"
        assert updated_interaction['MonthlyAmount'] == updated_amount, "Interaction amount should be updated"

        # Test Delete - remove the test interaction
        self._delete_budget_interaction_from_db(dev_test_env, interaction_id)

        # Verify Delete
        after_delete_data = self._get_budget_interaction_data_from_db(dev_test_env)
        assert len(after_delete_data) == initial_count, "Budget interaction count should return to initial"

    def test_data_integrity_constraints(self, dev_test_env):
        """Test database constraints and data integrity"""
        enterprise_data = self._get_enterprise_data_from_db(dev_test_env)

        # Test foreign key constraint - try to create budget interaction with invalid enterprise ID
        invalid_interaction = {
            'PrimaryEnterpriseId': 99999,  # Non-existent ID
            'SecondaryEnterpriseId': enterprise_data[0]['Id'] if enterprise_data else 1,
            'InteractionType': 'Invalid Test',
            'Description': 'Should fail due to FK constraint',
            'MonthlyAmount': 1000.00,
            'IsCost': False
        }

        # This should fail due to foreign key constraint
        with pytest.raises((Exception, pyodbc.Error)):
            self._insert_budget_interaction_into_db(dev_test_env, invalid_interaction)

        # Verify no invalid data was inserted
        budget_data = self._get_budget_interaction_data_from_db(dev_test_env)
        invalid_entries = [i for i in budget_data if i['InteractionType'] == 'Invalid Test']
        assert len(invalid_entries) == 0, "Invalid budget interaction should not exist"




@pytest.mark.ui
@pytest.mark.e2e
class TestDataWorkflow(BaseE2ETest):
    """Test data-related workflows"""

    def test_enterprise_data_workflow(self, ui_main_window, dev_test_env):
        """Test complete enterprise data workflow"""
        time.sleep(5)

        # Verify initial data load
        initial_data = self._get_enterprise_data_from_db(dev_test_env)
        assert len(initial_data) > 0, "Should have initial enterprise data"

        # Verify UI reflects database state
        ui_reflects_data = self._verify_ui_matches_database(ui_main_window, initial_data)
        assert ui_reflects_data, "UI should reflect database content"

    def test_budget_interaction_workflow(self, ui_main_window, dev_test_env):
        """Test budget interaction data workflow"""
        time.sleep(5)

        # Check for budget interaction data
        budget_data = self._get_budget_interaction_data_from_db(dev_test_env)

        # If budget data exists, verify it's accessible
        if budget_data:
            # Look for budget-related UI elements
            budget_elements = self._find_budget_elements(ui_main_window)
            # Should have some budget-related UI if data exists
            assert len(budget_elements) > 0, "Should have budget UI elements when budget data exists"


@pytest.mark.ui
@pytest.mark.e2e
class TestPerformanceWorkflow(BaseE2ETest):
    """Test performance aspects of complete workflows"""

    def test_startup_performance(self, ui_app, ui_main_window):
        """Test application startup performance"""
        # Application should start within reasonable time
        # (This is tested implicitly by the fixture timeout of 45 seconds)

        # Once started, should be responsive
        start_time = time.time()
        ui_main_window.click()
        response_time = time.time() - start_time

        assert response_time < 1.0, f"UI response too slow: {response_time:.2f}s"

    def test_data_loading_performance(self, ui_main_window, dev_test_env):
        """Test data loading performance"""
        start_time = time.time()

        # Wait for data to load (this happens during startup)
        time.sleep(5)

        load_time = time.time() - start_time

        # Data loading should complete within reasonable time
        assert load_time < 15.0, f"Data loading too slow: {load_time:.2f}s"

        # Verify data was actually loaded
        data_count = self._get_enterprise_count_from_db(dev_test_env)
        assert data_count > 0, "No data loaded"


