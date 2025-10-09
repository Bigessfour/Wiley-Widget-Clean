#nullable enable

using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Interface for Grok supercomputer AI services
/// </summary>
public interface IGrokSupercomputer
{
    /// <summary>
    /// Processes a query and returns an AI-generated response
    /// </summary>
    /// <param name="query">The query to process</param>
    /// <returns>The AI response</returns>
    Task<string> ProcessQueryAsync(string query);

    /// <summary>
    /// Analyzes municipal data and provides insights
    /// </summary>
    /// <param name="data">The data to analyze</param>
    /// <param name="context">Additional context for the analysis</param>
    /// <returns>Analysis results</returns>
    Task<string> AnalyzeMunicipalDataAsync(object data, string? context = null);

    /// <summary>
    /// Generates recommendations based on the provided data
    /// </summary>
    /// <param name="data">The data to base recommendations on</param>
    /// <returns>Generated recommendations</returns>
    Task<string> GenerateRecommendationsAsync(object data);
}