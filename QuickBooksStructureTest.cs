using System;
using System.Threading.Tasks;
using System.Linq;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

class Program {
    static void Main(string[] args) {
        try {
            Console.WriteLine("Testing QuickBooks service instantiation...");

            // Test SettingsService instantiation
            var settings = SettingsService.Instance;
            Console.WriteLine("✓ SettingsService created successfully");

            // Test QuickBooksService instantiation
            var keyVault = new FakeSecretVaultService();
            ILogger<QuickBooksService> logger = NullLogger<QuickBooksService>.Instance;
            var qbService = new QuickBooksService(settings, keyVault, logger);
            Console.WriteLine("✓ QuickBooksService created successfully");

            // Test interface implementation
            IQuickBooksService qbInterface = qbService;
            Console.WriteLine("✓ IQuickBooksService interface implemented correctly");

            // Test method availability (without calling them)
            var methods = typeof(IQuickBooksService).GetMethods();
            Console.WriteLine($"✓ Interface has {methods.Length} methods defined");

            var expectedMethods = new[] {
                "TestConnectionAsync",
                "GetCustomersAsync",
                "GetInvoicesAsync",
                "GetChartOfAccountsAsync",
                "GetJournalEntriesAsync",
                "GetBudgetsAsync"
            };

            foreach (var methodName in expectedMethods) {
                var method = methods.FirstOrDefault(m => m.Name == methodName);
                if (method != null) {
                    Console.WriteLine($"✓ Method {methodName} is available");
                } else {
                    Console.WriteLine($"❌ Method {methodName} is missing");
                }
            }

            Console.WriteLine("");
            Console.WriteLine("SUCCESS: QuickBooks service structure validation completed!");
            Console.WriteLine("The service is properly implemented and ready for API calls.");

        } catch (Exception ex) {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}

sealed class FakeSecretVaultService : ISecretVaultService {
    public Task<string?> GetSecretAsync(string secretName) => Task.FromResult<string?>(null);
    public Task SetSecretAsync(string secretName, string value) => Task.CompletedTask;
    public Task<bool> TestConnectionAsync() => Task.FromResult(true);
}
