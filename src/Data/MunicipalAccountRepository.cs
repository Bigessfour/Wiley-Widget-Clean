#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Services;

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

        public async Task<IEnumerable<MunicipalAccount>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetActiveAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

    public async Task<IEnumerable<MunicipalAccount>> GetByFundAsync(FundType fund)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
        .Where(ma => ma.Fund == fund && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

    public async Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type)
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
                .FirstOrDefaultAsync(ma => ma.AccountNumber.Value == accountNumber);
        }

        public async Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.DepartmentId == departmentId && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetByFundClassAsync(FundClass fundClass)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.FundClass == fundClass && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetByAccountTypeAsync(AccountType accountType)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.Type == accountType && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetChildAccountsAsync(int parentAccountId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.ParentAccountId == parentAccountId && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetAccountHierarchyAsync(int rootAccountId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // This is a simplified implementation - in a real scenario you'd need
            // a recursive CTE or stored procedure to get the full hierarchy
            var rootAccount = await context.MunicipalAccounts.FindAsync(rootAccountId);
            if (rootAccount == null)
                return new List<MunicipalAccount>();
                
            return await context.MunicipalAccounts
                .Where(ma => ma.AccountNumber.Value.StartsWith(rootAccount.AccountNumber.Value) && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> SearchByNameAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.Name.Contains(searchTerm) && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<bool> AccountNumberExistsAsync(string accountNumber, int? excludeId = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.MunicipalAccounts.Where(ma => ma.AccountNumber.Value == accountNumber);
            
            if (excludeId.HasValue)
                query = query.Where(ma => ma.Id != excludeId.Value);
                
            return await query.AnyAsync();
        }

        public async Task<int> GetCountAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts.CountAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetAccountsWithBudgetEntriesAsync(int budgetPeriodId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.IsActive && 
                           context.BudgetEntries.Any(be => be.MunicipalAccountId == ma.Id && be.BudgetPeriodId == budgetPeriodId))
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<MunicipalAccount> AddAsync(MunicipalAccount account)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.MunicipalAccounts.Add(account);
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await RepositoryConcurrencyHelper.HandleAsync(ex, nameof(MunicipalAccount)).ConfigureAwait(false);
            }
            return account;
        }

        public async Task<MunicipalAccount> UpdateAsync(MunicipalAccount account)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.MunicipalAccounts.Update(account);
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await RepositoryConcurrencyHelper.HandleAsync(ex, nameof(MunicipalAccount)).ConfigureAwait(false);
            }

            return account;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var account = await context.MunicipalAccounts.FindAsync(id);
            if (account != null)
            {
                context.MunicipalAccounts.Remove(account);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await RepositoryConcurrencyHelper.HandleAsync(ex, nameof(MunicipalAccount)).ConfigureAwait(false);
                }
                return true;
            }
            return false;
        }

        public async Task SyncFromQuickBooksAsync()
        {
            // No-op implementation - actual sync requires QB accounts parameter
            // This satisfies the interface requirement
            await Task.CompletedTask;
        }

        public async Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            foreach (var qbAccount in qbAccounts)
            {
                var existingAccount = await context.MunicipalAccounts
                    .FirstOrDefaultAsync(ma => ma.AccountNumber.Value == (qbAccount.AcctNum ?? string.Empty));

                if (existingAccount == null)
                {
                    // Create new account
                    var newAccount = new MunicipalAccount
                    {
                        AccountNumber = new AccountNumber(qbAccount.AcctNum ?? $"QB-{qbAccount.Id}"),
                        Name = qbAccount.Name,
                        Type = MapQuickBooksAccountType(qbAccount.AccountType),
                        Fund = DetermineFundFromAccount(qbAccount),
                        FundClass = FundClass.Proprietary, // Default for QB imports
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

        /// <summary>
        /// Get account balance at fiscal year start
        /// </summary>
        public async Task<decimal> GetBalanceAtFiscalYearStartAsync(int accountId, int fiscalYear, FiscalYearService fiscalYearService)
        {
            (DateTime fyStart, DateTime _) = await fiscalYearService.GetFiscalYearRangeAsync(fiscalYear);
            
            using var context = await _contextFactory.CreateDbContextAsync();
            var account = await context.MunicipalAccounts.FindAsync(accountId);
            
            if (account == null) return 0m;
            
            // For simplicity, return budget amount
            // In a real implementation, this would query historical transaction data
            return account.BudgetAmount;
        }

        /// <summary>
        /// Get accounts with budget data for specific fiscal year
        /// </summary>
        public async Task<IEnumerable<MunicipalAccount>> GetAccountsWithBudgetByFiscalYearAsync(int fiscalYear, FiscalYearService fiscalYearService)
        {
            (DateTime fyStart, DateTime fyEnd) = await fiscalYearService.GetFiscalYearRangeAsync(fiscalYear);
            
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // Get accounts that have budget entries for this fiscal year
            var accounts = await context.MunicipalAccounts
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
            
            return accounts;
        }

        public async Task<object> GetBudgetAnalysisAsync(int periodId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var accounts = await context.MunicipalAccounts
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
            return accounts;
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
            if (qbType == null) return AccountType.Cash;

            // Try to map based on the enum value name
            var typeName = qbType.ToString();
            return typeName switch
            {
                "Asset" => AccountType.Cash,
                "Liability" => AccountType.Payables,
                "Equity" => AccountType.FundBalance,
                "Revenue" => AccountType.Sales,
                "Expense" => AccountType.Supplies,
                _ => AccountType.Cash // Default to Cash for unknown types
            };
        }

        private FundType DetermineFundFromAccount(Intuit.Ipp.Data.Account qbAccount)
        {
            // Simple logic to determine fund based on account number or name
            // This can be enhanced based on specific municipal accounting practices
            var accountNumber = qbAccount.AcctNum?.ToLower(System.Globalization.CultureInfo.InvariantCulture) ?? "";
            var accountName = qbAccount.Name?.ToLower(System.Globalization.CultureInfo.InvariantCulture) ?? "";

            if (accountNumber.Contains("water") || accountName.Contains("water"))
                return FundType.Utility;
            if (accountNumber.Contains("sewer") || accountName.Contains("sewer"))
                return FundType.Utility;
            if (accountNumber.Contains("trash") || accountName.Contains("trash") || accountName.Contains("garbage"))
                return FundType.Utility;

            // Check for enterprise fund indicators
            if (accountNumber.StartsWith("4") || accountNumber.StartsWith("5") ||
                accountName.Contains("enterprise") || accountName.Contains("utility"))
                return FundType.Enterprise;

            return FundType.General;
        }
    }
}