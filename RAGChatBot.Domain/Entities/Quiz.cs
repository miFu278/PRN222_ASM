using System;

namespace RAGChatBot.Domain.Entities
{
    public class Quiz
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public int QuestionCount { get; set; } = 20;
        public Guid? DocumentId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
