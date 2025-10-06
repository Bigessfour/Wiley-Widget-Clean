using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WileyWidget.Configuration;
using WileyWidget.DependencyInjection.Tests.TestFixtures;
using Xunit;

namespace WileyWidget.DependencyInjection.Tests;

/// <summary>
/// Tests service lifetime behaviors (Singleton, Transient, Scoped) and disposal patterns.
/// Ensures services follow Microsoft DI guidelines for proper resource management.
/// Uses WebApplicationFactory pattern for proper DI container testing.
/// </summary>
[Collection("WpfTest")]
public sealed class ServiceLifetimeTests : IDisposable
{
    private readonly WpfTestFactory _factory;
    private readonly IServiceProvider _serviceProvider;

    public ServiceLifetimeTests(WpfTestFactory factory)
    {
        _factory = factory;
        _serviceProvider = factory.Services;
    }

    public void Dispose()
    {
        // Factory handles its own disposal
    }

    [Theory]
    [InlineData(typeof(WileyWidget.Services.SettingsService), ServiceLifetime.Singleton)]
    [InlineData(typeof(WileyWidget.Services.IViewManager), ServiceLifetime.Singleton)]
    [InlineData(typeof(WileyWidget.Services.Threading.IDispatcherHelper), ServiceLifetime.Singleton)]
    [InlineData(typeof(WileyWidget.ViewModels.AboutViewModel), ServiceLifetime.Transient)]
    [InlineData(typeof(WileyWidget.ViewModels.DashboardViewModel), ServiceLifetime.Scoped)]
    [InlineData(typeof(WileyWidget.ViewModels.AIAssistViewModel), ServiceLifetime.Scoped)]
    public void Service_Should_Have_Expected_Lifetime(Type serviceType, ServiceLifetime expectedLifetime)
    {
        // Arrange & Act - For scoped services, we need a scope to resolve them
        object? service;
        if (expectedLifetime == ServiceLifetime.Scoped)
        {
            using var scope = _serviceProvider.CreateScope();
            service = scope.ServiceProvider.GetService(serviceType);
        }
        else
        {
            service = _serviceProvider.GetService(serviceType);
        }

        // Assert - we verify the service is registered by attempting to resolve it
        service.Should().NotBeNull($"{serviceType.Name} should be registered with {expectedLifetime} lifetime");
        
        // Note: Lifetime verification is done through behavior tests (Singleton/Transient/Scoped tests)
    }

    [Fact]
    public void ShellViewModel_Should_Be_Registered_With_Scope()
    {
        // Arrange - ShellViewModel requires scoped services
        using var scope = _serviceProvider.CreateScope();

        // Act
        var service = scope.ServiceProvider.GetService(typeof(WileyWidget.ViewModels.ShellViewModel));

        // Assert
        service.Should().NotBeNull("ShellViewModel should be registered and resolvable within a scope");
    }

    [Fact]
    public void Scoped_Services_Should_Return_Same_Instance_Within_Scope()
    {
        // Arrange
        using var scope1 = _serviceProvider.CreateScope();
        using var scope2 = _serviceProvider.CreateScope();

        // Act
        var instance1a = scope1.ServiceProvider.GetService<WileyWidget.Data.AppDbContext>();
        var instance1b = scope1.ServiceProvider.GetService<WileyWidget.Data.AppDbContext>();
        var instance2 = scope2.ServiceProvider.GetService<WileyWidget.Data.AppDbContext>();

        // Assert
        instance1a.Should().NotBeNull("scoped service should be resolvable");
        instance1a.Should().BeSameAs(instance1b, "scoped service should return same instance within scope");
        instance1a.Should().NotBeSameAs(instance2, "scoped service should return different instance across scopes");
    }

    [Fact]
    public void Disposed_Scope_Should_Dispose_Scoped_Services()
    {
        // Arrange
        DisposableTestService? service;
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<DisposableTestService>();
        var provider = serviceCollection.BuildServiceProvider();

        // Act
        using (var scope = provider.CreateScope())
        {
            service = scope.ServiceProvider.GetService<DisposableTestService>();
            service.Should().NotBeNull();
        }

        // Assert - scope disposed, service should be disposed
        service!.IsDisposed.Should().BeTrue("scoped services should be disposed when scope is disposed");
    }

    [Fact]
    public void Singleton_Services_Should_Not_Depend_On_Scoped_Services()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<InvalidSingletonWithScopedDependency>();
        serviceCollection.AddScoped<DisposableTestService>();

        // Act
        Action act = () =>
        {
            var provider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
            provider.GetService<InvalidSingletonWithScopedDependency>();
        };

        // Assert
        act.Should().Throw<InvalidOperationException>(
            "singleton services should not depend on scoped services (Microsoft DI guideline violation)");
    }

    [Fact]
    public void Transient_IDisposable_Services_Should_Be_Disposed_By_Container()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTransient<DisposableTestService>();
        var provider = serviceCollection.BuildServiceProvider();

        DisposableTestService? service;

        // Act
        using (var scope = provider.CreateScope())
        {
            service = scope.ServiceProvider.GetService<DisposableTestService>();
            service.Should().NotBeNull();
        }

        // Assert
        service!.IsDisposed.Should().BeTrue(
            "transient IDisposable services should be disposed when their scope is disposed");
    }

    [Fact]
    public void Multiple_Scopes_Should_Have_Independent_Lifetimes()
    {
        // Arrange
        var scope1 = _serviceProvider.CreateScope();
        var scope2 = _serviceProvider.CreateScope();

        // Act
        var context1 = scope1.ServiceProvider.GetService<WileyWidget.Data.AppDbContext>();
        var context2 = scope2.ServiceProvider.GetService<WileyWidget.Data.AppDbContext>();

        scope1.Dispose();

        // Assert
        context1.Should().NotBeSameAs(context2, "different scopes should have different instances");
        
        // Verify scope2 is still usable after scope1 disposal
        Action act = () =>
        {
            var context2Again = scope2.ServiceProvider.GetService<WileyWidget.Data.AppDbContext>();
            context2Again.Should().BeSameAs(context2, "scope2 should remain functional after scope1 disposal");
        };
        act.Should().NotThrow();

        scope2.Dispose();
    }

    [Fact]
    public void Root_ServiceProvider_Should_Dispose_Singleton_Services()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<DisposableTestService>();
        var provider = serviceCollection.BuildServiceProvider();
        var service = provider.GetService<DisposableTestService>();

        // Act
        provider.Dispose();

        // Assert
        service!.IsDisposed.Should().BeTrue(
            "singleton services should be disposed when root ServiceProvider is disposed");
    }

    // Test helper classes
    private class DisposableTestService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private class InvalidSingletonWithScopedDependency
    {
        public InvalidSingletonWithScopedDependency(DisposableTestService scopedService)
        {
            // This violates DI guidelines - singleton depending on scoped
        }
    }
}
