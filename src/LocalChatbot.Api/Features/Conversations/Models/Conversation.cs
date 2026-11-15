namespace LocalChatbot.Api.Features.Conversations.Models;

public class Conversation
{
    public Guid Id { get; private set; }

    public string? ExternalChatId { get; private set; }

    public string? Title { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public ICollection<Message> Messages { get; private set; } = new List<Message>();

    private Conversation()
    {
    }

    public static Conversation Create(string? externalChatId = null)
    {
        var now = DateTime.UtcNow;

        return new Conversation
        {
            Id = Guid.NewGuid(),
            ExternalChatId = externalChatId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        Title = title.Length > 200
            ? title[..200]
            : title;

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTimestamp()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
