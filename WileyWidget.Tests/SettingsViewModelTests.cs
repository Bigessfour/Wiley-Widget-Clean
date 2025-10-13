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
using WileyWidget.Models;
using System.Linq;

namespace WileyWidget.Tests.ViewModels
{
    public class SettingsViewModelTests : IDisposable
    {
        private readonly Mock<AppDbContext> _mockDbContext;
        private readonly Mock<ISecretVaultService> _mockSecretVaultService;
        private readonly Mock<IQuickBooksService> _mockQuickBooksService;
        private readonly Mock<ISyncfusionLicenseService> _mockSyncfusionService;
        private readonly Mock<IAIService> _mockAIService;
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

            // Mock the AppSettings DbSet
            var mockAppSettingsDbSet = new Mock<DbSet<AppSettings>>();
            _mockDbContext.Setup(db => db.AppSettings).Returns(mockAppSettingsDbSet.Object);

            _mockSecretVaultService = new Mock<ISecretVaultService>();
            _mockQuickBooksService = new Mock<IQuickBooksService>();
            _mockSyncfusionService = new Mock<ISyncfusionLicenseService>();
            _mockAIService = new Mock<IAIService>();
            _mockLogger = new Mock<ILogger<SettingsViewModel>>();

            _viewModel = new SettingsViewModel(
                _mockLogger.Object,
                _mockDbContext.Object,
                _mockSecretVaultService.Object,
                _mockQuickBooksService.Object,
                _mockSyncfusionService.Object,
                _mockAIService.Object);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void SetupAppSettingsDbSet(AppSettings? settings = null)
        {
            var mockDbSet = new Mock<DbSet<AppSettings>>();
            if (settings != null)
            {
                mockDbSet.Setup(x => x.FindAsync(1)).ReturnsAsync(settings);
            }
            else
            {
                mockDbSet.Setup(x => x.FindAsync(1)).ReturnsAsync((AppSettings?)null);
            }
            _mockDbContext.Setup(db => db.AppSettings).Returns(mockDbSet.Object);
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
                _mockSecretVaultService.Object,
                null!, // null QuickBooks service
                _mockSyncfusionService.Object,
                _mockAIService.Object));
        }

        [Fact]
        public async Task SaveSyncfusionSettingsAsync_SavesLicenseKey()
        {
            // Arrange
            _viewModel.SyncfusionLicenseKey = "test-license-key";
            _mockSecretVaultService.Setup(x => x.SetSecretAsync("Syncfusion-LicenseKey", "test-license-key"))
                .Returns(Task.CompletedTask);

            // Act
            await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            _mockSecretVaultService.Verify(x => x.SetSecretAsync("Syncfusion-LicenseKey", "test-license-key"), Times.Once);
        }

        [Fact]
        public void OnPropertyChanged_WithUntrackedProperty_DoesNotSetHasUnsavedChanges()
        {
            // Arrange - Create a fresh view model to ensure clean state
            var freshViewModel = new SettingsViewModel(
                _mockLogger.Object,
                _mockDbContext.Object,
                _mockSecretVaultService.Object,
                _mockQuickBooksService.Object,
                _mockSyncfusionService.Object,
                _mockAIService.Object);

            // Act - trigger property change for a tracked property (SettingsStatus)
            freshViewModel.SettingsStatus = "Test Status";

            // Assert - HasUnsavedChanges should remain false for tracked properties
            Assert.False(freshViewModel.HasUnsavedChanges);
        }

        #region Database Loading Tests

        [Fact]
        public async Task LoadSettingsAsync_LoadsGeneralSettingsFromDatabase()
        {
            // Arrange
            var testSettings = new AppSettings
            {
                Id = 1,
                Theme = "FluentLight",
                WindowWidth = 1400,
                WindowHeight = 900,
                WindowMaximized = true
            };

            SetupAppSettingsDbSet(testSettings);

            // Act
            await _viewModel.LoadSettingsAsync();

            // Assert
            Assert.Equal("FluentLight", _viewModel.SelectedTheme);
            Assert.Equal(1400, _viewModel.WindowWidth);
            Assert.Equal(900, _viewModel.WindowHeight);
            Assert.True(_viewModel.MaximizeOnStartup);
        }

        [Fact]
        public async Task LoadSettingsAsync_LoadsAdvancedSettingsFromDatabase()
        {
            // Arrange
            var testSettings = new AppSettings
            {
                Id = 1,
                UseDynamicColumns = false,
                EnableDataCaching = false,
                CacheExpirationMinutes = 60,
                SelectedLogLevel = "Warning",
                EnableFileLogging = false,
                LogFilePath = "custom/logs/app.log"
            };

            SetupAppSettingsDbSet(testSettings);

            // Act
            await _viewModel.LoadSettingsAsync();

            // Assert
            Assert.False(_viewModel.EnableDynamicColumns);
            Assert.False(_viewModel.EnableDataCaching);
            Assert.Equal(60, _viewModel.CacheExpirationMinutes);
            Assert.Equal("Warning", _viewModel.SelectedLogLevel);
            Assert.False(_viewModel.EnableFileLogging);
            Assert.Equal("custom/logs/app.log", _viewModel.LogFilePath);
        }

        [Fact]
        public async Task LoadSettingsAsync_UsesDefaultsWhenNoSettingsInDatabase()
        {
            // Arrange
            SetupAppSettingsDbSet(); // No settings provided = null

            // Act
            await _viewModel.LoadSettingsAsync();

            // Assert - Check that defaults are loaded
            Assert.Equal("FluentDark", _viewModel.SelectedTheme);
            Assert.Equal(1200, _viewModel.WindowWidth);
            Assert.Equal(800, _viewModel.WindowHeight);
            Assert.False(_viewModel.MaximizeOnStartup);
            Assert.True(_viewModel.EnableDataCaching);
            Assert.Equal(30, _viewModel.CacheExpirationMinutes);
        }

        #endregion

        #region Database Saving Tests

        [Fact]
        public async Task SaveSettingsAsync_SavesGeneralSettingsToDatabase()
        {
            // Arrange
            _viewModel.SelectedTheme = "FluentLight";
            _viewModel.WindowWidth = 1400;
            _viewModel.WindowHeight = 900;
            _viewModel.MaximizeOnStartup = true;

            var savedSettings = new AppSettings { Id = 1 };
            SetupAppSettingsDbSet(savedSettings);
            _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("FluentLight", savedSettings.Theme);
            Assert.Equal(1400, savedSettings.WindowWidth);
            Assert.Equal(900, savedSettings.WindowHeight);
            Assert.True(savedSettings.WindowMaximized);
            _mockDbContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveSettingsAsync_SavesAdvancedSettingsToDatabase()
        {
            // Arrange
            _viewModel.EnableDynamicColumns = false;
            _viewModel.EnableDataCaching = false;
            _viewModel.CacheExpirationMinutes = 60;
            _viewModel.SelectedLogLevel = "Warning";
            _viewModel.EnableFileLogging = false;
            _viewModel.LogFilePath = "custom/logs/app.log";

            var savedSettings = new AppSettings { Id = 1 };
            SetupAppSettingsDbSet(savedSettings);
            _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.False(savedSettings.UseDynamicColumns);
            Assert.False(savedSettings.EnableDataCaching);
            Assert.Equal(60, savedSettings.CacheExpirationMinutes);
            Assert.Equal("Warning", savedSettings.SelectedLogLevel);
            Assert.False(savedSettings.EnableFileLogging);
            Assert.Equal("custom/logs/app.log", savedSettings.LogFilePath);
            _mockDbContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveSettingsAsync_CreatesNewSettingsRecordWhenNoneExists()
        {
            // Arrange
            _viewModel.SelectedTheme = "FluentLight";

            SetupAppSettingsDbSet(); // No existing settings
            _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            _mockDbContext.Verify(x => x.AppSettings.Add(It.Is<AppSettings>(s =>
                s.Id == 1 && s.Theme == "FluentLight")), Times.Once);
            _mockDbContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void WindowWidth_ValidatesMinimumValue()
        {
            // Act
            _viewModel.WindowWidth = 700; // Below minimum

            // Assert
            Assert.Contains("Minimum width is 800 pixels", _viewModel.WindowWidthValidation);
        }

        [Fact]
        public void WindowWidth_ValidatesMaximumValue()
        {
            // Act
            _viewModel.WindowWidth = 4000; // Above maximum

            // Assert
            Assert.Contains("Maximum width is 3840 pixels", _viewModel.WindowWidthValidation);
        }

        [Fact]
        public void WindowWidth_AcceptsValidValue()
        {
            // Act
            _viewModel.WindowWidth = 1200; // Valid value

            // Assert
            Assert.Empty(_viewModel.WindowWidthValidation);
        }

        [Fact]
        public void WindowHeight_ValidatesMinimumValue()
        {
            // Act
            _viewModel.WindowHeight = 500; // Below minimum

            // Assert
            Assert.Contains("Minimum height is 600 pixels", _viewModel.WindowHeightValidation);
        }

        [Fact]
        public void WindowHeight_ValidatesMaximumValue()
        {
            // Act
            _viewModel.WindowHeight = 2300; // Above maximum

            // Assert
            Assert.Contains("Maximum height is 2160 pixels", _viewModel.WindowHeightValidation);
        }

        [Fact]
        public void CacheExpirationMinutes_ValidatesMinimumValue()
        {
            // Act
            _viewModel.CacheExpirationMinutes = 0; // Below minimum

            // Assert
            Assert.Contains("Minimum cache expiration is 1 minute", _viewModel.CacheExpirationValidation);
        }

        [Fact]
        public void CacheExpirationMinutes_ValidatesMaximumValue()
        {
            // Act
            _viewModel.CacheExpirationMinutes = 1500; // Above maximum

            // Assert
            Assert.Contains("Maximum cache expiration is 1440 minutes", _viewModel.CacheExpirationValidation);
        }

        [Fact]
        public void FiscalYearStartDay_ValidatesRange()
        {
            // Act
            _viewModel.FiscalYearStartDay = 32; // Invalid day

            // Assert
            Assert.Contains("Day must be between 1 and 31", _viewModel.FiscalYearDayValidation);
        }

        [Fact]
        public void Temperature_ValidatesRange()
        {
            // Act
            _viewModel.Temperature = 3.0; // Above maximum

            // Assert
            Assert.Contains("Temperature must be between 0.0 and 2.0", _viewModel.TemperatureValidation);
        }

        [Fact]
        public void MaxTokens_ValidatesAgainstContextWindow()
        {
            // Arrange
            _viewModel.ContextWindowSize = 1024;

            // Act
            _viewModel.MaxTokens = 2048; // Above context window

            // Assert
            Assert.Contains("Max tokens cannot exceed context window size", _viewModel.MaxTokensValidation);
        }

        #endregion

        #region Property Change Tracking Tests

        [Fact]
        public void PropertyChanged_SetsHasUnsavedChanges_ForTrackedProperties()
        {
            // Arrange - Create a fresh view model to ensure clean state
            var freshViewModel = new SettingsViewModel(
                _mockLogger.Object,
                _mockDbContext.Object,
                _mockSecretVaultService.Object,
                _mockQuickBooksService.Object,
                _mockSyncfusionService.Object,
                _mockAIService.Object);

            // Act - change a tracked property
            freshViewModel.SelectedTheme = "FluentLight";

            // Assert
            Assert.True(freshViewModel.HasUnsavedChanges);
        }

        [Fact]
        public async Task SaveSettings_ResetsHasUnsavedChanges()
        {
            // Arrange
            _viewModel.SelectedTheme = "FluentLight";
            Assert.True(_viewModel.HasUnsavedChanges);

            var savedSettings = new AppSettings { Id = 1 };
            SetupAppSettingsDbSet(savedSettings);
            _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.False(_viewModel.HasUnsavedChanges);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task LoadSettingsAsync_HandlesDatabaseErrorsGracefully()
        {
            // Arrange
            var mockDbSet = new Mock<DbSet<AppSettings>>();
            mockDbSet.Setup(x => x.FindAsync(1))
                .ThrowsAsync(new Exception("Database connection failed"));
            _mockDbContext.Setup(db => db.AppSettings).Returns(mockDbSet.Object);

            // Act
            await _viewModel.LoadSettingsAsync();

            // Assert - Should not crash, should use defaults
            Assert.Equal("FluentDark", _viewModel.SelectedTheme);
            _mockLogger.Verify(x => x.LogError(It.IsAny<Exception>(),
                It.Is<string>(s => s.Contains("Failed to load general settings"))), Times.AtLeastOnce);
        }

        [Fact]
        public async Task SaveSettingsAsync_HandlesDatabaseErrors()
        {
            // Arrange
            _viewModel.SelectedTheme = "FluentLight";

            var savedSettings = new AppSettings { Id = 1 };
            SetupAppSettingsDbSet(savedSettings);
            _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database save failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _viewModel.SaveSettingsCommand.ExecuteAsync(null));
            _mockLogger.Verify(x => x.LogError(It.IsAny<Exception>(),
                It.Is<string>(s => s.Contains("Failed to save general settings"))), Times.Once);
        }

        #endregion

        #region Settings Categories Tests

        [Fact]
        public void AvailableThemes_IsInitializedCorrectly()
        {
            // Assert
            Assert.Contains("FluentDark", _viewModel.AvailableThemes);
            Assert.Contains("FluentLight", _viewModel.AvailableThemes);
        }

        [Fact]
        public void AvailableModels_IsInitializedCorrectly()
        {
            // Assert
            Assert.Contains("grok-4-0709", _viewModel.AvailableModels);
            Assert.Contains("grok-beta", _viewModel.AvailableModels);
            Assert.Contains("grok-1", _viewModel.AvailableModels);
        }

        [Fact]
        public void AvailableResponseStyles_IsInitializedCorrectly()
        {
            // Assert
            Assert.Contains("Balanced", _viewModel.AvailableResponseStyles);
            Assert.Contains("Creative", _viewModel.AvailableResponseStyles);
            Assert.Contains("Precise", _viewModel.AvailableResponseStyles);
            Assert.Contains("Concise", _viewModel.AvailableResponseStyles);
        }

        [Fact]
        public void AvailablePersonalities_IsInitializedCorrectly()
        {
            // Assert
            Assert.Contains("Professional", _viewModel.AvailablePersonalities);
            Assert.Contains("Friendly", _viewModel.AvailablePersonalities);
            Assert.Contains("Technical", _viewModel.AvailablePersonalities);
            Assert.Contains("Casual", _viewModel.AvailablePersonalities);
        }

        [Fact]
        public void LogLevels_IsInitializedCorrectly()
        {
            // Assert
            Assert.Contains("Debug", _viewModel.LogLevels);
            Assert.Contains("Information", _viewModel.LogLevels);
            Assert.Contains("Warning", _viewModel.LogLevels);
            Assert.Contains("Error", _viewModel.LogLevels);
            Assert.Contains("Critical", _viewModel.LogLevels);
        }

        [Fact]
        public void QuickBooksEnvironments_IsInitializedCorrectly()
        {
            // Assert
            Assert.Contains("Sandbox", _viewModel.QuickBooksEnvironments);
            Assert.Contains("Production", _viewModel.QuickBooksEnvironments);
        }

        #endregion
    }
}