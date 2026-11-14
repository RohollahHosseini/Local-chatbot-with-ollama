using LocalChatbot.Api.Infrastructure.Ollama;
using System.Text.Json;

namespace LocalChatbot.Api.Features.Chat
{
    public static class ChatEndpoints
    {
        public static IEndpointRouteBuilder MapChatEndpoints(
            this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost(
                "/api/chat",
                async (
                    ChatRequest request,
                    OllamaClient ollamaClient,
                    ILogger<ChatRequest> logger,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(request.Message))
                    {
                        return Results.BadRequest(new
                        {
                            Error = "Message is required."
                        });
                    }

                    try
                    {
                        var content = await ollamaClient.ChatAsync(
                            request.Message,
                            cancellationToken);

                        return Results.Ok(new ChatResponse(content));
                    }
                    catch (HttpRequestException exception)
                    {
                        logger.LogError(
                            exception,
                            "Failed to communicate with Ollama.");

                        var statusCode = exception.StatusCode is null
                            ? StatusCodes.Status503ServiceUnavailable
                            : StatusCodes.Status502BadGateway;

                        return Results.Problem(
                            statusCode: statusCode,
                            title: "Ollama communication failed.");
                    }
                    catch (InvalidOperationException exception)
                    {
                        logger.LogError(
                            exception,
                            "Ollama returned an invalid response.");

                        return Results.Problem(
                            statusCode: StatusCodes.Status502BadGateway,
                            title: "Invalid response from Ollama.");
                    }
                    catch (JsonException exception)
                    {
                        logger.LogError(
                            exception,
                            "Could not deserialize Ollama response.");

                        return Results.Problem(
                            statusCode: StatusCodes.Status502BadGateway,
                            title: "Invalid JSON response from Ollama.");
                    }
                });

            return endpoints;
        }
    }
}
