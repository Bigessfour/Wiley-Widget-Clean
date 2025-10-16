# File Organization Plan for WileyWidget Root Directory

## 📋 Analysis Date: October 15, 2025

### Current State Assessment

The root directory contains **81+ files/folders**, many of which should be organized into proper subdirectories.

---

## ✅ Files That BELONG in Root (Keep As-Is)

### Build & Project Configuration
- ✅ `WileyWidget.sln` - Solution file
- ✅ `WileyWidget.csproj` - Main project file
- ✅ `Directory.Build.props` - MSBuild properties
- ✅ `Directory.Build.targets` - MSBuild targets
- ✅ `Directory.Packages.props` - Central package management
- ✅ `global.json` - .NET SDK version
- ✅ `App.config` - Application configuration
- ✅ `appsettings.json` - Runtime configuration
- ✅ `appsettings.Development.json` - Development config
- ✅ `.editorconfig` - Editor settings
- ✅ `.gitignore` - Git exclusions

### Python Configuration (Root Level OK)
- ✅ `pyproject.toml` - Python project config
- ✅ `pyrightconfig.json` - Python type checking
- ✅ `pytest.ini` - Python test config
- ✅ `requirements-test.txt` - Python test dependencies
- ✅ `package.json` - Node/npm config
- ✅ `package-lock.json` - npm lock file

### Documentation (Root Level OK)
- ✅ `README.md` - Project overview
- ✅ `CHANGELOG.md` - Version history
- ✅ `CONTRIBUTING.md` - Contribution guidelines
- ✅ `SECURITY.md` - Security policy
- ✅ `RELEASE_NOTES.md` - Release documentation

### CI/CD & Security Configuration
- ✅ `.github/` - GitHub Actions workflows
- ✅ `.trunk/` - Trunk CLI configuration
- ✅ `.vscode/` - VS Code settings
- ✅ `.checkov.yaml` - Security scanning config
- ✅ `.gitleaks.toml` - Secret scanning config

---

## 📁 Files to MOVE/REORGANIZE

### 1. **Documentation Files → `docs/`**

#### Move to `docs/guides/`
```powershell
Move-Item "quickbooks-registration-guide.md" "docs/guides/"
Move-Item "QUICKBOOKS-SETUP.md" "docs/guides/"
Move-Item "AI_Integration_Plan.md" "docs/architecture/"
Move-Item "AI_INTEGRATION_DI_STATUS.md" "docs/architecture/"
Move-Item "LOGGING_ENHANCEMENTS.md" "docs/architecture/"
Move-Item "COMMAND_REVIEW_REPORT.md" "docs/reports/"
```

#### Move to `docs/analysis/`
```powershell
Move-Item "fetchability-resources.json" "docs/analysis/"
Move-Item "repomix-output.md" "docs/analysis/"
Move-Item "repomix-output.xml" "docs/analysis/"
Move-Item "wiley-widget-llm.txt" "docs/analysis/"
```

### 2. **Build Artifacts → DELETE or `build/logs/`**

#### DELETE (Temporary Build Files)
```powershell
Remove-Item "build-detailed.log" -ErrorAction SilentlyContinue
Remove-Item "build-diag.txt" -ErrorAction SilentlyContinue
Remove-Item "build-errors.log" -ErrorAction SilentlyContinue
Remove-Item "debug-hosted.log" -ErrorAction SilentlyContinue
Remove-Item "xaml-trace.log" -ErrorAction SilentlyContinue
Remove-Item "psscriptanalyzer-results.txt" -ErrorAction SilentlyContinue
```

### 3. **Scripts → `scripts/quickbooks/`**
```powershell
New-Item -ItemType Directory -Path "scripts/quickbooks" -Force
Move-Item "setup-quickbooks-sandbox.ps1" "scripts/quickbooks/"
Move-Item "setup-town-of-wiley.ps1" "scripts/quickbooks/"
Move-Item "test-qbo-keyvault-integration.ps1" "scripts/quickbooks/"
Move-Item "test-quickbooks-connection.ps1" "scripts/quickbooks/"
Move-Item "run-dashboard-tests.ps1" "scripts/testing/"
```

### 4. **Docker Files → `docker/`**
```powershell
New-Item -ItemType Directory -Path "docker" -Force
Move-Item "docker-compose.regionviewregistry-tests.yml" "docker/"
Move-Item "docker-compose.test.yml" "docker/"
Move-Item "Dockerfile.regionviewregistry-tests" "docker/"
Move-Item "Dockerfile.test" "docker/"
Move-Item "Dockerfile.test-regionviewregistry" "docker/"
```

### 5. **Test Files → `tests/` or DELETE**

#### Move to `tests/integration/`
```powershell
Move-Item "QuickBooksStructureTest.cs" "tests/integration/"
```

### 6. **Environment Files → `config/`**
```powershell
# .env.example should stay in root for visibility
# But .env.production.sample can move
Move-Item ".env.production.sample" "config/"
```

### 7. **Results/Output Files → DELETE**
```powershell
Remove-Item "startup-performance-results.json" -ErrorAction SilentlyContinue
Remove-Item ".packages.lastmodified" -ErrorAction SilentlyContinue
Remove-Item ".coverage" -ErrorAction SilentlyContinue
```

### 8. **Database Files → Move to `data/`**
```powershell
New-Item -ItemType Directory -Path "data" -Force
Move-Item "WileyWidgetDev.db" "data/" -ErrorAction SilentlyContinue
Move-Item "DatabaseSetup" "data/" -ErrorAction SilentlyContinue
Move-Item "DatabaseTest" "data/tests/" -ErrorAction SilentlyContinue
```

### 9. **Hidden Copilot Instructions → Keep in Root**
```powershell
# .copilot-instructions.md stays in root (VS Code Copilot requirement)
```

---

## 🗑️ Directories to CLEAN UP

### Temporary/Build Directories (Safe to Delete)
```powershell
Remove-Item "bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".buildcache" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "TestResults" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "node_modules" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".tmp.drivedownload" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".tmp.driveupload" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".pytest_cache" -Recurse -Force -ErrorAction SilentlyContinue
```

### Development Database (Move to `data/`)
```powershell
Move-Item "WileyWidgetDev.db" "data/" -ErrorAction SilentlyContinue
```

---

## 📂 Recommended Directory Structure

```
WileyWidget/
├── .github/                    # GitHub Actions (existing)
├── .trunk/                     # Trunk CLI config (existing)
├── .vscode/                    # VS Code settings (existing)
├── config/                     # Configuration files
│   ├── .env.production.sample
│   └── deployment/
├── data/                       # Database files (NEW)
│   ├── WileyWidgetDev.db
│   ├── DatabaseSetup/
│   └── tests/
├── docker/                     # Docker files (NEW)
│   ├── docker-compose.*.yml
│   └── Dockerfile.*
├── docs/                       # Documentation
│   ├── architecture/           # Design docs (NEW)
│   │   ├── AI_Integration_Plan.md
│   │   ├── AI_INTEGRATION_DI_STATUS.md
│   │   └── LOGGING_ENHANCEMENTS.md
│   ├── guides/                 # How-to guides (NEW)
│   │   ├── quickbooks-registration-guide.md
│   │   └── QUICKBOOKS-SETUP.md
│   ├── reports/                # Analysis reports (NEW)
│   │   └── COMMAND_REVIEW_REPORT.md
│   └── analysis/               # Code analysis (NEW)
│       ├── fetchability-resources.json
│       ├── repomix-output.md
│       └── wiley-widget-llm.txt
├── licenses/                   # License files (existing)
├── logs/                       # Runtime logs (existing)
├── scripts/                    # Automation scripts
│   ├── quickbooks/             # QuickBooks scripts (NEW)
│   │   ├── setup-quickbooks-sandbox.ps1
│   │   ├── setup-town-of-wiley.ps1
│   │   └── test-*.ps1
│   └── testing/                # Test scripts (NEW)
│       └── run-dashboard-tests.ps1
├── signing/                    # Code signing certs (existing)
├── src/                        # Source code (existing)
├── tests/                      # Test projects (existing)
│   └── integration/            # Integration tests
│       └── QuickBooksStructureTest.cs
├── tools/                      # Development tools (existing)
├── WileyWidget.Business/       # Business logic project (existing)
├── WileyWidget.Data/           # Data access project (existing)
├── WileyWidget.Models/         # Models project (existing)
├── WileyWidget.Tests/          # Unit tests (existing)
├── WileyWidget.UiTests/        # UI tests (existing)
├── wwwroot/                    # Static web files (existing)
│
├── .editorconfig               # Editor config
├── .gitignore                  # Git ignore
├── .env.example                # Environment template
├── App.config                  # App configuration
├── appsettings.json            # Runtime settings
├── CHANGELOG.md                # Change log
├── CONTRIBUTING.md             # Contribution guide
├── Directory.Build.props       # MSBuild properties
├── Directory.Build.targets     # MSBuild targets
├── Directory.Packages.props    # Package versions
├── global.json                 # .NET SDK version
├── package.json                # Node dependencies
├── pyproject.toml              # Python config
├── pytest.ini                  # Pytest config
├── README.md                   # Project readme
├── RELEASE_NOTES.md            # Release notes
├── SECURITY.md                 # Security policy
├── WileyWidget.csproj          # Main project
└── WileyWidget.sln             # Solution file
```

---

## 🚀 Execution Script

### PowerShell Script to Execute Organization

```powershell
# File Organization Script for WileyWidget
# Run from project root directory

# Create new directory structure
New-Item -ItemType Directory -Path "docs/architecture" -Force
New-Item -ItemType Directory -Path "docs/guides" -Force
New-Item -ItemType Directory -Path "docs/reports" -Force
New-Item -ItemType Directory -Path "docs/analysis" -Force
New-Item -ItemType Directory -Path "data" -Force
New-Item -ItemType Directory -Path "data/tests" -Force
New-Item -ItemType Directory -Path "docker" -Force
New-Item -ItemType Directory -Path "scripts/quickbooks" -Force
New-Item -ItemType Directory -Path "scripts/testing" -Force
New-Item -ItemType Directory -Path "tests/integration" -Force

# Move documentation
Move-Item "AI_Integration_Plan.md" "docs/architecture/" -Force -ErrorAction SilentlyContinue
Move-Item "AI_INTEGRATION_DI_STATUS.md" "docs/architecture/" -Force -ErrorAction SilentlyContinue
Move-Item "LOGGING_ENHANCEMENTS.md" "docs/architecture/" -Force -ErrorAction SilentlyContinue
Move-Item "quickbooks-registration-guide.md" "docs/guides/" -Force -ErrorAction SilentlyContinue
Move-Item "QUICKBOOKS-SETUP.md" "docs/guides/" -Force -ErrorAction SilentlyContinue
Move-Item "COMMAND_REVIEW_REPORT.md" "docs/reports/" -Force -ErrorAction SilentlyContinue
Move-Item "fetchability-resources.json" "docs/analysis/" -Force -ErrorAction SilentlyContinue
Move-Item "repomix-output.md" "docs/analysis/" -Force -ErrorAction SilentlyContinue
Move-Item "repomix-output.xml" "docs/analysis/" -Force -ErrorAction SilentlyContinue
Move-Item "wiley-widget-llm.txt" "docs/analysis/" -Force -ErrorAction SilentlyContinue

# Move scripts
Move-Item "setup-quickbooks-sandbox.ps1" "scripts/quickbooks/" -Force -ErrorAction SilentlyContinue
Move-Item "setup-town-of-wiley.ps1" "scripts/quickbooks/" -Force -ErrorAction SilentlyContinue
Move-Item "test-qbo-keyvault-integration.ps1" "scripts/quickbooks/" -Force -ErrorAction SilentlyContinue
Move-Item "test-quickbooks-connection.ps1" "scripts/quickbooks/" -Force -ErrorAction SilentlyContinue
Move-Item "run-dashboard-tests.ps1" "scripts/testing/" -Force -ErrorAction SilentlyContinue

# Move Docker files
Move-Item "docker-compose.regionviewregistry-tests.yml" "docker/" -Force -ErrorAction SilentlyContinue
Move-Item "docker-compose.test.yml" "docker/" -Force -ErrorAction SilentlyContinue
Move-Item "Dockerfile.regionviewregistry-tests" "docker/" -Force -ErrorAction SilentlyContinue
Move-Item "Dockerfile.test" "docker/" -Force -ErrorAction SilentlyContinue
Move-Item "Dockerfile.test-regionviewregistry" "docker/" -Force -ErrorAction SilentlyContinue

# Move test files
Move-Item "QuickBooksStructureTest.cs" "tests/integration/" -Force -ErrorAction SilentlyContinue

# Move configuration
Move-Item ".env.production.sample" "config/" -Force -ErrorAction SilentlyContinue

# Move database files
Move-Item "WileyWidgetDev.db" "data/" -Force -ErrorAction SilentlyContinue
Move-Item "DatabaseSetup" "data/" -Force -ErrorAction SilentlyContinue
Move-Item "DatabaseTest" "data/tests/" -Force -ErrorAction SilentlyContinue

# Delete temporary/build files
Remove-Item "build-detailed.log" -Force -ErrorAction SilentlyContinue
Remove-Item "build-diag.txt" -Force -ErrorAction SilentlyContinue
Remove-Item "build-errors.log" -Force -ErrorAction SilentlyContinue
Remove-Item "debug-hosted.log" -Force -ErrorAction SilentlyContinue
Remove-Item "xaml-trace.log" -Force -ErrorAction SilentlyContinue
Remove-Item "psscriptanalyzer-results.txt" -Force -ErrorAction SilentlyContinue
Remove-Item "startup-performance-results.json" -Force -ErrorAction SilentlyContinue
Remove-Item ".packages.lastmodified" -Force -ErrorAction SilentlyContinue
Remove-Item ".coverage" -Force -ErrorAction SilentlyContinue

# Delete build artifact directories
Remove-Item ".buildcache" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".tmp.drivedownload" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".tmp.driveupload" -Recurse -Force -ErrorAction SilentlyContinue

Write-Output "✅ File organization complete!"
Write-Output "📂 New directory structure created"
Write-Output "🗑️ Temporary files cleaned up"
Write-Output ""
Write-Output "Verify the changes with: git status"
```

---

## 📊 Impact Summary

### Files Moved: ~25 files
### Files Deleted: ~10 temporary files
### Directories Created: 9 new subdirectories
### Root Directory Reduction: ~35 fewer items in root

### Benefits
- ✅ Cleaner root directory (50% fewer files)
- ✅ Better organization by purpose
- ✅ Easier navigation
- ✅ Clearer project structure
- ✅ Follows standard .NET project conventions

---

## ⚠️ Important Notes

1. **Backup First**: Consider committing current state before reorganizing
2. **Git Tracking**: Use `git mv` instead of `Move-Item` if you want to preserve history
3. **Update References**: Some scripts may reference moved files - check and update paths
4. **CI/CD Updates**: Update GitHub Actions workflows if they reference moved files
5. **Documentation**: Update README.md to reflect new structure

---

## 🔄 Alternative: Git-Aware Reorganization

For better git history preservation:

```powershell
# Use git mv to preserve history
git mv "AI_Integration_Plan.md" "docs/architecture/"
git mv "quickbooks-registration-guide.md" "docs/guides/"
# ... etc for each file
```

---

**Created**: October 15, 2025  
**Status**: Ready for execution  
**Risk**: Low (mostly documentation and temporary files)
