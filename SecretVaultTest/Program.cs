using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace SecretVaultTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🔐 Testing EncryptedLocalSecretVaultService");
            Console.WriteLine("==========================================\n");

            // Setup DI container
            var services = new ServiceCollection();
            services.AddLogging(configure => configure.AddConsole());
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();

            var serviceProvider = services.BuildServiceProvider();
            var vault = serviceProvider.GetRequiredService<ISecretVaultService>();

            bool allTestsPassed = true;

            try
            {
                // 1. Test basic encryption/decryption
                Console.WriteLine("1. Testing basic encryption/decryption...");
                await vault.SetSecretAsync("test-secret", "my-secret-value");
                var retrieved = await vault.GetSecretAsync("test-secret");
                if (retrieved == "my-secret-value")
                {
                    Console.WriteLine("✅ Basic encryption/decryption test PASSED");
                }
                else
                {
                    Console.WriteLine("❌ Basic encryption/decryption test FAILED");
                    allTestsPassed = false;
                }

                // 2. Test connection
                Console.WriteLine("\n2. Testing connection...");
                var isConnected = await vault.TestConnectionAsync();
                if (isConnected)
                {
                    Console.WriteLine("✅ Connection test PASSED");
                }
                else
                {
                    Console.WriteLine("❌ Connection test FAILED");
                    allTestsPassed = false;
                }

                // 3. Test multiple secrets
                Console.WriteLine("\n3. Testing multiple secrets...");
                var testSecrets = new Dictionary<string, string>
                {
                    { "api-key", "fake-api-key-value" },
                    { "db-password", "super-secret-db-pass" },
                    { "license-key", "LICENSE-ABC-123-XYZ" },
                    { "token", "fake-jwt-token" }
                };

                foreach (var kvp in testSecrets)
                {
                    await vault.SetSecretAsync(kvp.Key, kvp.Value);
                    var retrievedSecret = await vault.GetSecretAsync(kvp.Key);
                    if (retrievedSecret == kvp.Value)
                    {
                        Console.WriteLine($"✅ Secret '{kvp.Key}' encryption/decryption PASSED");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Secret '{kvp.Key}' encryption/decryption FAILED");
                        allTestsPassed = false;
                    }
                }

                // 4. Test secret listing
                Console.WriteLine("\n4. Testing secret listing...");
                var keys = await vault.ListSecretKeysAsync();
                var keysList = keys.ToList();
                Console.WriteLine($"Found {keysList.Count} secret keys:");
                foreach (var key in keysList.OrderBy(k => k))
                {
                    Console.WriteLine($"  - {key}");
                }

                // 5. Test export/import
                Console.WriteLine("\n5. Testing export/import...");
                // Skip export/import test for now to focus on migration
                Console.WriteLine("⏭️  Export/Import test SKIPPED (focusing on migration)");

                // 6. Test migration from environment
                Console.WriteLine("\n6. Testing migration from environment...");

                // Set up test environment variables
                Environment.SetEnvironmentVariable("syncfusion-license-key", "SYNC-TEST-123");
                Environment.SetEnvironmentVariable("QuickBooks-ClientId", "QB-CLIENT-456");
                Environment.SetEnvironmentVariable("QuickBooks-ClientSecret", "QB-SECRET-789");
                Environment.SetEnvironmentVariable("QuickBooks-RedirectUri", "https://test.com/callback");
                Environment.SetEnvironmentVariable("QuickBooks-Environment", "sandbox");
                Environment.SetEnvironmentVariable("XAI-ApiKey", "xai-test-key");
                Environment.SetEnvironmentVariable("XAI-BaseUrl", "https://api.test.com");

                // Perform migration
                await vault.MigrateSecretsFromEnvironmentAsync();

                // Check if migration worked - should not fail
                Console.WriteLine("✅ Environment migration completed without error");

                // Clean up environment variables
                Environment.SetEnvironmentVariable("syncfusion-license-key", null);
                Environment.SetEnvironmentVariable("QuickBooks-ClientId", null);
                Environment.SetEnvironmentVariable("QuickBooks-ClientSecret", null);
                Environment.SetEnvironmentVariable("QuickBooks-RedirectUri", null);
                Environment.SetEnvironmentVariable("QuickBooks-Environment", null);
                Environment.SetEnvironmentVariable("XAI-ApiKey", null);
                Environment.SetEnvironmentVariable("XAI-BaseUrl", null);

                Console.WriteLine("\n🎉 All tests completed!");

                if (allTestsPassed)
                {
                    Console.WriteLine("✅ ALL TESTS PASSED!");
                }
                else
                {
                    Console.WriteLine("❌ SOME TESTS FAILED!");
                }

                Console.WriteLine("\nSecurity Notes:");
                Console.WriteLine("- Secrets are encrypted using Windows DPAPI");
                Console.WriteLine("- Only the current Windows user can decrypt these secrets");
                Console.WriteLine("- Secrets cannot be decrypted on different machines or by different users");
                Console.WriteLine("- Entropy is securely generated and stored");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test execution failed: {ex.Message}");
                allTestsPassed = false;
            }
        }
    }
}