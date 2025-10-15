using System;
using Xunit;
using WileyWidget;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using WileyWidget.ViewModels;
using Moq;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace WileyWidget.Tests
{
    /// <summary>
    /// Test class that mimics MainWindow authentication UI updates without WPF dependencies
    /// </summary>
    public class TestAuthUI
    {
        private AuthenticationService? _authService;

        public void SetAuthService(AuthenticationService authService)
        {
            _authService = authService;
        }

        // Test version of UpdateAuthenticationUI method
        public void UpdateAuthenticationUI()
        {
            // Test that authentication service interaction doesn't throw
            if (_authService != null)
            {
                var isAuthenticated = _authService.IsAuthenticated;
                var userInfo = _authService.GetUserInfo();
                // Basic test - just ensure no exceptions are thrown
            }
        }
    }

    public class FakeAuthService : AuthenticationService
    {
        private bool _isAuthenticated;
        private UserInfo? _userInfo;

        public FakeAuthService(bool isAuthenticated)
        {
            _isAuthenticated = isAuthenticated;
            _userInfo = isAuthenticated ? new UserInfo { Name = "Test User", Username = "test@example.com", Roles = new System.Collections.Generic.List<string> { "Admin" } } : null;
        }

        public override bool IsAuthenticated => _isAuthenticated;

        public override UserInfo GetUserInfo()
        {
            return _userInfo!;
        }
    }

    public class MainWindowAuthTests
    {
        [Fact]
        public void UpdateAuthenticationUI_NotAuthenticated_DoesNotThrow()
        {
            var services = new ServiceCollection();
#pragma warning disable CA2000 // The service provider will dispose the service
            services.AddSingleton<AuthenticationService>(new FakeAuthService(false));
#pragma warning restore CA2000
            using var provider = services.BuildServiceProvider();

            // Create mock dependencies for MainViewModel
            var mockRegionManager = new Mock<IRegionManager>();
            var mockDispatcherHelper = new Mock<IDispatcherHelper>();
            var mockLogger = new Mock<ILogger<MainViewModel>>();

            // Create mock service provider for App.CurrentContainer
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(x => x.GetService(typeof(IRegionManager))).Returns(mockRegionManager.Object);
            
            // Set the App.CurrentContainer using reflection on the backing field
            var currentContainerField = typeof(WileyWidget.App).GetField("CurrentContainer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            currentContainerField?.SetValue(null, mockServiceProvider.Object);

            // Create a test auth UI handler
            var authUI = new TestAuthUI();
            // Inject the fake service for testing
            var authService = provider.GetService<AuthenticationService>();
            if (authService != null)
            {
                authUI.SetAuthService(authService);
            }

            // Call the test method - should not throw
            authUI.UpdateAuthenticationUI();
        }

        [Fact]
        public void UpdateAuthenticationUI_Authenticated_DoesNotThrow()
        {
            var services = new ServiceCollection();
#pragma warning disable CA2000 // The service provider will dispose the service
            services.AddSingleton<AuthenticationService>(new FakeAuthService(true));
#pragma warning restore CA2000
            using var provider = services.BuildServiceProvider();

            // Create mock dependencies for MainViewModel
            var mockRegionManager = new Mock<IRegionManager>();
            var mockDispatcherHelper = new Mock<IDispatcherHelper>();
            var mockLogger = new Mock<ILogger<MainViewModel>>();

            // Create mock service provider for App.CurrentContainer
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(x => x.GetService(typeof(IRegionManager))).Returns(mockRegionManager.Object);
            
            // Set the App.CurrentContainer using reflection on the backing field
            var currentContainerField = typeof(WileyWidget.App).GetField("CurrentContainer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            currentContainerField?.SetValue(null, mockServiceProvider.Object);

            // Create a test auth UI handler
            var authUI = new TestAuthUI();
            // Inject the fake service for testing
            var authService = provider.GetService<AuthenticationService>();
            if (authService != null)
            {
                authUI.SetAuthService(authService);
            }

            // Call the test method - should not throw
            authUI.UpdateAuthenticationUI();
        }
    }
}
