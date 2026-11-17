using LocalChatbot.Api.Features.Conversations.Models;
using LocalChatbot.Api.Infrastructure.Ollama;
using LocalChatbot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;
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
                AppDbContext dbContext,
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

                var incomingMessages = new List<OllamaMessage>();

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

                    incomingMessages.Add(new OllamaMessage
                    {
                        Role = role,
                        Content = content
                    });
                }

                // Get the latest user message.
                var userMessage = incomingMessages
                    .LastOrDefault(x => x.Role == "user");

                if (userMessage is null)
                {
                    return Results.BadRequest(new
                    {
                        error = new
                        {
                            message = "At least one user message is required.",
                            type = "invalid_request_error"
                        }
                    });
                }

                // Get Open WebUI headers.
                var externalChatId =
                    context.Request.Headers["X-OpenWebUI-Chat-Id"].FirstOrDefault();

                var externalUserMessageId =
                    context.Request.Headers["X-OpenWebUI-User-Message-Id"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(externalChatId))
                {
                    return Results.BadRequest(new
                    {
                        error = new
                        {
                            message = "X-OpenWebUI-Chat-Id header is required.",
                            type = "invalid_request_error"
                        }
                    });
                }

                // Start a transaction to make find-or-create operation safe.
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(cancellationToken);

                // Acquire a transaction-level lock for this chat ID.
                // Requests with the same chat ID will be processed sequentially.
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({externalChatId}, 0))",
                    cancellationToken);

                // Find or create conversation.
                var conversation = await dbContext.Conversations
                    .FirstOrDefaultAsync(
                        x => x.ExternalChatId == externalChatId,
                        cancellationToken);

                if (conversation is null)
                {
                    conversation = Conversation.Create(externalChatId);
                    conversation.SetTitle(userMessage.Content);

                    dbContext.Conversations.Add(conversation);
                }

                // Prevent duplicate user messages.
                if (!string.IsNullOrWhiteSpace(externalUserMessageId))
                {
                    var existingMessage = await dbContext.Messages
                        .FirstOrDefaultAsync(
                            x => x.ExternalMessageId == externalUserMessageId,
                            cancellationToken);

                    if (existingMessage is not null &&
                        existingMessage.ConversationId != conversation.Id)
                    {
                        return Results.Conflict(new
                        {
                            error = new
                            {
                                message = "The external message ID belongs to another conversation.",
                                type = "invalid_request_error"
                            }
                        });
                    }

                    if (existingMessage is null)
                    {
                        dbContext.Messages.Add(Message.Create(
                            conversation.Id,
                            "user",
                            userMessage.Content,
                            externalUserMessageId));

                        conversation.UpdateTimestamp();
                    }
                }
                else
                {
                    dbContext.Messages.Add(Message.Create(
                        conversation.Id,
                        "user",
                        userMessage.Content));

                    conversation.UpdateTimestamp();
                }

                // Save conversation and user message.
                await dbContext.SaveChangesAsync(cancellationToken);

                // Release the advisory lock.
                await transaction.CommitAsync(cancellationToken);

                // Load conversation history from PostgreSQL.
                var history = await dbContext.Messages
                    .AsNoTracking()
                    .Where(x => x.ConversationId == conversation.Id)
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .Select(x => new OllamaMessage
                    {
                        Role = x.Role,
                        Content = x.Content
                    })
                    .ToListAsync(cancellationToken);

                // Preserve incoming system messages.
                var systemMessages = incomingMessages
                    .Where(x => x.Role == "system")
                    .ToList();

                var messages = systemMessages
                    .Concat(history)
                    .ToList();

                var isStreaming =
                    request.TryGetProperty("stream", out var streamElement) &&
                    streamElement.ValueKind == JsonValueKind.True;

                if (isStreaming)
                {
                    var id = $"chatcmpl-{Guid.NewGuid():N}";
                    var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var model = ollamaClient.Model;

                    var assistantContent = new StringBuilder();

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
                                    delta = new { role = "assistant" },
                                    finish_reason = (string?)null
                                }
                            }
                        },
                        cancellationToken);

                    await ollamaClient.StreamChatAsync(
                        messages,
                        async content =>
                        {
                            assistantContent.Append(content);

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
                                            delta = new { content },
                                            finish_reason = (string?)null
                                        }
                                    }
                                },
                                cancellationToken);
                        },
                        cancellationToken);

                    // Save assistant response after successful streaming.
                    dbContext.Messages.Add(Message.Create(
                        conversation.Id,
                        "assistant",
                        assistantContent.ToString()));

                    conversation.UpdateTimestamp();

                    await dbContext.SaveChangesAsync(cancellationToken);

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

                // Non-streaming response.
                var result = await ollamaClient.ChatAsync(
                    messages,
                    cancellationToken);

                dbContext.Messages.Add(Message.Create(
                    conversation.Id,
                    "assistant",
                    result));

                conversation.UpdateTimestamp();

                await dbContext.SaveChangesAsync(cancellationToken);

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