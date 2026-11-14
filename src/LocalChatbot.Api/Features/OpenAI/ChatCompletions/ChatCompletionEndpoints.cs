using LocalChatbot.Api.Infrastructure.Ollama;
using System.Text.Json;

namespace LocalChatbot.Api.Features.OpenAI.ChatCompletions;

public static class ChatCompletionEndpoints
{
    public static IEndpointRouteBuilder MapChatCompletionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/v1/chat/completions",
            async (
                HttpContext context,
                JsonElement request,
                OllamaClient ollamaClient,
                CancellationToken cancellationToken) =>
            {
                if (!request.TryGetProperty("messages", out var messagesElement) ||
                    messagesElement.ValueKind != JsonValueKind.Array)
                {
                    return Results.BadRequest(new
                    {
                        error = new
                        {
                            message = "The messages field is required.",
                            type = "invalid_request_error"
                        }
                    });
                }

                var messages = new List<OllamaMessage>();

                foreach (var item in messagesElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("role", out var roleElement) ||
                        !item.TryGetProperty("content", out var contentElement))
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                message = "Each message must contain role and content.",
                                type = "invalid_request_error"
                            }
                        });
                    }

                    var role = roleElement.GetString();
                    var content = contentElement.GetString();

                    if (string.IsNullOrWhiteSpace(role) || content is null)
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                message = "Invalid message role or content.",
                                type = "invalid_request_error"
                            }
                        });
                    }

                    messages.Add(new OllamaMessage
                    {
                        Role = role,
                        Content = content
                    });
                }

                var isStreaming =
                    request.TryGetProperty("stream", out var streamElement) &&
                    streamElement.ValueKind == JsonValueKind.True;

                if (isStreaming)
                {
                    var id = $"chatcmpl-{Guid.NewGuid():N}";
                    var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var model = ollamaClient.Model;

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "text/event-stream";
                    context.Response.Headers["Cache-Control"] = "no-cache";
                    context.Response.Headers["X-Accel-Buffering"] = "no";

                    await WriteChunkAsync(
                        context,
                        new
                        {
                            id,
                            @object = "chat.completion.chunk",
                            created,
                            model,
                            choices = new[]
                            {
                                new
                                {
                                    index = 0,
                                    delta = new
                                    {
                                        role = "assistant"
                                    },
                                    finish_reason = (string?)null
                                }
                            }
                        },
                        cancellationToken);

                    await ollamaClient.StreamChatAsync(
                        messages,
                        async content =>
                        {
                            await WriteChunkAsync(
                                context,
                                new
                                {
                                    id,
                                    @object = "chat.completion.chunk",
                                    created,
                                    model,
                                    choices = new[]
                                    {
                                        new
                                        {
                                            index = 0,
                                            delta = new
                                            {
                                                content
                                            },
                                            finish_reason = (string?)null
                                        }
                                    }
                                },
                                cancellationToken);
                        },
                        cancellationToken);

                    await WriteChunkAsync(
                        context,
                        new
                        {
                            id,
                            @object = "chat.completion.chunk",
                            created,
                            model,
                            choices = new[]
                            {
                                new
                                {
                                    index = 0,
                                    delta = new { },
                                    finish_reason = "stop"
                                }
                            }
                        },
                        cancellationToken);

                    await context.Response.WriteAsync(
                        "data: [DONE]\n\n",
                        cancellationToken);

                    await context.Response.Body.FlushAsync(
                        cancellationToken);

                    return Results.Empty;
                }

                var result = await ollamaClient.ChatAsync(
                    messages,
                    cancellationToken);

                return Results.Ok(new
                {
                    id = $"chatcmpl-{Guid.NewGuid():N}",
                    @object = "chat.completion",
                    created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    model = ollamaClient.Model,
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            message = new
                            {
                                role = "assistant",
                                content = result
                            },
                            finish_reason = "stop"
                        }
                    }
                });
            });

        return endpoints;
    }

    private static async Task WriteChunkAsync(
        HttpContext context,
        object chunk,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(chunk);

        await context.Response.WriteAsync(
            $"data: {json}\n\n",
            cancellationToken);

        await context.Response.Body.FlushAsync(
            cancellationToken);
    }
}