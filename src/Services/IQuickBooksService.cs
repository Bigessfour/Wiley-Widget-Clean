using System.Collections.Generic;
using System.Threading.Tasks;
using Intuit.Ipp.Data;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for QuickBooks integration operations
    /// </summary>
    public interface IQuickBooksService
    {
        Task<bool> TestConnectionAsync();
        Task<List<Customer>> GetCustomersAsync();
        Task<List<Invoice>> GetInvoicesAsync(string enterprise = null);
        Task<List<Account>> GetChartOfAccountsAsync();
        Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate);
        Task<List<Budget>> GetBudgetsAsync();
    }
}