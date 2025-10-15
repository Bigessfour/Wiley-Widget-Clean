using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Services;

namespace WileyWidget.Services;

/// <summary>
/// xAI service implementation for AI-powered insights and analysis
/// </summary>
public class XAIService : IAIService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<XAIService> _logger;
    private readonly IConfiguration _configuration;
    private bool _disposed;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public XAIService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<XAIService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _apiKey = configuration["XAI:ApiKey"] ?? throw new ArgumentNullException("XAI:ApiKey", "XAI API key not configured");

        var baseUrl = configuration["XAI:BaseUrl"] ?? "https://api.x.ai/v1/";
        var timeoutSeconds = double.Parse(configuration["XAI:TimeoutSeconds"] ?? "15");

        // Validate API key format (basic check)
        if (_apiKey.Length < 20)
        {
            throw new ArgumentException("API key appears to be invalid (too short)", "XAI:ApiKey");
        }

        _httpClient = httpClientFactory.CreateClient("AIServices");
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Set default headers only if not already set by the named client
        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
    }

    /// <summary>
    /// Get AI insights for the provided context and question
    /// </summary>
    public async Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context))
            throw new ArgumentException("Context cannot be null or empty", nameof(context));
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be null or empty", nameof(question));

        try
        {
            var model = _configuration["XAI:Model"] ?? "grok-4-0709";
            var request = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"You are a helpful AI assistant for a municipal utility management application called Wiley Widget. Context: {context}"
                    },
                    new
                    {
                        role = "user",
                        content = question
                    }
                },
                model = model,
                stream = false,
                temperature = 0.7
            };

            var response = await ExecuteWithRetryAsync(() =>
                _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken), cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<XAIResponse>(cancellationToken: cancellationToken);
            if (result?.error != null)
            {
                Log.Error("xAI API error: {ErrorType} - {ErrorMessage}", result.error.type, result.error.message);
                return $"API error: {result.error.message}";
            }

            if (result?.choices?.Length > 0)
            {
                var content = result.choices[0].message?.content;
                if (!string.IsNullOrEmpty(content))
                {
                    Log.Information("Successfully received xAI response for question: {Question}", question);
                    return content;
                }
            }

            Log.Warning("xAI API returned empty or invalid response");
            return "I apologize, but I received an empty response. Please try rephrasing your question.";
        }
        catch (InvalidOperationException ex)
        {
            Log.Error(ex, "xAI API authentication failed: {Message}", ex.Message);
            return "Authentication failed. Please check your API key configuration.";
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Network error calling xAI API: {Message}", ex.Message);
            return "I'm experiencing network connectivity issues. Please check your internet connection and try again.";
        }
        catch (TaskCanceledException ex)
        {
            Log.Error(ex, "xAI API request timed out after {TimeoutSeconds} seconds", _httpClient.Timeout.TotalSeconds);
            return $"The request timed out after {_httpClient.Timeout.TotalSeconds} seconds. The xAI service may be experiencing high load. Please try again later.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in xAI service: {Message}", ex.Message);
            return "I encountered an unexpected error. Please try again later.";
        }
    }

    /// <summary>
    /// Analyze data and provide insights
    /// </summary>
    public async Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
    {
        var question = $"Please analyze the following {analysisType} data and provide insights: {data}";
        return await GetInsightsAsync("Data Analysis", question, cancellationToken);
    }

    /// <summary>
    /// Review application areas and provide recommendations
    /// </summary>
    public async Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
    {
        var question = $"Please review the {areaName} area with current state: {currentState}. Provide recommendations for improvement.";
        return await GetInsightsAsync("Application Review", question, cancellationToken);
    }

    /// <summary>
    /// Generate mock data suggestions
    /// </summary>
    public async Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
    {
        var question = $"Please suggest mock data for {dataType} with these requirements: {requirements}";
        return await GetInsightsAsync("Mock Data Generation", question, cancellationToken);
    }

    /// <summary>
    /// Execute HTTP request with retry logic
    /// </summary>
    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> operation, CancellationToken cancellationToken = default)
    {
        var maxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(500);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await operation();

                // Handle authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Log.Error("xAI API authentication failed. Check API key.");
                    throw new InvalidOperationException("Authentication failed. Please check your API key configuration.");
                }

                // If rate limited, wait and retry
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (attempt < maxRetries)
                    {
                        Log.Warning("xAI API rate limit hit, retrying in {DelayMs}ms (attempt {Attempt})",
                                   delay.TotalMilliseconds, attempt + 1);
                        await Task.Delay(delay, cancellationToken);
                        delay = delay * 2; // Exponential backoff
                        continue;
                    }
                }

                return response;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                Log.Warning(ex, "HTTP request failed, retrying in {DelayMs}ms (attempt {Attempt})",
                           delay.TotalMilliseconds, attempt + 1);
                await Task.Delay(delay, cancellationToken);
                delay = delay * 2;
            }
        }

        throw new Exception($"Operation failed after {maxRetries + 1} attempts");
    }

    /// <summary>
    /// xAI API response model
    /// </summary>
    private class XAIResponse
    {
        public Choice[] choices { get; set; }
        public XAIError error { get; set; }

        public class Choice
        {
            public Message message { get; set; }
        }

        public class Message
        {
            public string content { get; set; }
        }

        public class XAIError
        {
            public string message { get; set; }
            public string type { get; set; }
            public string code { get; set; }
        }
    }

    /// <summary>
    /// Dispose of managed resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose pattern implementation
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
            _disposed = true;
        }
    }
}