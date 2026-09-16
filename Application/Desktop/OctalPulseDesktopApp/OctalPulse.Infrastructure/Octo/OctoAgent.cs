using System.Text.Json;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Octo;

public class OctoAgent : IOctoAgent
{
    private readonly IGeminiClient _geminiClient;
    private readonly IOctoToolRegistry _toolRegistry;
    private readonly ISecureStorageService _secureStorage;
    private readonly IUserSession _userSession;
    private readonly ILogger<OctoAgent> _logger;

    private const string GeminiApiKeySecretKey = "OctalPulse_GeminiApiKey";
    private const int MaxToolCallRounds = 6;

    public OctoAgent(
        IGeminiClient geminiClient,
        IOctoToolRegistry toolRegistry,
        ISecureStorageService secureStorage,
        IUserSession userSession,
        ILogger<OctoAgent> logger)
    {
        _geminiClient = geminiClient;
        _toolRegistry = toolRegistry;
        _secureStorage = secureStorage;
        _userSession = userSession;
        _logger = logger;
    }

    public async Task<OctoAgentResult> ProcessMessageAsync(
        string userMessage,
        OctoScope scope,
        IReadOnlyList<OctoMessage> conversationHistory,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _secureStorage.GetSecret(GeminiApiKeySecretKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new OctoAgentResult
            {
                Success = false,
                ErrorMessage = "No Gemini API key is configured. Please visit Settings to connect your Google Gemini API key.",
                ResponseText = "Hi! I'm Octo. To get started, please add your Google Gemini API key in **Settings & Integrations**. Your key is securely stored on your device using Windows DPAPI and never leaves your computer."
            };
        }

        statusCallback?.Invoke("Octo is thinking...");

        try
        {
            var systemInstruction = OctoPromptBuilder.BuildSystemPrompt(scope, _userSession.Name);
            var tools = _toolRegistry.GetGeminiTools();

            // Prepare Gemini chat request
            var request = new GeminiChatRequest
            {
                SystemInstruction = GeminiContent.ForSystem(systemInstruction),
                Tools = tools.ToList(),
                ToolConfig = new GeminiToolConfig
                {
                    FunctionCallingConfig = new GeminiFunctionCallingConfig { Mode = "AUTO" }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.4f,
                    MaxOutputTokens = 2048
                }
            };

            // Build historical contents (limit to recent messages to preserve token budget)
            var recentHistory = conversationHistory
                .Where(m => !m.IsError && !string.IsNullOrWhiteSpace(m.Content))
                .TakeLast(10)
                .ToList();

            foreach (var msg in recentHistory)
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    request.Contents.Add(GeminiContent.ForUser(msg.Content));
                }
                else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    request.Contents.Add(GeminiContent.ForModel(msg.Content));
                }
            }

            // Append current user message
            request.Contents.Add(GeminiContent.ForUser(userMessage));

            int rounds = 0;
            int totalToolsExecuted = 0;

            while (rounds < MaxToolCallRounds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rounds++;

                var response = await _geminiClient.GenerateContentAsync(request, apiKey, cancellationToken: cancellationToken);

                if (response.Error != null)
                {
                    _logger.LogWarning("Gemini returned error: {ErrorCode} - {ErrorMessage}", response.Error.Code, response.Error.Message);
                    return new OctoAgentResult
                    {
                        Success = false,
                        ErrorMessage = response.Error.Message,
                        ResponseText = $"Sorry, Gemini encountered an error: {response.Error.Message}"
                    };
                }

                var candidate = response.Candidates?.FirstOrDefault();
                if (candidate?.Content == null)
                {
                    return new OctoAgentResult
                    {
                        Success = false,
                        ResponseText = "I wasn't able to generate a response. Please try asking again."
                    };
                }

                // Check for function calls
                var functionCalls = candidate.Content.Parts
                    .Where(p => p.FunctionCall != null)
                    .Select(p => p.FunctionCall!)
                    .ToList();

                if (functionCalls.Count == 0)
                {
                    // Model emitted text response!
                    var textResponse = candidate.Content.Parts.FirstOrDefault(p => !string.IsNullOrEmpty(p.Text))?.Text ?? string.Empty;
                    return new OctoAgentResult
                    {
                        Success = true,
                        ResponseText = textResponse,
                        ExecutedToolCount = totalToolsExecuted
                    };
                }

                // Add model turn with function calls to conversation
                request.Contents.Add(candidate.Content);

                // Execute each tool and return response parts
                foreach (var fc in functionCalls)
                {
                    totalToolsExecuted++;
                    var friendlyToolName = FormatFriendlyToolName(fc.Name);
                    statusCallback?.Invoke($"{friendlyToolName}...");

                    _logger.LogInformation("Octo executing tool call {ToolName} with args: {Args}", fc.Name, fc.Args.ToString());
                    var toolOutputJson = await _toolRegistry.ExecuteToolAsync(fc.Name, fc.Args, scope, cancellationToken);

                    // Parse tool response to object for Gemini function response
                    object parsedResult;
                    try
                    {
                        parsedResult = JsonSerializer.Deserialize<JsonElement>(toolOutputJson);
                    }
                    catch
                    {
                        parsedResult = new { result = toolOutputJson };
                    }

                    // Add function response turn
                    request.Contents.Add(GeminiContent.ForFunctionResponse(fc.Name, parsedResult));
                }

                statusCallback?.Invoke("Analyzing retrieved data...");
            }

            return new OctoAgentResult
            {
                Success = true,
                ResponseText = "I gathered the task data, but reached the query step limit. Here is what I know so far: please ask a more specific follow-up question.",
                ExecutedToolCount = totalToolsExecuted
            };
        }
        catch (OperationCanceledException)
        {
            return new OctoAgentResult
            {
                Success = false,
                ErrorMessage = "The request was canceled."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in OctoAgent.ProcessMessageAsync");
            return new OctoAgentResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ResponseText = $"An unexpected error occurred while communicating with Octo: {ex.Message}"
            };
        }
    }

    private static string FormatFriendlyToolName(string toolName)
    {
        return toolName switch
        {
            "get_accessible_projects" => "Looking at your accessible projects",
            "get_tracks" => "Checking project tracks",
            "get_major_tasks" => "Inspecting major tasks in track",
            "get_minor_tasks" => "Reading subtask checklist",
            "get_task_details" => "Examining task details",
            "compare_tasks" => "Comparing selected tasks",
            "analyze_task_dependencies" => "Analyzing task sequence & blockers",
            _ => "Retrieving project context"
        };
    }
}
