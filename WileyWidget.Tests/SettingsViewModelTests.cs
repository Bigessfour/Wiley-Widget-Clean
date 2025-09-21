using Moq;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;
using System.Threading;
using System.Windows.Media;

namespace WileyWidget.Tests.ViewModels
{
    public class SettingsViewModelTests : IDisposable
    {
        private readonly Mock<AppDbContext> _mockDbContext;
        private readonly Mock<IAzureKeyVaultService> _mockAzureService;
        private readonly Mock<IQuickBooksService> _mockQuickBooksService;
        private readonly Mock<ISyncfusionLicenseService> _mockSyncfusionService;
        private readonly Mock<ILogger<SettingsViewModel>> _mockLogger;
        private readonly SettingsViewModel _viewModel;
        private bool _disposed;

        public SettingsViewModelTests()
        {
            // Create mock DbContext for testing
            _mockDbContext = new Mock<AppDbContext>();

            // Mock the Database property to avoid DatabaseFacade proxy issues
            var mockDatabaseFacade = new Mock<DatabaseFacade>(_mockDbContext.Object);
            mockDatabaseFacade.Setup(db => db.CanConnectAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockDbContext.Setup(db => db.Database).Returns(mockDatabaseFacade.Object);

            _mockAzureService = new Mock<IAzureKeyVaultService>();
            _mockQuickBooksService = new Mock<IQuickBooksService>();
            _mockSyncfusionService = new Mock<ISyncfusionLicenseService>();
            _mockLogger = new Mock<ILogger<SettingsViewModel>>();

            _viewModel = new SettingsViewModel(
                _mockLogger.Object,
                _mockDbContext.Object,
                _mockAzureService.Object,
                _mockQuickBooksService.Object,
                _mockSyncfusionService.Object);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // No need to dispose mock DbContext
                }
                _disposed = true;
            }
        }

        [Fact]
        public void Constructor_WithValidDependencies_DoesNotThrow()
        {
            // Assert - if we get here without exception, constructor worked
            Assert.NotNull(_viewModel);
        }

        [Fact]
        public void Constructor_WithNullQuickBooksService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(
                _mockLogger.Object,
                _mockDbContext.Object,
                _mockAzureService.Object,
                null, // null QuickBooks service
                _mockSyncfusionService.Object));
        }

        [Fact]
        public async Task LoadAzureSettingsAsync_SuccessfulConnection_UpdatesStatus()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AZURE_KEY_VAULT_URL", "https://test.vault.azure.net");
            Environment.SetEnvironmentVariable("AZURE_SQL_SERVER", "test-server.database.windows.net");
            Environment.SetEnvironmentVariable("AZURE_SQL_DATABASE", "test-db");

            // Act
            await _viewModel.LoadSettingsAsync();

            // Assert
            Assert.Equal("Configured", _viewModel.AzureConnectionStatus);
            Assert.Equal(Brushes.Orange, _viewModel.AzureStatusColor);

            // Cleanup
            Environment.SetEnvironmentVariable("AZURE_KEY_VAULT_URL", null);
            Environment.SetEnvironmentVariable("AZURE_SQL_SERVER", null);
            Environment.SetEnvironmentVariable("AZURE_SQL_DATABASE", null);
        }

        [Fact]
        public async Task SaveSyncfusionSettingsAsync_SavesLicenseKey()
        {
            // Arrange
            _viewModel.SyncfusionLicenseKey = "test-license-key";
            _mockAzureService.Setup(x => x.SetSecretAsync("Syncfusion-LicenseKey", "test-license-key"))
                .Returns(Task.CompletedTask);

            // Act
            await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            _mockAzureService.Verify(x => x.SetSecretAsync("Syncfusion-LicenseKey", "test-license-key"), Times.Once);
        }

        [Fact]
        public void OnPropertyChanged_WithUntrackedProperty_DoesNotSetHasUnsavedChanges()
        {
            // Arrange - Create a fresh view model to ensure clean state
            var freshViewModel = new SettingsViewModel(
                _mockLogger.Object,
                _mockDbContext.Object,
                _mockAzureService.Object,
                _mockQuickBooksService.Object,
                _mockSyncfusionService.Object);

            // Act - trigger property change for a tracked property (SettingsStatus)
            freshViewModel.SettingsStatus = "Test Status";

            // Assert - HasUnsavedChanges should remain false for tracked properties
            Assert.False(freshViewModel.HasUnsavedChanges);
        }
    }
}