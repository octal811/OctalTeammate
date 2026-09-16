using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IGeminiClient
{
    Task<bool> ValidateApiKeyAsync(string apiKey, string model = "", CancellationToken cancellationToken = default);
    Task<GeminiChatResponse> GenerateContentAsync(GeminiChatRequest request, string apiKey, string model = "", CancellationToken cancellationToken = default);
    Task<List<string>> GetAvailableModelsAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<string?> ResolveBestModelAsync(string apiKey, string? preferredModel = null, CancellationToken cancellationToken = default);
}
