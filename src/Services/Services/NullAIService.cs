using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// No-op AI service used in development/testing when API keys are not configured.
/// Prevents startup failures by providing predictable stub responses.
/// </summary>
public class NullAIService : IAIService
{
    public Task<string> GetInsightsAsync(string context, string question)
        => Task.FromResult("[Dev Stub] AI insights are disabled in development. Configure XAI_API_KEY to enable.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType)
        => Task.FromResult("[Dev Stub] Data analysis is disabled in development.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState)
        => Task.FromResult("[Dev Stub] Review is disabled in development.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements)
        => Task.FromResult("[Dev Stub] Mock data generation is disabled in development.");
}
