using System;
using Xunit;
using WileyWidget;
using WileyWidget.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using WileyWidget.ViewModels;

namespace WileyWidget.Tests
{
    public class FakeAuthService : AuthenticationService
    {
        private bool _isAuthenticated;
        private UserInfo _userInfo;

        public FakeAuthService(bool isAuthenticated)
        {
            _isAuthenticated = isAuthenticated;
            _userInfo = isAuthenticated ? new UserInfo { Name = "Test User", Username = "test@example.com", Roles = new System.Collections.Generic.List<string> { "Admin" } } : null;
        }

        public override bool IsAuthenticated => _isAuthenticated;

        public override UserInfo GetUserInfo()
        {
            return _userInfo;
        }
    }

    public class MainWindowAuthTests : TestApplication
    {
        [StaFact]
        public void UpdateAuthenticationUI_NotAuthenticated_DoesNotThrow()
        {
            var services = new ServiceCollection();
#pragma warning disable CA2000 // The service provider will dispose the service
            services.AddSingleton<AuthenticationService>(new FakeAuthService(false));
#pragma warning restore CA2000
            using var provider = services.BuildServiceProvider();

            // Run on UI thread to ensure proper WPF context
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                // Inject the fake service via reflection to avoid changing MainWindow API in tests
                var field = typeof(MainWindow).GetField("_authService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(window, provider.GetService<AuthenticationService>());

                // Ensure DataContext is a safe ViewModel
                window.DataContext = new MainViewModel();

                // Call the method under test
                window.GetType().GetMethod("UpdateAuthenticationUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(window, null);
            });
        }

        [StaFact]
        public void UpdateAuthenticationUI_Authenticated_DoesNotThrow()
        {
            var services = new ServiceCollection();
#pragma warning disable CA2000 // The service provider will dispose the service
            services.AddSingleton<AuthenticationService>(new FakeAuthService(true));
#pragma warning restore CA2000
            using var provider = services.BuildServiceProvider();

            // Run on UI thread to ensure proper WPF context
            RunOnUIThread(() =>
            {
                var window = new MainWindow();
                var field = typeof(MainWindow).GetField("_authService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(window, provider.GetService<AuthenticationService>());

                window.DataContext = new MainViewModel();

                window.GetType().GetMethod("UpdateAuthenticationUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(window, null);
            });
        }
    }
}
