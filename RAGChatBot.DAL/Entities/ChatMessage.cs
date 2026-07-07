using System;

namespace RAGChatBot.DAL.Entities
{
    public class ChatMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ThreadId { get; set; }
        public string Role { get; set; } = string.Empty; // "user" | "assistant"
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow.AddHours(7);

        // Navigation property
        public virtual ChatThread Thread { get; set; } = null!;
    }
}
