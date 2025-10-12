using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using WileyWidget.ViewModels;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Tests
{
    public class MainViewModelThreadingTests : IDisposable
    {
    private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepository;
    private readonly Mock<IMunicipalAccountRepository> _mockMunicipalAccountRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IQuickBooksService> _mockQuickBooksService;
        private readonly Mock<IAIService> _mockAIService;
        private readonly MainViewModel _viewModel;

        public MainViewModelThreadingTests()
        {
            _mockEnterpriseRepository = new Mock<IEnterpriseRepository>();
            _mockMunicipalAccountRepository = new Mock<IMunicipalAccountRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockQuickBooksService = new Mock<IQuickBooksService>();
            _mockAIService = new Mock<IAIService>();

            _mockEnterpriseRepository.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new System.Collections.Generic.List<Enterprise>());

            _mockUnitOfWork.SetupGet(u => u.Enterprises).Returns(_mockEnterpriseRepository.Object);
            _mockUnitOfWork.SetupGet(u => u.MunicipalAccounts).Returns(_mockMunicipalAccountRepository.Object);

            _viewModel = new MainViewModel(
                _mockUnitOfWork.Object,
                _mockQuickBooksService.Object,
                _mockAIService.Object,
                autoInitialize: false);
        }

        private static async Task InvokePrivateAsync(object instance, string methodName, params object[] parameters)
        {
            var method = instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null)
                throw new InvalidOperationException($"Private method '{methodName}' not found on type {instance.GetType().Name}");

            var result = method.Invoke(instance, parameters);
            if (result is Task t)
                await t;
        }

        [Fact]
        public async Task LoadEnterprisesAsync_CalledFromBackgroundThread_DoesNotThrow()
        {
            // Run the private LoadEnterprisesAsync on a thread-pool thread to simulate
            // background initialization. The call should not throw cross-thread
            // InvalidOperationException when updating chart/collections.
            await Task.Run(async () =>
            {
                await InvokePrivateAsync(_viewModel, "LoadEnterprisesAsync", CancellationToken.None);
            });

            // Ensure collections exist and were updated (may be empty but should be accessible)
            Assert.NotNull(_viewModel.Enterprises);
            Assert.NotNull(_viewModel.RevenueTrendData);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _viewModel?.Dispose();
            }
        }
    }
}
