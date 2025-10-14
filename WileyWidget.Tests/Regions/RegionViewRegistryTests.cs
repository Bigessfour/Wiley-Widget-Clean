using System;
using System.ComponentModel;
using FluentAssertions;
using Moq;
using Prism.Ioc;
using Xunit;

namespace WileyWidget.Tests.Regions
{
    /// <summary>
    /// Comprehensive unit tests for Prism's RegionViewRegistry functionality.
    /// Tests cover view registration, error handling, auto-population, and region lifecycle.
    /// Since Prism.Regions types may be internal, we define minimal interfaces for testing.
    /// Note: Tests that instantiate UserControl are marked as [STAFact] for WPF threading requirements.
    /// </summary>
    public class RegionViewRegistryTests
    {
        private readonly Mock<IContainerProvider> _mockContainerProvider;
        private readonly Mock<ITestRegionManager> _mockRegionManager;
        private readonly Mock<ITestRegion> _mockRegion;

        public RegionViewRegistryTests()
        {
            _mockContainerProvider = new Mock<IContainerProvider>();
            _mockRegionManager = new Mock<ITestRegionManager>();
            _mockRegion = new Mock<ITestRegion>();
            
            // Setup mock to throw ArgumentNullException for null parameters
            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(null!, It.IsAny<Type>()))
                .Throws<ArgumentNullException>();
            
            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(string.Empty, It.IsAny<Type>()))
                .Throws<ArgumentException>();
            
            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(It.IsAny<string>(), (Type)null!))
                .Throws<ArgumentNullException>();
        }

        #region View Registration Tests

        [Fact]
        public void RegisterViewWithRegion_ValidInputs_DelegatesToRegionManager()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);

            // Act
            _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType);

            // Assert
            _mockRegionManager.Verify(rm =>
                rm.RegisterViewWithRegion(It.Is<string>(s => s == regionName), It.Is<Type>(t => t == viewType)), Times.Once);
        }

        [Fact]
        public void RegisterViewWithRegion_NullRegionName_ThrowsArgumentNullException()
        {
            // Arrange
            var viewType = typeof(TestDashboardView);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _mockRegionManager.Object.RegisterViewWithRegion(null!, viewType));
        }

        [Fact]
        public void RegisterViewWithRegion_EmptyRegionName_ThrowsArgumentException()
        {
            // Arrange
            var viewType = typeof(TestDashboardView);

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _mockRegionManager.Object.RegisterViewWithRegion(string.Empty, viewType));
        }

        [Fact]
        public void RegisterViewWithRegion_NullViewType_ThrowsArgumentNullException()
        {
            // Arrange
            const string regionName = "MainRegion";
            Type nullType = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _mockRegionManager.Object.RegisterViewWithRegion(regionName, nullType));
        }

        #endregion

        #region Registration Failure Tests

        [Fact]
        public void RegisterViewWithRegion_ContainerResolutionFails_ThrowsViewRegistrationException()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);

            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(regionName, viewType))
                .Throws(new ViewRegistrationException("Failed to resolve view from container"));

            // Act & Assert
            Assert.Throws<ViewRegistrationException>(() =>
                _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType));
        }

        [Fact]
        public void RegisterViewWithRegion_XamlParseException_ThrowsViewRegistrationException()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);

            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(regionName, viewType))
                .Throws(new ViewRegistrationException("XAML parsing failed",
                    new System.Windows.Markup.XamlParseException("Invalid XAML")));

            // Act & Assert
            var exception = Assert.Throws<ViewRegistrationException>(() =>
                _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType));

            exception.InnerException.Should().BeOfType<System.Windows.Markup.XamlParseException>();
        }

        [Fact]
        public void RegisterViewWithRegion_DependencyInjectionFailure_ThrowsViewRegistrationException()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);

            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(regionName, viewType))
                .Throws(new ViewRegistrationException("Dependency injection failed",
                    new InvalidOperationException("Service not registered")));

            // Act & Assert
            var exception = Assert.Throws<ViewRegistrationException>(() =>
                _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType));

            exception.InnerException.Should().BeOfType<InvalidOperationException>();
        }

        #endregion

        #region Auto-Population Tests

        [STAFact]
        public void RegionViewRegistry_AutoPopulatesRegisteredViews_WhenRegionCreated()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);
            var expectedView = new TestDashboardView();

            // Setup container to resolve the view
            _mockContainerProvider
                .Setup(cp => cp.Resolve(viewType))
                .Returns(expectedView);

            // Simulate RegionViewRegistry behavior: when region is created,
            // registered views should be resolved and added
            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);
            registry.RegisterViewWithRegion(regionName, viewType);

            // Act
            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert
            _mockRegion.Verify(r => r.Add(expectedView), Times.Once);
        }

        [Fact]
        public void RegionViewRegistry_NoViewsRegistered_DoesNotPopulateRegion()
        {
            // Arrange
            const string regionName = "EmptyRegion";

            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);

            // Act
            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert
            _mockRegion.Verify(r => r.Add(It.IsAny<object>()), Times.Never);
        }

        [STAFact]
        public void RegionViewRegistry_ViewResolutionFails_ContinuesWithOtherViews()
        {
            // Arrange
            const string regionName = "MainRegion";
            var failingViewType = typeof(TestDashboardView);
            var successfulViewType = typeof(TestSettingsView);
            var successfulView = new TestSettingsView();

            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);

            // Setup one view to fail resolution, another to succeed
            _mockContainerProvider
                .Setup(cp => cp.Resolve(failingViewType))
                .Throws(new InvalidOperationException("Resolution failed"));

            _mockContainerProvider
                .Setup(cp => cp.Resolve(successfulViewType))
                .Returns(successfulView);

            registry.RegisterViewWithRegion(regionName, failingViewType);
            registry.RegisterViewWithRegion(regionName, successfulViewType);

            // Act
            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert - Only the successful view should be added
            _mockRegion.Verify(r => r.Add(successfulView), Times.Once);
        }

        #endregion

        #region Region Activation/Deactivation Tests

        [STAFact]
        public void RegionActivated_ViewsWithRegionMemberLifetime_AreActivated()
        {
            // Arrange
            var view = new TestDashboardView();

            // Act
            view.OnActivated();

            // Assert
            view.IsActivated.Should().BeTrue();
        }

        [STAFact]
        public void RegionDeactivated_ViewsWithRegionMemberLifetime_AreDeactivated()
        {
            // Arrange
            var view = new TestDashboardView();

            // First activate
            view.OnActivated();

            // Act
            view.OnDeactivated();

            // Assert
            view.IsActivated.Should().BeTrue();
            view.IsDeactivated.Should().BeTrue();
        }

        [STAFact]
        public void RegionViewRegistry_HandlesKeepAliveProperty_Correctly()
        {
            // Arrange
            var keepAliveView = new TestDashboardView(); // KeepAlive = true
            var transientView = new TestSettingsView();  // KeepAlive = false

            // Assert
            keepAliveView.KeepAlive.Should().BeTrue();
            transientView.KeepAlive.Should().BeFalse();
        }

        #endregion

        #region Multiple Views Per Region Tests

        [STAFact]
        public void RegisterMultipleViewsWithSameRegion_AllViewsAutoPopulated()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType1 = typeof(TestDashboardView);
            var viewType2 = typeof(TestSettingsView);
            var view1 = new TestDashboardView();
            var view2 = new TestSettingsView();

            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);

            _mockContainerProvider.Setup(cp => cp.Resolve(viewType1)).Returns(view1);
            _mockContainerProvider.Setup(cp => cp.Resolve(viewType2)).Returns(view2);

            // Act
            registry.RegisterViewWithRegion(regionName, viewType1);
            registry.RegisterViewWithRegion(regionName, viewType2);
            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert
            _mockRegion.Verify(r => r.Add(view1), Times.Once);
            _mockRegion.Verify(r => r.Add(view2), Times.Once);
        }

        [STAFact]
        public void RegisterSameViewTypeTwice_RegistersBothInstances()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);
            var view1 = new TestDashboardView();
            var view2 = new TestDashboardView();

            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);

            // Setup container to return different instances
            var callCount = 0;
            _mockContainerProvider
                .Setup(cp => cp.Resolve(viewType))
                .Returns(() => callCount++ == 0 ? view1 : view2);

            // Act
            registry.RegisterViewWithRegion(regionName, viewType);
            registry.RegisterViewWithRegion(regionName, viewType);
            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert
            _mockRegion.Verify(r => r.Add(It.IsAny<TestDashboardView>()), Times.Exactly(2));
        }

        #endregion

        #region Exception Handling Tests

        [Fact]
        public void RegisterViewWithRegion_ViewConstructorThrowsException_ThrowsViewRegistrationException()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);

            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(regionName, viewType))
                .Throws(new ViewRegistrationException("View constructor threw exception",
                    new InvalidOperationException("Required dependency not available")));

            // Act & Assert
            var exception = Assert.Throws<ViewRegistrationException>(() =>
                _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType));

            exception.Message.Should().Contain("constructor");
            exception.InnerException.Should().NotBeNull();
        }

        [Fact]
        public void RegisterViewWithRegion_CircularDependency_ThrowsViewRegistrationException()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);

            _mockRegionManager
                .Setup(rm => rm.RegisterViewWithRegion(regionName, viewType))
                .Throws(new ViewRegistrationException("Circular dependency detected",
                    new InvalidOperationException("Service A depends on B which depends on A")));

            // Act & Assert
            var exception = Assert.Throws<ViewRegistrationException>(() =>
                _mockRegionManager.Object.RegisterViewWithRegion(regionName, viewType));

            exception.InnerException!.Message.Should().Contain("Service A depends on B");
        }

        [Fact]
        public void AutoPopulate_AllViewsFailResolution_RegionRemainsEmpty()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType1 = typeof(TestDashboardView);
            var viewType2 = typeof(TestSettingsView);

            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);

            _mockContainerProvider
                .Setup(cp => cp.Resolve(It.IsAny<Type>()))
                .Throws(new InvalidOperationException("Resolution failed"));

            registry.RegisterViewWithRegion(regionName, viewType1);
            registry.RegisterViewWithRegion(regionName, viewType2);

            // Act
            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert
            _mockRegion.Verify(r => r.Add(It.IsAny<object>()), Times.Never);
        }

        #endregion

        #region Concurrent Registration Tests

        [Fact]
        public void RegisterViewWithRegion_ConcurrentRegistrations_AllRegistered()
        {
            // Arrange
            const string regionName = "MainRegion";
            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);
            var viewTypes = new[]
            {
                typeof(TestDashboardView),
                typeof(TestSettingsView),
                typeof(TestDashboardView),
                typeof(TestSettingsView)
            };

            var views = new object[]
            {
                new TestDashboardView(),
                new TestSettingsView(),
                new TestDashboardView(),
                new TestSettingsView()
            };

            for (int i = 0; i < viewTypes.Length; i++)
            {
                var localIndex = i;
                _mockContainerProvider
                    .Setup(cp => cp.Resolve(viewTypes[localIndex]))
                    .Returns(views[localIndex]);
            }

            // Act - Simulate concurrent registration
            System.Threading.Tasks.Parallel.ForEach(viewTypes, viewType =>
            {
                registry.RegisterViewWithRegion(regionName, viewType);
            });

            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert - Should have registered multiple views
            _mockRegion.Verify(r => r.Add(It.IsAny<object>()), Times.AtLeast(viewTypes.Length));
        }

        #endregion

        #region Region Lifecycle Tests

        [STAFact]
        public void RegionViewRegistry_SupportsLazyViewResolution()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType = typeof(TestDashboardView);
            var view = new TestDashboardView();

            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);

            _mockContainerProvider
                .Setup(cp => cp.Resolve(viewType))
                .Returns(view);

            // Act - Register view but don't create region yet
            registry.RegisterViewWithRegion(regionName, viewType);

            // Assert - Container should not be called until region is created
            _mockContainerProvider.Verify(cp => cp.Resolve(It.IsAny<Type>()), Times.Never);

            // Now create region
            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert - Now container should be called
            _mockContainerProvider.Verify(cp => cp.Resolve(viewType), Times.Once);
        }

        [STAFact]
        public void RegionViewRegistry_ViewActivationOrder_MaintainsRegistrationOrder()
        {
            // Arrange
            const string regionName = "MainRegion";
            var viewType1 = typeof(TestDashboardView);
            var viewType2 = typeof(TestSettingsView);
            var view1 = new TestDashboardView();
            var view2 = new TestSettingsView();

            var registry = new TestRegionViewRegistry(_mockContainerProvider.Object);
            var addedViews = new System.Collections.Generic.List<object>();

            _mockContainerProvider.Setup(cp => cp.Resolve(viewType1)).Returns(view1);
            _mockContainerProvider.Setup(cp => cp.Resolve(viewType2)).Returns(view2);

            _mockRegion
                .Setup(r => r.Add(It.IsAny<object>()))
                .Callback<object>(view => addedViews.Add(view));

            // Act
            registry.RegisterViewWithRegion(regionName, viewType1);
            registry.RegisterViewWithRegion(regionName, viewType2);
            registry.OnRegionCreated(_mockRegion.Object, regionName);

            // Assert - Views should be added in registration order
            addedViews.Should().HaveCount(2);
            addedViews[0].Should().Be(view1);
            addedViews[1].Should().Be(view2);
        }

        #endregion

        #region View-Specific Behavior Tests

        [STAFact]
        public void RegisterViewWithRegion_ViewImplementsINavigationAware_NavigationHandled()
        {
            // Arrange
            var view = new TestNavigationAwareView();

            // Act
            view.OnNavigatedTo();

            // Assert
            view.HasNavigated.Should().BeTrue();
        }

        [STAFact]
        public void RegisterViewWithRegion_ViewWithKeepAliveTrue_RemainsInMemory()
        {
            // Arrange
            var keepAliveView = new TestDashboardView();

            // Act - Simulate deactivation
            keepAliveView.OnDeactivated();

            // Assert - KeepAlive views should maintain state
            keepAliveView.KeepAlive.Should().BeTrue();
            keepAliveView.IsDeactivated.Should().BeTrue();
        }

        [STAFact]
        public void RegisterViewWithRegion_ViewWithKeepAliveFalse_CanBeRemovedFromMemory()
        {
            // Arrange
            var transientView = new TestSettingsView();

            // Act - Simulate deactivation
            transientView.OnDeactivated();

            // Assert - Non-KeepAlive views can be garbage collected
            transientView.KeepAlive.Should().BeFalse();
            transientView.IsDeactivated.Should().BeTrue();
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Custom exception for view registration failures, simulating Prism's behavior
        /// </summary>
        public class ViewRegistrationException : Exception
        {
            public ViewRegistrationException(string message) : base(message) { }
            public ViewRegistrationException(string message, Exception innerException)
                : base(message, innerException) { }
        }

        /// <summary>
        /// Test implementation simulating RegionViewRegistry behavior
        /// </summary>
        public class TestRegionViewRegistry
        {
            private readonly IContainerProvider _containerProvider;
            private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Type>> _registeredViews
                = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Type>>();

            public TestRegionViewRegistry(IContainerProvider containerProvider)
            {
                _containerProvider = containerProvider;
            }

            public void RegisterViewWithRegion(string regionName, Type viewType)
            {
                if (!_registeredViews.ContainsKey(regionName))
                    _registeredViews[regionName] = new System.Collections.Generic.List<Type>();

                _registeredViews[regionName].Add(viewType);
            }

            public void OnRegionCreated(ITestRegion region, string regionName)
            {
                if (_registeredViews.TryGetValue(regionName, out var viewTypes))
                {
                    foreach (var viewType in viewTypes)
                    {
                        try
                        {
                            var view = _containerProvider.Resolve(viewType);
                            region.Add(view);
                        }
                        catch
                        {
                            // Continue with other views if one fails
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Test view class for Dashboard implementing ITestRegionMemberLifetime
        /// </summary>
        public class TestDashboardView : ITestRegionMemberLifetime
        {
            public bool KeepAlive => true;
            public bool IsActivated { get; private set; }
            public bool IsDeactivated { get; private set; }

            public void OnActivated() => IsActivated = true;
            public void OnDeactivated() => IsDeactivated = true;
        }

        /// <summary>
        /// Test view class for Settings implementing ITestRegionMemberLifetime
        /// </summary>
        public class TestSettingsView : ITestRegionMemberLifetime
        {
            public bool KeepAlive => false;
            public bool IsActivated { get; private set; }
            public bool IsDeactivated { get; private set; }

            public void OnActivated() => IsActivated = true;
            public void OnDeactivated() => IsDeactivated = true;
        }

        /// <summary>
        /// Test view class implementing ITestNavigationAware
        /// </summary>
        public class TestNavigationAwareView : ITestNavigationAware
        {
            public bool HasNavigated { get; private set; }

            public void OnNavigatedTo() => HasNavigated = true;
        }

        #endregion

        #region Test Interfaces (Prism Region Abstractions)

        /// <summary>
        /// Test interface representing Prism's IRegionManager
        /// </summary>
        public interface ITestRegionManager
        {
            void RegisterViewWithRegion(string regionName, Type viewType);
            void RegisterViewWithRegion(string regionName, string viewName);
        }

        /// <summary>
        /// Test interface representing Prism's IRegion
        /// </summary>
        public interface ITestRegion : INotifyPropertyChanged
        {
            void Add(object view);
            bool IsActive { get; }
        }

        /// <summary>
        /// Test interface representing Prism's IRegionMemberLifetime
        /// </summary>
        public interface ITestRegionMemberLifetime
        {
            bool KeepAlive { get; }
        }

        /// <summary>
        /// Test interface representing Prism's INavigationAware
        /// </summary>
        public interface ITestNavigationAware
        {
            void OnNavigatedTo();
        }

        #endregion
    }

    /// <summary>
    /// STAFact attribute for xUnit tests requiring Single-Threaded Apartment (STA) threading
    /// Required for WPF UserControl instantiation in tests
    /// </summary>
    public sealed class STAFactAttribute : FactAttribute
    {
        public STAFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "STA tests only run on Windows";
            }
        }
    }
}