using WileyWidget.Models;

namespace WileyWidget.IntegrationTests.Infrastructure;

/// <summary>
/// Provides builder methods for creating test data entities.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Creates a sample MunicipalAccount with default values.
    /// </summary>
    public static MunicipalAccount CreateMunicipalAccount(
        string accountNumber = "405.1",
        string accountName = "Test Municipal Account",
        decimal budgetAmount = 10000.00m)
    {
        return new MunicipalAccount
        {
            AccountNumber = new AccountNumber(accountNumber),
            Name = accountName,
            Balance = budgetAmount,
            Type = AccountType.Asset,
            Fund = FundType.General,
            DepartmentId = 1,
            BudgetPeriodId = 1
        };
    }

    /// <summary>
    /// Creates a sample Department.
    /// </summary>
    public static Department CreateDepartment(
        string name = "Test Department",
        string code = "TEST")
    {
        return new Department
        {
            Name = name,
            Code = code
        };
    }

    /// <summary>
    /// Creates a sample Enterprise.
    /// </summary>
    public static Enterprise CreateEnterprise(
        string name = "Test Enterprise")
    {
        return new Enterprise
        {
            Name = name,
            CreatedDate = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
    }

    /// <summary>
    /// Creates a sample BudgetEntry.
    /// </summary>
    public static BudgetEntry CreateBudgetEntry(
        int municipalAccountId,
        decimal amount = 1000.00m)
    {
        return new BudgetEntry
        {
            MunicipalAccountId = municipalAccountId,
            Amount = amount,
            YearType = YearType.Current,
            EntryType = EntryType.Budget,
            BudgetPeriodId = 1
        };
    }

    /// <summary>
    /// Creates a sample Transaction.
    /// </summary>
    public static Transaction CreateTransaction(
        int municipalAccountId,
        decimal amount,
        string description = "Test Transaction")
    {
        return new Transaction
        {
            MunicipalAccountId = municipalAccountId,
            Amount = amount,
            Description = description,
            TransactionDate = DateTime.UtcNow,
            Type = TransactionType.Debit
        };
    }

    /// <summary>
    /// Creates a sample Vendor.
    /// </summary>
    public static Vendor CreateVendor(string name = "Test Vendor")
    {
        return new Vendor
        {
            Name = name,
            IsActive = true
        };
    }

    /// <summary>
    /// Creates a sample Invoice.
    /// </summary>
    public static Invoice CreateInvoice(
        int vendorId,
        int municipalAccountId,
        decimal amount,
        string invoiceNumber = "INV-001")
    {
        return new Invoice
        {
            VendorId = vendorId,
            MunicipalAccountId = municipalAccountId,
            Amount = amount,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30)
        };
    }
}
