#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data
{
    /// <summary>
    /// Repository implementation for MunicipalAccount data operations
    /// </summary>
    public class MunicipalAccountRepository : IMunicipalAccountRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public MunicipalAccountRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<List<MunicipalAccount>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<List<MunicipalAccount>> GetActiveAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<List<MunicipalAccount>> GetByFundAsync(FundType fund)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.Fund == fund && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<List<MunicipalAccount>> GetByTypeAsync(AccountType type)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.Type == type && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<MunicipalAccount?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts.FindAsync(id);
        }

        public async Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .FirstOrDefaultAsync(ma => ma.AccountNumber == accountNumber);
        }

        public async Task<MunicipalAccount> AddAsync(MunicipalAccount account)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.MunicipalAccounts.Add(account);
            await context.SaveChangesAsync();
            return account;
        }

        public async Task<MunicipalAccount> UpdateAsync(MunicipalAccount account)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.MunicipalAccounts.Update(account);
            await context.SaveChangesAsync();
            return account;
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var account = await context.MunicipalAccounts.FindAsync(id);
            if (account != null)
            {
                context.MunicipalAccounts.Remove(account);
                await context.SaveChangesAsync();
            }
        }

        public async Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            foreach (var qbAccount in qbAccounts)
            {
                var existingAccount = await GetByAccountNumberAsync(qbAccount.AcctNum ?? string.Empty);

                if (existingAccount == null)
                {
                    // Create new account
                    var newAccount = new MunicipalAccount
                    {
                        AccountNumber = qbAccount.AcctNum ?? $"QB-{qbAccount.Id}",
                        Name = qbAccount.Name,
                        Type = MapQuickBooksAccountType(qbAccount.AccountType),
                        Fund = DetermineFundFromAccount(qbAccount),
                        Balance = qbAccount.CurrentBalance,
                        QuickBooksId = qbAccount.Id,
                        LastSyncDate = DateTime.UtcNow,
                        IsActive = qbAccount.Active
                    };
                    await AddAsync(newAccount);
                }
                else
                {
                    // Update existing account
                    existingAccount.Name = qbAccount.Name;
                    existingAccount.Balance = qbAccount.CurrentBalance;
                    existingAccount.LastSyncDate = DateTime.UtcNow;
                    existingAccount.IsActive = qbAccount.Active;
                    await UpdateAsync(existingAccount);
                }
            }
        }

        public async Task<List<MunicipalAccount>> GetBudgetAnalysisAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        private AccountType MapQuickBooksAccountType(Intuit.Ipp.Data.AccountTypeEnum? qbType)
        {
            if (qbType == null) return AccountType.Asset;

            // Try to map based on the enum value name
            var typeName = qbType.ToString();
            return typeName switch
            {
                "Asset" => AccountType.Asset,
                "Liability" => AccountType.Liability,
                "Equity" => AccountType.Equity,
                "Revenue" => AccountType.Revenue,
                "Expense" => AccountType.Expense,
                _ => AccountType.Asset // Default to Asset for unknown types
            };
        }

        private FundType DetermineFundFromAccount(Intuit.Ipp.Data.Account qbAccount)
        {
            // Simple logic to determine fund based on account number or name
            // This can be enhanced based on specific municipal accounting practices
            var accountNumber = qbAccount.AcctNum?.ToLower(System.Globalization.CultureInfo.InvariantCulture) ?? "";
            var accountName = qbAccount.Name?.ToLower(System.Globalization.CultureInfo.InvariantCulture) ?? "";

            if (accountNumber.Contains("water") || accountName.Contains("water"))
                return FundType.Water;
            if (accountNumber.Contains("sewer") || accountName.Contains("sewer"))
                return FundType.Sewer;
            if (accountNumber.Contains("trash") || accountName.Contains("trash") || accountName.Contains("garbage"))
                return FundType.Trash;

            // Check for enterprise fund indicators
            if (accountNumber.StartsWith("4") || accountNumber.StartsWith("5") ||
                accountName.Contains("enterprise") || accountName.Contains("utility"))
                return FundType.Enterprise;

            return FundType.General;
        }
    }
}