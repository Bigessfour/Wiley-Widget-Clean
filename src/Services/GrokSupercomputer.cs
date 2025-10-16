#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of IGrokSupercomputer for AI-powered municipal analysis
/// </summary>
public class GrokSupercomputer : IGrokSupercomputer
{
    private readonly ILogger<GrokSupercomputer> _logger;
    private readonly IAIService _aiService;

    /// <summary>
    /// Initializes a new instance of the GrokSupercomputer class
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="aiService">The AI service for processing queries</param>
    public GrokSupercomputer(ILogger<GrokSupercomputer> logger, IAIService aiService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
    }

    /// <summary>
    /// Processes a query and returns an AI-generated response
    /// </summary>
    /// <param name="query">The query to process</param>
    /// <returns>The AI response</returns>
    public async Task<string> ProcessQueryAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace", nameof(query));

        try
        {
            _logger.LogInformation("Processing query: {Query}", query);

            // Use AI service for actual processing
            var context = "Municipal utility management and budgeting system";
            return await _aiService.GetInsightsAsync(context, query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Analyzes municipal data and provides insights
    /// </summary>
    /// <param name="data">The data to analyze</param>
    /// <param name="context">Additional context for the analysis</param>
    /// <returns>Analysis results</returns>
    public async Task<string> AnalyzeMunicipalDataAsync(object data, string? context = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        try
        {
            _logger.LogInformation("Analyzing municipal data with context: {Context}", context);

            // Convert data to string representation for AI analysis
            var dataString = data.ToString() ?? "No data representation available";
            var analysisType = "municipal_data_analysis";
            
            return await _aiService.AnalyzeDataAsync(dataString, analysisType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing municipal data");
            throw;
        }
    }

    /// <summary>
    /// Generates recommendations based on the provided data
    /// </summary>
    /// <param name="data">The data to base recommendations on</param>
    /// <returns>Generated recommendations</returns>
    public async Task<string> GenerateRecommendationsAsync(object data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        try
        {
            _logger.LogInformation("Generating recommendations based on data: {DataType}", data.GetType().Name);

            // Convert data to string and use AI service for recommendations
            var dataString = data.ToString() ?? "No data representation available";
            var context = "Municipal utility management and budgeting system";
            var question = $"Based on this data: {dataString}, what recommendations would you make for improving municipal operations and financial management?";
            
            return await _aiService.GetInsightsAsync(context, question);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recommendations");
            throw;
        }
    }
}