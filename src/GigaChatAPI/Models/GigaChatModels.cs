using System.Text.Json.Serialization;

namespace GigaChatAPI.Models;

public class GigaChatTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public class ModelsResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<ModelInfo> Data { get; set; } = [];
}

public class ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; set; } = string.Empty;
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty; // "user", "assistant", "system", "function"

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; } // For function responses

    [JsonPropertyName("functions_state_id")]
    public string? FunctionsStateId { get; set; }

    [JsonPropertyName("function_call")]
    public FunctionCall? FunctionCall { get; set; }
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "GigaChat";

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("repetition_penalty")]
    public double RepetitionPenalty { get; set; } = 1.0;

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    [JsonPropertyName("functions")]
    public List<FunctionDefinition>? Functions { get; set; }

    [JsonPropertyName("function_call")]
    public string? FunctionCall { get; set; } // "auto", "none", or specific function name
}

public class ChatResponse
{
    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = [];

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; } = new();
}

public class Choice
{
    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = string.Empty; // "stop", "function_call", "length"

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();
}

public class Usage
{
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("system_tokens")]
    public int SystemTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}

public class FunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }

    [JsonPropertyName("return_parameters")]
    public object? ReturnParameters { get; set; }
}
