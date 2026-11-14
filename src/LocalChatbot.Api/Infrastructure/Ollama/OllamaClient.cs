using LocalChatbot.Api.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LocalChatbot.Api.Infrastructure.Ollama;

public class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    public string Model;
    public OllamaClient(
        HttpClient httpClient,
        IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        Model = _options.ChatModel;
    }

    public async Task StreamChatAsync(
    IReadOnlyList<OllamaMessage> messages,
    Func<string, Task> onContent,
    CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
            Model: _options.ChatModel,
            Messages: messages,
            Stream: true
        );

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "api/chat")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var reader = new StreamReader(responseStream);

        var completed = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException(
                    $"Ollama error: {error.GetString()}");
            }

            if (root.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                var text = content.GetString();

                if (!string.IsNullOrEmpty(text))
                    await onContent(text);
            }

            if (root.TryGetProperty("done", out var done) &&
                done.ValueKind == JsonValueKind.True)
            {
                completed = true;
                break;
            }
        }

        if (!completed)
        {
            throw new InvalidOperationException(
                "Ollama streaming response ended unexpectedly.");
        }
    }

    public Task<string> ChatAsync(
     string message,
     CancellationToken cancellationToken)
    {
        return ChatAsync(
            [
                new OllamaMessage
                {
                    Role = "user",
                    Content = message
                }
            ],
            cancellationToken);
    }

    public async Task<string> ChatAsync(
        IReadOnlyList<OllamaMessage> messages,
        CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
            Model: _options.ChatModel,
            Messages: messages,
            Stream: false
        );

        using var response = await _httpClient.PostAsJsonAsync(
            "api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            cancellationToken: cancellationToken);

        return result?.Message?.Content
            ?? throw new InvalidOperationException(
                "Ollama returned an empty response.");
    }

    //public async Task<string> ChatAsync(
    //    string message,
    //    CancellationToken cancellationToken)
    //{
    //    var request = new OllamaChatRequest(
    //        Model: _options.ChatModel,
    //        Messages:
    //        [
    //            new OllamaMessage
    //        {
    //            Role = "user",
    //            Content = message
    //        }
    //        ],
    //        Stream: false
    //    );

    //    using var response = await _httpClient.PostAsJsonAsync(
    //        "api/chat",
    //        request,
    //        cancellationToken);

    //    response.EnsureSuccessStatusCode();

    //    var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
    //        cancellationToken: cancellationToken);

    //    return result?.Message?.Content
    //        ?? throw new InvalidOperationException(
    //            "Ollama returned an empty response.");
    //}
}
