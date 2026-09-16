using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IOctoAgent
{
    Task<OctoAgentResult> ProcessMessageAsync(
        string userMessage,
        OctoScope scope,
        IReadOnlyList<OctoMessage> conversationHistory,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default);
}
