using LocalChatbot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LocalChatbot.Api.Features.Conversations;

    public static class ConversationEndpoints
    {
        public static IEndpointRouteBuilder MapConversationEndpoints(
        this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/conversations")
                .WithTags("Conversations");

            // Create conversation
            group.MapPost("/", async (
                CreateConversationRequest request,
                AppDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                if (!string.IsNullOrWhiteSpace(request.ExternalChatId))
                {
                    var exists = await dbContext.Conversations
                        .AnyAsync(
                            x => x.ExternalChatId == request.ExternalChatId,
                            cancellationToken);

                    if (exists)
                        return Results.Conflict(
                            "A conversation with this external chat ID already exists.");
                }

                var conversation = Models.Conversation.Create(
                    request.ExternalChatId);

                if (!string.IsNullOrWhiteSpace(request.Title))
                    conversation.SetTitle(request.Title);

                dbContext.Conversations.Add(conversation);

                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.Created(
                    $"/api/conversations/{conversation.Id}",
                    new ConversationResponse(
                        conversation.Id,
                        conversation.ExternalChatId,
                        conversation.Title,
                        conversation.CreatedAt,
                        conversation.UpdatedAt));
            })
            .WithName("CreateConversation");

            // List conversations
            group.MapGet("/", async (
                AppDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var conversations = await dbContext.Conversations
                    .AsNoTracking()
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x => new ConversationListItemResponse(
                        x.Id,
                        x.Title,
                        x.CreatedAt,
                        x.UpdatedAt,
                        x.Messages.Count))
                    .ToListAsync(cancellationToken);

                return Results.Ok(conversations);
            })
            .WithName("GetConversations");

            // Get conversation with messages
            group.MapGet("/{id:guid}", async (
                Guid id,
                AppDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var conversation = await dbContext.Conversations
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new ConversationResponse(
                        x.Id,
                        x.ExternalChatId,
                        x.Title,
                        x.CreatedAt,
                        x.UpdatedAt))
                    .FirstOrDefaultAsync(cancellationToken);

                if (conversation is null)
                    return Results.NotFound();

                var messages = await dbContext.Messages
                    .AsNoTracking()
                    .Where(x => x.ConversationId == id)
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .Select(x => new MessageResponse(
                        x.Id,
                        x.Role,
                        x.Content,
                        x.CreatedAt))
                    .ToListAsync(cancellationToken);

                return Results.Ok(new ConversationDetailsResponse(
                    conversation.Id,
                    conversation.ExternalChatId,
                    conversation.Title,
                    conversation.CreatedAt,
                    conversation.UpdatedAt,
                    messages));
            })
            .WithName("GetConversationById");

            // Delete conversation
            group.MapDelete("/{id:guid}", async (
                Guid id,
                AppDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var conversation = await dbContext.Conversations
                    .FirstOrDefaultAsync(
                        x => x.Id == id,
                        cancellationToken);

                if (conversation is null)
                    return Results.NotFound();

                dbContext.Conversations.Remove(conversation);

                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            })
            .WithName("DeleteConversation");

            return app;
        }
    }
public sealed record CreateConversationRequest(
    string? Title,
    string? ExternalChatId);

public sealed record ConversationResponse(
    Guid Id,
    string? ExternalChatId,
    string? Title,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ConversationListItemResponse(
    Guid Id,
    string? Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount);

public sealed record ConversationDetailsResponse(
    Guid Id,
    string? ExternalChatId,
    string? Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<MessageResponse> Messages);

public sealed record MessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt);