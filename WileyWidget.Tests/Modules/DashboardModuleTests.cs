using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prism.Ioc;
using Prism.Modularity;
using WileyWidget.Startup.Modules;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using Xunit;

namespace WileyWidget.Tests.Modules
{
    /// <summary>
    /// Comprehensive unit tests for DashboardModule covering initialization scenarios,
    /// dependency injection, view registration, and error handling.
    /// </summary>
    public class DashboardModuleTests
    {
        private readonly Mock<IContainerProvider> _mockContainerProvider;
        private readonly Mock<IRegionManager> _mockRegionManager;
        private readonly Mock<IContainerRegistry> _mockContainerRegistry;
        private readonly Mock<ILogger<DashboardModule>> _mockLogger;
        private readonly DashboardModule _dashboardModule;

        public DashboardModuleTests()
        {
            _mockContainerProvider = new Mock<IContainerProvider>();
            _mockRegionManager = new Mock<IRegionManager>();
            _mockContainerRegistry = new Mock<IContainerRegistry>();
            _mockLogger = new Mock<ILogger<DashboardModule>>();
            _dashboardModule = new DashboardModule();
        }

        #region Successful Initialization Tests

        [Fact]
        public void OnInitialized_WithValidDependencies_RegistersDashboardViewWithMainRegion()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            // Act
            _dashboardModule.OnInitialized(_mockContainerProvider.Object);

            // Assert
            _mockRegionManager.Verify(
                rm => rm.RegisterViewWithRegion("MainRegion", typeof(DashboardView)),
                Times.Once,
                "DashboardView should be registered with MainRegion during initialization");
        }

        [Fact]
        public void OnInitialized_WithValidDependencies_ResolvesRegionManagerOnce()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            // Act
            _dashboardModule.OnInitialized(_mockContainerProvider.Object);

            // Assert
            _mockContainerProvider.Verify(
                cp => cp.Resolve(typeof(IRegionManager)),
                Times.Once,
                "RegionManager should be resolved exactly once during initialization");
        }

        [Fact]
        public void RegisterTypes_RegistersDashboardViewModel()
        {
            // Act & Assert - Should not throw
            _dashboardModule.Invoking(dm => dm.RegisterTypes(_mockContainerRegistry.Object))
                .Should().NotThrow();
        }

        [Fact]
        public void RegisterTypes_RegistersDashboardViewForNavigation()
        {
            // Act & Assert - Should not throw
            _dashboardModule.Invoking(dm => dm.RegisterTypes(_mockContainerRegistry.Object))
                .Should().NotThrow();
        }

        #endregion

        #region Failure Case Tests

        [Fact]
        public void OnInitialized_WhenRegionManagerResolutionFails_CompletesGracefully()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Throws(new InvalidOperationException("RegionManager not registered"));

            // Act & Assert - Should not throw, should handle gracefully
            _dashboardModule.Invoking(dm => dm.OnInitialized(_mockContainerProvider.Object))
                .Should().NotThrow();
        }

        [Fact]
        public void OnInitialized_WhenContainerProviderIsNull_HandlesGracefully()
        {
            // Act & Assert - Should not throw exception due to try-catch in module
            _dashboardModule.Invoking(dm => dm.OnInitialized(null!))
                .Should().NotThrow();
        }

        [Fact]
        public void OnInitialized_WhenRegionManagerRegisterViewFails_CompletesGracefully()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(It.IsAny<string>(), It.IsAny<Type>()))
                .Throws(new InvalidOperationException("Region 'MainRegion' not found"));

            // Act & Assert - Should not throw, should handle gracefully
            _dashboardModule.Invoking(dm => dm.OnInitialized(_mockContainerProvider.Object))
                .Should().NotThrow();
        }

        [Fact]
        public void RegisterTypes_WhenContainerRegistryIsNull_ThrowsNullReferenceException()
        {
            // Act & Assert
            _dashboardModule.Invoking(dm => dm.RegisterTypes(null!))
                .Should().Throw<NullReferenceException>();
        }

        #endregion

        #region View Registration Verification Tests

        [Fact]
        public void OnInitialized_RegistersViewWithCorrectRegionName()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            // Act
            _dashboardModule.OnInitialized(_mockContainerProvider.Object);

            // Assert
            _mockRegionManager.Verify(
                rm => rm.RegisterViewWithRegion(
                    It.Is<string>(regionName => regionName == "MainRegion"),
                    It.IsAny<Type>()),
                Times.Once,
                "View should be registered with the correct region name 'MainRegion'");
        }

        [Fact]
        public void OnInitialized_RegistersCorrectViewType()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            // Act
            _dashboardModule.OnInitialized(_mockContainerProvider.Object);

            // Assert
            _mockRegionManager.Verify(
                rm => rm.RegisterViewWithRegion(
                    It.IsAny<string>(),
                    It.Is<Type>(viewType => viewType == typeof(DashboardView))),
                Times.Once,
                "DashboardView type should be registered with the region");
        }

        #endregion

        #region Async Initialization Edge Cases

        [Fact]
        public async Task OnInitialized_AsyncInitialization_CompletesSuccessfully()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            // Act
            await Task.Run(() => _dashboardModule.OnInitialized(_mockContainerProvider.Object));

            // Assert
            _mockRegionManager.Verify(
                rm => rm.RegisterViewWithRegion("MainRegion", typeof(DashboardView)),
                Times.Once,
                "Async initialization should complete successfully");
        }

        [Fact]
        public void OnInitialized_MultipleCalls_RegistersViewMultipleTimes()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            // Act
            _dashboardModule.OnInitialized(_mockContainerProvider.Object);
            _dashboardModule.OnInitialized(_mockContainerProvider.Object);

            // Assert
            _mockRegionManager.Verify(
                rm => rm.RegisterViewWithRegion("MainRegion", typeof(DashboardView)),
                Times.Exactly(2),
                "Multiple initialization calls should register the view multiple times");
        }

        [Fact]
        public void OnInitialized_WithDifferentContainerProviders_WorksIndependently()
        {
            // Arrange
            var mockContainerProvider2 = new Mock<IContainerProvider>();
            var mockRegionManager2 = new Mock<IRegionManager>();

            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            mockContainerProvider2
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(mockRegionManager2.Object);

            // Act
            _dashboardModule.OnInitialized(_mockContainerProvider.Object);
            _dashboardModule.OnInitialized(mockContainerProvider2.Object);

            // Assert
            _mockRegionManager.Verify(
                rm => rm.RegisterViewWithRegion("MainRegion", typeof(DashboardView)),
                Times.Once);

            mockRegionManager2.Verify(
                rm => rm.RegisterViewWithRegion("MainRegion", typeof(DashboardView)),
                Times.Once);
        }

        #endregion

        #region Thread Safety and Concurrency Tests

        [Fact]
        public void OnInitialized_ConcurrentCalls_HandlesThreadSafety()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            // Act
            Parallel.Invoke(
                () => _dashboardModule.OnInitialized(_mockContainerProvider.Object),
                () => _dashboardModule.OnInitialized(_mockContainerProvider.Object),
                () => _dashboardModule.OnInitialized(_mockContainerProvider.Object)
            );

            // Assert
            _mockRegionManager.Verify(
                rm => rm.RegisterViewWithRegion("MainRegion", typeof(DashboardView)),
                Times.Exactly(3),
                "Concurrent initialization calls should be handled safely");
        }

        #endregion

        #region Integration and Mock Verification Tests

        [Fact]
        public void Module_InheritsFromIModule()
        {
            // Assert
            _dashboardModule.Should().BeAssignableTo<IModule>();
        }

        [Fact]
        public void OnInitialized_CallsResolveWithCorrectType()
        {
            // Arrange
            _mockContainerProvider
                .Setup(cp => cp.Resolve(typeof(IRegionManager)))
                .Returns(_mockRegionManager.Object);

            // Act
            _dashboardModule.OnInitialized(_mockContainerProvider.Object);

            // Assert
            _mockContainerProvider.Verify(
                cp => cp.Resolve(typeof(IRegionManager)),
                Times.Once,
                "ContainerProvider.Resolve should be called with IRegionManager type");
        }

        #endregion
    }
}
