using System.Text.Json;
using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IOctoTool
{
    string Name { get; }
    string Description { get; }
    GeminiFunctionDeclaration Declaration { get; }
    Task<string> ExecuteAsync(JsonElement arguments, OctoScope scope, CancellationToken cancellationToken = default);
}
