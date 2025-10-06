"""Generate realistic mock data for xaml_sleuth static analysis.

The script inspects the existing .sleuth reports to capture all discovered binding
paths and emits a consolidated JSON payload with sensible sample values.

Run from the repository root:
    python tools/python/mock-data/generate_mock_data.py
"""

from __future__ import annotations

import json
import re
from collections.abc import Iterable
from pathlib import Path
from typing import Any

# <repo>/tools/python/mock-data/generate_mock_data.py -> parents:
# 0: mock-data, 1: python, 2: tools, 3: repo root.
ROOT = Path(__file__).resolve().parents[3]
VIEWS_DIR = ROOT / "src" / "Views"
REPORT_PATTERN = re.compile(r"Binding path '([^']+)'")
OUTPUT_PATH = ROOT / "tools" / "python" / "mock-data" / "wiley-widget-default.json"


def _collect_binding_paths() -> set[str]:
    paths: set[str] = set()
    for report in VIEWS_DIR.glob("**/*.sleuth.txt"):
        for line in report.read_text(encoding="utf-8").splitlines():
            match = REPORT_PATTERN.search(line)
            if match:
                paths.add(match.group(1))
    return paths


def _ensure_path(container: dict[str, Any], path: str, value: Any) -> None:
    segments = path.split(".") if path else []
    if not segments:
        return
    cursor: dict[str, Any] = container
    for segment in segments[:-1]:
        cursor = cursor.setdefault(segment, {})
    cursor.setdefault(segments[-1], value)


def _boostrap_base_data() -> dict[str, Any]:
    """Seed the structure with domain-aware defaults before heuristics."""
    return {
        # Splash/About windows
        "Title": "Wiley Widget",
        "Subtitle": "Municipal Analytics Platform",
        "Version": "2025.9",
        "VersionInfo": "v2025.9 build 142",
        "SystemInfo": "Windows 11 • .NET 9",
        "StatusText": "Loading dashboards…",
        "Progress": 72,
        "IsLoading": False,
        "CopyrightText": "© 2025 Wiley Municipality",
        "OpenUrlCommand": "command",
        "CloseCommand": "command",
        # Main dashboard metrics
        "CurrentUserName": "Alex Rivera",
        "CurrentUserEmail": "alex.rivera@wiley.gov",
        "IsUserAdmin": True,
        "UserRoles": ["Administrator", "Finance"],
        "CurrentViewName": "Municipal Enterprises",
        "DashboardStatus": "Healthy",
        "HealthScore": 92,
        "BackupFrequency": "Weekly",
        "AutoRefreshData": True,
        "LastUpdated": "2025-09-30T08:00:00Z",
        "ApplicationVersion": "2025.9",
        "Widgets": [
            {
                "Title": "Water Utility",
                "Description": "Revenue and expense overview",
                "Category": "Enterprise",
            },
            {
                "Title": "Wastewater",
                "Description": "Capacity and compliance",
                "Category": "Enterprise",
            },
        ],
        "RibbonItems": [
            {
                "Header": "Home",
                "Bars": [
                    {
                        "Header": "Actions",
                        "Items": [
                            {"Label": "Refresh", "Command": "RefreshCommand", "SizeForm": "Large"},
                            {"Label": "Settings", "Command": "OpenSettingsCommand"},
                        ],
                    }
                ],
            }
        ],
        "SelectedRibbonItem": {
            "Header": "Home",
            "Bars": [],
        },
        "Views": [
            {"DisplayName": "Enterprises"},
            {"DisplayName": "Customers"},
        ],
        "SelectedView": {"DisplayName": "Enterprises"},
        "Enterprises": [
            {
                "Name": "Water Utility",
                "CitizenCount": 4100,
                "CurrentRate": 36.5,
                "MonthlyRevenue": 149_650,
                "MonthlyExpenses": 121_300,
                "MonthlyBalance": 28_350,
                "BreakEvenRate": 33.8,
                "Status": "Surplus",
            },
            {
                "Name": "Sanitation",
                "CitizenCount": 3200,
                "CurrentRate": 24.0,
                "MonthlyRevenue": 76_800,
                "MonthlyExpenses": 69_500,
                "MonthlyBalance": 7_300,
                "BreakEvenRate": 22.4,
                "Status": "Surplus",
            },
        ],
        "SelectedEnterprise": {
            "Name": "Water Utility",
            "Id": 1,
            "CitizenCount": 4100,
            "CurrentRate": 36.5,
            "MonthlyRevenue": 149_650,
            "MonthlyExpenses": 121_300,
            "MonthlyBalance": 28_350,
            "BreakEvenRate": 33.8,
            "Status": "Surplus",
        },
        "ActiveAlerts": [
            {
                "Priority": "High",
                "PriorityColor": "#FF5A5F",
                "Message": "Water usage exceeded forecast",
                "Timestamp": "2025-09-30T07:45:00Z",
            },
            {
                "Priority": "Medium",
                "PriorityColor": "#FFC046",
                "Message": "Upcoming meter calibration",
                "Timestamp": "2025-09-29T18:10:00Z",
            },
        ],
        "BudgetTrendData": [
            {"Period": "Q1", "Revenue": 325000, "Expenses": 296000},
            {"Period": "Q2", "Revenue": 338000, "Expenses": 305000},
        ],
        "EnterpriseTypeData": [
            {"Category": "Water", "Value": 41},
            {"Category": "Sanitation", "Value": 27},
            {"Category": "Apartments", "Value": 32},
        ],
        "QuickBooksTabs": [
            {"Header": "Customers"},
            {"Header": "Invoices"},
        ],
        "QuickBooksStatusMessage": "Connected to QuickBooks sandbox",
        "QuickBooksBusy": False,
        "QuickBooksHasError": False,
        "QuickBooksErrorMessage": "",
        "RecentFiles": [
            {"Name": "BudgetForecast.xlsx", "Path": "C:/Data/BudgetForecast.xlsx"},
            {"Name": "CustomerAging.csv", "Path": "C:/Data/CustomerAging.csv"},
        ],
        "BudgetDetails": [
            {
                "EnterpriseName": "Water Utility",
                "CitizenCount": 4100,
                "MonthlyRevenue": 149_650,
                "MonthlyExpenses": 121_300,
                "MonthlyBalance": 28_350,
                "BreakEvenRate": 33.8,
                "Status": "Surplus",
                "RateIncrease": 2.7,
            }
        ],
        "RateTrendData": [
            {"Period": "2024", "Rate": 31.5},
            {"Period": "2025", "Rate": 33.1},
        ],
        "ProjectedRateData": [
            {"Period": "2026", "Rate": 34.2},
            {"Period": "2027", "Rate": 35.4},
        ],
        "BudgetPerformanceData": [
            {"Enterprise": "Water", "Revenue": 149_650, "Expenses": 121_300},
            {"Enterprise": "Sanitation", "Revenue": 76_800, "Expenses": 69_500},
        ],
        "Customers": [
            {
                "AccountNumber": "AC-1001",
                "DisplayName": "Jordan Martinez",
                "CustomerTypeDescription": "Residential",
                "StatusDescription": "Active",
                "FormattedBalance": "$128.45",
            },
            {
                "AccountNumber": "AC-1042",
                "DisplayName": "Evergreen Condos",
                "CustomerTypeDescription": "Commercial",
                "StatusDescription": "Past Due",
                "FormattedBalance": "$2,412.16",
            },
        ],
        "SelectedCustomer": {
            "AccountNumber": "AC-1001",
            "FirstName": "Jordan",
            "LastName": "Martinez",
            "DisplayName": "Jordan Martinez",
            "CustomerTypeDescription": "Residential",
            "StatusDescription": "Active",
            "FormattedBalance": "$128.45",
            "ServiceAddress": "1201 River St",
            "ServiceCity": "Wiley",
            "ServiceState": "CO",
            "ServiceZipCode": "81092",
            "MailingAddress": "PO Box 132",
            "MailingCity": "Wiley",
            "MailingState": "CO",
            "MailingZipCode": "81092",
            "PhoneNumber": "719-555-0132",
            "EmailAddress": "jordan.martinez@example.com",
            "MeterNumber": "WM-8421",
            "TaxId": "98-7654321",
            "BusinessLicenseNumber": "LIC-5021",
            "CurrentBalance": 128.45,
            "Notes": "Prefers email notifications",
            "AccountOpenDate": "2021-03-15",
            "CreatedDate": "2021-03-15",
            "LastModifiedDate": "2025-09-10",
        },
        "CustomerTypes": ["Residential", "Commercial", "Industrial"],
        "CustomerStatuses": ["Active", "Inactive", "Past Due"],
        "ServiceLocations": ["InsideCityLimits", "OutsideCityLimits"],
        "SummaryText": "2 active customers • $2,540 outstanding",
        "BudgetSummaryText": "Municipal budget is tracking 4% ahead of forecast",
        "VarianceHierarchy": [
            {
                "AccountVariance": {
                    "Account": {
                        "AccountNumber": {"Value": "600-4100"},
                        "Name": "Water Treatment",
                        "Department": {"Name": "Utilities"},
                        "Fund": "Enterprise",
                        "BudgetAmount": 420000,
                        "Balance": 410500,
                    },
                    "VarianceAmount": -9500,
                    "VariancePercent": -2.26,
                }
            }
        ],
        "FundGridData": [
            {"FundName": "Water", "TotalBudget": 420000, "Actual": 410500},
            {"FundName": "Sanitation", "TotalBudget": 285000, "Actual": 276900},
        ],
        "DepartmentGridData": [
            {"DepartmentName": "Operations", "TotalBudget": 310000, "Actual": 301200},
            {"DepartmentName": "Maintenance", "TotalBudget": 195000, "Actual": 188400},
        ],
        "Analysis": {
            "Overview": {
                "TotalBudget": 705000,
                "TotalBalance": 688900,
                "TotalAccounts": 42,
                "Variance": -16099,
                "KeyRatios": [
                    {"Key": "Budget Utilization", "Value": "97%"},
                    {"Key": "Revenue Growth", "Value": "4.2%"},
                ],
            },
            "Variance": {
                "AccountsOverThreshold": 5,
                "AverageVariancePercent": -2.4,
            },
        },
        "ImportStats": {
            "AccountsImported": 42,
            "Errors": 1,
            "Warnings": 3,
        },
        "ImportPreviewRows": [
            {"AccountNumber": "600-4100", "BudgetAmount": 420000, "BudgetYear": 2025},
        ],
        "ChatHistory": [
            {
                "Sender": "Assistant",
                "Message": "Budget outlook remains stable.",
                "Timestamp": "2025-09-30T07:55:00Z",
            }
        ],
        "CurrentMessage": "How can I optimize sanitation routes?",
        "CurrentUser": "Alex",
        "IsServiceChargeMode": False,
        "IsWhatIfMode": True,
        "IsProactiveMode": False,
        "AIIsTyping": False,
        "EquipmentCost": 185000,
        "BenefitsIncreasePercentage": 3.2,
        "AnnualExpenses": 2_450_000,
        "GenerateAnalysisCommand": "command",
        "GenerateForecastCommand": "command",
        "CalculateServiceChargeCommand": "command",
        "GetProactiveAdviceCommand": "command",
        "ExportChatCommand": "command",
        "ConfigureAICommand": "command",
        "BrowseFileCommand": "command",
        "ImportCommand": "command",
        "PreviewCommand": "command",
        "StatusToColor": "#4CAF50",
        "AzureConnectionStatus": "Connected",
        "AzureStatusColor": "#4CAF50",
        "DatabaseStatus": "Online",
        "DatabaseStatusColor": "#4CAF50",
        "LicenseStatus": "Valid",
        "LicenseStatusColor": "#4CAF50",
        "EnableDynamicColumns": True,
        "EnableDataCaching": True,
        "CacheExpirationMinutes": 30,
        "AvailableThemes": ["Material", "Fluent", "Office"],
        "SelectedTheme": "Material",
        "TestAzureConnectionCommand": "command",
        "ValidateLicenseCommand": "command",
        "TestQuickBooksConnectionCommand": "command",
        "SaveSettingsCommand": "command",
        "ResetSettingsCommand": "command",
        "TestConnectionCommand": "command",
        "LoadCustomersAsyncCommand": "command",
        "SaveCustomerAsyncCommand": "command",
        "DeleteCustomerAsyncCommand": "command",
        "RefreshBudgetDataCommand": "command",
        "ExportReportCommand": "command",
        "TrendAnalysisCommand": "command",
        "BreakEvenAnalysisCommand": "command",
    }


def _generate_value(path: str) -> Any:
    leaf = path.split(".")[-1]
    lower = leaf.lower()

    if leaf.endswith("Command"):
        return "command"
    if lower.endswith("count") or lower.endswith("number") or lower.endswith("index"):
        return 3
    if lower.startswith("is") or lower.startswith("has") or lower.startswith("can") or "enabled" in lower:
        return True
    if lower.endswith("percent") or lower.endswith("percentage"):
        return 4.2
    if "date" in lower or "time" in lower:
        return "2025-09-30T08:00:00Z"
    if lower.endswith("color"):
        return "#4CAF50"
    if "status" in lower:
        return "OK"
    if lower.endswith("message") or "text" in lower or "description" in lower:
        return "Sample description"
    if any(token in lower for token in ["amount", "revenue", "expense", "balance", "rate", "cost", "value", "score", "progress", "utilization", "growth"]):
        return 12345.67
    if "email" in lower:
        return "finance@wiley.gov"
    if "phone" in lower:
        return "719-555-0100"
    if lower.endswith("id"):
        return "ID-1001"
    if "url" in lower or "link" in lower:
        return "https://example.com"
    if lower.endswith("city"):
        return "Wiley"
    if lower.endswith("state"):
        return "CO"
    if lower.endswith("zipcode") or lower.endswith("zip"):
        return "81092"
    if lower.endswith("address"):
        return "123 Main St"
    if lower.endswith("name") or "header" in lower or lower.endswith("title"):
        return "Sample " + leaf
    if lower.endswith("path"):
        return "C:/Data/sample.dat"
    return "Sample " + leaf


def _fill_with_heuristics(data: dict[str, Any], paths: Iterable[str]) -> None:
    for path in sorted(paths):
        segments = path.split(".") if path else []
        if not segments:
            continue
        cursor: Any = data
        skip = False
        for segment in segments[:-1]:
            if isinstance(cursor, list):
                if cursor and isinstance(cursor[0], dict):
                    cursor = cursor[0]
                else:
                    skip = True
                    break
            if not isinstance(cursor, dict):
                skip = True
                break
            existing = cursor.get(segment)
            if existing is None:
                next_container: dict[str, Any] = {}
                cursor[segment] = next_container
                cursor = next_container
            elif isinstance(existing, dict):
                cursor = existing
            elif isinstance(existing, list):
                cursor = existing
            else:
                next_container = {}
                cursor[segment] = next_container
                cursor = next_container
        if skip:
            continue
        if isinstance(cursor, list):
            if cursor and isinstance(cursor[0], dict):
                cursor = cursor[0]
            else:
                continue
        leaf = segments[-1]
        if leaf in cursor:
            continue
        cursor[leaf] = _generate_value(path)


def main() -> None:
    paths = _collect_binding_paths()
    base_data = _boostrap_base_data()
    _fill_with_heuristics(base_data, paths)

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(base_data, indent=2, sort_keys=True), encoding="utf-8")
    print(f"Mock data written to {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
