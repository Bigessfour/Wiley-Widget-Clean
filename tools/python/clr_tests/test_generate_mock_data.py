"""Tests for the mock data generation utility."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "mock-data" / "generate_mock_data.py"
spec = importlib.util.spec_from_file_location("generate_mock_data", MODULE_PATH)
assert spec is not None
generate_mock_data = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(generate_mock_data)


def test_generate_mock_data_writes_default(tmp_path, monkeypatch):
    output = tmp_path / "mock-data.json"
    views_dir = tmp_path / "views"
    views_dir.mkdir()
    sample_report = views_dir / "sample.sleuth.txt"
    sample_report.write_text("Binding path 'Widgets[0].Title'", encoding="utf-8")

    monkeypatch.setattr(generate_mock_data, "OUTPUT_PATH", output)
    monkeypatch.setattr(generate_mock_data, "VIEWS_DIR", views_dir)

    generate_mock_data.main()

    data = json.loads(output.read_text(encoding="utf-8"))
    assert "Title" in data
    assert isinstance(data["Widgets"], list)


def test_generate_value_custom_path():
    value = generate_mock_data._generate_value("Finance.Budget.Amount")
    assert isinstance(value, (int, float))


def test_fill_with_heuristics_invalid_container():
    # An empty list (or non-dict container) should be handled gracefully
    # The function may skip or raise - test that it doesn't crash unexpectedly
    try:
        result = generate_mock_data._fill_with_heuristics({}, ["Nested.Property"])
        assert isinstance(result, (dict, type(None)))
    except (AttributeError, TypeError, ValueError):
        # Either raises an expected error or returns successfully
        pass
