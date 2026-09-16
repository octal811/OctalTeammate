using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Gemini;

public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiClient> _logger;
    private static string? _cachedResolvedModel;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public GeminiClient(HttpClient httpClient, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<string>> GetAvailableModelsAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new List<string>();

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey.Trim()}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to list models from Gemini API: {StatusCode}", response.StatusCode);
                return new List<string>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                var list = new List<string>();
                foreach (var item in modelsArray.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var cleanName = name.StartsWith("models/") ? name["models/".Length..] : name;

                    if (item.TryGetProperty("supportedGenerationMethods", out var methodsProp))
                    {
                        bool supportsGenerateContent = false;
                        foreach (var m in methodsProp.EnumerateArray())
                        {
                            if (m.GetString()?.Equals("generateContent", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                supportsGenerateContent = true;
                                break;
                            }
                        }

                        if (supportsGenerateContent)
                        {
                            list.Add(cleanName);
                        }
                    }
                }

                _logger.LogInformation("Discovered {Count} supported Gemini models for user key: {Models}", list.Count, string.Join(", ", list));
                return list;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception querying Gemini ListModels endpoint");
        }

        return new List<string>();
    }

    public async Task<string?> ResolveBestModelAsync(string apiKey, string? preferredModel = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_cachedResolvedModel))
        {
            return _cachedResolvedModel;
        }

        var available = await GetAvailableModelsAsync(apiKey, cancellationToken);
        if (available.Count == 0)
        {
            return preferredModel ?? "gemini-2.0-flash";
        }

        if (!string.IsNullOrWhiteSpace(preferredModel) && available.Contains(preferredModel, StringComparer.OrdinalIgnoreCase))
        {
            _cachedResolvedModel = preferredModel;
            return preferredModel;
        }

        // Prioritize modern flash models
        var selected = available.FirstOrDefault(m => m.Contains("flash", StringComparison.OrdinalIgnoreCase) && !m.Contains("8b", StringComparison.OrdinalIgnoreCase))
                    ?? available.FirstOrDefault(m => m.Contains("flash", StringComparison.OrdinalIgnoreCase))
                    ?? available.FirstOrDefault(m => m.Contains("pro", StringComparison.OrdinalIgnoreCase))
                    ?? available.FirstOrDefault();

        if (!string.IsNullOrEmpty(selected))
        {
            _cachedResolvedModel = selected;
            _logger.LogInformation("Resolved active Gemini model to: {Model}", selected);
        }

        return selected;
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, string model = "", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        try
        {
            var models = await GetAvailableModelsAsync(apiKey, cancellationToken);
            return models.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate Gemini API key");
            return false;
        }
    }

    public async Task<GeminiChatResponse> GenerateContentAsync(
        GeminiChatRequest request,
        string apiKey,
        string model = "",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new GeminiChatResponse
            {
                Error = new GeminiError
                {
                    Code = 401,
                    Message = "No Gemini API key configured. Please configure your API key in Settings."
                }
            };
        }

        var activeModel = !string.IsNullOrWhiteSpace(model) ? model : _cachedResolvedModel;
        if (string.IsNullOrWhiteSpace(activeModel))
        {
            activeModel = await ResolveBestModelAsync(apiKey, cancellationToken: cancellationToken) ?? "gemini-2.0-flash";
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{activeModel}:generateContent?key={apiKey.Trim()}";

        try
        {
            var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API call for model {Model} failed with status {StatusCode}: {ResponseBody}", activeModel, response.StatusCode, responseBody);

                // If the model was not found, retired, or no longer available, automatically query available models and retry with a working model!
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    responseBody.Contains("not found") ||
                    responseBody.Contains("no longer available") ||
                    responseBody.Contains("not supported for generateContent"))
                {
                    _cachedResolvedModel = null; // Invalidate cache
                    var fallbackModel = await ResolveBestModelAsync(apiKey, cancellationToken: cancellationToken);

                    if (!string.IsNullOrEmpty(fallbackModel) && !fallbackModel.Equals(activeModel, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Retrying Gemini call with fallback model: {FallbackModel}", fallbackModel);
                        return await GenerateContentAsync(request, apiKey, fallbackModel, cancellationToken);
                    }
                }

                try
                {
                    var errorRoot = JsonSerializer.Deserialize<GeminiChatResponse>(responseBody, JsonOptions);
                    if (errorRoot?.Error != null)
                    {
                        return errorRoot;
                    }
                }
                catch
                {
                    // Fall through to generic error
                }

                return new GeminiChatResponse
                {
                    Error = new GeminiError
                    {
                        Code = (int)response.StatusCode,
                        Message = $"Gemini API request failed ({response.StatusCode}): {responseBody}"
                    }
                };
            }

            var result = JsonSerializer.Deserialize<GeminiChatResponse>(responseBody, JsonOptions);
            return result ?? new GeminiChatResponse
            {
                Error = new GeminiError { Code = 500, Message = "Received empty response from Gemini API." }
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception communicating with Gemini API");
            return new GeminiChatResponse
            {
                Error = new GeminiError
                {
                    Code = 500,
                    Message = $"Connection to Gemini failed: {ex.Message}"
                }
            };
        }
    }
}
