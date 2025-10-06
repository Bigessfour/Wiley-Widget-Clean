using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.DependencyInjection.Tests.TestFixtures;
using Xunit;

namespace WileyWidget.DependencyInjection.Tests;

/// <summary>
/// Tests constructor resolution logic to ensure no ambiguous constructors
/// and proper use of [ActivatorUtilitiesConstructor] attribute.
/// Uses WebApplicationFactory pattern for proper DI container testing.
/// </summary>
[Collection("WpfTest")]
public class ConstructorResolutionTests : IDisposable
{
    private readonly WpfTestFactory _factory;
    private readonly IServiceProvider _serviceProvider;

    public ConstructorResolutionTests(WpfTestFactory factory)
    {
        _factory = factory;
        _serviceProvider = factory.Services;
    }

    public void Dispose()
    {
        // Factory handles its own disposal
        GC.SuppressFinalize(this);
    }
    [Fact]
    public void ShellViewModel_Should_Have_ActivatorUtilities_Attribute()
    {
        // Arrange
        var shellViewModelType = typeof(WileyWidget.ViewModels.ShellViewModel);

        // Act
        var constructors = shellViewModelType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var constructorsWithAttribute = constructors
            .Where(c => c.GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>() != null)
            .ToList();

        // Assert
        constructorsWithAttribute.Should().HaveCount(1, 
            "ShellViewModel should have exactly one constructor marked with [ActivatorUtilitiesConstructor]");
    }

    [Fact]
    public void ShellViewModel_Should_Not_Have_Ambiguous_Constructors()
    {
        // Arrange - Use the factory's properly configured DI container
        // This follows Microsoft's WebApplicationFactory pattern for integration testing

        // Act - Build should succeed with proper configuration
        Action act = () =>
        {
            // Resolve from scope (since ShellViewModel is scoped)
            using var scope = _serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<WileyWidget.ViewModels.ShellViewModel>();
        };

        // Assert
        act.Should().NotThrow<InvalidOperationException>(
            "ShellViewModel should not have ambiguous constructors when properly configured");
    }

    [Theory]
    [InlineData(typeof(WileyWidget.ViewModels.MainViewModel))]
    [InlineData(typeof(WileyWidget.ViewModels.DashboardViewModel))]
    [InlineData(typeof(WileyWidget.ViewModels.EnterpriseViewModel))]
    public void ViewModels_Should_Have_At_Most_One_Public_Constructor_Or_Marked_Constructor(Type viewModelType)
    {
        // Arrange & Act
        var constructors = viewModelType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var constructorsWithAttribute = constructors
            .Where(c => c.GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>() != null)
            .ToList();

        // Assert
        if (constructors.Length > 1)
        {
            constructorsWithAttribute.Should().HaveCount(1,
                $"{viewModelType.Name} has {constructors.Length} constructors and must mark one with [ActivatorUtilitiesConstructor]");
        }
    }

    [Fact]
    public void ActivatorUtilities_Should_Prefer_Marked_Constructor()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<string>("test-value");
        serviceCollection.AddSingleton<TestNumberWrapper>(sp => new TestNumberWrapper(42));
        
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Act - ActivatorUtilities should use the marked constructor
        var instance = ActivatorUtilities.CreateInstance<TestClassWithMultipleConstructors>(serviceProvider);

        // Assert
        instance.UsedMarkedConstructor.Should().BeTrue(
            "ActivatorUtilities should prefer the constructor marked with [ActivatorUtilitiesConstructor]");
    }

    [Fact]
    public void Constructor_With_Optional_Parameters_Should_Be_Resolvable()
    {
        // Arrange - Use factory's properly configured DI container
        // SimpleTestViewModelWithOptional should be resolvable from the test container

        // Act - Constructor with optional parameter should still work
        Action act = () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<SimpleTestViewModelWithOptional>();
        };

        // Assert
        act.Should().NotThrow("constructors with optional parameters should be resolvable");
    }
    
    // Test helper class for optional parameter testing
    public class SimpleTestViewModelWithOptional
    {
        public SimpleTestViewModelWithOptional(
            WileyWidget.Services.SettingsService settingsService,
            Microsoft.ApplicationInsights.TelemetryClient? telemetryClient = null)
        {
        }
    }

    // Test helper classes
    private class TestNumberWrapper
    {
        public int Value { get; }
        public TestNumberWrapper(int value) => Value = value;
    }

    private class TestClassWithMultipleConstructors
    {
        public bool UsedMarkedConstructor { get; }

        public TestClassWithMultipleConstructors(string value)
        {
            UsedMarkedConstructor = false;
        }

        [ActivatorUtilitiesConstructor]
        public TestClassWithMultipleConstructors(string value, TestNumberWrapper number)
        {
            UsedMarkedConstructor = true;
        }
    }
}
