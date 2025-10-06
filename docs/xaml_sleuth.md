# XAML Sleuth ("WPF Whisperer")

`xaml_sleuth.py` is a lightweight debugging helper inspired by the techniques in *WPF Debugging and Performance*. It offers two workflows that complement Visual Studio tooling without requiring Snoop:

- **Static mode** walks a XAML file to flag suspicious bindings, missing namespaces, and schema smells before you run the app.
- **Runtime mode** attaches to a live WPF window through Windows UI Automation and highlights controls that look like binding failures.

## Installation

Create or activate the Python 3.11 environment used for tooling in this repository, then install the extra dependencies:

```pwsh
pip install -r tools/python/requirements-xaml_sleuth.txt
```

> The script depends on `lxml` for resilient XAML parsing and `uiautomation` for UI Automation access.

## Usage

The script lives at `tools/python/xaml_sleuth.py`. Run `python tools/python/xaml_sleuth.py --help` to view all options.

### Static analysis mode

```pwsh
python tools/python/xaml_sleuth.py path/to/MainWindow.xaml --mock-data path/to/mock-data.json
```

- Parses the XAML document, traverses the element tree, and reports potential binding gremlins.
- Provide `--mock-data` with a JSON object to describe the properties you expect in the data context. Missing keys or `null` values trigger targeted warnings.
- The repo now ships with `tools/python/mock-data/wiley-widget-default.json`, a realistic dataset generated from the latest `.sleuth` reports. The PowerShell helper automatically uses it when no mock file is supplied.

### Runtime inspection mode

```pwsh
python tools/python/xaml_sleuth.py --runtime --window-title "Wiley Widget" path/to/WileyWidget.exe
```

- Connects to the first window whose title matches the provided value (falls back to the executable name when omitted).
- Recursively explores the UI Automation tree up to `--max-depth` (default `5`). Empty text controls or unnamed elements are flagged.
- Use `--report gremlins.txt` to persist the findings.

## Customising the experience

- **Mock data tweaks**:
  - Use the bundled `tools/python/mock-data/wiley-widget-default.json` for a comprehensive baseline that satisfies the current set of bindings.
  - Regenerate it after large UI changes with `python tools/python/mock-data/generate_mock_data.py`.
  - To craft custom scenarios, drop a JSON file alongside your view model definitions, e.g.
  ```json
  {
    "ProcessName": "Explorer.exe",
    "StatusMessage": "Ready",
    "PendingAlerts": 3
  }
  ```
  Pass the file via `--mock-data` for richer validation.
- **Verbose mode**: `--verbose` prints every binding that successfully resolves in static mode and each control visited at runtime.
- **Depth control**: Bump `--max-depth` when working with deeply nested templates, or reduce it to keep output snappy.

## Troubleshooting

- Runtime inspection requires administrator privileges when the target app is elevated.
- `uiautomation` must run on Windows. Ensure the target application window is visible; hidden popups may not surface through UIA.
- If you need to capture output for later analysis, combine `--report` with `--verbose` for both a file and console log.

## Next steps

Suggested enhancements include exporting findings as JSON for CI pipelines, augmenting mock-data lookup with reflection via pythonnet, and wiring smoke tests that exercise the static scanner against representative XAML samples.
