using Xunit;
using WileyWidget.Services;
using System;
using System.Threading.Tasks;
using System.Globalization;

namespace WileyWidget.Tests;

/// <summary>
/// Tests for XAI service connection and functionality
/// </summary>
public class XAIServiceTests
{
    private readonly string _apiKey;

    public XAIServiceTests()
    {
        _apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("XAI_API_KEY environment variable is not set");
        }
    }

    [Fact]
    public async Task GetInsightsAsync_WithValidQuestion_ReturnsResponse()
    {
        // Arrange
        using var service = new XAIService(_apiKey);

        // Act
        var result = await service.GetInsightsAsync("Test context", "What is 2+2?");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.DoesNotContain("error", result.ToLower(CultureInfo.InvariantCulture));
        Assert.DoesNotContain("exception", result.ToLower(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task GetInsightsAsync_WithEmptyQuestion_ThrowsArgumentException()
    {
        // Arrange
        using var service = new XAIService(_apiKey);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetInsightsAsync("Test context", ""));
    }

    [Fact]
    public async Task AnalyzeDataAsync_ReturnsResponse()
    {
        // Arrange
        using var service = new XAIService(_apiKey);

        // Act
        var result = await service.AnalyzeDataAsync("Sample data: 1,2,3,4,5", "basic statistics");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ReviewApplicationAreaAsync_ReturnsResponse()
    {
        // Arrange
        using var service = new XAIService(_apiKey);

        // Act
        var result = await service.ReviewApplicationAreaAsync("User Interface", "Current UI needs improvement");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GenerateMockDataSuggestionsAsync_ReturnsResponse()
    {
        // Arrange
        using var service = new XAIService(_apiKey);

        // Act
        var result = await service.GenerateMockDataSuggestionsAsync("customer names", "realistic municipal utility customers");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}