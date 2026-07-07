using System;
using System.Collections.Generic;

namespace RAGChatBot.DAL.Entities
{
    public class ChatThread
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);

        // Navigation property
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
