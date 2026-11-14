namespace LocalChatbot.Api.Features.OpenAI.Models
{
    public static class ModelEndpoints
    {
        public static IEndpointRouteBuilder MapModelEndpoints(
       this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/v1/models", (IConfiguration configuration) =>
            {
                var model = configuration["Ollama:ChatModel"]
                    ?? throw new InvalidOperationException(
                        "Ollama ChatModel is not configured.");

                var response = new ModelListResponse(
                    Object: "list",
                    Data:
                    [
                        new ModelItem(
                        Id: model,
                        Object: "model",
                        Created: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        OwnedBy: "localai-chatbot")
                    ]);

                return Results.Ok(response);
            });

            return endpoints;
        }
    }
}
