using System.Text.Json;
using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IOctoToolRegistry
{
    IReadOnlyList<GeminiTool> GetGeminiTools();
    Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, OctoScope scope, CancellationToken cancellationToken = default);
    IOctoTool? GetTool(string toolName);
}
