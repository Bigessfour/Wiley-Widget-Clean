using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Configuration;
using Xunit;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;

namespace WileyWidget.Tests
{
    public class DiRegistrationTests
    {
        [Fact]
        public void EnterpriseServices_AreRegisteredAsScoped()
        {
            var services = new ServiceCollection();

            var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

            // Call the extension to register services
            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, inMemoryConfig);

            var provider = services.BuildServiceProvider();

            // Verify registrations exist
            var qbDescriptor = services.FirstOrDefault(s => s.ServiceType.Name == "IQuickBooksService");
            var aiDescriptor = services.FirstOrDefault(s => s.ServiceType.Name == "IAIService");
            var enterpriseRepo = services.FirstOrDefault(s => s.ServiceType.Name == "IEnterpriseRepository");

            Assert.NotNull(qbDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, qbDescriptor.Lifetime);

            Assert.NotNull(aiDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, aiDescriptor.Lifetime);

            Assert.NotNull(enterpriseRepo);
            Assert.Equal(ServiceLifetime.Scoped, enterpriseRepo.Lifetime);
        }

        [Fact]
        public void EnterpriseRepositories_AreRegisteredAsScoped()
        {
            var services = new ServiceCollection();
            var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, inMemoryConfig);

            var enterpriseRepoDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IEnterpriseRepository));
            var municipalAccountRepoDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IMunicipalAccountRepository));
            var utilityCustomerRepoDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IUtilityCustomerRepository));

            Assert.NotNull(enterpriseRepoDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, enterpriseRepoDescriptor.Lifetime);
            Assert.Equal(typeof(EnterpriseRepository), enterpriseRepoDescriptor.ImplementationType);

            Assert.NotNull(municipalAccountRepoDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, municipalAccountRepoDescriptor.Lifetime);
            Assert.Equal(typeof(MunicipalAccountRepository), municipalAccountRepoDescriptor.ImplementationType);

            Assert.NotNull(utilityCustomerRepoDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, utilityCustomerRepoDescriptor.Lifetime);
            Assert.Equal(typeof(UtilityCustomerRepository), utilityCustomerRepoDescriptor.ImplementationType);
        }

        [Fact]
        public void DbContextFactory_IsRegisteredAsSingleton()
        {
            var services = new ServiceCollection();
            var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, inMemoryConfig);

            var dbContextFactoryDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IDbContextFactory<AppDbContext>));
            var dbContextDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(AppDbContext));

            Assert.NotNull(dbContextFactoryDescriptor);
            Assert.Equal(ServiceLifetime.Singleton, dbContextFactoryDescriptor.Lifetime);

            Assert.NotNull(dbContextDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, dbContextDescriptor.Lifetime);
        }

        [Fact]
        public void SingletonServices_AreRegisteredCorrectly()
        {
            var services = new ServiceCollection();
            var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, inMemoryConfig);

            var chargeCalculatorDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IChargeCalculatorService));
            var whatIfEngineDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IWhatIfScenarioEngine));
            var healthCheckConfigDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(WileyWidget.Models.HealthCheckConfiguration));

            Assert.NotNull(chargeCalculatorDescriptor);
            Assert.Equal(ServiceLifetime.Singleton, chargeCalculatorDescriptor.Lifetime);
            Assert.Equal(typeof(ServiceChargeCalculatorService), chargeCalculatorDescriptor.ImplementationType);

            Assert.NotNull(whatIfEngineDescriptor);
            Assert.Equal(ServiceLifetime.Singleton, whatIfEngineDescriptor.Lifetime);
            Assert.Equal(typeof(WhatIfScenarioEngine), whatIfEngineDescriptor.ImplementationType);

            Assert.NotNull(healthCheckConfigDescriptor);
            Assert.Equal(ServiceLifetime.Singleton, healthCheckConfigDescriptor.Lifetime);
        }

        [Fact]
        public void UnitOfWork_IsRegisteredAsScoped()
        {
            var services = new ServiceCollection();
            var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, inMemoryConfig);

            var unitOfWorkDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IUnitOfWork));

            Assert.NotNull(unitOfWorkDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, unitOfWorkDescriptor.Lifetime);
            Assert.Equal(typeof(UnitOfWork), unitOfWorkDescriptor.ImplementationType);
        }

        [Fact]
        public void Services_CanBeResolvedFromContainer_WithoutDatabase()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:"
                })
                .Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, config);
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging();

            var provider = services.BuildServiceProvider();

            // Test that non-database services can be resolved
            var chargeCalculator = provider.GetService<IChargeCalculatorService>();
            var whatIfEngine = provider.GetService<IWhatIfScenarioEngine>();
            var healthCheckConfig = provider.GetService<WileyWidget.Models.HealthCheckConfiguration>();

            Assert.NotNull(chargeCalculator);
            Assert.NotNull(whatIfEngine);
            Assert.NotNull(healthCheckConfig);
        }

        [Fact]
        public void ScopedServices_HaveCorrectLifetimeBehavior()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:"
                })
                .Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, config);
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging();

            var provider = services.BuildServiceProvider();

            // Test singleton services (don't require database)
            var chargeCalc1 = provider.GetService<IChargeCalculatorService>();
            var whatIfEngine1 = provider.GetService<IWhatIfScenarioEngine>();

            using (var scope1 = provider.CreateScope())
            {
                var chargeCalc2 = scope1.ServiceProvider.GetService<IChargeCalculatorService>();
                var whatIfEngine2 = scope1.ServiceProvider.GetService<IWhatIfScenarioEngine>();

                Assert.Same(chargeCalc1, chargeCalc2); // Same instance across scopes
                Assert.Same(whatIfEngine1, whatIfEngine2); // Same instance across scopes
            }
        }

        [Fact]
        public void SingletonServices_HaveCorrectLifetimeBehavior()
        {
            var services = new ServiceCollection();
            var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, inMemoryConfig);
            services.AddSingleton<IConfiguration>(inMemoryConfig);
            services.AddLogging();

            var provider = services.BuildServiceProvider();

            // Test singleton services across different scopes
            var chargeCalc1 = provider.GetService<IChargeCalculatorService>();
            var whatIfEngine1 = provider.GetService<IWhatIfScenarioEngine>();

            using (var scope1 = provider.CreateScope())
            {
                var chargeCalc2 = scope1.ServiceProvider.GetService<IChargeCalculatorService>();
                var whatIfEngine2 = scope1.ServiceProvider.GetService<IWhatIfScenarioEngine>();

                Assert.Same(chargeCalc1, chargeCalc2); // Same instance across scopes
                Assert.Same(whatIfEngine1, whatIfEngine2); // Same instance across scopes
            }

            using (var scope2 = provider.CreateScope())
            {
                var chargeCalc3 = scope2.ServiceProvider.GetService<IChargeCalculatorService>();
                var whatIfEngine3 = scope2.ServiceProvider.GetService<IWhatIfScenarioEngine>();

                Assert.Same(chargeCalc1, chargeCalc3); // Same instance across all scopes
                Assert.Same(whatIfEngine1, whatIfEngine3); // Same instance across all scopes
            }
        }

        [Fact]
        public async Task AIService_ReturnsStubService_WhenApiKeyNotConfigured()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["XAI:ApiKey"] = "",
                    ["XAI:RequireService"] = "false"
                })
                .Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, config);
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging();

            var provider = services.BuildServiceProvider();
            var aiService = provider.GetService<IAIService>();

            Assert.NotNull(aiService);

            // Test that it returns stub responses
            var result = await aiService.GetInsightsAsync("test", "test");
            Assert.Contains("[Dev Stub]", result);
        }

        [Fact]
        public async Task AIService_ReturnsStubService_WhenRequiredButNotConfigured()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["XAI:ApiKey"] = "",
                    ["XAI:RequireService"] = "true"
                })
                .Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, config);
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging();

            var provider = services.BuildServiceProvider();
            var aiService = provider.GetService<IAIService>();

            // Should still register stub service even when required
            Assert.NotNull(aiService);

            // Test that it returns stub responses
            var result = await aiService.GetInsightsAsync("test", "test");
            Assert.Contains("[Dev Stub]", result);
        }

        [Fact]
        public void ContainerValidation_Succeeds_WithValidConfiguration()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, config);
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging();

            // Build with validation
            var options = new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            };

            using var provider = services.BuildServiceProvider(options);

            // Should not throw any exceptions
            Assert.NotNull(provider);
        }

        [Fact]
        public void AllServices_HaveValidDependencies()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:"
                })
                .Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, config);
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging();

            var provider = services.BuildServiceProvider();

            // Test that non-database services can be instantiated
            var serviceTypes = services
                .Where(s => s.ServiceType != typeof(IDbContextFactory<AppDbContext>) &&
                           s.ServiceType != typeof(AppDbContext) &&
                           !s.ServiceType.Name.Contains("Repository") &&
                           s.ServiceType != typeof(IQuickBooksService) &&
                           s.ServiceType != typeof(IAIService) &&
                           s.ServiceType != typeof(IUnitOfWork) &&
                           !s.ServiceType.Name.StartsWith("IOptions") &&
                           !s.ServiceType.Name.StartsWith("ILogger"))
                .Select(s => s.ServiceType)
                .Distinct();

            foreach (var serviceType in serviceTypes)
            {
                try
                {
                    var instance = provider.GetService(serviceType);
                    Assert.NotNull(instance);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed to resolve service {serviceType.Name}: {ex.Message}");
                }
            }
        }

        [Fact]
        public void HealthCheckConfiguration_IsRegisteredAsSingleton()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

            DatabaseConfiguration.AddEnterpriseDatabaseServices(services, config);

            var healthCheckConfigDescriptor = services.FirstOrDefault(s =>
                s.ServiceType == typeof(WileyWidget.Models.HealthCheckConfiguration));

            Assert.NotNull(healthCheckConfigDescriptor);
            Assert.Equal(ServiceLifetime.Singleton, healthCheckConfigDescriptor.Lifetime);
        }
    }
}
