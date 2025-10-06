using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Intuit.Ipp.Data;

namespace WileyWidget.Services;

/// <summary>
/// Mock QuickBooks data service for testing when API keys are not configured
/// Generates realistic municipal utility data for Wiley Widget testing
/// </summary>
public class MockQuickBooksDataService
{
    private readonly Random _random = new Random(42); // Fixed seed for consistent test data

    /// <summary>
    /// Generates mock customers representing municipal utility customers
    /// </summary>
    public List<Customer> GenerateMockCustomers(int count = 50)
    {
        var customers = new List<Customer>();
        var firstNames = new[] { "John", "Mary", "Robert", "Jennifer", "Michael", "Lisa", "David", "Sarah", "James", "Patricia" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Davis", "Garcia", "Miller", "Wilson", "Moore", "Taylor" };
        var streets = new[] { "Main", "Oak", "Pine", "Elm", "Maple", "Cedar", "Birch", "Spruce", "Willow", "Ash" };
        var cities = new[] { "Springfield", "Riverside", "Oakwood", "Pineville", "Mapleton" };

        for (int i = 0; i < count; i++)
        {
            var firstName = firstNames[_random.Next(firstNames.Length)];
            var lastName = lastNames[_random.Next(lastNames.Length)];
            var street = streets[_random.Next(streets.Length)];
            var city = cities[_random.Next(cities.Length)];

            var customer = new Customer
            {
                Id = $"QB-CUST-{i + 1:D4}",
                GivenName = firstName,
                FamilyName = lastName,
                FullyQualifiedName = $"{firstName} {lastName}",
                CompanyName = _random.Next(3) == 0 ? $"{lastName} Enterprises" : null, // 33% commercial
                PrimaryEmailAddr = new EmailAddress
                {
                    Address = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.com"
                },
                PrimaryPhone = new TelephoneNumber
                {
                    FreeFormNumber = $"({555 + _random.Next(100):000}) {_random.Next(100, 1000):000}-{_random.Next(1000, 10000):0000}"
                },
                BillAddr = new PhysicalAddress
                {
                    Line1 = $"{100 + i * 7} {street} Street",
                    City = city,
                    CountrySubDivisionCode = "IL",
                    PostalCode = $"6270{_random.Next(10)}"
                },
                Active = true,
                CustomerTypeRef = new ReferenceType
                {
                    name = _random.Next(2) == 0 ? "Residential" : "Commercial",
                    Value = (_random.Next(2) + 1).ToString()
                }
            };

            customers.Add(customer);
        }

        return customers;
    }

    /// <summary>
    /// Generates mock invoices for municipal utility billing
    /// </summary>
    public List<Invoice> GenerateMockInvoices(List<Customer> customers, int count = 100)
    {
        var invoices = new List<Invoice>();
        var services = new[]
        {
            ("Water Service", 25.00m, 75.00m),
            ("Sewer Service", 15.00m, 45.00m),
            ("Trash Collection", 12.00m, 36.00m),
            ("Water & Sewer", 35.00m, 105.00m),
            ("Utility Bundle", 45.00m, 135.00m)
        };

        for (int i = 0; i < count; i++)
        {
            var customer = customers[_random.Next(customers.Count)];
            var service = services[_random.Next(services.Length)];
            var baseAmount = service.Item2 + (decimal)(_random.NextDouble() * (double)(service.Item3 - service.Item2));
            var amount = Math.Round((decimal)baseAmount, 2);

            var invoiceDate = DateTime.Now.AddDays(-_random.Next(90)); // Last 90 days
            var dueDate = invoiceDate.AddDays(30);

            var invoice = new Invoice
            {
                Id = $"QB-INV-{i + 1:D4}",
                DocNumber = $"INV-{(DateTime.Now.Year % 100):00}{i + 1:D4}",
                TxnDate = invoiceDate,
                DueDate = dueDate,
                CustomerRef = new ReferenceType
                {
                    name = customer.FullyQualifiedName,
                    Value = customer.Id
                },
                TotalAmt = amount,
                Balance = _random.Next(2) == 0 ? 0 : amount, // 50% paid, 50% outstanding
                EmailStatus = EmailStatusEnum.NotSet,
                Line = new List<Line>
                {
                    new Line
                    {
                        Id = "1",
                        LineNum = "1",
                        Description = service.Item1,
                        Amount = amount,
                        DetailType = LineDetailTypeEnum.SalesItemLineDetail,
                        DetailTypeSpecified = true,
                        AnyIntuitObject = new SalesItemLineDetail
                        {
                            ItemRef = new ReferenceType { name = service.Item1, Value = "1" }
                        }
                    }
                }.ToArray()
            };

            invoices.Add(invoice);
        }

        return invoices.OrderByDescending(i => i.TxnDate).ToList();
    }

    /// <summary>
    /// Generates mock chart of accounts for municipal utilities
    /// </summary>
    public List<Account> GenerateMockChartOfAccounts()
    {
        var accounts = new List<Account>
        {
            // Assets
            new Account { Id = "1", Name = "Cash", AccountType = AccountTypeEnum.Bank, Active = true },
            new Account { Id = "2", Name = "Accounts Receivable", AccountType = AccountTypeEnum.AccountsReceivable, Active = true },
            new Account { Id = "3", Name = "Inventory", AccountType = AccountTypeEnum.OtherCurrentAsset, Active = true },
            new Account { Id = "4", Name = "Fixed Assets", AccountType = AccountTypeEnum.FixedAsset, Active = true },

            // Liabilities
            new Account { Id = "5", Name = "Accounts Payable", AccountType = AccountTypeEnum.AccountsPayable, Active = true },
            new Account { Id = "6", Name = "Loans Payable", AccountType = AccountTypeEnum.LongTermLiability, Active = true },

            // Equity
            new Account { Id = "7", Name = "Retained Earnings", AccountType = AccountTypeEnum.Equity, Active = true },

            // Income
            new Account { Id = "8", Name = "Water Revenue", AccountType = AccountTypeEnum.Income, Active = true },
            new Account { Id = "9", Name = "Sewer Revenue", AccountType = AccountTypeEnum.Income, Active = true },
            new Account { Id = "10", Name = "Trash Revenue", AccountType = AccountTypeEnum.Income, Active = true },
            new Account { Id = "11", Name = "Utility Revenue", AccountType = AccountTypeEnum.Income, Active = true },

            // Expenses
            new Account { Id = "12", Name = "Water Operating Expenses", AccountType = AccountTypeEnum.Expense, Active = true },
            new Account { Id = "13", Name = "Sewer Operating Expenses", AccountType = AccountTypeEnum.Expense, Active = true },
            new Account { Id = "14", Name = "Trash Operating Expenses", AccountType = AccountTypeEnum.Expense, Active = true },
            new Account { Id = "15", Name = "Maintenance Expenses", AccountType = AccountTypeEnum.Expense, Active = true },
            new Account { Id = "16", Name = "Administrative Expenses", AccountType = AccountTypeEnum.Expense, Active = true },
            new Account { Id = "17", Name = "Depreciation Expense", AccountType = AccountTypeEnum.Expense, Active = true }
        };

        return accounts;
    }

    /// <summary>
    /// Generates mock journal entries for municipal financial transactions
    /// </summary>
    public List<JournalEntry> GenerateMockJournalEntries(DateTime startDate, DateTime endDate, List<Account> accounts)
    {
        var entries = new List<JournalEntry>();
        var descriptions = new[]
        {
            "Monthly utility billing",
            "Customer payment received",
            "Equipment maintenance",
            "Salary expense",
            "Supply purchase",
            "Utility repair",
            "Administrative expense",
            "Depreciation adjustment"
        };

        var days = (endDate - startDate).Days;
        for (int i = 0; i < 50; i++)
        {
            var entryDate = startDate.AddDays(_random.Next(days));
            var description = descriptions[_random.Next(descriptions.Length)];
            var amount = (decimal)(_random.Next(100, 5000) + _random.NextDouble() * 1000);

            var entry = new JournalEntry
            {
                Id = $"QB-JE-{i + 1:D4}",
                TxnDate = entryDate,
                DocNumber = $"JE-{entryDate:yyyyMMdd}-{i + 1:D2}",
                PrivateNote = description,
                Line = new List<Line>
                {
                    new Line
                    {
                        Id = "1",
                        Description = description,
                        Amount = amount,
                        DetailType = LineDetailTypeEnum.JournalEntryLineDetail,
                        DetailTypeSpecified = true,
                        AnyIntuitObject = new JournalEntryLineDetail
                        {
                            AccountRef = new ReferenceType
                            {
                                name = accounts[_random.Next(accounts.Count)].Name,
                                Value = accounts[_random.Next(accounts.Count)].Id
                            },
                            PostingType = PostingTypeEnum.Debit,
                            PostingTypeSpecified = true
                        }
                    },
                    new Line
                    {
                        Id = "2",
                        Description = description,
                        Amount = amount,
                        DetailType = LineDetailTypeEnum.JournalEntryLineDetail,
                        DetailTypeSpecified = true,
                        AnyIntuitObject = new JournalEntryLineDetail
                        {
                            AccountRef = new ReferenceType
                            {
                                name = accounts[_random.Next(accounts.Count)].Name,
                                Value = accounts[_random.Next(accounts.Count)].Id
                            },
                            PostingType = PostingTypeEnum.Credit,
                            PostingTypeSpecified = true
                        }
                    }
                }.ToArray()
            };

            entries.Add(entry);
        }

        return entries.OrderByDescending(e => e.TxnDate).ToList();
    }
}