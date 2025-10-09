using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Interface for AI services providing insights and analysis
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Get AI insights for the provided context and question
    /// </summary>
    Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze data and provide insights
    /// </summary>
    Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Review application areas and provide recommendations
    /// </summary>
    Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate mock data suggestions
    /// </summary>
    Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default);
}