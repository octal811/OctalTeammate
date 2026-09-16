using System.Text.Json;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Octo;

public class OctoToolRegistry : IOctoToolRegistry
{
    private readonly Dictionary<string, IOctoTool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<OctoToolRegistry> _logger;

    public OctoToolRegistry(IEnumerable<IOctoTool> tools, ILogger<OctoToolRegistry> logger)
    {
        _logger = logger;
        foreach (var tool in tools)
        {
            _tools[tool.Name] = tool;
        }
    }

    public IReadOnlyList<GeminiTool> GetGeminiTools()
    {
        return new List<GeminiTool>
        {
            new()
            {
                FunctionDeclarations = _tools.Values.Select(t => t.Declaration).ToList()
            }
        };
    }

    public async Task<string> ExecuteToolAsync(
        string toolName,
        JsonElement arguments,
        OctoScope scope,
        CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            _logger.LogWarning("Gemini requested unknown tool '{ToolName}'", toolName);
            return JsonSerializer.Serialize(new { error = $"Tool '{toolName}' does not exist." });
        }

        try
        {
            _logger.LogInformation("Executing Octo tool '{ToolName}' with scope {Scope}", toolName, scope.DisplayText);
            return await tool.ExecuteAsync(arguments, scope, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool '{ToolName}'", toolName);
            return JsonSerializer.Serialize(new { error = $"Error executing tool '{toolName}': {ex.Message}" });
        }
    }

    public IOctoTool? GetTool(string toolName)
    {
        _tools.TryGetValue(toolName, out var tool);
        return tool;
    }
}
