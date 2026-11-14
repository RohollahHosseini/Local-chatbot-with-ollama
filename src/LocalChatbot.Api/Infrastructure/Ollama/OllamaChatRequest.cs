namespace LocalChatbot.Api.Infrastructure.Ollama;

    //public sealed record OllamaChatRequest(string Model, MessageChatRequest Messages);
    //public record MessageChatRequest(ChatRequestRole Role,string Content, string[]? images = null, object[]? Tool_Call=null);



public sealed record OllamaChatRequest(
    string Model,
    IReadOnlyList<OllamaMessage> Messages,
    bool Stream
);

public enum ChatRequestRole:byte
{
    system=1,
    user=2,
    assitanse=3,
    tool=4
}
