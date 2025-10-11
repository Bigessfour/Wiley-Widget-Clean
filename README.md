# WileyWidget

**A Modern WPF Application for Budget Management and Financial Analysis**

[![.NET Version](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml/badge.svg)](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/badge/coverage-70%25+-brightgreen.svg)](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml)

**Version:** 0.1.0 - Preview Release
**Framework:** .NET 9.0 WPF
**UI Framework:** Syncfusion WPF Controls v31.1.20

## 📋 Overview

WileyWidget is a modern Windows desktop application built with WPF and Syncfusion controls, designed for budget management and financial data analysis. The application features a comprehensive MVVM architecture with Entity Framework Core integration, using local SQL Server Express for data storage.

### Key Capabilities

- **Budget Management**: Multi-year budget tracking with detailed financial analysis
- **Data Visualization**: Interactive charts and dashboards using Syncfusion controls
- **Enterprise Integration**: QuickBooks Online API integration for financial data
- **Modern UI**: Fluent Design themes with dark/light mode switching
- **Robust Architecture**: MVVM pattern with dependency injection and comprehensive testing

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10/11** (64-bit)
- **.NET 9.0 SDK** (9.0.305 or later)
- **SQL Server Express** (local database)
- **Syncfusion Community License** (free for individual developers)

### Installation & Setup

1. **Clone the Repository**
   ```bash
   git clone https://github.com/Bigessfour/Wiley-Widget.git
   cd Wiley-Widget
   ```

2. **Setup Syncfusion License**
   ```powershell
   # Set environment variable (recommended)
   [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','YOUR_LICENSE_KEY','User')

   # Or place license.key file beside the executable
   ```

3. **Build and Run**
   ```powershell
   # Restore dependencies and build
   dotnet build WileyWidget.csproj

   # Run the application
   dotnet run --project WileyWidget.csproj
   ```

### First Launch

The application will:
- Initialize the local SQL Server Express database (WileyWidgetDev)
- Load default themes and settings
- Display the main dashboard with budget management interface

---

---

## 🏗️ Architecture

### Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| **Framework** | .NET | 9.0 |
| **UI Framework** | WPF | .NET 9.0 |
| **MVVM** | CommunityToolkit.Mvvm | 8.4.0 |
| **Database** | Entity Framework Core | 9.0.8 |
| **UI Controls** | Syncfusion WPF | 31.1.20 |
| **Dependency Injection** | Microsoft.Extensions.DI | 9.0.8 |
| **Logging** | Serilog | 4.3.0 |
| **Testing** | NUnit | Latest |
| **AI Integration** | Microsoft.Extensions.AI | Latest |
| **Reporting** | Bold Reports WPF | 5.2.26 |
| **QuickBooks** | Intuit SDK | 14.7.0 |

### Project Structure

```
WileyWidget/
├── src/                          # Main application source
│   ├── App.xaml                 # Application entry point
│   ├── App.xaml.cs              # Application startup logic
│   ├── Program.cs               # Main program entry point
│   ├── Models/                  # Data models and entities
│   │   ├── Budget/             # Budget-related models
│   │   ├── Enterprise/         # Enterprise data models
│   │   └── QuickBooks/         # QuickBooks integration models
│   ├── ViewModels/              # MVVM view models
│   │   ├── Base/               # Base view model classes
│   │   ├── Dashboard/          # Dashboard view models
│   │   └── Settings/           # Settings view models
│   ├── Views/                   # XAML UI files
│   │   ├── Dashboard/          # Dashboard views
│   │   ├── Budget/             # Budget management views
│   │   └── Settings/           # Settings views
│   ├── Services/                # Business logic and integrations
│   │   ├── Data/               # Data access services
│   │   ├── QuickBooks/         # QuickBooks API services
│   │   └── AI/                 # AI integration services
│   ├── Data/                    # EF Core DbContext and repositories
│   │   ├── Contexts/           # Database contexts
│   │   ├── Repositories/       # Repository implementations
│   │   └── Migrations/         # EF Core migrations
│   ├── Configuration/           # App configuration management
│   ├── Controls/                # Custom WPF controls
│   ├── Converters/              # Value converters
│   ├── Helpers/                 # Utility classes
│   ├── Themes/                  # UI themes and styles
│   ├── Resources/               # Application resources
│   ├── Diagnostics/             # Diagnostic and monitoring tools
│   ├── Reports/                 # Reporting components
│   ├── Startup/                 # Application startup services
│   └── NavigationRequestEventArgs.cs
├── scripts/                     # Build and deployment scripts
├── tests/                       # Unit and integration tests
│   ├── Unit/                   # Unit test projects
│   ├── Integration/            # Integration test projects
│   └── UITests/                # UI automation tests
├── docs/                        # Documentation
├── tools/                       # Development tools
├── DatabaseSetup/               # Database initialization project
├── DatabaseTest/                # Database testing utilities
└── WileyWidget.Tests/           # Main test project
```
├── tests/                 # Unit and integration tests
└── docs/                  # Documentation
```

### Database Support

- **SQL Server Express**: Local database for development and production
- **Entity Framework Core**: ORM with migrations and code-first approach

---

## ✨ Features

### Core Functionality

- **📊 Budget Management**
  - Multi-year budget tracking
  - Department-wise budget allocation
  - Budget variance analysis
  - Historical data comparison

- **🎨 Modern UI**
  - Syncfusion DataGrid with advanced features
  - Interactive charts and visualizations
  - Fluent Design themes (Dark/Light)
  - Responsive layout with docking panels

- **🔗 Enterprise Integration**
  - QuickBooks Online API integration
  - Secure OAuth2 authentication
  - Automated data synchronization
  - Real-time financial updates

- **⚙️ System Features**
  - Persistent user settings
  - Comprehensive logging
  - Error handling and diagnostics
  - Performance monitoring

### Advanced Features

- **AI Integration**: Microsoft.Extensions.AI for intelligent insights
- **Reporting**: Bold Reports for advanced reporting capabilities
- **Security**: Local configuration and secrets management
- **Performance**: Optimized startup with background initialization
- **Testing**: Comprehensive unit and UI test coverage

---

## 📋 **Project Structure**

```
WileyWidget/
├── Models/           # Enterprise data models (Phase 1)
├── Data/            # EF Core DbContext & Repositories
├── ViewModels/      # MVVM view models (Phase 2)
├── Views/          # XAML UI files (Phase 2)
├── Services/       # Business logic & AI integration (Phase 3)
├── scripts/               # Build and deployment scripts
└── docs/           # North Star & implementation guides
```

---

## 🛠️ **Development Guidelines**

### **Rule #1: No Plan Changes Without Group Consensus**
**ME, Grok-4, and Grok Fast Code-1 must ALL agree** to any plan changes. This prevents scope creep and keeps us focused.

### **Code Standards**
- **EF Core 9.0.8:** Use for all data operations with SQL Server Express
- **CommunityToolkit.Mvvm 8.4.0:** For ViewModel bindings and commands
- **Syncfusion WPF 31.1.20:** For UI components and theming
- **Serilog 4.3.0:** For structured logging with file and async sinks
- **No nullable reference types:** Per project guidelines
- **Repository Pattern:** For data access abstraction
- **Dependency Injection:** Microsoft.Extensions.DI for service registration
- **AI Integration:** Microsoft.Extensions.AI for intelligent features

### **Testing Strategy**
- **Unit Tests:** NUnit for business logic and ViewModels
- **Integration Tests:** Database operations and API integrations
- **UI Tests:** FlaUI for smoke tests and critical user flows
- **Coverage Target:** 80% by Phase 4 with CI/CD validation
- **Test Categories:** unit, smoke, integration, slow (marked appropriately)

---

## 📚 **Documentation**

- **[North Star Roadmap](docs/wiley-widget-north-star-v1.1.md)** - Complete implementation plan
- **[Contributing Guide](CONTRIBUTING.md)** - Development workflow
- **[Testing Guide](docs/TESTING.md)** - Testing standards

---

## 🎯 **Success Metrics**

By Phase 4 completion:
- ✅ Realistic rates covering operations + employees + quality services
- ✅ "Aha!" moments from dashboards for city leaders
- ✅ AI responses feel like helpful neighbors, not robots
- ✅ Clerk says "This isn't total BS"
- 🎯 **Bonus:** Version 1.0 released on GitHub

---

## 🤝 **Contributing**

See [CONTRIBUTING.md](CONTRIBUTING.md) for development workflow and standards.

**Remember:** This is a hobby-paced project (8-12 weeks to MVP). Small wins, benchmarks, and no pressure—just building something that actually helps your town!

## Documentation

### **Project Documentation**
- **[Project Plan](.vscode/project-plan.md)**: True North vision and phased roadmap
- **[Development Guide](docs/development-guide.md)**: Comprehensive development standards and best practices
- **[Copilot Instructions](.vscode/copilot-instructions.md)**: AI assistant guidelines and project standards
- **[Database Setup Guide](docs/database-setup.md)**: SQL Server Express installation and configuration
- **[Syncfusion License Setup](docs/syncfusion-license-setup.md)**: License acquisition and registration guide
- **[Contributing Guide](CONTRIBUTING.md)**: Development workflow and contribution guidelines
- **[Release Notes](RELEASE_NOTES.md)**: Version history and upcoming features
- **[Changelog](CHANGELOG.md)**: Technical change history

## Featuress://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml/badge.svg)

Single-user WPF application scaffold (NET 9) using Syncfusion WPF controls (pinned v30.2.7) with pragmatic tooling.

## Current Status (v0.1.0)

- Core app scaffold stable (build + unit tests green on CI)
- Binary MSBuild logging added (`/bl:msbuild.binlog`) – artifact: `build-diagnostics` (includes `TestResults/msbuild.binlog` & `MSBuildDebug.zip`)
- Coverage threshold: 70% (enforced in CI)
- UI smoke test harness present (optional; not yet comprehensive)
- Syncfusion license loading supports env var, file, or inline (sample left commented)
- Logging: Serilog rolling files + basic enrichers; no structured sink beyond file yet
- Nullable refs intentionally disabled for early simplicity
- Next likely enhancements: richer UI automation, live theme switching via `SfSkinManager`, packaging/signing

## Setup Scripts

### Database Setup

```powershell
# Check database status
pwsh ./scripts/setup-database.ps1 -CheckOnly

# Setup database (SQL Server Express)
pwsh ./scripts/setup-database.ps1
```

### Syncfusion License Setup

```powershell
# Check current license status
pwsh ./scripts/setup-license.ps1 -CheckOnly

# Interactive license setup
pwsh ./scripts/setup-license.ps1

# Setup with specific license key
pwsh ./scripts/setup-license.ps1 -LicenseKey "YOUR_LICENSE_KEY"

# Watch license registration (for debugging)
pwsh ./scripts/setup-license.ps1 -Watch

# Remove license setup
pwsh ./scripts/setup-license.ps1 -Remove
```

Minimal enough that future-you won’t hate past-you.

## Features

- Syncfusion DataGrid + Ribbon (add your license key)
- MVVM (CommunityToolkit.Mvvm)
- NUnit tests + coverage
- CI & Release GitHub workflows
- Central versioning (`Directory.Build.targets`)
- Global exception logging to `%AppData%/WileyWidget/logs`
- Theme persistence (FluentDark / FluentLight)
- User settings stored in `%AppData%/WileyWidget/settings.json`
- About dialog with version info
- Window size/position + state persistence
- External license key loader (license.key beside exe)
- Status bar: total item count, selected widget name & price preview
- Theme change logging (recorded via Serilog)

## Environment Configuration

WileyWidget uses secure environment variable management for sensitive configuration:

### Environment Variables

```powershell
# Load environment variables from .env file
.\scripts\load-env.ps1 -Load

# Check current status
.\scripts\load-env.ps1 -Status

# Test connections
.\scripts\load-env.ps1 -TestConnections

# Unload environment variables
.\scripts\load-env.ps1 -Unload
```

### Configuration Hierarchy

1. **Configuration System** (appsettings.json + environment variables)
2. **Environment Variables** (loaded from .env file)
3. **User Secrets** (for development secrets)
4. **Machine Environment Variables** (fallback)

### Local Database Configuration

The application uses local SQL Server Express for development and production:

- **Server**: .\SQLEXPRESS (local instance)
- **Database**: WileyWidgetDev
- **Authentication**: Windows Authentication (Integrated Security)

### Security Notes

- **Database backups** are recommended for production use
- **Use strong passwords** if switching to SQL Authentication
- **Regular maintenance** of SQL Server Express instance
  Pinned packages (NuGet):

```pwsh
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.Licensing --version 30.2.7
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.SfGrid.WPF --version 30.2.7
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.SfSkinManager.WPF --version 30.2.7
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.Tools.WPF --version 30.2.7
```

License placement (choose one):

1. Environment variable (recommended): set `SYNCFUSION_LICENSE_KEY` (User scope) then restart shell/app
   ```pwsh
   [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','<your-key>','User')
   ```
2. Provide a `license.key` file beside the executable (auto‑loaded)
3. Hard‑code in `App.xaml.cs` register call (NOT recommended for OSS / commits)

**WARNING:** Never commit `license.key` or a hard‑coded key – both are ignored/avoidance reinforced via `.gitignore`.

## License Verification

Quick ways to confirm your Syncfusion license is actually registering:

1. Environment variable present:
   ```pwsh
   [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','User')
   ```
   Should output a 90+ char key (don’t echo in screen recordings).
2. Script watch (streams detection + registration path):
   ```pwsh
   pwsh ./scripts/show-syncfusion-license.ps1 -Watch
   ```
   Look for: `Syncfusion license registered from environment variable.`
3. Log inspection:
   ```pwsh
   explorer %AppData%/WileyWidget/logs
   ```
   Open today’s `app-*.log` and verify registration line.
4. File fallback: drop a `license.key` beside the built `WileyWidget.exe` (use `license.sample.key` as format reference).

If none of the above register, ensure the key hasn’t expired and you’re on a supported version (v30.2.4 here).

## Raw File References (machine-consumable)

| Purpose             | Raw URL (replace OWNER/REPO if forked)                                                                 |
| ------------------- | ------------------------------------------------------------------------------------------------------ |
| Settings Service    | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/Services/SettingsService.cs |
| Main Window         | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/MainWindow.xaml             |
| Build Script        | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/scripts/build.ps1                       |
| App Entry           | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/App.xaml.cs                 |
| About Dialog        | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/AboutWindow.xaml            |
| License Loader Note | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/App.xaml.cs                 |

## Raw URLs (Machine Readability)

Direct raw links to key project artifacts for automation / ingestion:

- Project file: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/WileyWidget.csproj
- Solution file: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget.sln
- CI workflow: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/.github/workflows/ci.yml
- Release workflow: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/.github/workflows/release.yml
- Settings service: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/Services/SettingsService.cs
- License sample: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/LicenseKey.Private.sample.cs
- Build script: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/scripts/build.ps1

Replace REPO_OWNER/REPO_NAME with the actual GitHub org/repo when published.

## License Key (Inline Option)

If you prefer inline (e.g., private fork) uncomment and set in `App.xaml.cs`:

```csharp
SyncfusionLicenseProvider.RegisterLicense("YOUR_KEY");
```

Official docs: https://help.syncfusion.com/common/essential-studio/licensing/how-to-register-in-an-application

## Build & Run (Direct)

```pwsh
dotnet build WileyWidget.sln
dotnet run --project WileyWidget/WileyWidget.csproj
```

## Preferred One-Step Build Script

```pwsh
pwsh ./scripts/build.ps1                               # restore + build + unit tests + coverage
RUN_UI_TESTS=1 pwsh ./scripts/build.ps1                # include UI smoke tests
TEST_FILTER='Category=UiSmokeTests' pwsh ./scripts/build.ps1 -Config Debug  # ad-hoc filtered run
pwsh ./scripts/build.ps1 -Publish                      # publish single-file (framework-dependent)
pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64  # self-contained executable
```

Tip: For always self-contained releases use: `pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64`.

## Versioning

Edit `Directory.Build.targets` (Version / FileVersion) or use release workflow (updates automatically).

## Logging

Structured logging via Serilog writes rolling daily files at:
`%AppData%/WileyWidget/logs/app-YYYYMMDD.log`

Included enrichers: ProcessId, ThreadId, MachineName.

Quick access: `explorer %AppData%\WileyWidget\logs`

- File Header (optional for tiny POCOs) kept minimal – class XML summary suffices.
- Public classes, methods, and properties: XML doc comments (///) summarizing intent.
- Private helpers: brief inline // comment only when intent isn't obvious from name.
- Regions avoided; prefer small, cohesive methods.
- No redundant comments (e.g., // sets X) – focus on rationale, edge cases, side-effects.
- When behavior might surprise (fallbacks, error swallowing), call it out explicitly.

Example pattern:

```csharp
/// <summary>Loads persisted user settings or creates defaults on first run.</summary>
public void Load()
{
	// Corruption handling: rename bad file and recreate defaults.
}
```

## Settings & Theme Persistence

User settings JSON auto-created at `%AppData%/WileyWidget/settings.json`.
Theme buttons update the stored theme immediately; applied on next launch (applied via planned `SfSkinManager` integration).

Environment override (tests / portable mode):

```pwsh
$env:WILEYWIDGET_SETTINGS_DIR = "$PWD/.wiley_settings"
```

If set, the service writes `settings.json` under that directory instead of AppData.

Active theme button is visually indicated (✔) and disabled to prevent redundant clicks.

Example `settings.json`:

```json
{
  "Theme": "FluentDark",
  "Window": { "Width": 1280, "Height": 720, "X": 100, "Y": 100, "State": "Normal" },
  "LastWidgetSelected": "WidgetX"
}
```

## About Dialog

Ribbon: Home > Help > About shows version (AssemblyInformationalVersion).

## Release Flow

1. Decide new version (e.g. 0.1.1)
2. Run GitHub Action: Release (provide version)
3. Download artifact from GitHub Releases page
4. Distribute (self-contained zip includes dependencies)

Artifacts follow naming pattern: `WileyWidget-vX.Y.Z-win-x64.zip`

Releases: https://github.com/Bigessfour/Wiley-Widget/releases

## Project Structure

```
WileyWidget/            # App
WileyWidget.Tests/      # Unit tests
WileyWidget.UiTests/    # Placeholder UI harness
scripts/                # build.ps1
.github/workflows/      # ci.yml, release.yml
CHANGELOG.md / RELEASE_NOTES.md
```

## Tests

```pwsh
dotnet test WileyWidget.sln --collect:"XPlat Code Coverage"
```

Coverage report HTML produced in CI (artifact). Locally you can install ReportGenerator:

```pwsh
dotnet tool update --global dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
```

One-liner (local quick view):

```pwsh
dotnet test --collect:"XPlat Code Coverage" ; reportgenerator -reports:**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html ; start CoverageReport/index.html
```

### Coverage Threshold (CI)

CI enforces a minimum line coverage (default 70%). Adjust `COVERAGE_MIN` env var in `.github/workflows/ci.yml` as the test suite grows.

## Next (Optional)

- Integrate `SfSkinManager` for live theme switch (doc-backed pattern)
- UI automation (FlaUI) for DataGrid + Ribbon smoke
  - Basic smoke test already included: launches app, asserts main window title & UI children
- Dynamic DataGrid column generation snippet (future):
  ```csharp
  dataGrid.Columns.Clear();
  foreach (var prop in source.First().GetType().GetProperties())
  		dataGrid.Columns.Add(new GridTextColumn { MappingName = prop.Name });
  ```
- Signing + updater
- Pre-push hook (see scripts/pre-push) to gate pushes

Nullable reference types disabled intentionally for simpler interop & to reduce annotation noise at this early stage (may revisit later).

## UI Tests

Basic FlaUI-based smoke test ensures the WPF app launches, main window title contains "Wiley" and a DataGrid control is present. Category: `UiSmokeTests`.

Run only smoke UI tests:

```pwsh
dotnet test WileyWidget.sln --filter Category=UiSmokeTests
```

Or via build script (set optional filter):

```pwsh
$env:TEST_FILTER='Category=UiSmokeTests'
pwsh ./scripts/build.ps1 -Config Debug
```

Enable unit + UI phases (separate runs) without manual filter:

```pwsh
RUN_UI_TESTS=1 pwsh ./scripts/build.ps1
```

Notes:

- UI smoke tests optional; set RUN_UI_TESTS to include them, or filter manually.
- Script performs pre-test process cleanup (kills lingering WileyWidget/testhost) and retries up to 3 times.
- Automation sets `WILEYWIDGET_AUTOCLOSE_LICENSE=1` to auto-dismiss Syncfusion trial dialog when present.

## Troubleshooting

Diagnostics & common gotchas:

- File locking (MSB3026/MSB3027/MSB3021): build script auto-cleans (kills WileyWidget/testhost/vstest.console); verify no stray processes.
- MSB4166 (child node exited): binary log captured at `TestResults/msbuild.binlog`. Open with MSBuild Structured Log Viewer. Raw debug files (if any) archived under `TestResults/MSBuildDebug` + zip. A marker file keeps the folder non-empty for CI retention.
- Capture fresh logs manually:
  ```pwsh
  $env:MSBUILDDEBUGPATH = "$env:TEMP/MSBuildDebug"
  pwsh ./scripts/build.ps1
  # Inspect TestResults/msbuild.binlog or $env:TEMP/MSBuildDebug/ for MSBuild_*.failure.txt
  ```
- No UI tests discovered: use `--filter Category=UiSmokeTests` or set `RUN_UI_TESTS=1`.
- Syncfusion license not detected: run `pwsh ./scripts/show-syncfusion-license.ps1 -Watch`; ensure env var or `license.key` file present.
- Syncfusion trial dialog blocks exit: set `WILEYWIDGET_AUTOCLOSE_LICENSE=1` during automation.
- Coverage report missing: confirm `coverage.cobertura.xml` under `TestResults/`; install ReportGenerator for HTML view.
- Coverage threshold fail: adjust `COVERAGE_MIN` env var or pass `-SkipCoverageCheck` for exploratory build.

## Contributing & Workflow (Single-Dev Friendly)

Even as a solo developer, a light process keeps history clean and releases reproducible.

Branching (Simple)

- main: always buildable; reflects latest completed work.
- feature/short-description: optional for riskier changes; squash merge or fast-forward.

Commit Messages

- Imperative present tense: Add window state persistence
- Group logically (avoid giant mixed commits). Small cohesive commits aid bisecting.

Release Tags

1. Run tests locally
2. Update version via Release workflow (or adjust `Directory.Build.targets` manually for pre-release experiments)
3. Verify artifact zip on the GitHub Release
4. Tag follows semantic versioning (e.g., v0.1.1)

Hotfix Flow

1. branch: hotfix/issue
2. fix + test
3. bump patch version via Release workflow
4. merge/tag

Code Style & Comments

- Enforced informally via `.editorconfig` (spaces, 4 indent, trim trailing whitespace)
- XML docs for public surface, rationale comments for non-obvious private logic
- No redundant narrations (avoid // increment i)

Checklist Before Push

- Build: success
- Tests: all green
- README: updated if feature/user-facing change
- No secrets (ensure `license.key` not committed)
- Logs, publish artifacts, coverage directories excluded

Future (Optional Enhancements)

- Add pre-push git hook to run build+tests
- Add code coverage threshold gate in CI
- Introduce analyzer set (.editorconfig rules) when complexity grows

## QuickBooks Online (Experimental Integration)

### 1. Environment Variables (set once, User scope)

```pwsh
[Environment]::SetEnvironmentVariable('QBO_CLIENT_ID','<client-id>','User')
[Environment]::SetEnvironmentVariable('QBO_CLIENT_SECRET','<client-secret>','User')   # optional for public / PKCE-only flow
[Environment]::SetEnvironmentVariable('QBO_REDIRECT_URI','http://localhost:8080/callback/','User')
[Environment]::SetEnvironmentVariable('QBO_REALM_ID','<realm-id>','User')             # company (realm) id
```

Close and reopen your shell (process must inherit the variables). Redirect URI must EXACTLY match what is configured in the Intuit developer portal.

### 2. Manual Test Flow

1. `dotnet run --project WileyWidget/WileyWidget.csproj`
2. Open the "QuickBooks" tab.
3. Click "Load Customers" (or "Load Invoices").
4. If no tokens stored, the interactive OAuth flow (external browser) should occur; complete consent.
5. After redirect completes and tokens are stored, click the buttons again – data should populate without another consent.

Expected Columns:

- Customers: Name, Email, Phone (auto-generated columns from Intuit `Customer` model)
- Invoices: Number (DocNumber), Total (TotalAmt), Customer (CustomerRef.name), Due Date (DueDate)

### 3. Token Persistence

Tokens are saved in `%AppData%/WileyWidget/settings.json` under:

```jsonc
// excerpt
{
  "QboAccessToken": "...",
  "QboRefreshToken": "...",
  "QboTokenExpiry": "2025-08-12T12:34:56.789Z",
}
```

Delete this file to force a fresh OAuth flow:

```pwsh
Remove-Item "$env:AppData\WileyWidget\settings.json" -ErrorAction SilentlyContinue
```

Token considered valid if:

- `QboAccessToken` not blank AND
- `QboTokenExpiry` > `UtcNow + 60s` (early refresh safety window)

### 4. Troubleshooting

| Symptom                            | Likely Cause                                       | Fix                                                                   |
| ---------------------------------- | -------------------------------------------------- | --------------------------------------------------------------------- |
| "QBO_CLIENT_ID not set" exception  | Env var missing in new shell                       | Re-set variable (User scope) and reopen shell                         |
| No customers/invoices and no error | Empty sandbox company                              | Add sample data in Intuit dashboard                                   |
| Repeated auth prompt               | Tokens not written (settings file locked or crash) | Check logs, ensure `%AppData%/WileyWidget/settings.json` updates      |
| Refresh every click                | `QboTokenExpiry` stayed default                    | Confirm refresh succeeded; inspect log for "QBO token refreshed" line |
| Invoices empty but customers load  | Filter (future) / realm mismatch                   | Verify `QBO_REALM_ID` matches company ID                              |
| Unhandled invalid refresh token    | Token revoked / expired                            | Delete settings file and re-authorize                                 |

### 5. Logs

Open the latest log file to diagnose:

```pwsh
explorer %AppData%\WileyWidget\logs
```

Look for lines:

- `QBO token refreshed (exp ...)`
- `QBO customers fetch failed` / `QBO invoices fetch failed`
- `Syncfusion license registered ...`

### 6. Reset / Clean

```pwsh
taskkill /IM WileyWidget.exe /F 2>$null
taskkill /IM testhost.exe /F 2>$null
dotnet build WileyWidget.sln
```

### 7. Removing Tokens

Just delete the settings file or manually blank the Qbo\* entries; a new auth will occur on next fetch.

> NOTE: Tokens are stored in plain text for early development convenience. Do NOT ship production builds without encrypting or using a secure credential store.

## Version Control Quick Start (Solo Flow)

Daily minimal:

```pwsh
git status
git add .
git commit -m "feat: describe change"
git pull --rebase
git push
```

Feature (risky) change:

```pwsh
git checkout -b feature/thing
# edits
git commit -m "feat: add thing"
git checkout main
git pull --rebase
git merge --ff-only feature/thing
git branch -d feature/thing
git push
```

Undo helpers:

- Discard unstaged file: `git checkout -- path`
- Amend last commit message: `git commit --amend`
- Revert a pushed commit: `git revert <hash>`

Tag release:

```pwsh
git tag -a v0.1.0 -m "v0.1.0"
git push --tags
```

## Extra Git Aliases

Moved to `CONTRIBUTING.md` to keep this lean.

## Syncfusion License Verification

To verify your Syncfusion Community License (v30.2.7) is correctly set up:

1. **Check Environment Variable**:

   ```pwsh
   [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User')
   ```

   Should return a ~96+ character key (do NOT paste it in issues). If null/empty, it's not registered.

2. **Run Inspection Script (live watch)**:

   ```pwsh
   pwsh ./scripts/show-syncfusion-license.ps1 -Watch
   ```

   Confirms detection source (env var vs file) and registration status in real-time.

3. **Verify Logs** (app has run at least once):

   ```pwsh
   explorer %AppData%\WileyWidget\logs
   ```

   Open today's `app-YYYYMMDD.log` and look for:

   ```
   Syncfusion license registered from environment variable.
   ```

   (or `...from file` if using `license.key`).

4. **Fallback (File Placement)**:
   Place a `license.key` file beside `WileyWidget.exe` (same folder) containing ONLY the key text. See `license.sample.key` for format. Re-run the app or the watch script.

5. **Troubleshooting**:
   - Ensure the env var scope is `User` (not `Process` only) if launching from a new shell.
   - Confirm no hidden BOM / whitespace in `license.key`.
   - Multiple keys: only the first non-empty source is used (env var takes precedence over file).
   - Upgrading Syncfusion: keep version compatibility (here pinned to `30.2.7`).

If issues persist, re-set the env var and restart your terminal:

```pwsh
[System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','<your-key>','User')
```
# Test commit for manifest generation
