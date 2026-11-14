namespace LocalChatbot.Api.Conversations.Entities
{
    public class Message
    {
        public Guid Id { get; private set; }

        public Guid ConversationId { get; private set; }

        public string Role { get; private set; } = null!;

        public string Content { get; private set; } = null!;

        public string? ExternalMessageId { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public Conversation Conversation { get; private set; } = null!;

        private Message()
        {
        }

        public static Message Create(
            Guid conversationId,
            string role,
            string content,
            string? externalMessageId = null)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new ArgumentException("Message role is required.", nameof(role));

            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Message content is required.", nameof(content));

            var allowedRoles = new[] { "system", "user", "assistant" };

            if (!allowedRoles.Contains(role))
                throw new ArgumentException("Invalid message role.", nameof(role));

            return new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = role,
                Content = content,
                ExternalMessageId = externalMessageId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
