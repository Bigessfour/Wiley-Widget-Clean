using Xunit;
using WileyWidget.Services;
using System;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using System.Net.Http;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Tests;

/// <summary>
/// Tests for XAI service connection and functionality
/// </summary>
public class XAIServiceTests : IDisposable
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<XAIService>> _mockLogger;
    private readonly MockHttpMessageHandler _mockHttpHandler;
    private readonly HttpClient _mockHttpClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public XAIServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<XAIService>>();
        _mockHttpHandler = new MockHttpMessageHandler();

        // Setup configuration
        _mockConfiguration.Setup(c => c["XAI:ApiKey"]).Returns("test-api-key-12345678901234567890");
        _mockConfiguration.Setup(c => c["XAI:BaseUrl"]).Returns("https://api.x.ai/v1/");
        _mockConfiguration.Setup(c => c["XAI:TimeoutSeconds"]).Returns("15");
        _mockConfiguration.Setup(c => c["XAI:Model"]).Returns("grok-4-0709");

        // Setup HTTP client factory
        _mockHttpClient = new HttpClient(_mockHttpHandler)
        {
            BaseAddress = new Uri("https://api.x.ai/v1/")
        };
        _mockHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key-12345678901234567890");

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient("AIServices")).Returns(_mockHttpClient);
        _httpClientFactory = mockFactory.Object;
    }

    [Fact]
    public async Task GetInsightsAsync_WithValidInput_ReturnsResponse()
    {
        // Arrange
        _mockHttpHandler.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", "{\"choices\":[{\"message\":{\"content\":\"Test AI response\"}}]}");

        using var service = new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890");

        // Act
        var result = await service.GetInsightsAsync("Test context", "What is 2+2?");

        // Assert
        result.Should().Be("Test AI response");
    }

    [Fact]
    public async Task GetInsightsAsync_WithEmptyContext_ThrowsArgumentException()
    {
        // Arrange
        using var service = new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetInsightsAsync("", "What is 2+2?"));
    }

    [Fact]
    public async Task GetInsightsAsync_WithEmptyQuestion_ThrowsArgumentException()
    {
        // Arrange
        using var service = new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetInsightsAsync("Test context", ""));
    }

    [Fact]
    public async Task GetInsightsAsync_WithApiError_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", "{\"error\":{\"type\":\"invalid_request\",\"message\":\"Bad request\",\"code\":\"400\"}}");

        using var service = new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890");

        // Act
        var result = await service.GetInsightsAsync("Test context", "What is 2+2?");

        // Assert
        result.Should().Be("API error: Bad request");
    }

    [Fact]
    public async Task GetInsightsAsync_WithAuthenticationError_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.When("https://api.x.ai/v1/chat/completions")
            .Respond(HttpStatusCode.Unauthorized);

        using var service = new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890");

        // Act
        var result = await service.GetInsightsAsync("Test context", "What is 2+2?");

        // Assert
        result.Should().Contain("Authentication failed");
    }

    [Fact]
    public async Task AnalyzeDataAsync_ReturnsResponse()
    {
        // Arrange
        _mockHttpHandler.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", "{\"choices\":[{\"message\":{\"content\":\"Data analysis result\"}}]}");

        using var service = new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890");

        // Act
        var result = await service.AnalyzeDataAsync("Sample data: 1,2,3,4,5", "basic statistics");

        // Assert
        result.Should().Be("Data analysis result");
    }

    [Fact]
    public async Task ReviewApplicationAreaAsync_ReturnsResponse()
    {
        // Arrange
        _mockHttpHandler.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", "{\"choices\":[{\"message\":{\"content\":\"Review recommendations\"}}]}");

        using var service = new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890");

        // Act
        var result = await service.ReviewApplicationAreaAsync("User Interface", "Current UI needs improvement");

        // Assert
        result.Should().Be("Review recommendations");
    }

    [Fact]
    public async Task GenerateMockDataSuggestionsAsync_ReturnsResponse()
    {
        // Arrange
        _mockHttpHandler.When("https://api.x.ai/v1/chat/completions")
            .Respond("application/json", "{\"choices\":[{\"message\":{\"content\":\"Mock data suggestions\"}}]}");

        using var service = new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890");

        // Act
        var result = await service.GenerateMockDataSuggestionsAsync("customer names", "realistic municipal utility customers");

        // Assert
        result.Should().Be("Mock data suggestions");
    }

    [Fact]
    public void Constructor_WithInvalidApiKey_ThrowsArgumentNullException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["XAI:ApiKey"]).Returns((string)null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890"));
    }

    [Fact]
    public void Constructor_WithShortApiKey_ThrowsArgumentException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["XAI:ApiKey"]).Returns("short");

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new XAIService(_httpClientFactory, _mockConfiguration.Object, _mockLogger.Object, "test-api-key-12345678901234567890"));
    }

    public void Dispose()
    {
        _mockHttpClient?.Dispose();
    }
}
