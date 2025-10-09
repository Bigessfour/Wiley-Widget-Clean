# Wiley Widget Startup Architecture & Procedures (October 2025)

The Wiley Widget application now runs on a fully modernized startup pipeline that follows Microsoft's Generic Host guidance for WPF, adopts Prism for MVVM composition, and delivers predictable developer ergonomics. This document replaces the original transformation checklist with an authoritative snapshot of what is live today and how to work with it.

---

## 🚀 Executive Summary

- ✅ **Generic Host everywhere** – `Program.Main` bootstraps Serilog, ensures STA threading, and defers all heavy work to `App.OnStartup`.
- ✅ **Phased startup pipeline** – Splash screen + progress telemetry, host construction, StartupTaskRunner, and hosted services coordinate work without blocking the UI.
- ✅ **Hosted WPF integration** – `HostedWpfApplication` creates the main window from DI, closes the splash on `ContentRendered`, and logs performance metrics.
- ✅ **Background initialization** – `BackgroundInitializationService`, `ParallelStartupService`, and health checks run asynchronously after the UI is live.
- ✅ **Instrumentation-first** – `WILEY_DEBUG_STARTUP=true` emits `logs/startup-*` diagnostics, with structured telemetry for every phase.

The refactor is complete; ongoing work focuses on incremental performance tuning and optional telemetry expansion.

---

## 🧭 Startup Pipeline at a Glance

The following phases are implemented in `App.OnStartup` (see `src/App.xaml.cs`). Percentages match the `StartupProgressService` updates that drive the splash screen.

| Phase | Progress | Responsibilities | Key Types |
|-------|----------|------------------|-----------|
| Phase 0 | 0–15% | Debug instrumentation, global exception wiring, orphaned process cleanup | `App.InitializeDebugInstrumentation`, `ConfigureGlobalExceptionHandling` |
| Phase 1 | 15–25% | Splash screen creation and attachment to progress reporter | `SplashScreenWindow`, `StartupProgressService` |
| Phase 2 | 25–75% | Host builder, configuration hierarchy, Serilog takeover, DI registrations, startup task pipeline | `Host.CreateApplicationBuilder`, `WpfHostingExtensions`, `StartupTaskRunner` |
| Phase 3 | 80–95% | Host start, `HostedWpfApplication` shows `MainWindow`, splash fades on `ContentRendered` | `HostedWpfApplication`, `ViewManager` |
| Phase 4 | 95–100% | Prism `base.OnStartup`, background services kick in, startup telemetry recorded | `PrismApplication.OnStartup`, `BackgroundInitializationService`, `HealthCheckHostedService` |

The progress reporter also feeds automated logs, enabling regression detection when a phase slows down.

---

## 🧱 Key Components (Live in `src/`)

| Area | Purpose | Highlights |
|------|---------|------------|
| `Program.cs` | Entry point | STA enforced, Serilog bootstrap logger, optional `testmain` harness for UI smoke tests. |
| `App.xaml.cs` | Startup orchestrator | Progress tracking, host creation, .env loading, splash coordination, startup timing logs, Prism integration. |
| `Configuration/WpfHostingExtensions.cs` | Host composition | Central location for configuration sources, Serilog setup, hosted services, DbContext factory registration, Options binding, HttpClient policies. |
| `Services/Startup/*.cs` + `StartupTaskRunner` | Deterministic early work | Syncfusion license, settings hydration, diagnostics snapshot executed before host start with DI scopes and cancellation support. |
| `Services/HostedWpfApplication.cs` | WPF + host bridge | Creates `MainWindow`, ties into `IHostApplicationLifetime`, tracks warm vs cold startups, and closes the splash. |
| `Services/BackgroundInitializationService.cs` & friends | Async follow-up | Database validation, health check activation, telemetry warm-up without blocking UI thread. |
| `Services/ParallelStartupService.cs` | Performance optimization | Concurrent background operations guarded by semaphores and comprehensive logging. |
| `Services/StartupProgressService.cs` | UX feedback | Single source of truth for splash screen progress and completion messaging. |

All of the above are covered by the service validation hosted service (`ServiceProviderValidationHostedService`) that runs immediately after the host is built.

---

## 🛠️ Developer Startup Workflow

Follow this flow for day-to-day development. All commands assume a PowerShell session in the repo root (`pwsh.exe`).

1. **Clean stale processes & artifacts**
   ```powershell
   python scripts/dev-start.py --clean-only
   ```
   Uses Tasklist/Taskkill to remove orphaned `dotnet` and `WileyWidget.exe` processes, then clears top-level `bin/` and `obj/` directories.

2. **Launch the development session**
   ```powershell
   python scripts/dev-start.py
   ```
   Steps performed automatically:
   - Confirm no conflicting processes
   - `dotnet clean WileyWidget.csproj`
   - Optional Azure performance lock via `scripts/lock-azure-performance.ps1 -SkipAuth`
   - Starts either `dotnet watch run --project WileyWidget.csproj` (default) or `dotnet run` when `--no-watch` is supplied.

3. **Hot reload and Prism navigation**
   - `dotnet watch` triggers Prism view reloads; watch for Serilog output tagged with the startup phase when the app restarts.
   - Use the splash progress log to confirm warm start (<2s) vs cold start times.

4. **Debugging with debugpy (optional)**
   ```powershell
   python scripts/dev-start-debugpy.py --timing
   ```
   - Waits for VS Code’s “Python: Attach to debugpy”.
   - Breakpoints are pre-seeded around cleanup, build, and run phases.
   - `--skip-cleanup` retains caches for faster inner loops.

5. **Profiling startup**
   ```powershell
   pwsh -File scripts/profile-startup.ps1 -Iterations 3
   ```
   - Executes the full startup pipeline multiple times, aggregates timings, and highlights regressions relative to the committed baseline stored under `logs/`.

---

## 🩺 Diagnostics & Observability

| Tool | How to enable | What you get |
|------|---------------|--------------|
| Startup instrumentation | `setx WILEY_DEBUG_STARTUP true` (persist) or `$env:WILEY_DEBUG_STARTUP = "true"` (session) | Detailed `logs/startup-debug.log` with phase timings, assembly loads, and configuration decisions. |
| Splash analytics | Automatic | `StartupProgressService` logs every progress update via Serilog using the `StartupProgressService` context. |
| Host health | Always on | `HealthCheckHostedService` updates `App.LatestHealthReport`; expose in UI dashboards or telemetry as needed. |
| Self-log | Automatic | `logs/serilog-selflog.txt` captures sink misconfiguration without crashing the app. |
| Startup timing regression | `scripts/profile-startup.ps1` outputs CSV + Markdown summary under `logs/profile/`. |
| Debug cleanup | `python scripts/dev-start.py --clean-only` prior to profiling ensures consistent baselines. |

For production telemetry, hook `ApplicationInsights:ConnectionString` in `appsettings.*.json`; `WpfHostingExtensions` already wires telemetry services when a connection string is present.

---

## ✅ Validation Checklist

- [x] `Program.Main` returns 0 after running smoke tests via `dotnet run --project WileyWidget.csproj`.
- [x] Splash progress reaches 100% and closes automatically.
- [x] `logs/startup-.log` contains Phase 0–4 entries with elapsed milliseconds.
- [x] `ServiceProviderValidationHostedService` reports no missing registrations.
- [x] `StartupTaskRunner` executes Syncfusion, Settings, Diagnostics tasks without exceptions.
- [x] `BackgroundInitializationService` completes database checks asynchronously (look for `📊 Database initialization delegated` log).
- [x] `profile-startup.ps1` median cold start <= 5s on reference hardware.

---

## 🔭 Next Opportunities

These items are optional optimizations and are **not** blockers for the current architecture:

1. **Azure Key Vault provider** – Implement a desktop-friendly configuration source now that the hosting story is stable.
2. **Warm startup cache** – Extend `ParallelStartupService` with persisted cache hydration to shave another ~300 ms on cold boots.
3. **Application Insights dashboards** – Wire `StartupProgressService` events into dashboards once telemetry is enabled.
4. **UI smoke automation** – Leverage the existing `Program.Main("testmain")` hook inside UI tests to validate window composition after each build.

---

**Document history:** updated 2025-10-09 to reflect the completed startup refactor and provide current procedures for developers and operators.
