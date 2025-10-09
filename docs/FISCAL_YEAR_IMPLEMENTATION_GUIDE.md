# Fiscal Year Implementation Guide for Wiley Widget

## Executive Summary

This document outlines the **GASB-compliant fiscal year accounting implementation** for Wiley Widget's municipal enterprise fund management system. It provides a comprehensive review of all components requiring fiscal year date awareness.

## GASB Context

**Governmental Accounting Standards Board (GASB)** requires:
- **Fiscal year-based reporting** (typically July 1 - June 30 for municipalities)
- **Period-based budget tracking** with multi-year comparisons
- **Fund accounting** principles with fiscal year boundaries
- **Budget vs. Actual** comparisons within fiscal periods
- **Encumbrance tracking** by fiscal year

## Global Fiscal Year Configuration

### 1. FiscalYearSettings Model (`src/Models/FiscalYearSettings.cs`)

**Purpose**: Singleton configuration for organization-wide fiscal year dates

**Key Properties**:
```csharp
public int FiscalYearStartMonth { get; set; } = 7;    // July default
public int FiscalYearStartDay { get; set; } = 1;      // 1st of month
public DateTime FiscalYearStartDate { get; }          // Computed property
```

**Key Methods**:
- `GetCurrentFiscalYearStart(DateTime referenceDate)` - Calculate FY start
- `GetCurrentFiscalYearEnd(DateTime referenceDate)` - Calculate FY end  
- `IsCurrentFiscalYear(DateTime date)` - Check if date is in current FY

**Database Configuration**:
- Stored in `FiscalYearSettings` table
- Singleton pattern (only ONE record with Id = 1)
- Row versioning for concurrency control
- Configured in `AppDbContext.cs` (line 70)

## Components Requiring Fiscal Year Integration

### 2. Budget Management System

#### 2.1 BudgetPeriod Model (`src/Models/BudgetPeriod.cs`)
**CRITICAL**: Budget periods MUST align with fiscal years

**Required Integration**:
```csharp
public class BudgetPeriod
{
    public DateTime StartDate { get; set; }   // Must match FY start
    public DateTime EndDate { get; set; }     // Must match FY end
    public int FiscalYear { get; set; }       // e.g., 2025 for FY2024-2025
    // ... other properties
}
```

**Implementation Checklist**:
- ✅ Ensure BudgetPeriod creation uses `FiscalYearSettings.GetCurrentFiscalYearStart()`
- ⚠️ **TODO**: Add validation to prevent budget periods that don't align with FY
- ⚠️ **TODO**: Add database constraint ensuring StartDate matches configured FY start

#### 2.2 BudgetEntry Model (`src/Models/BudgetEntry.cs`)
**Purpose**: Individual budget line items tied to budget periods

**Required Integration**:
- All BudgetEntry records must reference a valid BudgetPeriod
- Queries should filter by current fiscal year using `BudgetPeriod.FiscalYear`

### 3. Financial Reporting

#### 3.1 OverallBudget Model (`src/Models/OverallBudget.cs`)
**Purpose**: Municipal budget snapshots

**Current Implementation**:
```csharp
public DateTime SnapshotDate { get; set; }    // Timestamp of snapshot
public bool IsCurrent { get; set; }           // One current snapshot only
```

**Required Integration**:
- ✅ SnapshotDate tracked
- ⚠️ **TODO**: Add `FiscalYear` property for easier filtering
- ⚠️ **TODO**: Add method `GetSnapshotsForFiscalYear(int fiscalYear)`

#### 3.2 Enterprise Revenue/Expense Tracking
**Files**: `src/Models/Enterprise.cs`, `src/Models/BudgetInteraction.cs`

**Current Properties**:
```csharp
public decimal CurrentRate { get; set; }
public decimal MonthlyExpenses { get; set; }
```

**Required Integration**:
- ⚠️ **TODO**: Consider adding `FiscalYearToDate` revenue/expense properties
- ⚠️ **TODO**: Add methods to calculate YTD (Year-To-Date) within current FY
- ⚠️ **TODO**: Implement fiscal year rollover procedures

### 4. Repository Layer - Query Filtering

#### 4.1 BudgetPeriodRepository (`src/Data/BudgetPeriodRepository.cs`)
**CRITICAL**: All budget queries must be fiscal year aware

**Required Methods** (if not present, ADD THEM):
```csharp
Task<BudgetPeriod?> GetCurrentFiscalYearPeriodAsync();
Task<IEnumerable<BudgetPeriod>> GetByFiscalYearAsync(int fiscalYear);
Task<IEnumerable<BudgetPeriod>> GetPastFiscalYearsAsync(int yearsBack);
```

**Implementation Pattern**:
```csharp
public async Task<BudgetPeriod?> GetCurrentFiscalYearPeriodAsync()
{
    var settings = await _context.FiscalYearSettings.FirstOrDefaultAsync();
    if (settings == null) return null;
    
    var fyStart = settings.GetCurrentFiscalYearStart(DateTime.Now);
    var fyEnd = settings.GetCurrentFiscalYearEnd(DateTime.Now);
    
    return await _context.BudgetPeriods
        .FirstOrDefaultAsync(bp => bp.StartDate >= fyStart && bp.EndDate <= fyEnd);
}
```

#### 4.2 MunicipalAccountRepository (`src/Data/MunicipalAccountRepository.cs`)
**Purpose**: Account balance tracking with fiscal year context

**Required Integration**:
- ⚠️ **TODO**: Add method `GetBalanceAtFiscalYearStart(int accountId, int fiscalYear)`
- ⚠️ **TODO**: Add method `GetTransactionsByFiscalYear(int accountId, int fiscalYear)`

### 5. ViewModels - UI Data Presentation

#### 5.1 BudgetViewModel (if exists)
**Required Features**:
- Fiscal year selector dropdown (current year ± 3 years)
- Filter all budget data by selected fiscal year
- Display "Current Fiscal Year: FY2024-2025" indicator
- Budget vs. Actual reports filtered by FY

#### 5.2 ReportingViewModel (if exists)
**Required Features**:
- All financial reports must have fiscal year parameter
- Comparative reports: "This FY vs. Last FY"
- MTD (Month-To-Date) and YTD (Year-To-Date) within FY context

### 6. Services Layer

#### 6.1 FiscalYearService (RECOMMENDED TO CREATE)
**Purpose**: Centralized fiscal year business logic

**Recommended Implementation**:
```csharp
public class FiscalYearService
{
    private readonly AppDbContext _context;
    
    public async Task<FiscalYearSettings> GetSettingsAsync()
    {
        return await _context.FiscalYearSettings.FirstOrDefaultAsync()
            ?? new FiscalYearSettings(); // Default settings
    }
    
    public async Task<DateTime> GetCurrentFiscalYearStartAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.GetCurrentFiscalYearStart(DateTime.Now);
    }
    
    public async Task<DateTime> GetCurrentFiscalYearEndAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.GetCurrentFiscalYearEnd(DateTime.Now);
    }
    
    public async Task<int> GetCurrentFiscalYearNumberAsync()
    {
        var start = await GetCurrentFiscalYearStartAsync();
        return start.Month >= 7 ? start.Year + 1 : start.Year;
    }
    
    public async Task<(DateTime Start, DateTime End)> GetFiscalYearRange(int fiscalYear)
    {
        var settings = await GetSettingsAsync();
        var start = new DateTime(fiscalYear - 1, settings.FiscalYearStartMonth, settings.FiscalYearStartDay);
        var end = start.AddYears(1).AddDays(-1);
        return (start, end);
    }
}
```

#### 6.2 WhatIfScenarioEngine (`src/Services/WhatIfScenarioEngine.cs`)
**Current Status**: ✅ Exists, but may need FY awareness

**Required Integration**:
- Scenario projections should span fiscal year boundaries
- "What if we raise rates in FY2025?" type scenarios
- Multi-year projections by fiscal year

### 7. Database Schema Requirements

#### 7.1 Migration Required
**File**: Create new migration for FiscalYearSettings table

**Required Columns**:
```sql
CREATE TABLE FiscalYearSettings (
    Id INT PRIMARY KEY DEFAULT 1,
    FiscalYearStartMonth INT NOT NULL DEFAULT 7,
    FiscalYearStartDay INT NOT NULL DEFAULT 1,
    RowVersion ROWVERSION,
    LastModified DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT CHK_OnlyOneRow CHECK (Id = 1),
    CONSTRAINT CHK_ValidMonth CHECK (FiscalYearStartMonth BETWEEN 1 AND 12),
    CONSTRAINT CHK_ValidDay CHECK (FiscalYearStartDay BETWEEN 1 AND 31)
);

-- Seed default fiscal year (July 1)
INSERT INTO FiscalYearSettings (Id, FiscalYearStartMonth, FiscalYearStartDay, LastModified)
VALUES (1, 7, 1, GETUTCDATE());
```

#### 7.2 Existing Tables Requiring Updates

**BudgetPeriods Table**:
```sql
ALTER TABLE BudgetPeriods
ADD FiscalYear INT NULL;  -- e.g., 2025 for FY2024-2025

CREATE INDEX IX_BudgetPeriods_FiscalYear ON BudgetPeriods(FiscalYear);
```

**OverallBudgets Table**:
```sql
ALTER TABLE OverallBudgets
ADD FiscalYear INT NULL;

CREATE INDEX IX_OverallBudgets_FiscalYear ON OverallBudgets(FiscalYear);
```

### 8. UI Components Requiring Updates

#### 8.1 Settings/Configuration View
**Required Features**:
- Form to edit FiscalYearSettings
- Month/Day picker for fiscal year start
- Preview showing "Current FY: July 1, 2024 - June 30, 2025"
- Warning when changing (affects all budget periods)

#### 8.2 Budget Management Views
**Required Features**:
- Fiscal year dropdown filter
- "Current FY" highlighted/default selection
- Date pickers validate against FY boundaries
- Warnings for dates outside current FY

#### 8.3 Reporting Views
**Required Features**:
- Fiscal year selector
- "This FY", "Last FY", "All FYs" quick filters
- Export reports with FY label (e.g., "FY2024-2025 Budget Report")

### 9. Testing Requirements

#### 9.1 Unit Tests
**File**: Create `FiscalYearSettingsTests.cs`

**Test Cases**:
```csharp
[Test] public void GetCurrentFiscalYearStart_JulyStart_CorrectDate()
[Test] public void GetCurrentFiscalYearEnd_JulyStart_JuneEnd()
[Test] public void IsCurrentFiscalYear_DateInRange_ReturnsTrue()
[Test] public void IsCurrentFiscalYear_DateOutOfRange_ReturnsFalse()
[Test] public void GetCurrentFiscalYearStart_BeforeFYStart_UsesPriorYear()
```

#### 9.2 Integration Tests
**Test Budget Period Creation**:
```csharp
[Test] public async Task CreateBudgetPeriod_UsesFiscalYearSettings()
{
    var settings = await context.FiscalYearSettings.FirstAsync();
    var period = await budgetService.CreateNewFiscalYearPeriodAsync();
    Assert.That(period.StartDate.Month, Is.EqualTo(settings.FiscalYearStartMonth));
}
```

### 10. Configuration Management

#### 10.1 AppSettings Integration
**File**: `appsettings.json`, `appsettings.Development.json`

**Recommended Section**:
```json
{
  "FiscalYear": {
    "DefaultStartMonth": 7,
    "DefaultStartDay": 1,
    "DisplayFormat": "FY{0}-{1}",  // e.g., "FY2024-2025"
    "AllowMidYearChange": false
  }
}
```

### 11. Data Migration Strategy

#### 11.1 Retroactive Fiscal Year Assignment
**For Existing Data**:

```csharp
public async Task AssignFiscalYearsToExistingBudgetPeriods()
{
    var settings = await _context.FiscalYearSettings.FirstOrDefaultAsync();
    var periods = await _context.BudgetPeriods.Where(p => p.FiscalYear == null).ToListAsync();
    
    foreach (var period in periods)
    {
        var fyStart = settings.GetCurrentFiscalYearStart(period.StartDate);
        period.FiscalYear = fyStart.Month >= 7 ? fyStart.Year + 1 : fyStart.Year;
    }
    
    await _context.SaveChangesAsync();
}
```

## Implementation Checklist

### Phase 1: Database & Models (CURRENT)
- ✅ Create FiscalYearSettings model
- ✅ Add DbSet to AppDbContext
- ⚠️ Create and apply migration
- ⚠️ Seed default fiscal year settings

### Phase 2: Service Layer (NEXT)
- ⚠️ Create FiscalYearService
- ⚠️ Update BudgetPeriodRepository with FY methods
- ⚠️ Update MunicipalAccountRepository with FY methods
- ⚠️ Add FY validation to budget operations

### Phase 3: Repository Integration (REQUIRED)
- ⚠️ Add `GetByFiscalYearAsync()` to all financial repositories
- ⚠️ Add `GetCurrentFiscalYearAsync()` methods
- ⚠️ Update existing queries to be FY-aware

### Phase 4: UI Components (HIGH PRIORITY)
- ⚠️ Create FiscalYearSettingsView for admin
- ⚠️ Add FY dropdown to budget views
- ⚠️ Add FY filter to report views
- ⚠️ Display current FY in dashboard

### Phase 5: Testing & Validation (ESSENTIAL)
- ⚠️ Unit tests for FiscalYearSettings
- ⚠️ Integration tests for FY-filtered queries
- ⚠️ End-to-end tests for budget workflows
- ⚠️ Data migration validation

## Best Practices

### 1. Always Use FiscalYearSettings
❌ **DON'T**: Hard-code fiscal year logic
```csharp
var fiscalStart = new DateTime(DateTime.Now.Year, 7, 1);  // BAD
```

✅ **DO**: Use FiscalYearSettings
```csharp
var settings = await _context.FiscalYearSettings.FirstOrDefaultAsync();
var fiscalStart = settings.GetCurrentFiscalYearStart(DateTime.Now);
```

### 2. Validate Dates Against Fiscal Year
```csharp
public async Task<bool> ValidateBudgetPeriodAsync(BudgetPeriod period)
{
    var settings = await _fiscalYearService.GetSettingsAsync();
    var expectedStart = settings.GetCurrentFiscalYearStart(period.StartDate);
    
    if (period.StartDate != expectedStart)
    {
        throw new ValidationException("Budget period must start on fiscal year boundary");
    }
    return true;
}
```

### 3. Query Patterns
**Current Fiscal Year Only**:
```csharp
var currentFY = await _fiscalYearService.GetCurrentFiscalYearNumberAsync();
var budgets = await _context.BudgetPeriods
    .Where(bp => bp.FiscalYear == currentFY)
    .ToListAsync();
```

**Date Range within FY**:
```csharp
var (start, end) = await _fiscalYearService.GetFiscalYearRange(2025);
var transactions = await _context.Transactions
    .Where(t => t.TransactionDate >= start && t.TransactionDate <= end)
    .ToListAsync();
```

### 4. Reporting Guidelines
- All financial reports MUST include fiscal year context
- Use "FY2024-2025" format for display
- Comparative reports: "vs. Prior FY" not "vs. Last Year"
- Year-end reports show full FY (July 1 - June 30)

## Critical Files to Review/Update

### Immediate Priority (P0)
1. ✅ `src/Models/FiscalYearSettings.cs` - **EXISTS, REVIEW COMPLETE**
2. ✅ `src/Data/AppDbContext.cs` - **DbSet configured at line 70**
3. ⚠️ `Migrations/` - **CREATE MIGRATION FOR FiscalYearSettings table**
4. ⚠️ `src/Data/BudgetPeriodRepository.cs` - **ADD FY methods**

### High Priority (P1)
5. ⚠️ `src/Services/FiscalYearService.cs` - **CREATE NEW**
6. ⚠️ `src/Models/BudgetPeriod.cs` - **ADD FiscalYear property**
7. ⚠️ `src/Models/OverallBudget.cs` - **ADD FiscalYear property**
8. ⚠️ `src/ViewModels/BudgetViewModel.cs` - **ADD FY filtering**

### Medium Priority (P2)
9. ⚠️ `src/Views/Settings/FiscalYearSettingsView.xaml` - **CREATE NEW**
10. ⚠️ `src/Services/BudgetService.cs` - **UPDATE with FY validation**
11. ⚠️ `src/Data/MunicipalAccountRepository.cs` - **ADD FY methods**

### Documentation (P3)
12. ⚠️ User manual updates for fiscal year configuration
13. ⚠️ Admin guide for fiscal year rollover procedures
14. ⚠️ API documentation for FY-related methods

## Fiscal Year Rollover Procedure

### Annual Process (Before New Fiscal Year Starts)
1. **Close Current FY**:
   - Mark all current FY budget periods as closed
   - Generate year-end reports
   - Archive transactional data

2. **Create New FY Budget Periods**:
   ```csharp
   var newPeriod = new BudgetPeriod
   {
       FiscalYear = 2026,
       StartDate = new DateTime(2025, 7, 1),
       EndDate = new DateTime(2026, 6, 30),
       Status = "Active"
   };
   ```

3. **Carry Forward Balances**:
   - Transfer ending balances to new FY opening balances
   - Apply any budget adjustments

4. **Notification**:
   - Email administrators 30 days before FY end
   - Display banner in UI: "Fiscal Year 2024-2025 ends in 30 days"

## Related GASB Standards

### GASB Statement No. 34
- Basic Financial Statements and Management's Discussion and Analysis
- Requires fiscal year-based reporting

### GASB Statement No. 54
- Fund Balance Reporting and Governmental Fund Type Definitions
- Fiscal year context for fund classifications

### GASB Statement No. 63
- Financial Reporting of Deferred Outflows of Resources
- Multi-year fiscal period tracking

## Support & Troubleshooting

### Common Issues

**Issue**: Budget periods don't align with fiscal year
**Solution**: Run data migration to retroactively assign fiscal years

**Issue**: Reports show wrong year
**Solution**: Verify FiscalYearSettings.FiscalYearStartMonth is correct

**Issue**: Fiscal year change mid-year
**Solution**: This is generally NOT recommended; requires careful data migration

## Conclusion

Proper fiscal year implementation is **CRITICAL** for GASB compliance and accurate municipal financial reporting. All financial data queries, budget operations, and reports MUST use the FiscalYearSettings configuration.

**Next Steps**:
1. Apply FiscalYearSettings migration
2. Create FiscalYearService
3. Update all repositories with FY-aware methods
4. Add FY filtering to UI components
5. Comprehensive testing

---

**Document Version**: 1.0  
**Last Updated**: October 8, 2025  
**Author**: AI Development Assistant  
**Review Status**: Ready for Implementation
