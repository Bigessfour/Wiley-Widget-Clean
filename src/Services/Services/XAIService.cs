using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Services;

namespace WileyWidget.Services;

/// <summary>
/// xAI service implementation for AI-powered insights and analysis
/// </summary>
public class XAIService : IAIService, IDisposable
{
    private const string DefaultBaseUrl = "https://api.x.ai/v1/";
    private const string DocsUrl = "https://docs.x.ai/docs/tutorial";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<XAIService> _logger;
    private bool _disposed;
    private readonly Uri _apiBaseUri;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public XAIService(string apiKey, ILogger<XAIService> logger)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("xAI API key is required", nameof(apiKey));
        }

        _apiKey = apiKey;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var baseUrl = Environment.GetEnvironmentVariable("XAI_BASE_URL") ?? DefaultBaseUrl;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBaseUri))
        {
            throw new InvalidOperationException($"xAI base URL '{baseUrl}' is not a valid absolute URI. Set XAI_BASE_URL to a value such as {DefaultBaseUrl}.");
        }

        _apiBaseUri = parsedBaseUri;

        _httpClient = new HttpClient
        {
            BaseAddress = _apiBaseUri,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Set default headers
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    /// <summary>
    /// Get AI insights for the provided context and question
    /// </summary>
    public async Task<string> GetInsightsAsync(string context, string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be null or empty", nameof(question));

        try
        {
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
                model = "grok-4-0709",
                stream = false,
                temperature = 0.7
            };

            var response = await ExecuteWithRetryAsync(() =>
                _httpClient.PostAsJsonAsync("chat/completions", request));

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("xAI API returned {StatusCode} - {Reason}. Body: {Body}", (int)response.StatusCode, response.ReasonPhrase, responseBody);
                return BuildTroubleshootingMessage(response.StatusCode, responseBody);
            }

            var result = await response.Content.ReadFromJsonAsync<XAIResponse>();
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
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Network error calling xAI API: {Message}", ex.Message);
            return $"I couldn't reach the xAI endpoint at {_apiBaseUri}. Check firewall/proxy settings and confirm https://api.x.ai is reachable. See {DocsUrl} for credential setup.";
        }
        catch (TaskCanceledException ex)
        {
            Log.Error(ex, "xAI API request timed out");
            return $"The request to xAI timed out. Confirm the endpoint {_apiBaseUri} is accessible and reduce question size if necessary. Setup steps: {DocsUrl}.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in xAI service: {Message}", ex.Message);
            return $"Something went wrong while talking to xAI. Review application logs and verify your API key at {DocsUrl}.";
        }
    }

    /// <summary>
    /// Analyze data and provide insights
    /// </summary>
    public async Task<string> AnalyzeDataAsync(string data, string analysisType)
    {
        var question = $"Please analyze the following {analysisType} data and provide insights: {data}";
        return await GetInsightsAsync("Data Analysis", question);
    }

    /// <summary>
    /// Review application areas and provide recommendations
    /// </summary>
    public async Task<string> ReviewApplicationAreaAsync(string areaName, string currentState)
    {
        var question = $"Please review the {areaName} area with current state: {currentState}. Provide recommendations for improvement.";
        return await GetInsightsAsync("Application Review", question);
    }

    /// <summary>
    /// Generate mock data suggestions
    /// </summary>
    public async Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements)
    {
        var question = $"Please suggest mock data for {dataType} with these requirements: {requirements}";
        return await GetInsightsAsync("Mock Data Generation", question);
    }

    /// <summary>
    /// Execute HTTP request with retry logic
    /// </summary>
    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> operation)
    {
        var maxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(500);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await operation();

                // If rate limited, wait and retry
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (attempt < maxRetries)
                    {
                        Log.Warning("xAI API rate limit hit, retrying in {DelayMs}ms (attempt {Attempt})",
                                   delay.TotalMilliseconds, attempt + 1);
                        await Task.Delay(delay);
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
                await Task.Delay(delay);
                delay = delay * 2;
            }
        }

        throw new Exception($"Operation failed after {maxRetries + 1} attempts");
    }

    private string BuildTroubleshootingMessage(HttpStatusCode statusCode, string responseBody)
    {
        var status = (int)statusCode;
        switch (statusCode)
        {
            case HttpStatusCode.Unauthorized:
                return $"xAI rejected the request (HTTP {status}). Confirm your XAI_API_KEY is current and has sufficient credits. Regenerate a key in the xAI console: {DocsUrl}.";
            case HttpStatusCode.Forbidden:
                return $"xAI reported insufficient permissions (HTTP {status}). Ensure your account has access to the requested model and the key is enabled. See {DocsUrl}.";
            case (HttpStatusCode)429:
                return "xAI is rate limiting requests (HTTP 429). Wait a moment before retrying or lower request frequency.";
            case HttpStatusCode.BadRequest:
                return $"xAI could not process the request (HTTP {status}). Double-check the prompt payload and model name. Refer to {DocsUrl} for request examples.";
            case HttpStatusCode.NotFound:
                return $"xAI returned 404. Verify the API route exists and the base URL {_apiBaseUri} is correct (default https://api.x.ai/v1/).";
            default:
                return $"xAI returned HTTP {status}. Review the logs for details and confirm network access to {_apiBaseUri}. Response: {responseBody}";
        }
    }

    /// <summary>
    /// xAI API response model
    /// </summary>
    private class XAIResponse
    {
        public Choice[]? choices { get; set; }

        public class Choice
        {
            public Message? message { get; set; }
        }

        public class Message
        {
            public string? content { get; set; }
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