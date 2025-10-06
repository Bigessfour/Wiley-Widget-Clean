using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Configuration;
using Xunit;

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
    }
}
