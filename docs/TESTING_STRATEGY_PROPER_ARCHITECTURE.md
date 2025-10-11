# Proper Testing Strategy for WileyWidget

## 🎯 **The Right Approach: Layered Architecture**

Based on Microsoft's official guidance, here's the **CORRECT** testing strategy for .NET 9.0 WPF applications.

---

## ❌ **What We Did Wrong**

**Previous Approach (FAILED)**:
- Created `WileyWidget.IntegrationTests` that directly references the WPF project
- Tried to test database operations through WPF project reference
- Hit .NET 9.0 WPF temporary project generation issues
- Tests can't compile due to framework conflicts

**Why It Failed**:
> "WPF projects create temporary build artifacts during compilation. Test projects compiling against these temporary projects lose access to their own dependencies."
> — Microsoft Documentation

---

## ✅ **The Correct Architecture**

### **Layered N-Tier Structure**

From [Microsoft's N-Tier Data Applications Overview](https://learn.microsoft.com/en-us/visualstudio/data-tools/n-tier-data-applications-overview):

```
┌─────────────────────────────────────────────────────────────┐
│  WileyWidget (Presentation - WPF)         │ .NET 9.0-windows │
│  - Views (XAML)                                              │
│  - ViewModels (MVVM)                                         │
│  - UI Logic Only                                             │
│  References: WileyWidget.Business                            │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  WileyWidget.Business (BLL)               │ .NET 8.0         │
│  - Business Logic                                            │
│  - Validation Rules                                          │
│  - Application Services                                      │
│  References: WileyWidget.Data, WileyWidget.Models           │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  WileyWidget.Data (DAL)                   │ .NET 8.0         │
│  - AppDbContext                                              │
│  - Entity Framework Core                                     │
│  - Repositories                                              │
│  References: WileyWidget.Models                              │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  WileyWidget.Models (Domain)              │ .NET 8.0         │
│  - Entity Classes (MunicipalAccount, Department, etc.)       │
│  - DTOs                                                      │
│  - Interfaces                                                │
│  No Dependencies                                             │
└─────────────────────────────────────────────────────────────┘
```

### **Test Projects Structure**

```
┌─────────────────────────────────────────────────────────────┐
│  WileyWidget.Tests                        │ .NET 8.0         │
│  - Unit Tests for Business Logic                            │
│  - ViewModel Unit Tests                                     │
│  - Mock Repositories                                         │
│  References: WileyWidget.Business (NOT WPF project)          │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  WileyWidget.IntegrationTests            │ .NET 8.0         │
│  - Database Integration Tests                                │
│  - Repository Tests                                          │
│  - TestContainers for SQL Server                             │
│  References: WileyWidget.Data, WileyWidget.Models           │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  WileyWidget.UiTests                      │ .NET 9.0-windows │
│  - WPF UI Automation Tests                                   │
│  - Can reference WPF project (same framework)                │
│  Uses: FlaUI, TestStack.White, or Appium                    │
└─────────────────────────────────────────────────────────────┘
```

---

## 📋 **Implementation Plan**

### **Phase 1: Create Core Libraries** (2-4 hours)

#### 1.1 **Create WileyWidget.Models** (.NET 8.0)
```bash
dotnet new classlib -n WileyWidget.Models -f net8.0
```

**Move from WileyWidget/**:
- `Models/MunicipalAccount.cs`
- `Models/Department.cs`
- `Models/Enterprise.cs`
- `Models/BudgetEntry.cs`
- All entity classes

#### 1.2 **Create WileyWidget.Data** (.NET 8.0)
```bash
dotnet new classlib -n WileyWidget.Data -f net8.0
```

**Move from WileyWidget/**:
- `Data/AppDbContext.cs`
- `Data/Repositories/` (if exists)
- `Data/Configurations/` (EF configurations)
- All EF Core code

**Add NuGet Packages**:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.8" />
```

#### 1.3 **Create WileyWidget.Business** (.NET 8.0)
```bash
dotnet new classlib -n WileyWidget.Business -f net8.0
```

**Create**:
- `Services/` - Business logic services
- `Validators/` - Business rule validation
- `Interfaces/` - Service contracts

Example:
```csharp
// WileyWidget.Business/Services/MunicipalAccountService.cs
public class MunicipalAccountService : IMunicipalAccountService
{
    private readonly AppDbContext _context;
    
    public MunicipalAccountService(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<MunicipalAccount> GetByIdAsync(int id)
    {
        return await _context.MunicipalAccounts
            .Include(m => m.Departments)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
    
    // Business logic here - NOT in WPF project
}
```

### **Phase 2: Update WPF Project** (1-2 hours)

#### 2.1 **Update WileyWidget.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference business layer only -->
    <ProjectReference Include="..\WileyWidget.Business\WileyWidget.Business.csproj" />
    <!-- Models and Data are transitive dependencies -->
  </ItemGroup>
</Project>
```

#### 2.2 **Update ViewModels to Use Services**
```csharp
// WileyWidget/ViewModels/DashboardViewModel.cs
public class DashboardViewModel : ViewModelBase
{
    private readonly IMunicipalAccountService _accountService;
    
    public DashboardViewModel(IMunicipalAccountService accountService)
    {
        _accountService = accountService;
    }
    
    public async Task LoadDataAsync()
    {
        Accounts = await _accountService.GetAllAsync();
    }
}
```

### **Phase 3: Create Proper Test Projects** (2-3 hours)

#### 3.1 **Unit Tests** - `WileyWidget.Tests`
```bash
dotnet new xunit -n WileyWidget.Tests -f net8.0
```

**Test Structure**:
```
WileyWidget.Tests/
├── Business/
│   ├── Services/
│   │   └── MunicipalAccountServiceTests.cs
│   └── Validators/
│       └── DepartmentValidatorTests.cs
├── ViewModels/
│   └── DashboardViewModelTests.cs  # Mock services
└── Helpers/
    └── MockDataFactory.cs
```

**Example Unit Test**:
```csharp
public class MunicipalAccountServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsAccount()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;
        
        await using var context = new AppDbContext(options);
        context.MunicipalAccounts.Add(new MunicipalAccount { Id = 1, Name = "Test" });
        await context.SaveChangesAsync();
        
        var service = new MunicipalAccountService(context);
        
        // Act
        var result = await service.GetByIdAsync(1);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
    }
}
```

#### 3.2 **Integration Tests** - `WileyWidget.IntegrationTests`

**Update .csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference Data and Models ONLY - NOT WPF project -->
    <ProjectReference Include="..\WileyWidget.Data\WileyWidget.Data.csproj" />
    <ProjectReference Include="..\WileyWidget.Models\WileyWidget.Models.csproj" />
    
    <!-- Test packages -->
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="Testcontainers.MsSql" Version="4.2.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
  </ItemGroup>
</Project>
```

**Test Structure**:
```
WileyWidget.IntegrationTests/
├── Data/
│   ├── AppDbContextTests.cs
│   └── MigrationTests.cs
├── Repositories/
│   └── MunicipalAccountRepositoryTests.cs
├── Performance/
│   └── BulkOperationTests.cs
└── Infrastructure/
    └── SqlServerTestBase.cs  # TestContainers setup
```

#### 3.3 **UI Tests** - `WileyWidget.UiTests`

**Create NEW project** (.NET 9.0-windows):
```bash
dotnet new xunit -n WileyWidget.UiTests -f net9.0-windows
```

**Add FlaUI for WPF automation**:
```xml
<PackageReference Include="FlaUI.Core" Version="4.0.0" />
<PackageReference Include="FlaUI.UIA3" Version="4.0.0" />
```

**Example UI Test**:
```csharp
public class DashboardWindowTests : IDisposable
{
    private readonly Application _app;
    private readonly Window _mainWindow;
    
    public DashboardWindowTests()
    {
        _app = Application.Launch("WileyWidget.exe");
        _mainWindow = _app.GetMainWindow(Automation);
    }
    
    [Fact]
    public void Dashboard_LoadsSuccessfully()
    {
        Assert.True(_mainWindow.IsAvailable);
        Assert.Equal("WileyWidget Dashboard", _mainWindow.Title);
    }
    
    public void Dispose()
    {
        _app?.Close();
    }
}
```

---

## 🎯 **Testing Strategy by Layer**

### **1. Model/Domain Tests** (Fastest)
- **What**: Entity validation, business rules
- **How**: Pure unit tests, no dependencies
- **Example**: `Department.CanAddEmployee()` validation

### **2. Data Layer Tests** (Fast-Medium)
- **What**: Repository operations, EF queries
- **How**: In-memory database or TestContainers
- **Example**: CRUD operations, complex queries

### **3. Business Logic Tests** (Medium)
- **What**: Service methods, orchestration
- **How**: Mock repositories, real business logic
- **Example**: `CreateMunicipalAccount()` with validation

### **4. Integration Tests** (Medium-Slow)
- **What**: Full stack without UI
- **How**: Real database (TestContainers), real services
- **Example**: End-to-end workflows

### **5. UI Tests** (Slowest)
- **What**: User interactions, visual validation
- **How**: FlaUI automation
- **Example**: "Click Dashboard tab, verify data loads"

---

## 📊 **Benefits of This Architecture**

### ✅ **Testability**
- **Unit tests** run in milliseconds (no DB, no UI)
- **Integration tests** are isolated to data layer
- **UI tests** can run independently

### ✅ **Maintainability**
- Changes to UI don't break data tests
- Business logic is centralized
- Clear separation of concerns

### ✅ **Scalability**
- Easy to add new features
- Can switch data sources
- Can add web/mobile UI later

### ✅ **.NET 9.0 Compatibility**
- WPF project stays on .NET 9.0
- Test projects use .NET 8.0
- No framework conflicts!

---

## 🚀 **Migration Steps**

### **Step 1: Create Projects** (30 min)
```bash
# In solution folder
dotnet new classlib -n WileyWidget.Models -f net8.0
dotnet new classlib -n WileyWidget.Data -f net8.0
dotnet new classlib -n WileyWidget.Business -f net8.0

# Add to solution
dotnet sln add WileyWidget.Models/WileyWidget.Models.csproj
dotnet sln add WileyWidget.Data/WileyWidget.Data.csproj
dotnet sln add WileyWidget.Business/WileyWidget.Business.csproj
```

### **Step 2: Move Files** (1-2 hours)
Use VS Code or Visual Studio to move files between projects.
Update namespaces and references.

### **Step 3: Update References** (30 min)
```bash
# Data references Models
dotnet add WileyWidget.Data/WileyWidget.Data.csproj reference WileyWidget.Models/WileyWidget.Models.csproj

# Business references Data and Models
dotnet add WileyWidget.Business/WileyWidget.Business.csproj reference WileyWidget.Data/WileyWidget.Data.csproj
dotnet add WileyWidget.Business/WileyWidget.Business.csproj reference WileyWidget.Models/WileyWidget.Models.csproj

# WPF references Business only
dotnet add WileyWidget.csproj reference WileyWidget.Business/WileyWidget.Business.csproj
```

### **Step 4: Fix Integration Tests** (30 min)
```bash
# Update integration test project
dotnet add WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj reference WileyWidget.Data/WileyWidget.Data.csproj
dotnet add WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj reference WileyWidget.Models/WileyWidget.Models.csproj

# Remove existing tests compilation exclusion
# Edit WileyWidget.IntegrationTests.csproj - remove:
#   <Compile Remove="**\*.cs" />
#   <None Include="**\*.cs" />
```

### **Step 5: Build & Test** (15 min)
```bash
dotnet build
dotnet test
```

---

## 📚 **References**

- [Testing in .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm)
- [N-Tier Data Applications](https://learn.microsoft.com/en-us/visualstudio/data-tools/n-tier-data-applications-overview)
- [Creating Business Logic Layer](https://learn.microsoft.com/en-us/aspnet/web-forms/overview/data-access/introduction/creating-a-business-logic-layer-cs)
- [Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

---

## ⏱️ **Estimated Time**

- **Quick Fix** (Tests working): 2-3 hours
- **Full Architecture**: 6-8 hours
- **All Tests Migrated**: 8-10 hours

---

## 🎓 **Key Takeaway**

> **Never reference a WPF project from test projects!**
>
> Instead, extract business logic and data access into separate .NET 8.0 class libraries, then test those directly.

This is the **industry standard** approach used by Microsoft and enterprise applications worldwide.
