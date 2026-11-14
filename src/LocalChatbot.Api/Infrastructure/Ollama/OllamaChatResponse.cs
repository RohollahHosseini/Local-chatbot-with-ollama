using System.Text.Json.Serialization;

namespace LocalChatbot.Api.Infrastructure.Ollama;

//public sealed record OllamaChatResponse(string Model,string created_at, MessageResponse message,bool Done,string Done_Reason,int Total_Duration,int Load_Duration,int prompt_eval_count, int prompt_eval_duration,int eval_count,int eval_duration);

//public sealed record MessageResponse(ChatRequestRole role,string Content);


public sealed class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; init; }
}

public sealed class OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}