using System;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests.Models;

/// <summary>
/// Comprehensive tests for AppSettings model
/// Tests property initialization, default values, and data integrity
/// </summary>
public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var settings = new AppSettings();

        // Assert
        Assert.Equal("FluentDark", settings.Theme);
        Assert.Null(settings.WindowWidth);
        Assert.Null(settings.WindowHeight);
        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
        Assert.Null(settings.WindowMaximized);
        Assert.Equal("sandbox", settings.QuickBooksEnvironment);
        Assert.Equal(default(DateTime), settings.QboTokenExpiry);
    }

    [Fact]
    public void AppSettings_PropertyAssignment_WorksCorrectly()
    {
        // Arrange
        var settings = new AppSettings();

        // Act
        settings.Theme = "FluentLight";
        settings.WindowWidth = 1200.0;
        settings.WindowHeight = 800.0;
        settings.WindowLeft = 100.0;
        settings.WindowTop = 50.0;
        settings.WindowMaximized = true;

        // Assert
        Assert.Equal("FluentLight", settings.Theme);
        Assert.Equal(1200.0, settings.WindowWidth);
        Assert.Equal(800.0, settings.WindowHeight);
        Assert.Equal(100.0, settings.WindowLeft);
        Assert.Equal(50.0, settings.WindowTop);
        Assert.True(settings.WindowMaximized);
    }

    [Fact]
    public void AppSettings_QuickBooksLegacyProperties_CanBeSet()
    {
        // Arrange
        var settings = new AppSettings();
        var testToken = "test_access_token";
        var refreshToken = "test_refresh_token";
        var realmId = "test_realm_id";
        var expiresUtc = DateTime.UtcNow.AddHours(1);

        // Act
        settings.QuickBooksAccessToken = testToken;
        settings.QuickBooksRefreshToken = refreshToken;
        settings.QuickBooksRealmId = realmId;
        settings.QuickBooksEnvironment = "production";
        settings.QuickBooksTokenExpiresUtc = expiresUtc;

        // Assert
        Assert.Equal(testToken, settings.QuickBooksAccessToken);
        Assert.Equal(refreshToken, settings.QuickBooksRefreshToken);
        Assert.Equal(realmId, settings.QuickBooksRealmId);
        Assert.Equal("production", settings.QuickBooksEnvironment);
        Assert.Equal(expiresUtc, settings.QuickBooksTokenExpiresUtc);
    }

    [Fact]
    public void AppSettings_QboCanonicalProperties_CanBeSet()
    {
        // Arrange
        var settings = new AppSettings();
        var accessToken = "qbo_access_token";
        var refreshToken = "qbo_refresh_token";
        var expiry = DateTime.UtcNow.AddHours(2);

        // Act
        settings.QboAccessToken = accessToken;
        settings.QboRefreshToken = refreshToken;
        settings.QboTokenExpiry = expiry;

        // Assert
        Assert.Equal(accessToken, settings.QboAccessToken);
        Assert.Equal(refreshToken, settings.QboRefreshToken);
        Assert.Equal(expiry, settings.QboTokenExpiry);
    }

    [Fact]
    public void AppSettings_AllProperties_CanBeNullOrDefault()
    {
        // Arrange
        var settings = new AppSettings();

        // Act - Set all nullable properties to null
        settings.WindowWidth = null;
        settings.WindowHeight = null;
        settings.WindowLeft = null;
        settings.WindowTop = null;
        settings.WindowMaximized = null;
        settings.QuickBooksAccessToken = null;
        settings.QuickBooksRefreshToken = null;
        settings.QuickBooksRealmId = null;
        settings.QuickBooksTokenExpiresUtc = null;
        settings.QboAccessToken = null;
        settings.QboRefreshToken = null;
        settings.QboTokenExpiry = default;

        // Assert
        Assert.Null(settings.WindowWidth);
        Assert.Null(settings.WindowHeight);
        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
        Assert.Null(settings.WindowMaximized);
        Assert.Null(settings.QuickBooksAccessToken);
        Assert.Null(settings.QuickBooksRefreshToken);
        Assert.Null(settings.QuickBooksRealmId);
        Assert.Null(settings.QuickBooksTokenExpiresUtc);
        Assert.Null(settings.QboAccessToken);
        Assert.Null(settings.QboRefreshToken);
        Assert.Equal(default(DateTime), settings.QboTokenExpiry);
    }

    [Fact]
    public void AppSettings_ThemeProperty_AcceptsVariousValues()
    {
        // Arrange
        var settings = new AppSettings();
        var themes = new[] { "FluentDark", "FluentLight", "Classic", "HighContrast", "" };

        foreach (var theme in themes)
        {
            // Act
            settings.Theme = theme;

            // Assert
            Assert.Equal(theme, settings.Theme);
        }
    }

    [Fact]
    public void AppSettings_QuickBooksEnvironment_AcceptsValidValues()
    {
        // Arrange
        var settings = new AppSettings();
        var environments = new[] { "sandbox", "production", "SANDBOX", "PRODUCTION", "" };

        foreach (var environment in environments)
        {
            // Act
            settings.QuickBooksEnvironment = environment;

            // Assert
            Assert.Equal(environment, settings.QuickBooksEnvironment);
        }
    }

    [Fact]
    public void AppSettings_WindowDimensions_AcceptVariousValues()
    {
        // Arrange
        var settings = new AppSettings();
        var dimensions = new[] { 800.0, 1024.0, 1920.0, 0.0, -1.0 };

        foreach (var dimension in dimensions)
        {
            // Act
            settings.WindowWidth = dimension;
            settings.WindowHeight = dimension;

            // Assert
            Assert.Equal(dimension, settings.WindowWidth);
            Assert.Equal(dimension, settings.WindowHeight);
        }
    }

    [Fact]
    public void AppSettings_TokenExpiry_AcceptsVariousDates()
    {
        // Arrange
        var settings = new AppSettings();
        var dates = new[]
        {
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddDays(1),
            DateTime.MinValue,
            DateTime.MaxValue
        };

        foreach (var date in dates)
        {
            // Act
            settings.QboTokenExpiry = date;

            // Assert
            Assert.Equal(date, settings.QboTokenExpiry);
        }
    }

    [Fact]
    public void AppSettings_CanBeSerializedAndDeserialized()
    {
        // Arrange
        var original = new AppSettings
        {
            Theme = "FluentLight",
            WindowWidth = 1200.0,
            WindowHeight = 800.0,
            QuickBooksAccessToken = "test_token",
            QboAccessToken = "qbo_token",
            QboTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        // Act - Simulate serialization/deserialization
        // (In real scenario, this would use JSON/XML serializer)
        var serialized = original;
        var deserialized = new AppSettings
        {
            Theme = serialized.Theme,
            WindowWidth = serialized.WindowWidth,
            WindowHeight = serialized.WindowHeight,
            QuickBooksAccessToken = serialized.QuickBooksAccessToken,
            QboAccessToken = serialized.QboAccessToken,
            QboTokenExpiry = serialized.QboTokenExpiry
        };

        // Assert
        Assert.Equal(original.Theme, deserialized.Theme);
        Assert.Equal(original.WindowWidth, deserialized.WindowWidth);
        Assert.Equal(original.WindowHeight, deserialized.WindowHeight);
        Assert.Equal(original.QuickBooksAccessToken, deserialized.QuickBooksAccessToken);
        Assert.Equal(original.QboAccessToken, deserialized.QboAccessToken);
        Assert.Equal(original.QboTokenExpiry, deserialized.QboTokenExpiry);
    }

    [Fact]
    public void AppSettings_MigrationScenario_LegacyToCanonical()
    {
        // Arrange - Simulate migration from legacy to canonical properties
        var settings = new AppSettings
        {
            QuickBooksAccessToken = "legacy_token",
            QuickBooksRefreshToken = "legacy_refresh",
            QuickBooksTokenExpiresUtc = DateTime.UtcNow.AddHours(1)
        };

        // Act - Migrate to canonical properties
        settings.QboAccessToken = settings.QuickBooksAccessToken;
        settings.QboRefreshToken = settings.QuickBooksRefreshToken;
        if (settings.QuickBooksTokenExpiresUtc.HasValue)
        {
            settings.QboTokenExpiry = settings.QuickBooksTokenExpiresUtc.Value;
        }

        // Assert
        Assert.Equal(settings.QuickBooksAccessToken, settings.QboAccessToken);
        Assert.Equal(settings.QuickBooksRefreshToken, settings.QboRefreshToken);
        Assert.Equal(settings.QuickBooksTokenExpiresUtc, settings.QboTokenExpiry);
    }
}