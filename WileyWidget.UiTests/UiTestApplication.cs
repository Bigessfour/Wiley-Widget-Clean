using System;
using System.Windows;
using WileyWidget.Tests;

namespace WileyWidget.UiTests;

/// <summary>
/// Base class for UI tests that require dependency injection.
/// Initializes the DI container and WPF Application context.
/// </summary>
public class UiTestApplication : TestApplication
{
    static UiTestApplication()
    {
        // Initialize DI container once for all UI tests
        TestDiSetup.Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the UiTestApplication class.
    /// </summary>
    public UiTestApplication()
    {
        // DI container is already initialized in static constructor
    }
}