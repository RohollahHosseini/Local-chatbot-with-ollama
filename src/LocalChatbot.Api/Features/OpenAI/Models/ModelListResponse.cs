namespace LocalChatbot.Api.Features.OpenAI.Models;

public sealed record ModelListResponse(
    string Object,
    IReadOnlyList<ModelItem> Data);

public sealed record ModelItem(
    string Id,
    string Object,
    long Created,
    string OwnedBy);
