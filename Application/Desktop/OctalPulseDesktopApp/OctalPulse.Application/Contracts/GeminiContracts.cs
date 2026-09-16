using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctalPulse.Application.Contracts;

public class GeminiChatRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("systemInstruction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiContent? SystemInstruction { get; set; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GeminiTool>? Tools { get; set; }

    [JsonPropertyName("toolConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiToolConfig? ToolConfig { get; set; }

    [JsonPropertyName("generationConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

public class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; set; }
}

public class GeminiContent
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();

    public static GeminiContent ForUser(string text) => new()
    {
        Role = "user",
        Parts = new List<GeminiPart> { new() { Text = text } }
    };

    public static GeminiContent ForModel(string text) => new()
    {
        Role = "model",
        Parts = new List<GeminiPart> { new() { Text = text } }
    };

    public static GeminiContent ForSystem(string text) => new()
    {
        Role = "system",
        Parts = new List<GeminiPart> { new() { Text = text } }
    };

    public static GeminiContent ForFunctionResponse(string functionName, object responseData) => new()
    {
        Role = "function",
        Parts = new List<GeminiPart>
        {
            new()
            {
                FunctionResponse = new GeminiFunctionResponse
                {
                    Name = functionName,
                    Response = responseData
                }
            }
        }
    };
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("functionCall")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiFunctionCall? FunctionCall { get; set; }

    [JsonPropertyName("functionResponse")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiFunctionResponse? FunctionResponse { get; set; }
}

public class GeminiFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public JsonElement Args { get; set; }
}

public class GeminiFunctionResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public object Response { get; set; } = new();
}

public class GeminiTool
{
    [JsonPropertyName("functionDeclarations")]
    public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = new();
}

public class GeminiToolConfig
{
    [JsonPropertyName("functionCallingConfig")]
    public GeminiFunctionCallingConfig FunctionCallingConfig { get; set; } = new();
}

public class GeminiFunctionCallingConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "AUTO";
}

public class GeminiFunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public GeminiSchema Parameters { get; set; } = new();
}

public class GeminiSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "OBJECT";

    [JsonPropertyName("properties")]
    public Dictionary<string, GeminiSchemaProperty> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }
}

public class GeminiSchemaProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "STRING";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiSchemaProperty? Items { get; set; }
}

public class GeminiChatResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }

    [JsonPropertyName("error")]
    public GeminiError? Error { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }
}

public class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int? PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int? CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int? TotalTokenCount { get; set; }
}

public class GeminiError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
