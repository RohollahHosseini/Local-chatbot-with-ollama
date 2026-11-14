using System.ComponentModel.DataAnnotations;

namespace LocalChatbot.Api.Options
{
    public class OllamaOptions
    {
        public const string SectionName = "Ollama";

        [Required]
        [Url]
        public string BaseUrl { get; init; } = "http://localhost:11434/";

        [Required]
        public string ChatModel { get; init; } = "qwen3:4b";

        [Range(1, 600)]
        public int TimeoutSeconds { get; init; } = 120;
    }
}
