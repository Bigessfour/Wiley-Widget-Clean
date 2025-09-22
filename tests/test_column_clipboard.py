#!/usr/bin/env python3
"""
Unit tests for column building and clipboard operations
Tests mock implementations of C# grid functionality
Run with: pytest tests/test_column_clipboard.py
"""

import pytest
from unittest.mock import patch, MagicMock, mock_open
import json
import os


class MockColumnBuilder:
    """Mock implementation of column building logic"""

    def __init__(self):
        self.columns = []

    def add_column(self, name, display_name, data_type, width=100):
        """Add a column to the grid"""
        column = {
            'name': name,
            'display_name': display_name,
            'data_type': data_type,
            'width': width,
            'visible': True
        }
        self.columns.append(column)
        return column

    def get_columns(self):
        """Get all columns"""
        return self.columns

    def hide_column(self, name):
        """Hide a column by name"""
        for col in self.columns:
            if col['name'] == name:
                col['visible'] = False
                return True
        return False


class MockClipboardOperations:
    """Mock implementation of clipboard operations"""

    def __init__(self):
        self.clipboard_content = None

    def copy_to_clipboard(self, data):
        """Copy data to clipboard"""
        if isinstance(data, list):
            # Convert list of dicts to CSV-like format
            if data:
                headers = list(data[0].keys())
                rows = [headers]
                for item in data:
                    row = [str(item.get(h, '')) for h in headers]
                    rows.append(row)
                self.clipboard_content = '\n'.join(['\t'.join(row) for row in rows])
            else:
                self.clipboard_content = ''
        else:
            self.clipboard_content = str(data)
        return True

    def get_clipboard_content(self):
        """Get current clipboard content"""
        return self.clipboard_content

    def paste_from_clipboard(self):
        """Paste data from clipboard"""
        if not self.clipboard_content:
            return []

        lines = self.clipboard_content.split('\n')
        if len(lines) < 2:
            return []

        headers = lines[0].split('\t')
        data = []
        for line in lines[1:]:
            if line.strip():
                values = line.split('\t')
                if len(values) == len(headers):
                    item = dict(zip(headers, values))
                    data.append(item)
        return data


class TestColumnBuilding:
    """Test column building functionality"""

    def setup_method(self):
        """Setup for each test"""
        self.builder = MockColumnBuilder()

    @pytest.mark.unit
    def test_add_basic_column(self):
        """Test adding a basic column"""
        col = self.builder.add_column('name', 'Name', 'string', 150)

        assert col['name'] == 'name'
        assert col['display_name'] == 'Name'
        assert col['data_type'] == 'string'
        assert col['width'] == 150
        assert col['visible'] is True

    @pytest.mark.unit
    def test_add_multiple_columns(self):
        """Test adding multiple columns"""
        self.builder.add_column('id', 'ID', 'int')
        self.builder.add_column('name', 'Name', 'string')
        self.builder.add_column('amount', 'Amount', 'decimal')

        columns = self.builder.get_columns()
        assert len(columns) == 3
        assert columns[0]['name'] == 'id'
        assert columns[1]['name'] == 'name'
        assert columns[2]['name'] == 'amount'

    @pytest.mark.unit
    def test_hide_column(self):
        """Test hiding a column"""
        self.builder.add_column('name', 'Name', 'string')
        self.builder.add_column('hidden', 'Hidden', 'string')

        result = self.builder.hide_column('hidden')
        assert result is True

        columns = self.builder.get_columns()
        assert columns[0]['visible'] is True
        assert columns[1]['visible'] is False

    @pytest.mark.unit
    def test_hide_nonexistent_column(self):
        """Test hiding a column that doesn't exist"""
        self.builder.add_column('name', 'Name', 'string')

        result = self.builder.hide_column('nonexistent')
        assert result is False

    @pytest.mark.unit
    def test_default_column_width(self):
        """Test default column width"""
        col = self.builder.add_column('test', 'Test', 'string')
        assert col['width'] == 100


class TestClipboardOperations:
    """Test clipboard operations functionality"""

    def setup_method(self):
        """Setup for each test"""
        self.clipboard = MockClipboardOperations()

    @pytest.mark.unit
    def test_copy_simple_text(self):
        """Test copying simple text to clipboard"""
        result = self.clipboard.copy_to_clipboard("Hello World")
        assert result is True
        assert self.clipboard.get_clipboard_content() == "Hello World"

    @pytest.mark.unit
    def test_copy_data_list(self):
        """Test copying a list of dictionaries to clipboard"""
        data = [
            {'name': 'John', 'age': '30', 'city': 'NYC'},
            {'name': 'Jane', 'age': '25', 'city': 'LA'}
        ]

        result = self.clipboard.copy_to_clipboard(data)
        assert result is True

        content = self.clipboard.get_clipboard_content()
        lines = content.split('\n') # type: ignore
        assert len(lines) == 3  # header + 2 data rows
        assert 'name\tage\tcity' in lines[0]

    @pytest.mark.unit
    def test_paste_from_clipboard(self):
        """Test pasting data from clipboard"""
        # First copy some data
        data = [
            {'name': 'Alice', 'score': '95'},
            {'name': 'Bob', 'score': '87'}
        ]
        self.clipboard.copy_to_clipboard(data)

        # Then paste it back
        pasted_data = self.clipboard.paste_from_clipboard()

        assert len(pasted_data) == 2
        assert pasted_data[0]['name'] == 'Alice'
        assert pasted_data[0]['score'] == '95'
        assert pasted_data[1]['name'] == 'Bob'
        assert pasted_data[1]['score'] == '87'

    @pytest.mark.unit
    def test_paste_empty_clipboard(self):
        """Test pasting from empty clipboard"""
        pasted_data = self.clipboard.paste_from_clipboard()
        assert pasted_data == []

    @pytest.mark.unit
    def test_copy_empty_list(self):
        """Test copying an empty list"""
        result = self.clipboard.copy_to_clipboard([])
        assert result is True
        assert self.clipboard.get_clipboard_content() == ''

    @pytest.mark.unit
    def test_paste_malformed_data(self):
        """Test pasting malformed clipboard data"""
        # Set malformed content directly
        self.clipboard.clipboard_content = "name\tage\nJohn"  # Missing second column

        pasted_data = self.clipboard.paste_from_clipboard()
        # Should handle gracefully - either empty or partial data
        assert isinstance(pasted_data, list)


class TestIntegration:
    """Test integration between column building and clipboard operations"""

    @pytest.mark.unit
    def test_column_data_to_clipboard(self):
        """Test copying column data to clipboard"""
        builder = MockColumnBuilder()
        builder.add_column('id', 'ID', 'int')
        builder.add_column('name', 'Name', 'string')
        builder.add_column('active', 'Active', 'bool')

        columns = builder.get_columns()

        clipboard = MockClipboardOperations()
        clipboard.copy_to_clipboard(columns)

        # Verify the data was copied correctly
        content = clipboard.get_clipboard_content()
        assert 'name\tdisplay_name\tdata_type\twidth\tvisible' in content # type: ignore

        # Paste it back
        pasted = clipboard.paste_from_clipboard()
        assert len(pasted) == 3
        assert pasted[0]['name'] == 'id'
        assert pasted[1]['name'] == 'name'
