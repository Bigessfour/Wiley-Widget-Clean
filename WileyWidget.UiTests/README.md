# WileyWidget UI Tests

## Running locally

- Ensure the main app builds first (Debug/net9.0-windows).
- Run tests from VS Test Explorer or via the provided VS Code task: test-ui.
- Optional env vars:
  - WILEYWIDGET_AUTOCLOSE_LICENSE=1 (auto-dismiss Syncfusion dialogs during tests)
  - XAI_API_KEY (if tests exercise AI service registration)

## Traits for filtering

- UI-HighRisk: end-to-end FlaUI flows
- UI-Bindings: binding/visibility tests
- UI-Themes: theme coverage

Filter examples:

- Include only high-risk UI tests: xUnit filter Trait=UI-HighRisk
- Exclude FlaUI flows: Trait!=UI-HighRisk

## CI guidance

- Use Windows runners only for WPF UI tests.
- Disable test parallelization (already configured via AssemblyInfo.cs).
- Ensure desktop interaction is available (Windows VM/agent with session).
- Build app before running FlaUI tests so WileyWidget.exe exists in bin/Debug/net9.0-windows.
