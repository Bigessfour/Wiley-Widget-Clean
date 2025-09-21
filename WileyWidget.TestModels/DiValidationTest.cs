using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Services;

namespace WileyWidget.TestModels
{
    // Small console program to validate DI container composition and scope validation
    internal static class DiValidationTest
    {
        public static int Main()
        {
            try
            {
                var services = new ServiceCollection();

                // Minimal configuration
                var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();
                var configuration = configBuilder.Build();
                services.AddSingleton<IConfiguration>(configuration);

                // Logging
                services.AddLogging(builder => builder.AddConsole());

                // App services used during startup
                services.AddSingleton<AuthenticationService>();

                // Add database services using existing helper to mirror actual startup
                services.AddEnterpriseDatabaseServices(configuration);

                // Add HttpClient support (the csproj now includes Microsoft.Extensions.Http)
                services.AddHttpClient();

                // Health check configuration and service
                services.AddSingleton<Models.HealthCheckConfiguration>();
                services.AddSingleton<Services.HealthCheckService>();

                // Build provider with validation
                using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

                Console.WriteLine("DI validation succeeded");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"DI validation failed: {ex}");
                return 2;
            }
        }
    }
}
